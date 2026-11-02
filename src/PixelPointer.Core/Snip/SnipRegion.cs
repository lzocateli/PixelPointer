namespace PixelPointer.Core.Snip;

/// <summary>Região retangular de recorte em pixels da imagem.</summary>
public readonly record struct SnipRegion(int X, int Y, int Width, int Height)
{
    public const int MinSize = 2;

    /// <summary>
    /// Normaliza dois cantos arrastados em qualquer direção e limita à imagem;
    /// devolve null quando a seleção é pequena demais para ser intencional.
    /// </summary>
    public static SnipRegion? FromCorners(float x1, float y1, float x2, float y2, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0
            || !float.IsFinite(x1) || !float.IsFinite(y1) || !float.IsFinite(x2) || !float.IsFinite(y2))
        {
            return null;
        }

        var left = (int)Math.Clamp(MathF.Floor(MathF.Min(x1, x2)), 0, imageWidth);
        var top = (int)Math.Clamp(MathF.Floor(MathF.Min(y1, y2)), 0, imageHeight);
        var right = (int)Math.Clamp(MathF.Ceiling(MathF.Max(x1, x2)), 0, imageWidth);
        var bottom = (int)Math.Clamp(MathF.Ceiling(MathF.Max(y1, y2)), 0, imageHeight);
        return right - left < MinSize || bottom - top < MinSize
            ? null
            : new SnipRegion(left, top, right - left, bottom - top);
    }
}
