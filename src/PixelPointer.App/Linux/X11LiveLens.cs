using PixelPointer.Core.LiveZoom;
using SDL;
using static SDL.SDL3;

namespace PixelPointer.App.Linux;

/// <summary>
/// Live Zoom no X11 como lupa em janela: o X11 não permite excluir a própria janela da captura, então a lupa fica
/// fora da região ampliada (<see cref="LensLayout"/>). Atualizada pelo laço principal do daemon, que é dono do SDL.
/// </summary>
internal sealed unsafe class X11LiveLens : IDisposable
{
    private const int WaitMilliseconds = 16;

    private readonly nint _display;
    private readonly nint _root;
    private readonly SDL_Window* _window;
    private readonly SDL_Renderer* _renderer;
    private SDL_Texture* _texture;
    private int _textureWidth;
    private int _textureHeight;
    private ScreenRect _lens;
    private bool _suspended;
    private bool _shown;
    private int _disposed;

    private X11LiveLens(float level, nint display)
    {
        Level = Math.Clamp(level, LiveZoomView.MinLevel, LiveZoomView.MaxLevel);
        _display = display;
        _root = Xlib.XDefaultRootWindow(display);
        var flags = SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP
            | SDL_WindowFlags.SDL_WINDOW_UTILITY | SDL_WindowFlags.SDL_WINDOW_NOT_FOCUSABLE | SDL_WindowFlags.SDL_WINDOW_HIDDEN;
        _window = SDL_CreateWindow("PixelPointer Live Zoom", 320, 180, flags);
        _renderer = _window is null ? null : SDL_CreateRenderer(_window, (Utf8String)null);
        if (_renderer is null)
        {
            var error = SDL_GetError();
            Dispose();
            throw new InvalidOperationException($"Não foi possível criar a lupa: {error}");
        }
    }

    internal float Level { get; }

    internal static X11LiveLens Start(float level)
    {
        if (!OperatingSystem.IsLinux() || SessionInfo.IsWayland)
        {
            throw new NotSupportedException("A lupa em janela exige uma sessão X11.");
        }

        var display = Xlib.XOpenDisplay(null);
        return display == 0
            ? throw new InvalidOperationException("Não foi possível abrir o display X11.")
            : new X11LiveLens(level, display);
    }

    /// <summary>Esconde a lupa enquanto uma sobreposição captura e ocupa a tela.</summary>
    internal void Suspend()
    {
        _suspended = true;
        SDL_HideWindow(_window);
        _shown = false;
    }

    internal void Resume() => _suspended = false;

    /// <summary>Atende os eventos do SDL (inclusive a tray) e redesenha a lupa onde o cursor estiver.</summary>
    internal void Tick()
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

        float x, y;
        SDL_GetGlobalMouseState(&x, &y);
        var point = new SDL_Point { x = (int)x, y = (int)y };
        SDL_Rect bounds;
        if (!SDL_GetDisplayBounds(SDL_GetDisplayForPoint(&point), &bounds))
        {
            return;
        }

        var layout = LensLayout.Compute(point.x, point.y, new ScreenRect(bounds.x, bounds.y, bounds.w, bounds.h), Level);
        if (!CaptureSource(layout.Source))
        {
            return;
        }

        if (layout.Lens != _lens)
        {
            SDL_SetWindowSize(_window, layout.Lens.Width, layout.Lens.Height);
            SDL_SetWindowPosition(_window, layout.Lens.X, layout.Lens.Y);
            _lens = layout.Lens;
        }

        if (!_shown)
        {
            SDL_ShowWindow(_window);
            // Alguns gerenciadores ignoram a posição pedida antes do mapeamento.
            SDL_SetWindowPosition(_window, layout.Lens.X, layout.Lens.Y);
            _shown = true;
        }

        SDL_RenderTexture(_renderer, _texture, null, null);
        var scale = (float)layout.Lens.Width / layout.Source.Width;
        var cursorX = (point.x - layout.Source.X) * scale;
        var cursorY = (point.y - layout.Source.Y) * scale;
        SDL_SetRenderDrawColor(_renderer, 255, 0, 0, 255);
        SDL_RenderLine(_renderer, cursorX - 12, cursorY, cursorX + 12, cursorY);
        SDL_RenderLine(_renderer, cursorX, cursorY - 12, cursorX, cursorY + 12);
        SDL_SetRenderDrawColor(_renderer, 32, 58, 94, 255);
        var frame = new SDL_FRect { x = 0, y = 0, w = layout.Lens.Width, h = layout.Lens.Height };
        SDL_RenderRect(_renderer, &frame);
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

        Xlib.XCloseDisplay(_display);
    }

    private bool CaptureSource(ScreenRect source)
    {
        var image = Xlib.XGetImage(_display, _root, source.X, source.Y, (uint)source.Width, (uint)source.Height, ~0UL, Xlib.ZPixmap);
        if (image == 0)
        {
            return false;
        }

        try
        {
            var header = (Xlib.XImage*)image;
            if (header->bits_per_pixel != 32)
            {
                throw new NotSupportedException($"Profundidade de cor não suportada: {header->bits_per_pixel} bpp.");
            }

            if (_texture is null || _textureWidth != source.Width || _textureHeight != source.Height)
            {
                if (_texture is not null)
                {
                    SDL_DestroyTexture(_texture);
                }

                // XRGB ignora o canal alfa indefinido de visuais de 24 bits.
                _texture = SDL_CreateTexture(_renderer, SDL_PixelFormat.SDL_PIXELFORMAT_XRGB8888,
                    SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, source.Width, source.Height);
                if (_texture is null)
                {
                    return false;
                }

                _textureWidth = source.Width;
                _textureHeight = source.Height;
            }

            return SDL_UpdateTexture(_texture, null, header->data, header->bytes_per_line);
        }
        finally
        {
            Xlib.XDestroyImage(image);
        }
    }
}
