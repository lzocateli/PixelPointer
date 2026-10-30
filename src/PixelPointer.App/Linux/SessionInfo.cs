namespace PixelPointer.App.Linux;

internal static class SessionInfo
{
    internal static bool IsWayland =>
        OperatingSystem.IsLinux() && (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland"
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")));

    internal static bool IsX11 =>
        OperatingSystem.IsLinux() && !IsWayland
        && (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "x11"
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")));

    internal static string Describe() => OperatingSystem.IsLinux()
        ? $"{Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "?"} / {Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "?"}"
        : "n/a";
}
