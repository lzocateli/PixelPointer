using SkiaSharp;

namespace PixelPointer.App.Overlay;

/// <summary>Limites lógicos de um monitor, como informados pelo SDL.</summary>
internal readonly record struct DisplayBounds(int X, int Y, int Width, int Height);

internal static class DisplayLayout
{
    internal static SKRect RecordingCrop(DisplayBounds target, IReadOnlyList<DisplayBounds> displays,
        int captureWidth, int captureHeight)
    {
        if (target.Width <= 0 || target.Height <= 0 || displays.Count == 0)
        {
            throw new InvalidOperationException("Não foi possível determinar o monitor da gravação.");
        }

        var left = displays.Min(display => display.X);
        var top = displays.Min(display => display.Y);
        var right = displays.Max(display => display.X + display.Width);
        var bottom = displays.Max(display => display.Y + display.Height);
        var scaleX = (float)captureWidth / (right - left);
        var scaleY = (float)captureHeight / (bottom - top);
        return SKRect.Create((target.X - left) * scaleX, (target.Y - top) * scaleY,
            target.Width * scaleX, target.Height * scaleY);
    }

    /// <summary>
    /// Região do monitor <paramref name="target"/> numa captura da área de trabalho inteira, cuja origem é o canto
    /// superior esquerdo da união dos monitores (que pode ter coordenadas negativas no Windows).
    /// </summary>
    internal static SKRect Crop(DisplayBounds target, IReadOnlyList<DisplayBounds> displays, int captureWidth,
        int captureHeight, int windowWidth, int windowHeight)
    {
        if (captureWidth == windowWidth && captureHeight == windowHeight)
        {
            return new SKRect(0, 0, captureWidth, captureHeight);
        }

        if (target.Width <= 0 || target.Height <= 0 || displays.Count == 0)
        {
            throw new InvalidOperationException("Não foi possível determinar o monitor na captura da área de trabalho.");
        }

        var left = displays.Min(display => display.X);
        var top = displays.Min(display => display.Y);
        var scale = (float)windowWidth / target.Width;
        return SKRect.Create((target.X - left) * scale, (target.Y - top) * scale, windowWidth, windowHeight);
    }

    internal static SKBitmap Crop(SKBitmap source, SKRect bounds)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!(bounds.Width > 0) || !(bounds.Height > 0)
            || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > source.Width || bounds.Bottom > source.Height)
        {
            throw new InvalidOperationException("Os limites do monitor não correspondem à captura da área de trabalho.");
        }

        var width = (int)MathF.Round(bounds.Width);
        var height = (int)MathF.Round(bounds.Height);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("A captura do monitor não tem dimensões válidas.");
        }

        var cropped = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var image = SKImage.FromBitmap(source);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawImage(image, bounds, SKRect.Create(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        return cropped;
    }
}
