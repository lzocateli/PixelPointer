using System.Runtime.InteropServices;
using PixelPointer.App.Hotkeys;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App;

internal sealed unsafe partial class TrayController : IDisposable
{
    private const uint TrayEntryButton = 1;
    private const uint TrayEntrySubmenu = 4;
    private const uint TrayEntryDisabled = 0x80000000;
    private static readonly TrayCallback Callback = OnTrayCallback;

    private readonly SKBitmap _iconBitmap;
    private readonly nint _iconSurface;
    private readonly nint _tray;
    private readonly List<GCHandle> _callbacks;
    private readonly List<TrayLabel> _labels = [];
    private int _disposed;

    private TrayController(SKBitmap iconBitmap, nint iconSurface, nint tray, List<GCHandle> callbacks)
    {
        _iconBitmap = iconBitmap;
        _iconSurface = iconSurface;
        _tray = tray;
        _callbacks = callbacks;
    }

    internal static TrayController? TryCreate(
        Action<HotkeyCommand> onCommand,
        Action onQuit,
        out string? unavailableReason)
    {
        var bitmap = AppIcon.CreateBitmap();
        var surface = AppIcon.CreateSurface(bitmap);
        if (surface == 0)
        {
            bitmap.Dispose();
            unavailableReason = $"SDL não criou a superfície do ícone: {SDL_GetError()}";
            return null;
        }

        var tray = SdlTrayNative.CreateTray(surface, "PixelPointer — zoom e desenho");
        if (tray == 0)
        {
            AppIcon.DestroySurface(surface);
            bitmap.Dispose();
            var error = SDL_GetError();
            unavailableReason = string.IsNullOrWhiteSpace(error)
                ? "o backend SDL não oferece suporte à tray"
                : error;
            return null;
        }

        var callbacks = new List<GCHandle>();
        var controller = new TrayController(bitmap, surface, tray, callbacks);
        try
        {
            var menu = SdlTrayNative.CreateTrayMenu(tray);
            if (menu == 0)
            {
                throw new InvalidOperationException($"SDL não criou o menu da tray: {SDL_GetError()}");
            }

            controller.AddEntry(menu, "Zoom", () => onCommand(new HotkeyCommand("zoom")));
            controller.AddEntry(menu, "Desenhar", () => onCommand(new HotkeyCommand("draw")));
            controller.AddEntry(menu, "Timer", () => onCommand(new HotkeyCommand("timer")));
            controller.AddEntry(menu, "Live Zoom", () => onCommand(new HotkeyCommand("livezoom")));
            controller.AddEntry(menu, "Recorte", () => onCommand(new HotkeyCommand("snip")));
            controller.AddEntry(menu, "Gravar / parar gravação", () => onCommand(new HotkeyCommand("record")));
            var about = controller.AddEntry(menu, "Sobre e atalhos", null, TrayEntrySubmenu);
            var aboutMenu = SdlTrayNative.CreateTraySubmenu(about);
            if (aboutMenu == 0)
            {
                throw new InvalidOperationException($"SDL não criou o submenu de informações: {SDL_GetError()}");
            }

            foreach (var label in AboutEntries)
            {
                controller.AddEntry(aboutMenu, label, null, TrayEntryButton | TrayEntryDisabled);
            }

            controller.AddEntry(menu, "Sair", onQuit);
            unavailableReason = null;
            return controller;
        }
        catch (InvalidOperationException ex)
        {
            controller.Dispose();
            unavailableReason = ex.Message;
            return null;
        }
        catch
        {
            controller.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SdlTrayNative.DestroyTray(_tray);
        foreach (var callback in _callbacks)
        {
            callback.Free();
        }

        foreach (var label in _labels)
        {
            label.Dispose();
        }

        AppIcon.DestroySurface(_iconSurface);
        _iconBitmap.Dispose();
    }

    internal static IReadOnlyList<string> AboutEntries =>
    [
        $"PixelPointer {AppInfo.Version}",
        "Atalhos globais padrão",
        .. HotkeyDefaults.Shortcuts.Select(shortcut => $"{shortcut.Trigger}  —  {shortcut.Description}"),
        "O desktop pode redefinir os atalhos.",
        "Disponibilidade depende da plataforma e das permissões.",
    ];

    private nint AddEntry(nint menu, string label, Action? action, uint flags = TrayEntryButton)
    {
        // O backend D-Bus do SDL 3.5.0 retém o ponteiro, sem copiar o texto.
        var nativeLabel = new TrayLabel(label);
        _labels.Add(nativeLabel);
        var entry = SdlTrayNative.InsertTrayEntryAt(menu, -1, nativeLabel, flags);
        if (entry == 0)
        {
            throw new InvalidOperationException($"SDL não adicionou a opção '{label}' à tray: {SDL_GetError()}");
        }

        if ((flags & TrayEntryDisabled) != 0)
        {
            SdlTrayNative.SetTrayEntryEnabled(entry, false);
        }

        if (action is not null)
        {
            var callback = GCHandle.Alloc(action);
            _callbacks.Add(callback);
            SdlTrayNative.SetTrayEntryCallback(entry, Marshal.GetFunctionPointerForDelegate(Callback), GCHandle.ToIntPtr(callback));
        }

        return entry;
    }

    private static void OnTrayCallback(nint userdata, nint entry)
    {
        if (GCHandle.FromIntPtr(userdata).Target is Action action)
        {
            action();
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TrayCallback(nint userdata, nint entry);

    private static partial class SdlTrayNative
    {
        [LibraryImport("SDL3", EntryPoint = "SDL_CreateTray", StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial nint CreateTray(nint icon, string tooltip);

        [LibraryImport("SDL3", EntryPoint = "SDL_CreateTrayMenu")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial nint CreateTrayMenu(nint tray);

        [LibraryImport("SDL3", EntryPoint = "SDL_CreateTraySubmenu")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial nint CreateTraySubmenu(nint entry);

        [LibraryImport("SDL3", EntryPoint = "SDL_InsertTrayEntryAt")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial nint InsertTrayEntryAt(nint menu, int position, TrayLabel label, uint flags);

        [LibraryImport("SDL3", EntryPoint = "SDL_SetTrayEntryEnabled")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void SetTrayEntryEnabled(nint entry, [MarshalAs(UnmanagedType.U1)] bool enabled);

        [LibraryImport("SDL3", EntryPoint = "SDL_SetTrayEntryCallback")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void SetTrayEntryCallback(nint entry, nint callback, nint userdata);

        [LibraryImport("SDL3", EntryPoint = "SDL_DestroyTray")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void DestroyTray(nint tray);

    }

    internal sealed class TrayLabel : SafeHandle
    {
        internal TrayLabel(string text) : base(0, ownsHandle: true) =>
            SetHandle(Marshal.StringToCoTaskMemUTF8(text));

        public override bool IsInvalid => handle == 0;

        protected override bool ReleaseHandle()
        {
            Marshal.FreeCoTaskMem(handle);
            return true;
        }
    }
}
