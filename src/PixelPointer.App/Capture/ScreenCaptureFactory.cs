using PixelPointer.App.Linux;
using PixelPointer.App.MacOS;
using PixelPointer.App.Windows;

namespace PixelPointer.App.Capture;

internal static class ScreenCaptureFactory
{
    /// <summary>No Wayland com mais de um monitor, o Screenshot cobre a área inteira; o ScreenCast entrega um monitor só.</summary>
    internal static IScreenCapture Create(PortalClient? portal, int displayCount = 1)
    {
        if (OperatingSystem.IsWindows())
        {
            return new GdiScreenCapture();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ScreencaptureToolCapture();
        }

        if (!SessionInfo.IsWayland)
        {
            return new X11ScreenCapture();
        }

        var client = portal ?? throw new InvalidOperationException("Wayland exige o portal de captura.");
        return displayCount == 1 ? new ScreenCastCapture(client, TryCreateTokenStore()) : new PortalScreenCapture(client);
    }

    private static RestoreTokenStore? TryCreateTokenStore()
    {
        try
        {
            return RestoreTokenStore.CreateDefault();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
