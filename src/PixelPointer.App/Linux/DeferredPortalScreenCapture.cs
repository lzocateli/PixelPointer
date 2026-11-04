using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

internal sealed class DeferredPortalScreenCapture(Func<PortalClient> connect) : IScreenCapture
{
    public string Name => "xdg-desktop-portal Screenshot";

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        using var portal = connect();
        return await new PortalScreenCapture(portal).CaptureAsync(cancellationToken).ConfigureAwait(false);
    }
}
