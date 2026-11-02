using PixelPointer.App.Windows;
using PixelPointer.Core.LiveZoom;

namespace PixelPointer.UnitTests;

public sealed class LiveZoomViewTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(960, 480)]
    [InlineData(1919, 960)]
    [InlineData(5000, 960)]
    [InlineData(-50, 0)]
    public void Origin_KeepsThePointUnderTheCursorInsideTheScreen(int cursor, int expected) =>
        Assert.Equal(expected, LiveZoomView.Origin(cursor, 1920, 2f));

    [Fact]
    public void Origin_KeepsTheCursorPixelFixedOnScreen()
    {
        const int cursor = 700;
        const float level = 4f;

        var origin = LiveZoomView.Origin(cursor, 1920, level);

        // O conteúdo sob o cursor aparece na mesma posição da tela depois de ampliado.
        Assert.InRange((cursor - origin) * level, cursor - level, cursor + level);
    }

    [Theory]
    [InlineData(1f, 1.25f)]
    [InlineData(100f, 16f)]
    public void Origin_ClampsTheLevel(float level, float effective) =>
        Assert.Equal(LiveZoomView.Origin(1000, 1920, effective), LiveZoomView.Origin(1000, 1920, level));

    [Fact]
    public void Origin_HandlesEmptyScreen() => Assert.Equal(0, LiveZoomView.Origin(10, 0, 2f));

    [Theory]
    [InlineData(960, 540, 640, 360, 640, 360)]
    [InlineData(0, 0, 640, 360, 0, 0)]
    [InlineData(1919, 1079, 640, 360, 1280, 720)]
    public void CropAroundCursor_StaysInsideDisplay(int cursorX, int cursorY, int cropWidth, int cropHeight,
        int expectedX, int expectedY)
    {
        var crop = LiveZoomView.CropAroundCursor(cursorX, cursorY, 1920, 1080, cropWidth, cropHeight);

        Assert.Equal(new ScreenRect(expectedX, expectedY, cropWidth, cropHeight), crop);
    }

    [Fact]
    public void CropAroundCursor_ClampsOversizedCropToDisplay() =>
        Assert.Equal(new ScreenRect(0, 0, 1920, 1080),
            LiveZoomView.CropAroundCursor(960, 540, 1920, 1080, 2000, 1200));

    [Fact]
    public void CropAroundCursor_RejectsEmptyDisplay() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LiveZoomView.CropAroundCursor(0, 0, 0, 1080, 1, 1));

    [Theory]
    [InlineData(1280, 2f, 640)]
    [InlineData(1280, 1f, 1024)]
    public void CropExtent_DividesPixelViewportByClampedZoom(int viewportPixels, float level, int expected) =>
        Assert.Equal(expected, LiveZoomView.CropExtent(viewportPixels, level));

    [Fact]
    public void Start_IsRejectedOutsideWindows()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "No Windows a lupa é real.");

        Assert.Throws<NotSupportedException>(() => LiveZoom.Start(2f));
    }
}
