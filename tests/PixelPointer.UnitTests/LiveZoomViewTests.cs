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

    [Fact]
    public void Start_IsRejectedOutsideWindows()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "No Windows a lupa é real.");

        Assert.Throws<NotSupportedException>(() => LiveZoom.Start(2f));
    }
}
