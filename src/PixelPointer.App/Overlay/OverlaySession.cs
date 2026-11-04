using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelPointer.App.Capture;
using PixelPointer.Core.Annotations;
using PixelPointer.Core.Settings;
using PixelPointer.Core.Snip;
using PixelPointer.Core.Timer;
using PixelPointer.Rendering;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Overlay;

internal enum OverlayMode
{
    Zoom,
    Draw,
    Timer,
    Snip,
}

internal static class OverlayModes
{
    internal static OverlayMode FromCommand(string? id) => id switch
    {
        "draw" => OverlayMode.Draw,
        "timer" => OverlayMode.Timer,
        "snip" => OverlayMode.Snip,
        _ => OverlayMode.Zoom,
    };
}

/// <summary>Métricas de uma sessão de sobreposição, impressas ao final para o relatório da F0.</summary>
internal sealed record OverlayMetrics(string VideoDriver, string Renderer, bool Gpu, int PixelWidth, int PixelHeight, float PixelDensity,
    double WindowToFirstFrameMs, int Frames, double AverageFrameMs, double P95FrameMs, int PenEvents, float MaxPressure);

/// <summary>
/// Janela de sobreposição em tela cheia: SDL3 cria a janela e entrega a entrada; o Skia desenha em CPU, num
/// buffer enviado como textura, ou em OpenGL quando habilitado. Implementa o essencial do Zoom (Ctrl+1) e do Desenho (Ctrl+2).
/// </summary>
internal sealed unsafe partial class OverlaySession
{
    private const uint SnapshotOverlaySettleDelayMs = 250;

    private readonly SKBitmap? _screenshot;
    private readonly SKImage? _image;
    private readonly IScreenCapture? _snapshotCapture;
    private readonly AnnotationDocument _annotations;
    private readonly List<double> _frameTimes = [];
    private OverlayMode _mode;
    private float _zoom = 1f;
    private float _targetZoom;
    private float _mouseX;
    private float _mouseY;
    private float _density = 1f;
    private int _width;
    private int _height;
    private SKRect _view;
    private SKRect _crop;
    private SKRect _snapshotOverlayBounds;
    private readonly bool _transparentBackground;
    private readonly bool _gpuRequested;
    private bool _penDown;
    private SDL_Window* _window;
    private bool _tabHeld;
    private float _pressure = 1f;
    private int _penEvents;
    private float _maxPressure;
    private DisplayBounds? _targetDisplayBounds;
    private string? _saveMessage;
    private bool _saveFailed;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BreakTimer? _timer;
    private string? _timerText;
    private SKPoint? _snipStart;
    private readonly DisplayBounds? _snipMonitor;

    internal OverlaySession(SKBitmap? screenshot, OverlayMode mode, bool transparentBackground = false,
        AppSettings? settings = null, IScreenCapture? snapshotCapture = null, DisplayBounds? snipMonitor = null)
    {
        if (screenshot is null && (!transparentBackground || mode != OverlayMode.Draw))
        {
            throw new ArgumentException("Uma captura é obrigatória, exceto no desenho transparente.", nameof(screenshot));
        }

        settings ??= AppSettings.Default;
        _screenshot = screenshot;
        _image = screenshot is null ? null : SKImage.FromBitmap(screenshot);
        _snapshotCapture = snapshotCapture;
        _snipMonitor = mode == OverlayMode.Snip ? snipMonitor : null;
        _mode = mode;
        _transparentBackground = transparentBackground;
        _annotations = new AnnotationDocument(settings.PenColor, settings.PenWidth);
        _targetZoom = mode == OverlayMode.Zoom ? settings.InitialZoom : 1f;
        _timer = mode == OverlayMode.Timer ? new BreakTimer(_clock.Elapsed) : null;
        // A janela transparente X11 depende do visual de 32 bits do caminho em CPU.
        _gpuRequested = settings.Gpu && !transparentBackground
            && !string.Equals(SDL_GetHint(SDL_HINT_RENDER_DRIVER), "software", StringComparison.OrdinalIgnoreCase);
    }

