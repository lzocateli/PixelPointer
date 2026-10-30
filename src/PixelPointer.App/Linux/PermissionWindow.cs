using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App.Linux;

/// <summary>
/// Janela de primeiro uso: o GNOME só mostra o diálogo de acesso do portal quando o app solicitante está focado,
/// então a permissão de captura é pedida com esta janela em primeiro plano. Depois de concedida, a captura
/// não interativa funciona sem janela.
/// </summary>
internal static unsafe class PermissionWindow
{
    internal static string Run(PortalClient portal, bool auto)
    {
        var window = SDL_CreateWindow("PixelPointer — permissão", 560, 200, SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
        var renderer = SDL_CreateRenderer(window, (Utf8String)null);
        SDL_RaiseWindow(window);
        var status = "Pressione Enter para pedir a permissão de captura de tela.";
        Task<string>? request = null;
        var result = "cancelado";
        var opened = Environment.TickCount64;
        try
        {
            var running = true;
            while (running)
            {
                SDL_Event e;
                while (SDL_PollEvent(&e))
                {
                    if (e.Type is SDL_EventType.SDL_EVENT_QUIT or SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED
                        || (e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN && e.key.key == SDL_Keycode.SDLK_ESCAPE))
                    {
                        running = false;
                    }
                    else if (e.Type == SDL_EventType.SDL_EVENT_KEY_DOWN && e.key.key == SDL_Keycode.SDLK_RETURN && request is null)
                    {
                        status = "Aguardando o diálogo do sistema...";
                        request = portal.ScreenshotAsync(CancellationToken.None);
                    }
                }

                if (auto && request is null && Environment.TickCount64 - opened > 1500)
                {
                    status = "Aguardando o diálogo do sistema...";
                    request = portal.ScreenshotAsync(CancellationToken.None);
                }

                if (request is { IsCompleted: true })
                {
                    if (request.IsCompletedSuccessfully)
                    {
                        File.Delete(new Uri(request.Result).LocalPath);
                        result = "concedida";
                    }
                    else
                    {
                        result = $"falhou: {request.Exception?.InnerException?.Message}";
                    }

                    running = false;
                }

                Draw(window, renderer, status);
                SDL_Delay(16);
            }
        }
        finally
        {
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
        }

        return result;
    }

    private static void Draw(SDL_Window* window, SDL_Renderer* renderer, string status)
    {
        int w, h;
        SDL_GetWindowSizeInPixels(window, &w, &h);
        var density = SDL_GetWindowPixelDensity(window);
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x20, 0x22, 0x28));
        using var title = new SKFont(SKTypeface.Default, 20 * density);
        using var body = new SKFont(SKTypeface.Default, 15 * density);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText("PixelPointer", 24 * density, 48 * density, SKTextAlign.Left, title, paint);
        canvas.DrawText("O zoom e o desenho precisam capturar a tela.", 24 * density, 96 * density, SKTextAlign.Left, body, paint);
        canvas.DrawText(status, 24 * density, 128 * density, SKTextAlign.Left, body, paint);
        using var pixmap = surface.PeekPixels();
        var texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, w, h);
        SDL_UpdateTexture(texture, null, pixmap.GetPixels(), pixmap.RowBytes);
        SDL_RenderTexture(renderer, texture, null, null);
        SDL_RenderPresent(renderer);
        SDL_DestroyTexture(texture);
    }
}
