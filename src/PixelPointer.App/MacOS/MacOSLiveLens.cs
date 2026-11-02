using PixelPointer.Core.LiveZoom;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.MacOS;

internal sealed unsafe class MacOSLiveLens : ILiveLens
{
    private const int WaitMilliseconds = 16;
    private const int Gap = 24;

    private readonly SDL_Rect _displayBounds;
    private readonly int _lensWidth;
    private readonly SDL_Window* _window;
    private readonly SDL_Renderer* _renderer;
    private readonly ScreenCaptureKitFrameSource _capture;
    private SDL_Texture* _texture;
    private int _textureWidth;
    private int _textureHeight;
    private bool _suspended;
    private bool _shown;
    private int _disposed;

    private MacOSLiveLens(float level)
    {
        Level = Math.Clamp(level, LiveZoomView.MinLevel, LiveZoomView.MaxLevel);
        SDL_Rect displayBounds;
        if (!SDL_GetDisplayBounds(SDL_GetPrimaryDisplay(), &displayBounds))
        {
            throw new InvalidOperationException($"SDL_GetDisplayBounds: {SDL_GetError()}");
        }
        _displayBounds = displayBounds;

        var width = Math.Min(_displayBounds.w, Math.Max(160, _displayBounds.w / 3));
        var height = Math.Min(_displayBounds.h, Math.Max(90, _displayBounds.h / 3));
        _lensWidth = width;
        var flags = SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP
            | SDL_WindowFlags.SDL_WINDOW_UTILITY | SDL_WindowFlags.SDL_WINDOW_NOT_FOCUSABLE | SDL_WindowFlags.SDL_WINDOW_HIDDEN;
        _window = SDL_CreateWindow("PixelPointer Live Zoom", width, height, flags);
        _renderer = _window is null ? null : SDL_CreateRenderer(_window, (Utf8String)null);
        try
        {
            if (_renderer is null)
            {
                throw new InvalidOperationException($"Não foi possível criar a lupa: {SDL_GetError()}");
            }

            _capture = ScreenCaptureKitFrameSource.Start();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public float Level { get; }

    internal static MacOSLiveLens Start(float level)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new NotSupportedException("O Live Zoom do macOS exige uma sessão macOS.");
        }

        return new MacOSLiveLens(level);
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

        float cursorX, cursorY;
        SDL_GetGlobalMouseState(&cursorX, &cursorY);
        int windowWidth, windowHeight;
        SDL_GetWindowSizeInPixels(_window, &windowWidth, &windowHeight);
        var scaleX = (float)_capture.DisplayWidth / _displayBounds.w;
        var scaleY = (float)_capture.DisplayHeight / _displayBounds.h;
        var pixelX = Math.Clamp((int)((cursorX - _displayBounds.x) * scaleX), 0, _capture.DisplayWidth - 1);
        var pixelY = Math.Clamp((int)((cursorY - _displayBounds.y) * scaleY), 0, _capture.DisplayHeight - 1);
        var sourceWidth = Math.Min(LiveZoomView.CropExtent(windowWidth, Level), _capture.DisplayWidth);
        var sourceHeight = Math.Min(LiveZoomView.CropExtent(windowHeight, Level), _capture.DisplayHeight);
        var source = LiveZoomView.CropAroundCursor(pixelX, pixelY, _capture.DisplayWidth,
            _capture.DisplayHeight, sourceWidth, sourceHeight);
        var frame = _capture.CaptureCrop(source.X, source.Y, source.Width, source.Height);
        if (frame is null)
        {
            return;
        }

        if (_texture is null || _textureWidth != frame.Width || _textureHeight != frame.Height)
        {
            if (_texture is not null)
            {
                SDL_DestroyTexture(_texture);
            }

            _texture = SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
                SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, frame.Width, frame.Height);
            if (_texture is null)
            {
                throw new InvalidOperationException($"Não foi possível criar a textura da lupa: {SDL_GetError()}");
            }

            _textureWidth = frame.Width;
            _textureHeight = frame.Height;
        }

        if (!SDL_UpdateTexture(_texture, null, (nint)frame.GetPixels(), frame.RowBytes))
        {
            throw new InvalidOperationException($"Não foi possível atualizar a textura da lupa: {SDL_GetError()}");
        }

        if (!_shown)
        {
            SDL_ShowWindow(_window);
            SDL_SetWindowPosition(_window, _displayBounds.x + _displayBounds.w - _lensWidth - Gap,
                _displayBounds.y + Gap);
            _shown = true;
        }

        SDL_RenderTexture(_renderer, _texture, null, null);
        var crossX = (pixelX - source.X) * windowWidth / source.Width;
        var crossY = (pixelY - source.Y) * windowHeight / source.Height;
        SDL_SetRenderDrawColor(_renderer, 255, 0, 0, 255);
        SDL_RenderLine(_renderer, crossX - 12, crossY, crossX + 12, crossY);
        SDL_RenderLine(_renderer, crossX, crossY - 12, crossX, crossY + 12);
        SDL_RenderPresent(_renderer);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
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

        if (_capture is not null)
        {
            _capture.Dispose();
        }
    }
}
