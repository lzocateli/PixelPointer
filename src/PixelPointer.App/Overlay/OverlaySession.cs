using System.Diagnostics;
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
internal sealed record OverlayMetrics(string VideoDriver, string Renderer, int PixelWidth, int PixelHeight, float PixelDensity,
    double WindowToFirstFrameMs, int Frames, double AverageFrameMs, double P95FrameMs, int PenEvents, float MaxPressure);

/// <summary>
/// Janela de sobreposição em tela cheia: SDL3 cria a janela e entrega a entrada; o Skia desenha em CPU
/// num buffer que é enviado como textura. Implementa o essencial do Zoom (Ctrl+1) e do Desenho (Ctrl+2).
/// </summary>
internal sealed unsafe class OverlaySession
{
    private static readonly SKColor[] PenColors =
    [
        SKColors.Red, SKColors.LimeGreen, SKColors.DodgerBlue, SKColors.Yellow, SKColors.Orange, SKColors.HotPink,
    ];

    private readonly SKBitmap _screenshot;
    private readonly SKImage _image;
    private readonly List<Stroke> _strokes = [];
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
    private Stroke? _current;
    private SKColor _color = SKColors.Red;
    private float _penWidth = 6f;
    private SKColor? _board;
    private bool _penDown;
    private float _pressure = 1f;
    private int _penEvents;
    private float _maxPressure;

    internal OverlaySession(SKBitmap screenshot, OverlayMode mode)
    {
        _screenshot = screenshot;
        _image = SKImage.FromBitmap(screenshot);
        _mode = mode;
        _targetZoom = mode == OverlayMode.Zoom ? 2f : 1f;
    }

    internal OverlayMetrics Run()
    {
        var opened = Stopwatch.StartNew();
        var flags = SDL_WindowFlags.SDL_WINDOW_FULLSCREEN | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            | SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
        var window = SDL_CreateWindow("PixelPointer", 0, 0, flags);
        if (window is null)
        {
            throw new InvalidOperationException($"SDL_CreateWindow: {SDL_GetError()}");
        }

        var renderer = SDL_CreateRenderer(window, (Utf8String)null);
        if (renderer is null)
        {
            SDL_DestroyWindow(window);
            throw new InvalidOperationException($"SDL_CreateRenderer: {SDL_GetError()}");
        }

        SDL_SetRenderVSync(renderer, 1);
        SDL_RaiseWindow(window);

        SDL_Texture* texture = null;
        SKSurface? surface = null;
        int width = 0, height = 0;
        double firstFrameMs = 0;
        var running = true;
        var dirty = true;

        try
        {
            while (running)
            {
                int pw, ph;
                SDL_GetWindowSizeInPixels(window, &pw, &ph);
                if (pw != width || ph != height)
                {
                    width = _width = pw;
                    height = _height = ph;
                    _density = SDL_GetWindowPixelDensity(window);
                    if (texture is not null)
                    {
                        SDL_DestroyTexture(texture);
                    }

                    surface?.Dispose();
                    texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
                        SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, width, height);
                    surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                    _crop = ComputeCrop(window, width, height);
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

                if (!dirty || surface is null || texture is null)
                {
                    continue;
                }

                var frame = Stopwatch.StartNew();
                Render(surface.Canvas, width, height);
                using (var pixmap = surface.PeekPixels())
                {
                    SDL_UpdateTexture(texture, null, pixmap.GetPixels(), pixmap.RowBytes);
                }

                SDL_RenderTexture(renderer, texture, null, null);
                _frameTimes.Add(frame.Elapsed.TotalMilliseconds);
                SDL_RenderPresent(renderer);
                if (firstFrameMs == 0)
                {
                    firstFrameMs = opened.Elapsed.TotalMilliseconds;
                }

                dirty = false;
            }

            var sorted = _frameTimes.Order().ToArray();
            return new OverlayMetrics(SDL_GetCurrentVideoDriver() ?? "?", SDL_GetRendererName(renderer) ?? "?", width, height,
                _density, firstFrameMs, sorted.Length, sorted.Length == 0 ? 0 : sorted.Average(),
                sorted.Length == 0 ? 0 : sorted[(int)(sorted.Length * 0.95)], _penEvents, _maxPressure);
        }
        finally
        {
            _image.Dispose();
            surface?.Dispose();
            if (texture is not null)
            {
                SDL_DestroyTexture(texture);
            }

            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
        }
    }

    private bool Animating => _zoom != _targetZoom;

