namespace PixelPointer.Core.LiveZoom;

/// <summary>Retângulo em pixels da área de trabalho.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Intersects(ScreenRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// Lupa em janela para sistemas sem exclusão da própria janela na captura (X11): a região capturada fica centrada
/// no cursor e a lupa é posicionada fora dela, para nunca ampliar a si mesma.
/// </summary>
public readonly record struct LensLayout(ScreenRect Lens, ScreenRect Source)
{
    public const int Gap = 16;

    public static LensLayout Compute(int cursorX, int cursorY, ScreenRect screen, float level)
    {
        level = Math.Clamp(level, LiveZoomView.MinLevel, LiveZoomView.MaxLevel);
        var lensWidth = Math.Min(screen.Width, Math.Max(160, screen.Width / 3));
        var lensHeight = Math.Min(screen.Height, Math.Max(90, screen.Height / 3));
        var sourceWidth = Math.Max(1, (int)Math.Round(lensWidth / level));
        var sourceHeight = Math.Max(1, (int)Math.Round(lensHeight / level));
        var source = new ScreenRect(
            Clamp(cursorX - (sourceWidth / 2), screen.X, screen.Right - sourceWidth),
            Clamp(cursorY - (sourceHeight / 2), screen.Y, screen.Bottom - sourceHeight),
            sourceWidth, sourceHeight);

        // Abaixo à direita, abaixo à esquerda, acima à direita e acima à esquerda da região capturada.
        ReadOnlySpan<(int X, int Y)> candidates =
        [
            (source.Right + Gap, source.Bottom + Gap),
            (source.X - Gap - lensWidth, source.Bottom + Gap),
            (source.Right + Gap, source.Y - Gap - lensHeight),
            (source.X - Gap - lensWidth, source.Y - Gap - lensHeight),
        ];
        ScreenRect? first = null;
        foreach (var (x, y) in candidates)
        {
            var lens = new ScreenRect(Clamp(x, screen.X, screen.Right - lensWidth),
                Clamp(y, screen.Y, screen.Bottom - lensHeight), lensWidth, lensHeight);
            if (!lens.Intersects(source))
            {
                return new LensLayout(lens, source);
            }

            first ??= lens;
        }

        return new LensLayout(first!.Value, source);
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, Math.Max(min, max)));
}
