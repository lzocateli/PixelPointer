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
        return UseScreenCast(displayCount, Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"))
            ? new ScreenCastCapture(client, TryCreateTokenStore())
            : new PortalScreenCapture(client);
    }

    // No GNOME 46 o indicador de compartilhamento fica preso após ScreenCasts curtos; o Screenshot não o aciona.
    internal static bool UseScreenCast(int displayCount, string? currentDesktop) =>
        displayCount == 1 && !IsGnome(currentDesktop);

    private static bool IsGnome(string? currentDesktop) =>
        currentDesktop?.Split(':').Any(name => name.Equals("GNOME", StringComparison.OrdinalIgnoreCase)) == true;

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
