using System.Diagnostics;
using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.MacOS;

internal sealed class ScreenCaptureKitCapture : IScreenCapture
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);
    private const int PollMilliseconds = 16;

    public string Name => "macOS ScreenCaptureKit";

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        using var capture = ScreenCaptureKitFrameSource.Start();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < FrameTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = capture.CaptureCrop(0, 0, capture.DisplayWidth, capture.DisplayHeight);
            if (frame is not null)
            {
                return frame.Copy()
                    ?? throw new InvalidOperationException("Não foi possível copiar o quadro do ScreenCaptureKit.");
            }

            await Task.Delay(PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("O ScreenCaptureKit não entregou um quadro em até 5 segundos.");
    }
}
