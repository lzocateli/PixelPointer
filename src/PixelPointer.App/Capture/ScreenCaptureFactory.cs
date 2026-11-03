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

    internal static void EnsureWaylandLiveZoomAllowed(string? currentDesktop)
    {
        if (IsGnome(currentDesktop))
        {
            throw new NotSupportedException("O Live Zoom ScreenCast está desabilitado no GNOME para preservar a política de captura Screenshot.");
        }
    }

    /// <summary>Fonte contínua para gravação; quem recebe passa a ser dono e a descarta se for <see cref="IDisposable"/>.</summary>
    internal static IScreenCapture CreateRecordingSource(PortalClient? portal)
    {
        if (OperatingSystem.IsWindows())
        {
            return new GdiScreenCapture();
        }

        if (OperatingSystem.IsLinux() && !SessionInfo.IsWayland)
        {
            return new X11ScreenCapture();
        }

        if (OperatingSystem.IsLinux() && portal is not null)
        {
            return ScreenCastStream.OpenAsync(portal, TryCreateTokenStore(), CancellationToken.None).GetAwaiter().GetResult();
        }

        throw new NotSupportedException("A gravação ainda não está disponível neste sistema (disponível no Windows e no Linux).");
    }

    internal static Task<ScreenCastStream> OpenWaylandLiveZoomAsync(PortalClient portal,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() || !SessionInfo.IsWayland)
        {
            throw new NotSupportedException("O Live Zoom ScreenCast exige uma sessão Wayland.");
        }

        EnsureWaylandLiveZoomAllowed(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));

        return ScreenCastStream.OpenAsync(portal, TryCreateTokenStore(), cancellationToken, cursorMetadata: true);
    }

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
