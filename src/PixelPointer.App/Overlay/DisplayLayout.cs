using SkiaSharp;

namespace PixelPointer.App.Overlay;

/// <summary>Limites lógicos de um monitor, como informados pelo SDL.</summary>
internal readonly record struct DisplayBounds(int X, int Y, int Width, int Height);

internal static class DisplayLayout
{
    /// <summary>
    /// Região do monitor <paramref name="target"/> numa captura da área de trabalho inteira, cuja origem é o canto
    /// superior esquerdo da união dos monitores (que pode ter coordenadas negativas no Windows).
    /// </summary>
    internal static SKRect Crop(DisplayBounds target, IReadOnlyList<DisplayBounds> displays, int captureWidth,
        int captureHeight, int windowWidth, int windowHeight)
    {
        if ((captureWidth == windowWidth && captureHeight == windowHeight) || target.Width <= 0 || displays.Count == 0)
        {
            return new SKRect(0, 0, captureWidth, captureHeight);
        }

        var left = displays.Min(display => display.X);
        var top = displays.Min(display => display.Y);
        var scale = (float)windowWidth / target.Width;
        return SKRect.Create((target.X - left) * scale, (target.Y - top) * scale, windowWidth, windowHeight);
    }
}
