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

    public static ScreenRect CropAroundCursor(int cursorX, int cursorY, int displayWidth, int displayHeight,
        int cropWidth, int cropHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayHeight);
        cropWidth = Math.Clamp(cropWidth, 1, displayWidth);
        cropHeight = Math.Clamp(cropHeight, 1, displayHeight);
        var x = Math.Clamp(cursorX - cropWidth / 2, 0, displayWidth - cropWidth);
        var y = Math.Clamp(cursorY - cropHeight / 2, 0, displayHeight - cropHeight);
        return new ScreenRect(x, y, cropWidth, cropHeight);
    }

    public static int CropExtent(int viewportPixels, float level)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportPixels);
        level = Math.Clamp(level, MinLevel, MaxLevel);
        return Math.Max(1, (int)Math.Ceiling(viewportPixels / level));
    }
}
