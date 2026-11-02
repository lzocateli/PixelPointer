using PixelPointer.Core.Snip;

namespace PixelPointer.UnitTests;

public sealed class SnipRegionTests
{
    [Theory]
    [InlineData(10, 20, 50, 60)]
    [InlineData(50, 60, 10, 20)]
    [InlineData(50, 20, 10, 60)]
    public void FromCorners_NormalizesAnyDragDirection(float x1, float y1, float x2, float y2) =>
        Assert.Equal(new SnipRegion(10, 20, 40, 40), SnipRegion.FromCorners(x1, y1, x2, y2, 100, 100));

    [Fact]
    public void FromCorners_ExpandsFractionsToWholePixelsAndClampsToTheImage() =>
        Assert.Equal(new SnipRegion(0, 9, 100, 91), SnipRegion.FromCorners(-30.5f, 9.7f, 140.2f, 120f, 100, 100));

    [Theory]
    [InlineData(10, 10, 11, 50)]
    [InlineData(10, 10, 10, 10)]
    [InlineData(200, 200, 300, 300)]
    [InlineData(float.NaN, 0, 10, 10)]
    public void FromCorners_RejectsTinyOrOutsideSelections(float x1, float y1, float x2, float y2) =>
        Assert.Null(SnipRegion.FromCorners(x1, y1, x2, y2, 100, 100));

    [Fact]
    public void FromCorners_RejectsEmptyImage() =>
        Assert.Null(SnipRegion.FromCorners(0, 0, 10, 10, 0, 100));
}