    /// <summary>A captura cobre a área de trabalho inteira; recorta o monitor onde a janela está.</summary>
    private SKRect ComputeCrop(SDL_Window* window, int width, int height)
    {
        SDL_Rect bounds;
        var display = SDL_GetDisplayForWindow(window);
        if (_screenshot.Width == width && _screenshot.Height == height || !SDL_GetDisplayBounds(display, &bounds) || bounds.w == 0)
        {
            return new SKRect(0, 0, _screenshot.Width, _screenshot.Height);
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
                _current?.Points.Add(new StrokePoint(ToImage(_mouseX, _mouseY), 1f));
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                if ((SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0 || _mode == OverlayMode.Draw)
                {
                    _penWidth = Math.Clamp(_penWidth + (e.wheel.y > 0 ? 2f : -2f), 1f, 60f);
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
                _current = null;
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
                    _current?.Points.Add(new StrokePoint(ToImage(_mouseX, _mouseY), _pressure));
                }

                break;

            case SDL_EventType.SDL_EVENT_PEN_UP:
                _penDown = false;
                _current = null;
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
                if (_strokes.Count > 0)
                {
                    _strokes.RemoveAt(_strokes.Count - 1);
                }

                break;
            case SDL_Keycode.SDLK_E:
                _strokes.Clear();
                break;
            case SDL_Keycode.SDLK_W:
                _board = _board == SKColors.White ? null : SKColors.White;
                break;
            case SDL_Keycode.SDLK_K:
                _board = _board == SKColors.Black ? null : SKColors.Black;
                break;
            case SDL_Keycode.SDLK_S when ctrl:
                SaveSnapshot();
                break;
            case SDL_Keycode.SDLK_R:
                _color = PenColors[0];
                break;
            case SDL_Keycode.SDLK_G:
                _color = PenColors[1];
                break;
            case SDL_Keycode.SDLK_B:
                _color = PenColors[2];
                break;
            case SDL_Keycode.SDLK_Y:
                _color = PenColors[3];
                break;
            case SDL_Keycode.SDLK_O:
                _color = PenColors[4];
                break;
            case SDL_Keycode.SDLK_P:
                _color = PenColors[5];
                break;
        }

        return true;
    }

    /// <summary>Ponteiros sintéticos (caneta e toque emulando mouse) já chegam pelos eventos próprios.</summary>
    private static bool IsSynthetic(SDL_MouseID which) => (uint)which >= (uint)SDL_PEN_MOUSEID;

    private void BeginStroke(float x, float y, float pressure)
    {
        // A espessura fica em coordenadas da imagem para o traço acompanhar o zoom.
        _current = new Stroke(_color, _penWidth / ViewScale, [new StrokePoint(ToImage(x, y), pressure)]);
        _strokes.Add(_current);
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
            using var cursor = new SKPaint { Color = _color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            canvas.DrawCircle(_mouseX, _mouseY, _penWidth / 2, cursor);
        }
    }

    private void DrawScene(SKCanvas canvas, SKRect view, int outputWidth)
    {
        canvas.Clear(SKColors.Black);
        canvas.Save();
        canvas.Scale(outputWidth / view.Width);
        canvas.Translate(-view.Left, -view.Top);
        if (_board is { } board)
        {
            using var fill = new SKPaint { Color = board };
            canvas.DrawRect(_crop, fill);
        }
        else
        {
            canvas.DrawImage(_image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        foreach (var stroke in _strokes)
        {
            DrawStroke(canvas, stroke);
        }

        canvas.Restore();
    }

    private static void DrawStroke(SKCanvas canvas, Stroke stroke)
    {
        using var paint = new SKPaint
        {
            Color = stroke.Color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = stroke.Width,
        };

        if (stroke.Points.Count == 1)
        {
            canvas.DrawPoint(stroke.Points[0].Position, paint);
            return;
        }

        if (stroke.Points.TrueForAll(p => p.Pressure >= 0.999f))
        {
            using var builder = new SKPathBuilder();
            builder.MoveTo(stroke.Points[0].Position);
            for (var i = 1; i < stroke.Points.Count; i++)
            {
                builder.LineTo(stroke.Points[i].Position);
            }

            using var path = builder.Detach();
            canvas.DrawPath(path, paint);
            return;
        }

        // Caneta com pressão: espessura variável por segmento.
        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var a = stroke.Points[i - 1];
            var b = stroke.Points[i];
            paint.StrokeWidth = stroke.Width * MathF.Max(0.15f, (a.Pressure + b.Pressure) / 2);
            canvas.DrawLine(a.Position, b.Position, paint);
        }
    }

    private void SaveSnapshot()
    {
        using var surface = SKSurface.Create(new SKImageInfo(_width, _height));
        DrawScene(surface.Canvas, _view, _width);
        using var data = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(Environment.CurrentDirectory, $"pixelpointer-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var file = File.Create(path);
        data.SaveTo(file);
        Console.Error.WriteLine($"Salvo: {path}");
    }

    private readonly record struct StrokePoint(SKPoint Position, float Pressure);

    private sealed record Stroke(SKColor Color, float Width, List<StrokePoint> Points);
}
