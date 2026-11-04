using PixelPointer.App.Linux;
using PixelPointer.App.Overlay;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Capture;

internal sealed record CursorMonitor(DisplayBounds Bounds, IReadOnlyList<DisplayBounds> Displays)
{
    internal static unsafe CursorMonitor? Select()
    {
        if (string.Equals(SDL_GetCurrentVideoDriver(), "wayland", StringComparison.Ordinal)
            || OperatingSystem.IsMacOS())
        {
            return null;
        }

        int cursorX, cursorY;
        if (OperatingSystem.IsLinux() && SessionInfo.IsX11)
        {
            var connection = Xlib.XOpenDisplay(null);
            if (connection == 0)
            {
                throw new InvalidOperationException("Não foi possível consultar o cursor X11.");
            }

            try
            {
                if (Xlib.XQueryPointer(connection, Xlib.XDefaultRootWindow(connection), out _, out _,
                    out cursorX, out cursorY, out _, out _, out _) == 0)
                {
                    throw new InvalidOperationException("Não foi possível localizar o cursor X11 na área de trabalho.");
                }
            }
            finally
            {
                Xlib.XCloseDisplay(connection);
            }
        }
        else
        {
            float positionX, positionY;
            SDL_GetGlobalMouseState(&positionX, &positionY);
            cursorX = (int)positionX;
            cursorY = (int)positionY;
        }

        var displays = new List<DisplayBounds>();
        using var ids = SDL_GetDisplays();
        for (var index = 0; ids is not null && index < ids.Count; index++)
        {
            SDL_Rect other;
            if (!SDL_GetDisplayBounds(ids[index], &other))
            {
                throw new InvalidOperationException("Não foi possível obter os limites dos monitores.");
            }

            displays.Add(new DisplayBounds(other.x, other.y, other.w, other.h));
        }

        var monitor = FromPoint(cursorX, cursorY, displays);
        DiagnosticLog.Write("DISPLAY", FormattableString.Invariant(
            $"cursor-monitor cursor=({cursorX},{cursorY}) target=({monitor.Bounds.X},{monitor.Bounds.Y},{monitor.Bounds.Width},{monitor.Bounds.Height})"));
        return monitor;
    }

    internal static CursorMonitor FromPoint(int cursorX, int cursorY, IReadOnlyList<DisplayBounds> displays)
    {
        foreach (var bounds in displays)
        {
            if (cursorX >= bounds.X && cursorX < bounds.X + bounds.Width
                && cursorY >= bounds.Y && cursorY < bounds.Y + bounds.Height)
            {
                return new CursorMonitor(bounds, displays);
            }
        }

        throw new InvalidOperationException("O cursor não corresponde a um monitor disponível.");
    }
}

internal sealed class MonitorScreenCapture(IScreenCapture capture, CursorMonitor monitor) : IScreenCapture, IDisposable
{
    private bool _logged;

    public string Name => $"{capture.Name} (monitor sob o cursor)";

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        using var desktop = await capture.CaptureAsync(cancellationToken);
        var bounds = DisplayLayout.RecordingCrop(monitor.Bounds, monitor.Displays, desktop.Width, desktop.Height);
        if (!_logged)
        {
            DiagnosticLog.Write("RECORDING", FormattableString.Invariant(
                $"desktop={desktop.Width}x{desktop.Height} crop=({bounds.Left},{bounds.Top},{bounds.Width},{bounds.Height})"));
            _logged = true;
        }
        return DisplayLayout.Crop(desktop, bounds);
    }

    public void Dispose() => (capture as IDisposable)?.Dispose();
}
