using PixelPointer.App.Capture;
using PixelPointer.Core.LiveZoom;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Linux;

internal sealed unsafe class WaylandLiveLens : ILiveLens
{
    private const int WaitMilliseconds = 16;
    private const int CaptureIntervalMilliseconds = 25;

    private readonly SDL_Rect _displayBounds;
    private readonly SDL_Window* _window;
    private readonly SDL_Renderer* _renderer;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _frameLock = new();
    private readonly Task? _captureTask;
    private SKBitmap? _latestFrame;
    private SpaCursorPosition? _cursor;
    private SDL_Texture* _texture;
    private int _textureWidth;
    private int _textureHeight;
    private bool _suspended;
    private bool _shown;
    private int _disposed;

    private WaylandLiveLens(float level, PortalClient portal)
    {
        Level = Math.Clamp(level, LiveZoomView.MinLevel, LiveZoomView.MaxLevel);
        using var displays = SDL_GetDisplays();
        if (displays is null || displays.Count != 1)
        {
            throw new NotSupportedException("O Live Zoom Wayland exige somente um monitor nesta versão.");
        }

        var display = SDL_GetPrimaryDisplay();
        SDL_Rect displayBounds;
        if (!SDL_GetDisplayBounds(display, &displayBounds))
        {
            throw new InvalidOperationException($"SDL_GetDisplayBounds: {SDL_GetError()}");
        }
        _displayBounds = displayBounds;

        var flags = SDL_WindowFlags.SDL_WINDOW_FULLSCREEN | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            | SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP
            | SDL_WindowFlags.SDL_WINDOW_NOT_FOCUSABLE | SDL_WindowFlags.SDL_WINDOW_TRANSPARENT
            | SDL_WindowFlags.SDL_WINDOW_HIDDEN;
        _window = SDL_CreateWindow("PixelPointer Live Zoom", _displayBounds.w, _displayBounds.h, flags);
        _renderer = _window is null ? null : SDL_CreateRenderer(_window, (Utf8String)null);
        try
        {
            if (_renderer is null)
            {
                throw new InvalidOperationException($"Não foi possível criar a lupa: {SDL_GetError()}");
            }

            var token = _stop.Token;
            _captureTask = WaylandCapturePump.Start(portal, token, PublishFrame);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public float Level { get; }

    internal static WaylandLiveLens Start(float level, PortalClient portal)
    {
        if (!OperatingSystem.IsLinux() || !SessionInfo.IsWayland)
        {
            throw new NotSupportedException("O Live Zoom Wayland exige uma sessão Wayland.");
        }

        return new WaylandLiveLens(level, portal);
    }

    public void Suspend()
    {
        _suspended = true;
        SDL_HideWindow(_window);
        _shown = false;
    }

    public void Resume() => _suspended = false;

    public void Tick()
    {
        SDL_Event e;
        if (SDL_WaitEventTimeout(&e, WaitMilliseconds))
        {
            while (SDL_PollEvent(&e))
            {
            }
        }

        if (_suspended)
        {
            return;
        }

        if (_captureTask?.IsFaulted == true)
        {
            var failure = _captureTask.Exception?.GetBaseException();
            throw new InvalidOperationException($"Não foi possível iniciar o Live Zoom Wayland: {failure?.Message}", failure);
        }

        int windowWidth, windowHeight;
        if (!SDL_GetWindowSizeInPixels(_window, &windowWidth, &windowHeight))
        {
            throw new InvalidOperationException($"SDL_GetWindowSizeInPixels: {SDL_GetError()}");
        }

        lock (_frameLock)
        {
            if (_latestFrame is null || _cursor is not { } cursor)
            {
                return;
            }

            var screen = new ScreenRect(0, 0, _latestFrame.Width, _latestFrame.Height);
            var layout = LensLayout.Compute(cursor.X, cursor.Y, screen, Level);
            if (_texture is null || _textureWidth != layout.Source.Width || _textureHeight != layout.Source.Height)
            {
                if (_texture is not null)
                {
                    SDL_DestroyTexture(_texture);
                }

                _texture = SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
                    SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, layout.Source.Width, layout.Source.Height);
                if (_texture is null)
                {
                    throw new InvalidOperationException($"Não foi possível criar a textura da lupa: {SDL_GetError()}");
                }

                _textureWidth = layout.Source.Width;
                _textureHeight = layout.Source.Height;
            }

            var pixels = (byte*)_latestFrame.GetPixels() + (layout.Source.Y * _latestFrame.RowBytes)
                + (layout.Source.X * sizeof(uint));
            if (!SDL_UpdateTexture(_texture, null, (nint)pixels, _latestFrame.RowBytes))
            {
                throw new InvalidOperationException($"Não foi possível atualizar a textura da lupa: {SDL_GetError()}");
            }

            if (!_shown)
            {
                if (!WaylandInputRegion.SetEmpty(_window))
                {
                    return;
                }

                SDL_ShowWindow(_window);
                if (!WaylandInputRegion.SetEmpty(_window))
                {
                    SDL_HideWindow(_window);
                    return;
                }

                _shown = true;
            }

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);
            SDL_RenderClear(_renderer);
            var scaleX = (float)windowWidth / _latestFrame.Width;
            var scaleY = (float)windowHeight / _latestFrame.Height;
            var destination = new SDL_FRect
            {
                x = layout.Lens.X * scaleX,
                y = layout.Lens.Y * scaleY,
                w = layout.Lens.Width * scaleX,
                h = layout.Lens.Height * scaleY,
            };
            SDL_RenderTexture(_renderer, _texture, null, &destination);

            var crossX = (cursor.X - layout.Source.X) * destination.w / layout.Source.Width + destination.x;
            var crossY = (cursor.Y - layout.Source.Y) * destination.h / layout.Source.Height + destination.y;
            SDL_SetRenderDrawColor(_renderer, 255, 0, 0, 255);
            SDL_RenderLine(_renderer, crossX - 12, crossY, crossX + 12, crossY);
            SDL_RenderLine(_renderer, crossX, crossY - 12, crossX, crossY + 12);
            SDL_SetRenderDrawColor(_renderer, 32, 58, 94, 255);
            var frame = new SDL_FRect { x = destination.x, y = destination.y, w = destination.w, h = destination.h };
            SDL_RenderRect(_renderer, &frame);
            SDL_RenderPresent(_renderer);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        if (_captureTask is not null)
        {
            try
            {
                _captureTask.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine("aviso: a sessão ScreenCast Wayland não encerrou em 5 s.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"aviso: falha ao encerrar o Live Zoom Wayland ({ex.GetType().Name}).");
            }
        }
        _stop.Dispose();

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }

        if (_texture is not null)
        {
            SDL_DestroyTexture(_texture);
        }

        if (_renderer is not null)
        {
            SDL_DestroyRenderer(_renderer);
        }

        if (_window is not null)
        {
            SDL_DestroyWindow(_window);
        }
    }

    private void PublishFrame(SKBitmap frame, SpaCursorPosition? cursor)
    {
        lock (_frameLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                frame.Dispose();
                return;
            }

            _latestFrame?.Dispose();
            _latestFrame = frame;
            if (cursor is not null)
            {
                _cursor = cursor;
            }
        }
    }
}
