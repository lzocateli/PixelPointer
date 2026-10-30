using System.Runtime.InteropServices;
using PixelPointer.App.Hotkeys;

namespace PixelPointer.App.Linux;

/// <summary>Atalhos globais com XGrabKey na janela raiz (sessões X11).</summary>
internal sealed class X11Hotkeys : IHotkeyService
{
    private const uint ControlMask = 1 << 2;
    private const uint LockMask = 1 << 1;
    private const uint Mod2Mask = 1 << 4; // NumLock
    private const long KeyPressMask = 1 << 0;
    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;

    private Thread? _thread;

    public string Name => "X11 XGrabKey";

    public Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken)
    {
        Xlib.XInitThreads();
        var display = Xlib.XOpenDisplay(null);
        if (display == 0)
        {
            throw new InvalidOperationException("Não foi possível abrir o display X11.");
        }

        var root = Xlib.XDefaultRootWindow(display);
        var keycodes = new Dictionary<int, string>();
        foreach (var shortcut in HotkeyDefaults.Shortcuts)
        {
            var keycode = Xlib.XKeysymToKeycode(display, shortcut.Key);
            keycodes[keycode] = shortcut.Id;
            // O grab é por combinação exata de modificadores: repete com CapsLock e NumLock ligados.
            foreach (var extra in new[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask })
            {
                Xlib.XGrabKey(display, keycode, ControlMask | extra, root, 0, GrabModeAsync, GrabModeAsync);
            }
        }

        Xlib.XSelectInput(display, root, KeyPressMask);
        _thread = new Thread(() =>
        {
            var buffer = Marshal.AllocHGlobal(192); // sizeof(XEvent)
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Xlib.XNextEvent(display, buffer);
                    if (Marshal.ReadInt32(buffer) == KeyPress && keycodes.TryGetValue(Marshal.ReadInt32(buffer, 84), out var id))
                    {
                        onActivated(new HotkeyCommand(id));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        })
        { IsBackground = true, Name = "x11-hotkeys" };
        _thread.Start();
        return Task.FromResult(string.Join(", ", HotkeyDefaults.Shortcuts.Select(s => $"{s.Id}={s.Trigger}")));
    }

    public void Dispose()
    {
    }
}
