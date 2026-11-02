using PixelPointer.Core.LiveZoom;

namespace PixelPointer.UnitTests;

public sealed class LensLayoutTests
{
    private static readonly ScreenRect Screen = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(960, 540)]
    [InlineData(5, 5)]
    [InlineData(1915, 5)]
    [InlineData(5, 1075)]
    [InlineData(1915, 1075)]
    [InlineData(1200, 300)]
    public void Compute_NeverPlacesTheLensOverTheMagnifiedRegion(int x, int y)
    {
        var layout = LensLayout.Compute(x, y, Screen, 2f);

        Assert.False(layout.Lens.Intersects(layout.Source));
        Assert.True(Inside(layout.Lens, Screen) && Inside(layout.Source, Screen));
    }

    [Fact]
    public void Compute_CentersTheSourceOnTheCursorAndMagnifiesIt()
    {
        var layout = LensLayout.Compute(960, 540, Screen, 2f);

        Assert.Equal(new ScreenRect(640, 360, 640, 360), layout.Lens with { X = 640, Y = 360 });
        Assert.Equal(new ScreenRect(800, 450, 320, 180), layout.Source);
        Assert.Equal(new ScreenRect(1136, 646, 640, 360), layout.Lens);
    }

    [Fact]
    public void Compute_RespectsMonitorOffsets()
    {
        var secondary = new ScreenRect(-1280, 0, 1280, 1024);

        var layout = LensLayout.Compute(-640, 512, secondary, 4f);

        Assert.True(Inside(layout.Lens, secondary) && Inside(layout.Source, secondary));
        Assert.False(layout.Lens.Intersects(layout.Source));
        Assert.Equal(layout.Lens.Width / 4, layout.Source.Width);
    }

    [Theory]
    [InlineData(0, 0, 10, 10, 10, 0, 5, 5, false)]
    [InlineData(0, 0, 10, 10, 9, 9, 5, 5, true)]
    public void Intersects_UsesExclusiveEdges(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh, bool expected) =>
        Assert.Equal(expected, new ScreenRect(ax, ay, aw, ah).Intersects(new ScreenRect(bx, by, bw, bh)));

    private static bool Inside(ScreenRect inner, ScreenRect outer) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
}
