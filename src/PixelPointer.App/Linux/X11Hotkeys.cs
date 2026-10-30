using System.Runtime.InteropServices;
using PixelPointer.App.Hotkeys;

namespace PixelPointer.App.Linux;

/// <summary>Atalhos globais com XGrabKey na janela raiz (sessões X11).</summary>
internal sealed class X11Hotkeys : IHotkeyService
{
    private const uint ControlMask = 1 << 2;
    private const uint LockMask = 1 << 1;
    private const uint Mod2Mask = 1 << 4;
    private const long KeyPressMask = 1 << 0;
    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;
    private const int BadAccess = 10;

    private static readonly object ErrorHandlerLock = new();
    private static readonly Xlib.XErrorHandler ErrorHandler = HandleXError;
    private static nint _previousErrorHandler;
    private static nint _activeDisplay;
    private static bool _grabFailed;

    private readonly List<(int Keycode, uint Modifiers)> _grabs = [];
    private readonly Dictionary<int, string> _keycodes = [];
    private Thread? _thread;
    private CancellationTokenSource? _stop;
    private CancellationTokenRegistration _cancellationRegistration;
    private nint _display;
    private nint _root;
    private int _disposed;

    public string Name => "X11 XGrabKey";

    public Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken)
    {
        if (_display != 0)
        {
            throw new InvalidOperationException("Os atalhos X11 já foram iniciados.");
        }

        Xlib.XInitThreads();
        _display = Xlib.XOpenDisplay(null);
        if (_display == 0)
        {
            throw new InvalidOperationException("Não foi possível abrir o display X11.");
        }

        _root = Xlib.XDefaultRootWindow(_display);
        var unavailable = new List<string>();
        foreach (var shortcut in HotkeyDefaults.Shortcuts)
        {
            var keycode = Xlib.XKeysymToKeycode(_display, shortcut.Key);
            if (keycode == 0 || !TryGrabShortcut(keycode))
            {
                unavailable.Add($"{shortcut.Id} ({shortcut.Trigger})");
                continue;
            }

            _keycodes[keycode] = shortcut.Id;
        }

        Xlib.XSelectInput(_display, _root, KeyPressMask);
        if (_keycodes.Count == 0)
        {
            Dispose();
            throw new InvalidOperationException($"Nenhum atalho X11 pôde ser registrado: {string.Join(", ", unavailable)}.");
        }

        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationRegistration = cancellationToken.Register(_stop.Cancel);
        var stopToken = _stop.Token;
        var display = _display;
        _thread = new Thread(() =>
        {
            var buffer = Marshal.AllocHGlobal(192);
            try
            {
                while (!stopToken.IsCancellationRequested)
                {
                    if (Xlib.XPending(display) == 0)
                    {
                        stopToken.WaitHandle.WaitOne(10);
                        continue;
                    }

                    Xlib.XNextEvent(display, buffer);
                    if (Marshal.ReadInt32(buffer) == KeyPress
                        && _keycodes.TryGetValue(Marshal.ReadInt32(buffer, X11EventLayout.KeycodeOffset), out var id))
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

        var available = HotkeyDefaults.Shortcuts
            .Where(shortcut => _keycodes.Values.Contains(shortcut.Id))
            .Select(shortcut => $"{shortcut.Id}={shortcut.Trigger}");
        var status = string.Join(", ", available);
        if (unavailable.Count > 0)
        {
            status += $"; conflitos: {string.Join(", ", unavailable)}";
        }

        return Task.FromResult(status);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop?.Cancel();
        _thread?.Join();
        _cancellationRegistration.Dispose();
        _stop?.Dispose();
        if (_display == 0)
        {
            return;
        }

        foreach (var (keycode, modifiers) in _grabs)
        {
            Xlib.XUngrabKey(_display, keycode, modifiers, _root);
        }

        Xlib.XSync(_display, 0);
        Xlib.XCloseDisplay(_display);
        _display = 0;
        _grabs.Clear();
        _keycodes.Clear();
    }

    private bool TryGrabShortcut(int keycode)
    {
        var successfulGrabs = new List<(int Keycode, uint Modifiers)>();
        foreach (var extra in new[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask })
        {
            var modifiers = ControlMask | extra;
            if (GrabKey(keycode, modifiers))
            {
                successfulGrabs.Add((keycode, modifiers));
            }
            else
            {
                foreach (var (grabbedKeycode, grabbedModifiers) in successfulGrabs)
                {
                    Xlib.XUngrabKey(_display, grabbedKeycode, grabbedModifiers, _root);
                }

                Xlib.XSync(_display, 0);
                return false;
            }
        }

        _grabs.AddRange(successfulGrabs);
        return true;
    }

    private bool GrabKey(int keycode, uint modifiers)
    {
        lock (ErrorHandlerLock)
        {
            _activeDisplay = _display;
            _grabFailed = false;
            _previousErrorHandler = Xlib.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(ErrorHandler));
            try
            {
                Xlib.XGrabKey(_display, keycode, modifiers, _root, 0, GrabModeAsync, GrabModeAsync);
                Xlib.XSync(_display, 0);
                return !_grabFailed;
            }
            finally
            {
                Xlib.XSetErrorHandler(_previousErrorHandler);
                _previousErrorHandler = 0;
                _activeDisplay = 0;
            }
        }
    }

    private static int HandleXError(nint display, nint errorEvent)
    {
        var error = Marshal.PtrToStructure<Xlib.XErrorEvent>(errorEvent);
        if (display == _activeDisplay && error.ErrorCode == BadAccess)
        {
            _grabFailed = true;
            return 0;
        }

        return _previousErrorHandler == 0
            ? 0
            : Marshal.GetDelegateForFunctionPointer<Xlib.XErrorHandler>(_previousErrorHandler)(display, errorEvent);
    }
}

internal static class X11EventLayout
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyEvent
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Window;
        internal nuint Root;
        internal nuint Subwindow;
        internal nuint Time;
        internal int X;
        internal int Y;
        internal int XRoot;
        internal int YRoot;
        internal uint State;
        internal uint Keycode;
        internal int SameScreen;
    }

    internal static int KeycodeOffset => Marshal.OffsetOf<KeyEvent>(nameof(KeyEvent.Keycode)).ToInt32();
}
