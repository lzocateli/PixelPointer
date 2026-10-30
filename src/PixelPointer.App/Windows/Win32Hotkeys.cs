using System.Runtime.InteropServices;
using PixelPointer.App.Hotkeys;

namespace PixelPointer.App.Windows;

/// <summary>Atalhos globais com RegisterHotKey numa thread com fila de mensagens própria.</summary>
internal sealed partial class Win32Hotkeys : IHotkeyService
{
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;

    private Thread? _thread;
    private uint _threadId;

    public string Name => "Win32 RegisterHotKey";

    public Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken)
    {
        var registered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            var bound = new List<string>();
            for (var i = 0; i < HotkeyDefaults.Shortcuts.Count; i++)
            {
                var shortcut = HotkeyDefaults.Shortcuts[i];
                bound.Add(RegisterHotKey(0, i + 1, ModControl | ModNoRepeat, shortcut.Key)
                    ? $"{shortcut.Id}={shortcut.Trigger}"
                    : $"{shortcut.Id}=FALHOU({Marshal.GetLastPInvokeError()})");
            }

            registered.SetResult(string.Join(", ", bound));
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                if (msg.message == WmHotkey && msg.wParam > 0 && (int)msg.wParam <= HotkeyDefaults.Shortcuts.Count)
                {
                    onActivated(new HotkeyCommand(HotkeyDefaults.Shortcuts[(int)msg.wParam - 1].Id));
                }
            }
        })
        { IsBackground = true, Name = "win32-hotkeys" };
        _thread.Start();
        return registered.Task;
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            PostThreadMessageW(_threadId, WmQuit, 0, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
        public uint lPrivate;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);

    [LibraryImport("user32.dll")]
    private static partial int GetMessageW(out Msg msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageW(uint threadId, uint msg, nuint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
