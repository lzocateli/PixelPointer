using System.Runtime.InteropServices;
using PixelPointer.Core.LiveZoom;

namespace PixelPointer.App.Windows;

/// <summary>
/// Live Zoom pela lupa de tela cheia do Windows (Magnification API, a mesma do ZoomIt): a tela continua ao vivo
/// e o mouse e o teclado seguem funcionando; a área ampliada acompanha o cursor numa thread própria.
/// </summary>
internal sealed partial class LiveZoom : IDisposable
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(16);

    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    private LiveZoom(float level)
    {
        Level = Math.Clamp(level, LiveZoomView.MinLevel, LiveZoomView.MaxLevel);
        _thread = new Thread(Run) { IsBackground = true, Name = "live-zoom" };
    }

    internal float Level { get; }

    /// <summary>Ativa a lupa; falha se a Magnification API não puder ser inicializada.</summary>
    internal static LiveZoom Start(float level)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new NotSupportedException("O Live Zoom ainda não está disponível neste sistema (disponível no Windows e no X11).");
        }

        var zoom = new LiveZoom(level);
        zoom._thread.Start();
        zoom._started.Task.GetAwaiter().GetResult();
        return zoom;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        _thread.Join();
        _stop.Dispose();
    }

    private void Run()
    {
        // As chamadas da lupa ficam todas nesta thread, do MagInitialize ao MagUninitialize.
        _ = SetProcessDpiAwarenessContext(-4);
        if (!MagInitialize())
        {
            _started.SetException(new InvalidOperationException($"MagInitialize falhou ({Marshal.GetLastPInvokeError()})."));
            return;
        }

        try
        {
            _started.SetResult();
            var lastX = int.MinValue;
            var lastY = int.MinValue;
            while (!_stop.IsCancellationRequested)
            {
                if (GetCursorPos(out var cursor))
                {
                    var x = LiveZoomView.Origin(cursor.X, GetSystemMetrics(SmCxScreen), Level);
                    var y = LiveZoomView.Origin(cursor.Y, GetSystemMetrics(SmCyScreen), Level);
                    if (x != lastX || y != lastY)
                    {
                        _ = MagSetFullscreenTransform(Level, x, y);
                        lastX = x;
                        lastY = y;
                    }
                }

                _stop.Token.WaitHandle.WaitOne(Interval);
            }
        }
        finally
        {
            _ = MagSetFullscreenTransform(1f, 0, 0);
            _ = MagUninitialize();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [LibraryImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagInitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagUninitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagSetFullscreenTransform(float level, int xOffset, int yOffset);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);
}
