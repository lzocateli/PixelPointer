using PixelPointer.App.Linux;
using PixelPointer.App.MacOS;
using PixelPointer.App.Windows;

namespace PixelPointer.App.Capture;

internal static class ScreenCaptureFactory
{
    internal static IScreenCapture Create(PortalClient? portal)
    {
        if (OperatingSystem.IsWindows())
        {
            return new GdiScreenCapture();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ScreencaptureToolCapture();
        }

        return SessionInfo.IsWayland
            ? new PortalScreenCapture(portal ?? throw new InvalidOperationException("Wayland exige o portal de captura."))
            : new X11ScreenCapture();
    }
}
