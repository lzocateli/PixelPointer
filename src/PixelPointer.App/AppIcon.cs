using System.Runtime.InteropServices;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

namespace PixelPointer.App;

internal static unsafe partial class AppIcon
{
    internal const int Size = 32;

    internal static void SetWindowIcon(SDL_Window* window)
    {
        using var bitmap = CreateBitmap();
        var surface = CreateSurface(bitmap);
        if (surface == 0)
        {
            throw new InvalidOperationException($"SDL não criou a superfície do ícone: {SDL_GetError()}");
        }

        try
        {
            if (SetIcon(window, surface) == 0)
            {
                Console.Error.WriteLine($"aviso: ícone de janela indisponível neste backend: {SDL_GetError()}");
            }
        }
        finally
        {
            DestroySurface(surface);
        }
    }

    internal static nint CreateSurface(SKBitmap bitmap) =>
        CreateSurfaceFrom(Size, Size, (uint)SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
            bitmap.GetPixels(), bitmap.RowBytes);

    internal static SKBitmap CreateBitmap()
    {
        var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var background = new SKPaint { Color = new SKColor(0x20, 0x3A, 0x5E), IsAntialias = true };
        using var pixel = new SKPaint { Color = new SKColor(0x42, 0xD6, 0xC5), IsAntialias = true };
        using var pointer = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = 3.5f,
            Style = SKPaintStyle.Stroke,
        };
        canvas.DrawRoundRect(SKRect.Create(1, 1, 30, 30), 8, 8, background);
        canvas.DrawRect(SKRect.Create(7, 7, 7, 7), pixel);
        canvas.DrawRect(SKRect.Create(18, 7, 7, 7), pixel);
        canvas.DrawRect(SKRect.Create(7, 18, 7, 7), pixel);
        canvas.DrawLine(17, 15, 24, 25, pointer);
        canvas.DrawLine(24, 25, 21, 21, pointer);
        canvas.Flush();
        return bitmap;
    }

    [LibraryImport("SDL3", EntryPoint = "SDL_CreateSurfaceFrom")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial nint CreateSurfaceFrom(int width, int height, uint format, nint pixels, int pitch);

    [LibraryImport("SDL3", EntryPoint = "SDL_DestroySurface")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroySurface(nint surface);

    [LibraryImport("SDL3", EntryPoint = "SDL_SetWindowIcon")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial byte SetIcon(SDL_Window* window, nint surface);
}
