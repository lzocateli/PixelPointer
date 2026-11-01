using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App;

internal static unsafe class SetupWindow
{
    private const int Width = 560;
    private const int Height = 440;
    private static readonly SetupAction[] Actions =
        [SetupAction.Diagnose, SetupAction.RegisterDesktop, SetupAction.RequestCapture, SetupAction.StartDaemon, SetupAction.Close];

    internal static SetupAction Run(SetupPolicy policy, string session, string status)
    {
        var window = SDL_CreateWindow("PixelPointer - primeiro uso", Width, Height, SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
        if (window is null) { throw new InvalidOperationException($"SDL_CreateWindow: {SDL_GetError()}"); }
        var renderer = SDL_CreateRenderer(window, (Utf8String)null);
        if (renderer is null)
        {
            SDL_DestroyWindow(window);
            throw new InvalidOperationException($"SDL_CreateRenderer: {SDL_GetError()}");
        }

        var selected = Actions.Length - 1;
        var dirty = true;
        try
        {
            AppIcon.SetWindowIcon(window);
            SDL_RaiseWindow(window);
            while (true)
            {
                if (dirty)
                {
                    Draw(window, renderer, policy, session, status, selected);
                    dirty = false;
                }
                SDL_Event input;
                if (!SDL_WaitEventTimeout(&input, 100)) { continue; }
                dirty = true;
                if (input.Type is SDL_EventType.SDL_EVENT_QUIT or SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED)
                {
                    return SetupAction.Close;
                }
                if (input.Type == SDL_EventType.SDL_EVENT_KEY_DOWN)
                {
                    if (input.key.key == SDL_Keycode.SDLK_ESCAPE) { return SetupAction.Close; }
                    if (input.key.key == SDL_Keycode.SDLK_RETURN && policy.IsAvailable(Actions[selected])) { return Actions[selected]; }
                    if (input.key.key is SDL_Keycode.SDLK_TAB or SDL_Keycode.SDLK_DOWN or SDL_Keycode.SDLK_UP)
                    {
                        var direction = input.key.key == SDL_Keycode.SDLK_UP ? -1 : 1;
                        do { selected = (selected + direction + Actions.Length) % Actions.Length; }
                        while (!policy.IsAvailable(Actions[selected]));
                    }
                }
                if (input.Type == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN && input.button.button == 1)
                {
                    int width, height;
                    SDL_GetWindowSize(window, &width, &height);
                    if (width <= 0 || height <= 0) { continue; }
                    var scale = Math.Min((float)width / Width, (float)height / Height);
                    var x = (input.button.x - (width - Width * scale) / 2) / scale;
                    var y = (input.button.y - (height - Height * scale) / 2) / scale;
                    var action = HitTest(x, y);
                    if (action is { } chosen && policy.IsAvailable(chosen)) { return chosen; }
                }
            }
        }
        finally
        {
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
        }
    }

    internal static SetupAction? HitTest(float x, float y)
    {
        if (x < 24 || x >= Width - 24) { return null; }
        for (var index = 0; index < Actions.Length; index++)
        {
            var top = 116 + index * 52;
            if (y >= top && y < top + 40) { return Actions[index]; }
        }
        return null;
    }

    private static void Draw(SDL_Window* window, SDL_Renderer* renderer, SetupPolicy policy,
        string session, string status, int selected)
    {
        int width, height;
        SDL_GetWindowSizeInPixels(window, &width, &height);
        if (width <= 0 || height <= 0) { return; }
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x20, 0x22, 0x28));
        var scale = Math.Min((float)width / Width, (float)height / Height);
        canvas.Translate((width - Width * scale) / 2, (height - Height * scale) / 2);
        canvas.Scale(scale);
        using var title = new SKFont(SKTypeface.Default, 22);
        using var body = new SKFont(SKTypeface.Default, 15);
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var icon = AppIcon.CreateBitmap();
        canvas.DrawBitmap(icon, SKRect.Create(24, 22, 32, 32), new SKSamplingOptions(SKFilterMode.Linear), paint);
        canvas.DrawText("PixelPointer", 68, 47, SKTextAlign.Left, title, paint);
        var summary = session + " / " + status;
        while (body.MeasureText(summary) > Width - 48 && summary.Length > 1) { summary = summary[..^1]; }
        canvas.DrawText(summary, 24, 88, SKTextAlign.Left, body, paint);
        for (var index = 0; index < Actions.Length; index++)
        {
            var action = Actions[index];
            var enabled = policy.IsAvailable(action);
            paint.Color = enabled && index == selected ? new SKColor(0x31, 0x58, 0x64) : new SKColor(0x2D, 0x30, 0x37);
            var top = 116 + index * 52;
            canvas.DrawRoundRect(SKRect.Create(24, top, Width - 48, 40), 4, 4, paint);
            paint.Color = enabled ? SKColors.White : new SKColor(0x8A, 0x8E, 0x96);
            var label = action switch
            {
                SetupAction.Diagnose => "Verificar ambiente",
                SetupAction.RegisterDesktop => "Registrar aplicativo no desktop",
                SetupAction.RequestCapture => "Solicitar captura de tela",
                SetupAction.StartDaemon => policy.DaemonRunning ? "Daemon em execução" : "Iniciar daemon",
                _ => "Fechar",
            };
            canvas.DrawText(label, Width / 2, top + 26, SKTextAlign.Center, body, paint);
        }
        using var pixels = surface.PeekPixels();
        var texture = SDL_CreateTexture(renderer, SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
            SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, width, height);
        if (texture is null) { throw new InvalidOperationException($"SDL_CreateTexture: {SDL_GetError()}"); }
        try
        {
            if (!SDL_UpdateTexture(texture, null, pixels.GetPixels(), pixels.RowBytes)) { throw new InvalidOperationException($"SDL_UpdateTexture: {SDL_GetError()}"); }
            SDL_RenderTexture(renderer, texture, null, null);
            SDL_RenderPresent(renderer);
        }
        finally { SDL_DestroyTexture(texture); }
    }
}
