using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

internal static class WaylandCapturePump
{
    private const int CaptureIntervalMilliseconds = 25;

    internal static Task Start(PortalClient portal, CancellationToken cancellationToken,
        Action<SKBitmap, SpaCursorPosition?> publish) =>
        Task.Run(() => RunAsync(portal, cancellationToken, publish), CancellationToken.None);

    private static async Task RunAsync(PortalClient portal, CancellationToken cancellationToken,
        Action<SKBitmap, SpaCursorPosition?> publish)
    {
        using var capture = await ScreenCaptureFactory.OpenWaylandLiveZoomAsync(portal, cancellationToken)
            .ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            var (frame, cursor) = await capture.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                publish(frame, cursor);
            }
            catch
            {
                frame.Dispose();
                throw;
            }

            await Task.Delay(CaptureIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }
}
