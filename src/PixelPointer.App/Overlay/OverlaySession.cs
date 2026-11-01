using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelPointer.Core.Annotations;
using PixelPointer.Core.Settings;
using PixelPointer.Rendering;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Overlay;

internal enum OverlayMode
{
    Zoom,
    Draw,
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
    private readonly SKBitmap? _screenshot;
    private readonly SKImage? _image;
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
    private readonly bool _transparentBackground;
    private readonly bool _gpuRequested;
    private bool _penDown;
    private float _pressure = 1f;
    private int _penEvents;
    private float _maxPressure;
    private string? _saveMessage;
    private bool _saveFailed;

    internal OverlaySession(SKBitmap? screenshot, OverlayMode mode, bool transparentBackground = false,
        AppSettings? settings = null)
    {
        if (screenshot is null && (!transparentBackground || mode != OverlayMode.Draw))
        {
            throw new ArgumentException("Uma captura é obrigatória, exceto no desenho transparente.", nameof(screenshot));
        }

        settings ??= AppSettings.Default;
        _screenshot = screenshot;
        _image = screenshot is null ? null : SKImage.FromBitmap(screenshot);
        _mode = mode;
        _transparentBackground = transparentBackground;
        _annotations = new AnnotationDocument(settings.PenColor, settings.PenWidth);
        _targetZoom = mode == OverlayMode.Zoom ? settings.InitialZoom : 1f;
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
        if (string.Equals(SDL_GetCurrentVideoDriver(), "wayland", StringComparison.Ordinal))
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

        SDL_Texture* texture = null;
        SKSurface? surface = null;
        int width = 0, height = 0;
        double firstFrameMs = 0;
        var running = true;
        var dirty = true;

        try
        {
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

            var sorted = _frameTimes.Order().ToArray();
            return new OverlayMetrics(SDL_GetCurrentVideoDriver() ?? "?",
                gl is not null ? GlOverlayTarget.Name : SDL_GetRendererName(renderer) ?? "?", gl is not null, width, height,
                _density, firstFrameMs, sorted.Length, sorted.Length == 0 ? 0 : sorted.Average(),
                sorted.Length == 0 ? 0 : sorted[(int)(sorted.Length * 0.95)], _penEvents, _maxPressure);
        }
        finally
        {
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

    /// <summary>A captura cobre a área de trabalho inteira; recorta o monitor onde a janela está.</summary>
    private SKRect ComputeCrop(SDL_Window* window, int width, int height)
    {
        if (_transparentBackground)
        {
            return SKRect.Create(0, 0, width, height);
        }

        var screenshot = _screenshot ?? throw new InvalidOperationException("A captura não está disponível nesta sobreposição.");
        SDL_Rect bounds;
        var display = SDL_GetDisplayForWindow(window);
        if (screenshot.Width == width && screenshot.Height == height
            || !SDL_GetDisplayBounds(display, &bounds) || bounds.w == 0)
        {
            return new SKRect(0, 0, screenshot.Width, screenshot.Height);
        }

        var scale = (float)width / bounds.w;
        return SKRect.Create(bounds.x * scale, bounds.y * scale, width, height);
    }

    private bool Handle(SDL_Event e)
    {
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_QUIT:
            case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                return false;

            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                return HandleKey(e.key);

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION when !IsSynthetic(e.motion.which):
                _mouseX = e.motion.x * _density;
                _mouseY = e.motion.y * _density;
                ContinueStroke(1f);
                break;

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

            case SDL_EventType.SDL_EVENT_PEN_DOWN when !e.ptouch.eraser:
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

    private bool HandleKey(SDL_KeyboardEvent key)
    {
        var ctrl = (key.mod & SDL_Keymod.SDL_KMOD_CTRL) != 0;
        switch (key.key)
        {
            case SDL_Keycode.SDLK_ESCAPE:
                return false;
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

    /// <summary>Ponteiros sintéticos (caneta e toque emulando mouse) já chegam pelos eventos próprios.</summary>
    private static bool IsSynthetic(SDL_MouseID which) => (uint)which >= (uint)SDL_PEN_MOUSEID;

    private void BeginStroke(float x, float y, float pressure)
    {
        // A espessura fica em coordenadas da imagem para o traço acompanhar o zoom.
        var point = ToImage(x, y);
        _annotations.BeginStroke(point.X, point.Y, pressure, ViewScale);
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
        if (_mode == OverlayMode.Draw)
        {
            using var cursor = new SKPaint { Color = new SKColor(_annotations.ColorArgb), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            canvas.DrawCircle(_mouseX, _mouseY, _annotations.PenWidth / 2, cursor);
        }

        if (_saveMessage is not null)
        {
            DrawSaveMessage(canvas, width);
        }
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

    private void SaveSnapshot()
    {
        try
        {
            var directory = SnapshotStorage.GetDirectory();
            using var surface = SKSurface.Create(new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException("Não foi possível preparar a imagem da captura.");
            DrawScene(surface.Canvas, _view, _width);
            using var data = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Não foi possível codificar a captura como PNG.");
            var path = SnapshotStorage.Save(directory, data.SaveTo);
            _saveMessage = $"Salvo: {path}";
            _saveFailed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException or ArgumentException)
        {
            _saveMessage = $"Não foi possível salvar a captura: {ex.Message}";
            _saveFailed = true;
        }

        Console.Error.WriteLine(_saveMessage);
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
