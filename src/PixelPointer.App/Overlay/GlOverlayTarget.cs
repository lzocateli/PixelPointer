using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Overlay;

/// <summary>Superfície Skia sobre o framebuffer OpenGL da janela SDL; a criação falha sem efeitos colaterais.</summary>
internal sealed unsafe class GlOverlayTarget : IDisposable
{
    private const uint GlRgba8 = 0x8058;

    private readonly SDL_Window* _window;
    private readonly SDL_GLContextState* _glContext;
    private readonly GRGlInterface _interface;
    private readonly GRContext _context;
    private GRBackendRenderTarget? _target;
    private SKSurface? _surface;
    private int _width;
    private int _height;

    private GlOverlayTarget(SDL_Window* window, SDL_GLContextState* glContext, GRGlInterface glInterface, GRContext context)
    {
        _window = window;
        _glContext = glContext;
        _interface = glInterface;
        _context = context;
    }

    /// <summary>Atributos lidos pelo SDL na criação da janela com SDL_WINDOW_OPENGL.</summary>
    internal static void PrepareWindowAttributes()
    {
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DOUBLEBUFFER, 1);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_RED_SIZE, 8);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_GREEN_SIZE, 8);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_BLUE_SIZE, 8);
    }

    internal static GlOverlayTarget? TryCreate(SDL_Window* window, out string? reason)
    {
        var glContext = SDL_GL_CreateContext(window);
        if (glContext is null)
        {
            reason = $"SDL_GL_CreateContext: {SDL_GetError()}";
            return null;
        }

        GRGlInterface? glInterface = null;
        try
        {
            if (!SDL_GL_MakeCurrent(window, glContext))
            {
                reason = $"SDL_GL_MakeCurrent: {SDL_GetError()}";
                return null;
            }

            SDL_GL_SetSwapInterval(1);
            // A variante com SDL_GL_GetProcAddress como delegate encerrou o processo (SIGSEGV) no Native AOT com Mesa.
            glInterface = GRGlInterface.Create();
            var context = glInterface is null ? null : GRContext.CreateGl(glInterface);
            if (context is null)
            {
                reason = "o Skia não aceitou o contexto OpenGL";
                return null;
            }

            reason = null;
            var target = new GlOverlayTarget(window, glContext, glInterface!, context);
            glInterface = null;
            glContext = null;
            return target;
        }
        finally
        {
            glInterface?.Dispose();
            if (glContext is not null)
            {
                SDL_GL_DestroyContext(glContext);
            }
        }
    }

    internal static string Name => "Skia OpenGL";

    internal SKCanvas GetCanvas(int width, int height)
    {
        if (_surface is null || width != _width || height != _height)
        {
            _surface?.Dispose();
            _target?.Dispose();
            _target = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, GlRgba8));
            _surface = SKSurface.Create(_context, _target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
                ?? throw new InvalidOperationException("Não foi possível criar a superfície OpenGL do Skia.");
            _width = width;
            _height = height;
        }

        return _surface.Canvas;
    }

    internal void Present()
    {
        _context.Flush();
        if (!SDL_GL_SwapWindow(_window))
        {
            throw new InvalidOperationException($"SDL_GL_SwapWindow: {SDL_GetError()}");
        }
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _target?.Dispose();
        _context.Dispose();
        _interface.Dispose();
        SDL_GL_DestroyContext(_glContext);
    }
}
