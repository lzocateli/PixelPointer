using PixelPointer.App.Overlay;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class DisplayLayoutTests
{
    private static readonly DisplayBounds Primary = new(0, 0, 1920, 1080);

    [Fact]
    public void Crop_UsesTheWholeCaptureWhenItMatchesTheWindow() =>
        Assert.Equal(new SKRect(0, 0, 1920, 1080), DisplayLayout.Crop(Primary, [Primary], 1920, 1080, 1920, 1080));

    [Fact]
    public void Crop_OffsetsASecondaryMonitorToTheRight()
    {
        var right = new DisplayBounds(1920, 0, 1920, 1080);

        Assert.Equal(SKRect.Create(1920, 0, 1920, 1080), DisplayLayout.Crop(right, [Primary, right], 3840, 1080, 1920, 1080));
    }

    [Fact]
    public void Crop_MeasuresFromTheDesktopCornerWhenAMonitorIsLeftOfThePrimary()
    {
        // No Windows, monitores \u00e0 esquerda do principal t\u00eam X negativo e a captura come\u00e7a neles.
        var left = new DisplayBounds(-1280, -200, 1280, 1024);

        Assert.Equal(SKRect.Create(1280, 200, 1920, 1080), DisplayLayout.Crop(Primary, [Primary, left], 3200, 1280, 1920, 1080));
        Assert.Equal(SKRect.Create(0, 0, 1280, 1024), DisplayLayout.Crop(left, [Primary, left], 3200, 1280, 1280, 1024));
    }

    [Fact]
    public void Crop_ScalesLogicalBoundsToWindowPixels()
    {
        var right = new DisplayBounds(1920, 0, 1920, 1080);

        Assert.Equal(SKRect.Create(3840, 0, 3840, 2160), DisplayLayout.Crop(right, [Primary, right], 7680, 2160, 3840, 2160));
    }

    [Fact]
    public void Crop_FallsBackToTheWholeCaptureWithoutUsableBounds() =>
        Assert.Equal(new SKRect(0, 0, 3840, 1080),
            DisplayLayout.Crop(new DisplayBounds(0, 0, 0, 0), [], 3840, 1080, 1920, 1080));
}