    internal OverlayMetrics Run(CancellationToken cancellationToken = default)
    {
        var opened = Stopwatch.StartNew();
        var flags = SDL_WindowFlags.SDL_WINDOW_FULLSCREEN | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            | SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
        if (_transparentBackground)
        {
            flags |= (SDL_WindowFlags)0x0000000040000000UL;
        }

        SDL_Window* window = null;
        var initialWidth = 0;
        var initialHeight = 0;
        var wayland = string.Equals(SDL_GetCurrentVideoDriver(), "wayland", StringComparison.Ordinal);
        // O Wayland não expõe o cursor global nem posiciona janelas; o screencapture do macOS cobre só o monitor principal.
        var cursorDisplay = wayland || OperatingSystem.IsMacOS() ? null : SecondaryDisplayUnderCursor();
        if (_snipMonitor is { } snipTarget)
        {
            SDL_Rect primaryBounds;
            var primary = SDL_GetDisplayBounds(SDL_GetPrimaryDisplay(), &primaryBounds)
                && snipTarget == new DisplayBounds(primaryBounds.x, primaryBounds.y, primaryBounds.w, primaryBounds.h);
            cursorDisplay = primary ? null
                : new SDL_Rect { x = snipTarget.X, y = snipTarget.Y, w = snipTarget.Width, h = snipTarget.Height };
            DiagnosticLog.Write("DISPLAY", $"snip-placement={(primary ? "default-fullscreen" : "secondary-position")}");
        }
        if (cursorDisplay is { } placed)
        {
            flags = (flags & ~SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) | SDL_WindowFlags.SDL_WINDOW_HIDDEN;
            initialWidth = placed.w;
            initialHeight = placed.h;
        }
        else if (wayland)
        {
            SDL_Rect displayBounds;
            if (!SDL_GetDisplayBounds(SDL_GetPrimaryDisplay(), &displayBounds))
            {
                throw new InvalidOperationException($"SDL_GetDisplayBounds: {SDL_GetError()}");
            }

            initialWidth = displayBounds.w;
            initialHeight = displayBounds.h;
        }
        if (_gpuRequested)
        {
            GlOverlayTarget.PrepareWindowAttributes();
            window = SDL_CreateWindow("PixelPointer", initialWidth, initialHeight, flags | SDL_WindowFlags.SDL_WINDOW_OPENGL);
            if (window is null)
            {
                Console.Error.WriteLine($"informação: janela OpenGL indisponível ({SDL_GetError()}); usando renderização em CPU.");
            }
        }

        window = window is not null ? window : SDL_CreateWindow("PixelPointer", initialWidth, initialHeight, flags);
        if (window is null)
        {
            throw new InvalidOperationException($"SDL_CreateWindow: {SDL_GetError()}");
        }

        if (cursorDisplay is { } target)
        {
            SDL_SetWindowPosition(window, target.x, target.y);
            if (_mode == OverlayMode.Snip)
            {
                var targetPoint = new SDL_Point { x = target.x + target.w / 2, y = target.y + target.h / 2 };
                var targetDisplay = SDL_GetDisplayForPoint(&targetPoint);
                var centered = SDL_WINDOWPOS_CENTERED_DISPLAY(targetDisplay);
                SDL_SetWindowPosition(window, centered, centered);
            }
            if (!SDL_SetWindowFullscreen(window, true))
            {
                SDL_DestroyWindow(window);
                throw new InvalidOperationException($"SDL_SetWindowFullscreen: {SDL_GetError()}");
            }

            SDL_ShowWindow(window);
        }

        GlOverlayTarget? gl = null;
        if (_gpuRequested && (SDL_GetWindowFlags(window) & SDL_WindowFlags.SDL_WINDOW_OPENGL) != 0)
        {
            gl = GlOverlayTarget.TryCreate(window, out var reason);
            if (gl is null)
            {
                Console.Error.WriteLine($"informação: renderização em GPU indisponível ({reason}); usando CPU.");
            }
        }

        SDL_Renderer* renderer = null;
        if (gl is null)
        {
            renderer = SDL_CreateRenderer(window, (Utf8String)null);
            if (renderer is null)
            {
                SDL_DestroyWindow(window);
                throw new InvalidOperationException($"SDL_CreateRenderer: {SDL_GetError()}");
            }

            SDL_SetRenderVSync(renderer, 1);
        }

        SDL_RaiseWindow(window);
        _window = window;
        LogDisplayContext("overlay-start", window);

        SDL_Texture* texture = null;
        SKSurface? surface = null;
        int width = 0, height = 0;
        double firstFrameMs = 0;
        var running = true;
        var dirty = true;
        var completed = false;

        try
        {
            if (string.Equals(SDL_GetCurrentVideoDriver(), "x11", StringComparison.Ordinal))
            {
                CaptureTargetDisplay();
            }

            DiagnosticLog.Write("OVERLAY", $"start mode={_mode} transparent={_transparentBackground}");
            AppIcon.SetWindowIcon(window);
            if (string.Equals(SDL_GetCurrentVideoDriver(), "wayland", StringComparison.Ordinal)
                && (SDL_GetWindowFlags(window) & SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) == 0
                && !SDL_SetWindowFullscreen(window, true))
            {
                throw new InvalidOperationException($"SDL_SetWindowFullscreen: {SDL_GetError()}");
            }
            while (running && !cancellationToken.IsCancellationRequested)
            {
                int pw, ph;
                SDL_GetWindowSizeInPixels(window, &pw, &ph);
                if (pw != width || ph != height)
                {
                    width = _width = pw;
                    height = _height = ph;
                    _density = SDL_GetWindowPixelDensity(window);
                    _crop = ComputeCrop(window, width, height);
                    if (gl is null)
                    {
                        if (texture is not null)
                        {
                            SDL_DestroyTexture(texture);
                        }

                        surface?.Dispose();
                        texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
                            SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, width, height);
                        surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                        if (_transparentBackground && OverlayNative.SetTextureBlendMode(texture, 0x10) == 0)
                        {
                            throw new InvalidOperationException($"SDL_SetTextureBlendMode: {SDL_GetError()}");
                        }
                    }

                    dirty = true;
                }

                SDL_Event e;
                var hasEvent = SDL_WaitEventTimeout(&e, Animating ? 1 : 16);
                while (hasEvent)
                {
                    running &= Handle(e);
                    dirty = true;
                    hasEvent = SDL_PollEvent(&e);
                }

                if (_timer is not null
                    && (_timer.IsExpired(_clock.Elapsed) || BreakTimer.Format(_timer.Remaining(_clock.Elapsed)) != _timerText))
                {
                    dirty = true;
                }

                if (Animating)
                {
                    _zoom += (_targetZoom - _zoom) * 0.25f;
                    if (MathF.Abs(_targetZoom - _zoom) < 0.002f)
                    {
                        _zoom = _targetZoom;
                    }

                    dirty = true;
                }

                if (!dirty || width == 0 || height == 0 || (gl is null && (surface is null || texture is null)))
                {
                    continue;
                }

                var frame = Stopwatch.StartNew();
                if (gl is not null)
                {
                    Render(gl.GetCanvas(width, height), width, height);
                    _frameTimes.Add(frame.Elapsed.TotalMilliseconds);
                    gl.Present();
                }
                else
                {
                    Render(surface!.Canvas, width, height);
                    using (var pixmap = surface.PeekPixels())
                    {
                        SDL_UpdateTexture(texture, null, pixmap.GetPixels(), pixmap.RowBytes);
                    }

                    if (_transparentBackground
                        && (OverlayNative.SetRenderDrawColor(renderer, 0, 0, 0, 0) == 0
                            || OverlayNative.RenderClear(renderer) == 0))
                    {
                        throw new InvalidOperationException($"SDL_RenderClear: {SDL_GetError()}");
                    }

                    SDL_RenderTexture(renderer, texture, null, null);
                    _frameTimes.Add(frame.Elapsed.TotalMilliseconds);
                    SDL_RenderPresent(renderer);
                }

                if (firstFrameMs == 0)
                {
                    firstFrameMs = opened.Elapsed.TotalMilliseconds;
                }

                dirty = false;
            }

            completed = true;
            var sorted = _frameTimes.Order().ToArray();
            return new OverlayMetrics(SDL_GetCurrentVideoDriver() ?? "?",
                gl is not null ? GlOverlayTarget.Name : SDL_GetRendererName(renderer) ?? "?", gl is not null, width, height,
                _density, firstFrameMs, sorted.Length, sorted.Length == 0 ? 0 : sorted.Average(),
                sorted.Length == 0 ? 0 : sorted[(int)(sorted.Length * 0.95)], _penEvents, _maxPressure);
        }
        finally
        {
            DiagnosticLog.Write("OVERLAY",
                $"finish mode={_mode} reason={(cancellationToken.IsCancellationRequested ? "cancelled" : completed ? "closed" : "error")}");
            _image?.Dispose();
            surface?.Dispose();
            if (texture is not null)
            {
                SDL_DestroyTexture(texture);
            }

            gl?.Dispose();
            if (renderer is not null)
            {
                SDL_DestroyRenderer(renderer);
            }

            SDL_DestroyWindow(window);
        }
    }

    private bool Animating => _zoom != _targetZoom;

    /// <summary>Monitor sob o cursor quando não é o principal; no principal o caminho padrão de criação já serve.</summary>
    private static SDL_Rect? SecondaryDisplayUnderCursor()
    {
        float x, y;
        SDL_GetGlobalMouseState(&x, &y);
        var point = new SDL_Point { x = (int)x, y = (int)y };
        var display = SDL_GetDisplayForPoint(&point);
        SDL_Rect bounds;
        if (display == default || display == SDL_GetPrimaryDisplay())
        {
            DiagnosticLog.Write("DISPLAY",
                FormattableString.Invariant($"selection=primary cursor-global=({x:0.##},{y:0.##})"));
            return null;
        }

        if (!SDL_GetDisplayBounds(display, &bounds))
        {
            DiagnosticLog.Write("DISPLAY",
                FormattableString.Invariant($"selection=primary cursor-global=({x:0.##},{y:0.##}) secondary-bounds=unavailable"));
            return null;
        }

        DiagnosticLog.Write("DISPLAY", FormattableString.Invariant(
            $"selection=secondary cursor-global=({x:0.##},{y:0.##}) bounds=({bounds.x},{bounds.y},{bounds.w},{bounds.h})"));
        return bounds;
    }

    private static void LogDisplayContext(string stage, SDL_Window* window)
    {
        float x, y;
        SDL_GetGlobalMouseState(&x, &y);
        var point = new SDL_Point { x = (int)x, y = (int)y };
        var cursorDisplay = SDL_GetDisplayForPoint(&point);
        var windowDisplay = SDL_GetDisplayForWindow(window);
        DiagnosticLog.Write("DISPLAY", FormattableString.Invariant(
            $"stage={stage} cursor-global=({x:0.##},{y:0.##}) cursor-bounds={DisplayBounds(cursorDisplay)} window-bounds={DisplayBounds(windowDisplay)}"));
    }

    private static string DisplayBounds(SDL_DisplayID display)
    {
        if (display == default)
        {
            return "unknown";
        }

        SDL_Rect bounds;
        return SDL_GetDisplayBounds(display, &bounds)
            ? FormattableString.Invariant($"({bounds.x},{bounds.y},{bounds.w},{bounds.h})")
            : "unavailable";
    }

    /// <summary>A captura cobre a área de trabalho inteira; recorta o monitor onde a janela está.</summary>
    private SKRect ComputeCrop(SDL_Window* window, int width, int height)
    {
        if (_screenshot is null && _transparentBackground)
        {
            return SKRect.Create(0, 0, width, height);
        }

        var screenshot = _screenshot ?? throw new InvalidOperationException("A captura não está disponível nesta sobreposição.");
        return ComputeCrop(window, width, height, screenshot);
    }

    private static SKRect ComputeCrop(SDL_Window* window, int width, int height, SKBitmap screenshot)
    {
        SDL_Rect bounds;
        if (!SDL_GetDisplayBounds(SDL_GetDisplayForWindow(window), &bounds))
        {
            if (screenshot.Width == width && screenshot.Height == height)
            {
                return new SKRect(0, 0, screenshot.Width, screenshot.Height);
            }

            throw new InvalidOperationException($"Não foi possível identificar o monitor da captura: {SDL_GetError()}");
        }

        var displays = new List<DisplayBounds>();
        using (var ids = SDL_GetDisplays())
        {
            for (var i = 0; ids is not null && i < ids.Count; i++)
            {
                SDL_Rect other;
                if (SDL_GetDisplayBounds(ids[i], &other))
                {
                    displays.Add(new DisplayBounds(other.x, other.y, other.w, other.h));
                }
            }
        }

        return DisplayLayout.Crop(new DisplayBounds(bounds.x, bounds.y, bounds.w, bounds.h), displays,
            screenshot.Width, screenshot.Height, width, height);
    }

    private bool Handle(SDL_Event e)
    {
        LogInput(e);
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_QUIT:
            case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                DiagnosticLog.Write("OVERLAY", "close reason=window-request");
                return false;

            case SDL_EventType.SDL_EVENT_KEY_DOWN when _annotations.IsTyping:
                HandleTypingKey(e.key);
                break;

            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                return HandleKey(e.key);

            case SDL_EventType.SDL_EVENT_TEXT_INPUT when _annotations.IsTyping:
                _annotations.AppendText(Marshal.PtrToStringUTF8((nint)e.text.text) ?? string.Empty);
                break;

            case SDL_EventType.SDL_EVENT_KEY_UP when e.key.key == SDL_Keycode.SDLK_TAB:
                _tabHeld = false;
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION when !IsSynthetic(e.motion.which):
                _mouseX = e.motion.x * _density;
                _mouseY = e.motion.y * _density;
                ContinueStroke(1f);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL when _mode == OverlayMode.Snip:
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when _mode == OverlayMode.Snip && !IsSynthetic(e.button.which):
                if (e.button.button == SDL_BUTTON_RIGHT)
                {
                    return false;
                }

                _snipStart = new SKPoint(e.button.x * _density, e.button.y * _density);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when _mode == OverlayMode.Snip && _snipStart is not null
                && !IsSynthetic(e.button.which) && e.button.button == SDL_BUTTON_LEFT:
                return !FinishSnip((SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0);

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL when _timer is not null:
                _timer.Adjust(e.wheel.y > 0 ? 1 : -1, _clock.Elapsed);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when _timer is not null:
                return e.button.button != SDL_BUTTON_RIGHT;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                if ((SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0 || _mode == OverlayMode.Draw)
                {
                    _annotations.StepPenWidth(e.wheel.y > 0 ? 1 : -1);
                }
                else
                {
                    _targetZoom = Math.Clamp(_targetZoom * (e.wheel.y > 0 ? 1.25f : 0.8f), 1f, 16f);
                }

                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when !IsSynthetic(e.button.which):
                EndText();
                if (e.button.button == SDL_BUTTON_RIGHT)
                {
                    // Como no ZoomIt: o botão direito sai do desenho; no zoom, encerra.
                    if (_mode == OverlayMode.Zoom || _targetZoom <= 1f)
                    {
                        return false;
                    }

                    _mode = OverlayMode.Zoom;
                }
                else if (e.button.button == SDL_BUTTON_LEFT)
                {
                    _mode = OverlayMode.Draw;
                    BeginStroke(e.button.x * _density, e.button.y * _density, 1f);
                }

                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when !IsSynthetic(e.button.which) && e.button.button == SDL_BUTTON_LEFT:
                _annotations.EndStroke();
                break;

            case SDL_EventType.SDL_EVENT_PEN_AXIS when e.paxis.axis == SDL_PenAxis.SDL_PEN_AXIS_PRESSURE:
                _pressure = e.paxis.value;
                _maxPressure = MathF.Max(_maxPressure, _pressure);
                _penEvents++;
                break;

            case SDL_EventType.SDL_EVENT_PEN_DOWN when !e.ptouch.eraser && _timer is null && _mode != OverlayMode.Snip:
                _penDown = true;
                _mode = OverlayMode.Draw;
                _penEvents++;
                BeginStroke(e.ptouch.x * _density, e.ptouch.y * _density, _pressure);
                break;

            case SDL_EventType.SDL_EVENT_PEN_MOTION:
                _penEvents++;
                _mouseX = e.pmotion.x * _density;
                _mouseY = e.pmotion.y * _density;
                if (_penDown)
                {
                    ContinueStroke(_pressure);
                }

                break;

            case SDL_EventType.SDL_EVENT_PEN_UP:
                _penDown = false;
                _annotations.EndStroke();
                break;
        }

        return true;
    }

    private void HandleTypingKey(SDL_KeyboardEvent key)
    {
        switch (key.key)
        {
            case SDL_Keycode.SDLK_ESCAPE:
                EndText();
                break;
            case SDL_Keycode.SDLK_BACKSPACE:
                _annotations.RemoveLastCharacter();
                break;
            case SDL_Keycode.SDLK_RETURN or SDL_Keycode.SDLK_KP_ENTER:
                _annotations.AppendText("\n");
                break;
        }
    }

    private void BeginText()
    {
        _mode = OverlayMode.Draw;
        var point = ToImage(_mouseX, _mouseY);
        _annotations.BeginText(point.X, point.Y, ViewScale);
        SDL_StartTextInput(_window);
    }

    private void EndText()
    {
        if (_annotations.IsTyping)
        {
            _annotations.EndText();
            SDL_StopTextInput(_window);
        }
    }

    /// <summary>No timer só ajuste, cor e saída valem: o intervalo não é uma sessão de desenho.</summary>
    private bool HandleTimerKey(SDL_KeyboardEvent key, BreakTimer timer)
    {
        switch (key.key)
        {
            case SDL_Keycode.SDLK_ESCAPE:
                return false;
            case SDL_Keycode.SDLK_UP or SDL_Keycode.SDLK_RIGHT:
                timer.Adjust(1, _clock.Elapsed);
                break;
            case SDL_Keycode.SDLK_DOWN or SDL_Keycode.SDLK_LEFT:
                timer.Adjust(-1, _clock.Elapsed);
                break;
            case SDL_Keycode.SDLK_R:
                _annotations.SelectColor(PenColor.Red);
                break;
            case SDL_Keycode.SDLK_G:
                _annotations.SelectColor(PenColor.Green);
                break;
            case SDL_Keycode.SDLK_B:
                _annotations.SelectColor(PenColor.Blue);
                break;
            case SDL_Keycode.SDLK_Y:
                _annotations.SelectColor(PenColor.Yellow);
                break;
            case SDL_Keycode.SDLK_O:
                _annotations.SelectColor(PenColor.Orange);
                break;
            case SDL_Keycode.SDLK_P:
                _annotations.SelectColor(PenColor.Pink);
                break;
        }

        return true;
    }

    private bool HandleKey(SDL_KeyboardEvent key)
    {
        if (_timer is not null)
        {
            return HandleTimerKey(key, _timer);
        }

        if (_mode == OverlayMode.Snip)
        {
            return key.key != SDL_Keycode.SDLK_ESCAPE;
        }

        var ctrl = (key.mod & SDL_Keymod.SDL_KMOD_CTRL) != 0;
        var shift = (key.mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0;
        switch (key.key)
        {
            case SDL_Keycode.SDLK_ESCAPE:
                DiagnosticLog.Write("OVERLAY", "close reason=escape");
                return false;
            case SDL_Keycode.SDLK_T when !ctrl:
                BeginText();
                break;
            case SDL_Keycode.SDLK_TAB:
                _tabHeld = true;
                break;
            case SDL_Keycode.SDLK_UP:
                _targetZoom = Math.Clamp(_targetZoom * 1.25f, 1f, 16f);
                break;
            case SDL_Keycode.SDLK_DOWN:
                _targetZoom = Math.Clamp(_targetZoom * 0.8f, 1f, 16f);
                break;
            case SDL_Keycode.SDLK_Z when ctrl:
                _annotations.Undo();
                break;
            case SDL_Keycode.SDLK_E:
                _annotations.Clear();
                break;
            case SDL_Keycode.SDLK_W:
                _annotations.ToggleBoard(Board.White);
                break;
            case SDL_Keycode.SDLK_K:
                _annotations.ToggleBoard(Board.Black);
                break;
            case SDL_Keycode.SDLK_S when ctrl:
                SaveSnapshot();
                break;
            case SDL_Keycode.SDLK_C when ctrl:
                CopySnapshot();
                break;
            case SDL_Keycode.SDLK_R:
                _annotations.SelectColor(PenColor.Red, shift);
                break;
            case SDL_Keycode.SDLK_G:
                _annotations.SelectColor(PenColor.Green, shift);
                break;
            case SDL_Keycode.SDLK_B:
                _annotations.SelectColor(PenColor.Blue, shift);
                break;
            case SDL_Keycode.SDLK_Y:
                _annotations.SelectColor(PenColor.Yellow, shift);
                break;
            case SDL_Keycode.SDLK_O:
                _annotations.SelectColor(PenColor.Orange, shift);
                break;
            case SDL_Keycode.SDLK_P:
                _annotations.SelectColor(PenColor.Pink, shift);
                break;
            case SDL_Keycode.SDLK_X:
                _annotations.SelectBlur(shift);
                break;
        }

        return true;
    }

    private static void LogInput(SDL_Event e)
    {
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
                DiagnosticLog.Write("INPUT",
                    $"key-{(e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN ? "down" : "up")} key={FormatKey(e.key.key)} modifiers={e.key.mod}");
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                DiagnosticLog.Write("INPUT", FormattableString.Invariant(
                    $"mouse-button-{(e.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN ? "down" : "up")} button={FormatMouseButton(e.button.button)} x={e.button.x:0.##} y={e.button.y:0.##} synthetic={IsSynthetic(e.button.which)}"));
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                DiagnosticLog.Write("INPUT",
                    FormattableString.Invariant($"mouse-wheel x={e.wheel.x:0.##} y={e.wheel.y:0.##} modifiers={SDL_GetModState()}"));
                break;

            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                DiagnosticLog.Write("WINDOW", "focus=gained");
                break;

            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                DiagnosticLog.Write("WINDOW", "focus=lost");
                break;

            case SDL_EventType.SDL_EVENT_WINDOW_HIDDEN:
                DiagnosticLog.Write("WINDOW", "visibility=hidden");
                break;

            case SDL_EventType.SDL_EVENT_WINDOW_SHOWN:
                DiagnosticLog.Write("WINDOW", "visibility=shown");
                break;
        }
    }

    private static string FormatKey(SDL_Keycode key) =>
        Enum.GetName(key)?.Replace("SDLK_", string.Empty, StringComparison.Ordinal) ?? $"0x{(int)key:X}";

    private static string FormatMouseButton(byte button) => button switch
    {
        1 => "left",
        2 => "middle",
        3 => "right",
        4 => "x1",
        5 => "x2",
        _ => button.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Ponteiros sintéticos (caneta e toque emulando mouse) já chegam pelos eventos próprios.</summary>
    private static bool IsSynthetic(SDL_MouseID which) => (uint)which >= (uint)SDL_PEN_MOUSEID;

    private void BeginStroke(float x, float y, float pressure)
    {
        // A espessura fica em coordenadas da imagem para o traço acompanhar o zoom.
        var point = ToImage(x, y);
        var mod = SDL_GetModState();
        var shape = StrokeShapes.FromModifiers((mod & SDL_Keymod.SDL_KMOD_CTRL) != 0,
            (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0, _tabHeld);
        _annotations.BeginStroke(point.X, point.Y, pressure, ViewScale, shape);
    }

    private void ContinueStroke(float pressure)
    {
        var point = ToImage(_mouseX, _mouseY);
        _annotations.ContinueStroke(point.X, point.Y, pressure);
    }

    private float ViewScale => _view.Width > 0 ? _width / _view.Width : 1f;

    private SKPoint ToImage(float x, float y) => new(_view.Left + (x / ViewScale), _view.Top + (y / ViewScale));

    /// <summary>No modo Zoom a vista segue o mouse (como o ZoomIt); no modo Desenho ela fica congelada.</summary>
    private void UpdateView(int width, int height)
    {
        var viewW = _crop.Width / _zoom;
        var viewH = _crop.Height / _zoom;
        float left, top;
        if (_mode == OverlayMode.Zoom || _view.Width == 0 || Animating)
        {
            var fx = width == 0 ? 0.5f : Math.Clamp(_mouseX / width, 0f, 1f);
            var fy = height == 0 ? 0.5f : Math.Clamp(_mouseY / height, 0f, 1f);
            left = _crop.Left + (fx * (_crop.Width - viewW));
            top = _crop.Top + (fy * (_crop.Height - viewH));
        }
        else
        {
            left = _view.Left;
            top = _view.Top;
        }

        _view = SKRect.Create(left, top, viewW, viewH);
    }

    private void Render(SKCanvas canvas, int width, int height)
    {
        UpdateView(width, height);
        DrawScene(canvas, _view, width);
        if (_timer is not null)
        {
            DrawTimer(canvas, _timer, width, height);
            return;
        }

        if (_mode == OverlayMode.Snip)
        {
            DrawSnipSelection(canvas, width, height);
            return;
        }

        if (_annotations.IsTyping)
        {
            var caret = SceneRenderer.TextCaret(_annotations.Strokes[^1]);
            using var caretPaint = new SKPaint { Color = new SKColor(_annotations.ColorArgb) };
            canvas.DrawRect(SKRect.Create((caret.Left - _view.Left) * ViewScale, (caret.Top - _view.Top) * ViewScale,
                MathF.Max(2f, caret.Width * ViewScale), caret.Height * ViewScale), caretPaint);
        }
        else if (_mode == OverlayMode.Draw)
        {
            // Na caneta de desfoque, o cursor fica cinza porque a cor não é usada.
            var cursorColor = _annotations.Effect == StrokeEffect.None ? _annotations.ColorArgb : 0xFFC0C0C0u;
            using var cursor = new SKPaint { Color = new SKColor(cursorColor), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            canvas.DrawCircle(_mouseX, _mouseY, _annotations.PenWidth / 2, cursor);
        }

        if (_saveMessage is not null)
        {
            DrawSaveMessage(canvas, width);
        }
    }

    private void DrawSnipSelection(SKCanvas canvas, int width, int height)
    {
        var selection = _snipStart is { } start
            ? new SKRect(MathF.Min(start.X, _mouseX), MathF.Min(start.Y, _mouseY), MathF.Max(start.X, _mouseX), MathF.Max(start.Y, _mouseY))
            : SKRect.Empty;
        using var dim = new SKPaint { Color = new SKColor(0, 0, 0, 0x90) };
        canvas.Save();
        if (!selection.IsEmpty)
        {
            canvas.ClipRect(selection, SKClipOperation.Difference);
        }

        canvas.DrawRect(0, 0, width, height, dim);
        canvas.Restore();
        using var border = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        if (!selection.IsEmpty)
        {
            canvas.DrawRect(selection, border);
        }

        using var font = new SKFont(SKTypeface.Default, 18 * _density);
        using var hint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(_saveMessage ?? "Arraste para recortar e copiar; segure Shift ao soltar para salvar em PNG; Esc cancela",
            width / 2f, 40 * _density, SKTextAlign.Center, font, hint);
    }

    /// <summary>Copia (ou salva, com Shift) a região selecionada da captura; devolve true quando concluído.</summary>
    private bool FinishSnip(bool save)
    {
        var start = _snipStart!.Value;
        _snipStart = null;
        var a = ToImage(start.X, start.Y);
        var b = ToImage(_mouseX, _mouseY);
        var screenshot = _screenshot ?? throw new InvalidOperationException("O recorte exige uma captura.");
        if (SnipRegion.FromCorners(a.X, a.Y, b.X, b.Y, screenshot.Width, screenshot.Height) is not { } region)
        {
            return false;
        }

        try
        {
            using var subset = new SKBitmap();
            if (!screenshot.ExtractSubset(subset, SKRectI.Create(region.X, region.Y, region.Width, region.Height)))
            {
                throw new InvalidOperationException("Não foi possível recortar a captura.");
            }

            using var image = SKImage.FromBitmap(subset);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Não foi possível codificar o recorte como PNG.");
            if (save)
            {
                var path = SnapshotStorage.Save(SnapshotStorage.GetDirectory(), data.SaveTo);
                Console.Error.WriteLine($"Recorte salvo: {path}");
            }
            else
            {
                ClipboardImage.SetPng(data.AsSpan());
                Console.Error.WriteLine($"Recorte {region.Width}x{region.Height} copiado para a área de transferência.");
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException or ArgumentException)
        {
            _saveMessage = $"Não foi possível concluir o recorte: {ex.Message}";
            _saveFailed = true;
            Console.Error.WriteLine(_saveMessage);
            return false;
        }
    }

    private void DrawTimer(SKCanvas canvas, BreakTimer timer, int width, int height)
    {
        var remaining = timer.Remaining(_clock.Elapsed);
        _timerText = BreakTimer.Format(remaining);
        using var dim = new SKPaint { Color = new SKColor(0, 0, 0, 0xB0) };
        canvas.DrawRect(0, 0, width, height, dim);
        // Ao terminar, o tempo pisca a cada meio segundo.
        var visible = remaining > TimeSpan.Zero || _clock.Elapsed.Milliseconds < 500;
        using var paint = new SKPaint { Color = new SKColor(AnnotationDocument.ToArgb(_annotations.Color)), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, height / 4f);
        if (visible)
        {
            canvas.DrawText(_timerText, width / 2f, (height / 2f) + (font.Size / 3), SKTextAlign.Center, font, paint);
        }

        using var hint = new SKFont(SKTypeface.Default, 18 * _density);
        paint.Color = new SKColor(0xFFC0C0C0);
        canvas.DrawText($"{timer.Minutes} min — setas ou roda ajustam e reiniciam; Esc sai", width / 2f, height - (40 * _density),
            SKTextAlign.Center, hint, paint);
    }

    private void DrawSaveMessage(SKCanvas canvas, int width)
    {
        var padding = 12 * _density;
        using var font = new SKFont(SKTypeface.Default, 16 * _density);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var character in _saveMessage!)
        {
            if (line.Length > 0 && font.MeasureText(line + character) > width - (padding * 4))
            {
                lines.Add(line);
                line = string.Empty;
            }

            line += character;
        }

        lines.Add(line);
        var lineHeight = 22 * _density;
        paint.Color = _saveFailed ? new SKColor(0x9C, 0x20, 0x20) : new SKColor(0x20, 0x3A, 0x5E);
        canvas.DrawRect(padding, padding, width - (padding * 2), (lines.Count * lineHeight) + padding, paint);
        paint.Color = SKColors.White;
        for (var index = 0; index < lines.Count; index++)
        {
            canvas.DrawText(lines[index], padding * 2, (padding * 2) + ((index + 0.6f) * lineHeight),
                SKTextAlign.Left, font, paint);
        }
    }

    private void DrawScene(SKCanvas canvas, SKRect view, int outputWidth) =>
        SceneRenderer.Draw(canvas, _annotations, _image, _crop, view, outputWidth, _transparentBackground);

    private SKData EncodeSnapshot()
        => EncodeSnapshot(null);

    private SKData EncodeSnapshot(SKBitmap? capturedBackground)
    {
        var outputWidth = capturedBackground?.Width ?? _width;
        var outputHeight = capturedBackground?.Height ?? _height;
        using var surface = SKSurface.Create(new SKImageInfo(outputWidth, outputHeight, SKColorType.Bgra8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Não foi possível preparar a imagem da captura.");
        using var capturedImage = capturedBackground is null ? null : SKImage.FromBitmap(capturedBackground);
        var crop = capturedBackground is null
            ? _crop
            : _snapshotOverlayBounds;
        var view = capturedBackground is null ? _view : SKRect.Create(0, 0, outputWidth, outputHeight);
        SceneRenderer.Draw(surface.Canvas, _annotations, capturedImage ?? _image, crop, view, outputWidth,
            transparent: _transparentBackground && capturedBackground is null,
            annotationOffset: capturedBackground is null ? SKPoint.Empty : new SKPoint(crop.Left, crop.Top));
        return surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Não foi possível codificar a captura como PNG.");
    }

    private void CopySnapshot()
    {
        try
        {
            using var data = EncodeSnapshot();
            ClipboardImage.SetPng(data.AsSpan());
            _saveMessage = "Copiado para a área de transferência.";
            _saveFailed = false;
        }
        catch (InvalidOperationException ex)
        {
            _saveMessage = $"Não foi possível copiar a captura: {ex.Message}";
            _saveFailed = true;
        }

        Console.Error.WriteLine(_saveMessage);
    }

    private void SaveSnapshot()
    {
        DiagnosticLog.Write("SNAPSHOT", "start");
        try
        {
            var directory = SnapshotStorage.GetDirectory();
            using var capturedBackground = _snapshotCapture is null ? null : CaptureSnapshotBackground();
            using var data = EncodeSnapshot(capturedBackground);
            var path = SnapshotStorage.Save(directory, data.SaveTo);
            _saveMessage = $"Salvo: {path}";
            _saveFailed = false;
            DiagnosticLog.Write("SNAPSHOT", "saved");
        }
        catch (OperationCanceledException)
        {
            _saveMessage = "Captura cancelada.";
            _saveFailed = true;
            DiagnosticLog.Write("SNAPSHOT", "cancelled");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException or ArgumentException)
        {
            _saveMessage = $"Não foi possível salvar a captura: {ex.Message}";
            _saveFailed = true;
            DiagnosticLog.Write("SNAPSHOT", $"failed error={ex.GetType().Name} message={ex.Message}");
        }

        Console.Error.WriteLine(_saveMessage);
    }

    private SKBitmap CaptureSnapshotBackground()
    {
        LogDisplayContext("before-snapshot", _window);
        DiagnosticLog.Write("SNAPSHOT", "hide-overlay");
        SDL_HideWindow(_window);
        SKBitmap? monitorCapture = null;
        try
        {
            DiagnosticLog.Write("SNAPSHOT", $"compositor-settle ms={SnapshotOverlaySettleDelayMs}");
            SDL_Delay(SnapshotOverlaySettleDelayMs);
            using var desktop = _snapshotCapture!.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var monitorBounds = ComputeCrop(_window, _width, _height, desktop);
            monitorCapture = DisplayLayout.Crop(desktop, monitorBounds);
            _snapshotOverlayBounds = SKRect.Create(0, 0, monitorCapture.Width, monitorCapture.Height);
            DiagnosticLog.Write("SNAPSHOT", FormattableString.Invariant(
                $"background-captured source={_snapshotCapture.Name} desktop={desktop.Width}x{desktop.Height} monitor-bounds=({monitorBounds.Left:0.##},{monitorBounds.Top:0.##},{monitorBounds.Width:0.##},{monitorBounds.Height:0.##}) monitor={monitorCapture.Width}x{monitorCapture.Height}"));
        }
        catch
        {
            monitorCapture?.Dispose();
            throw;
        }
        finally
        {
            try
            {
                RestoreOverlayWindow();
                if (!SDL_RaiseWindow(_window))
                {
                    DiagnosticLog.Write("SNAPSHOT", $"raise-failed error={SDL_GetError()}");
                    throw new InvalidOperationException($"SDL_RaiseWindow: {SDL_GetError()}");
                }

                DiagnosticLog.Write("SNAPSHOT", "overlay-raised");
                LogDisplayContext("after-snapshot", _window);
            }
            catch
            {
                monitorCapture?.Dispose();
                throw;
            }
        }

        return monitorCapture ?? throw new InvalidOperationException("A captura do monitor não foi produzida.");
    }

    private void RestoreOverlayWindow()
    {
        try
        {
            if (_targetDisplayBounds is { } target)
            {
                DiagnosticLog.Write("SNAPSHOT", FormattableString.Invariant(
                    $"restore-display bounds=({target.X},{target.Y},{target.Width},{target.Height})"));
                if (!SDL_SetWindowFullscreen(_window, false))
                {
                    throw new InvalidOperationException($"SDL_SetWindowFullscreen(false): {SDL_GetError()}");
                }

                SDL_SetWindowPosition(_window, target.X, target.Y);
                if (!SDL_SetWindowFullscreen(_window, true))
                {
                    throw new InvalidOperationException($"SDL_SetWindowFullscreen(true): {SDL_GetError()}");
                }
            }
        }
        finally
        {
            SDL_ShowWindow(_window);
            DiagnosticLog.Write("SNAPSHOT", "show-overlay");
        }
    }

    private void CaptureTargetDisplay()
    {
        var display = SDL_GetDisplayForWindow(_window);
        SDL_Rect bounds;
        if (display == default || !SDL_GetDisplayBounds(display, &bounds))
        {
            throw new InvalidOperationException($"Não foi possível identificar o monitor da sobreposição: {SDL_GetError()}");
        }

        _targetDisplayBounds = new DisplayBounds(bounds.x, bounds.y, bounds.w, bounds.h);
        DiagnosticLog.Write("DISPLAY", FormattableString.Invariant(
            $"restore-target bounds=({bounds.x},{bounds.y},{bounds.w},{bounds.h})"));
    }

    private static partial class OverlayNative
    {
        [LibraryImport("SDL3", EntryPoint = "SDL_SetTextureBlendMode")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial byte SetTextureBlendMode(SDL_Texture* texture, uint blendMode);

        [LibraryImport("SDL3", EntryPoint = "SDL_SetRenderDrawColor")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial byte SetRenderDrawColor(SDL_Renderer* renderer, byte red, byte green, byte blue, byte alpha);

        [LibraryImport("SDL3", EntryPoint = "SDL_RenderClear")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial byte RenderClear(SDL_Renderer* renderer);
    }
}
