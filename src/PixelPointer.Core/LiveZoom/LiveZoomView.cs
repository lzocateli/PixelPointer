namespace PixelPointer.Core.LiveZoom;

/// <summary>Geometria do Live Zoom: que parte da tela aparece ampliada para uma posição do cursor.</summary>
public static class LiveZoomView
{
    public const float MinLevel = 1.25f;
    public const float MaxLevel = 16f;

    /// <summary>
    /// Origem da área ampliada num eixo de <paramref name="extent"/> pixels: o ponto sob o cursor continua sob ele,
    /// como no ZoomIt, e a área nunca sai da tela.
    /// </summary>
    public static int Origin(int cursor, int extent, float level)
    {
        if (extent <= 0)
        {
            return 0;
        }

        level = Math.Clamp(level, MinLevel, MaxLevel);
        var visible = extent / level;
        var origin = cursor * (1 - (1 / level));
        return (int)Math.Round(Math.Clamp(origin, 0, extent - visible));
    }
}
