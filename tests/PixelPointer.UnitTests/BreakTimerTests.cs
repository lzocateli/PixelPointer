using PixelPointer.App.Overlay;
using PixelPointer.Core.Timer;

namespace PixelPointer.UnitTests;

public sealed class BreakTimerTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(100);

    [Fact]
    public void Remaining_CountsDownAndStopsAtZero()
    {
        var timer = new BreakTimer(Start, minutes: 2);

        Assert.Equal(TimeSpan.FromMinutes(2), timer.Remaining(Start));
        Assert.Equal(TimeSpan.FromSeconds(90), timer.Remaining(Start + TimeSpan.FromSeconds(30)));
        Assert.False(timer.IsExpired(Start + TimeSpan.FromSeconds(119)));
        Assert.Equal(TimeSpan.Zero, timer.Remaining(Start + TimeSpan.FromMinutes(5)));
        Assert.True(timer.IsExpired(Start + TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void Adjust_ClampsMinutesAndRestartsTheCountdown()
    {
        var timer = new BreakTimer(Start);
        var later = Start + TimeSpan.FromMinutes(3);

        timer.Adjust(1, later);

        Assert.Equal(BreakTimer.DefaultMinutes + 1, timer.Minutes);
        Assert.Equal(TimeSpan.FromMinutes(11), timer.Remaining(later));
        timer.Adjust(-500, later);
        Assert.Equal(BreakTimer.MinMinutes, timer.Minutes);
        timer.Adjust(500, later);
        Assert.Equal(BreakTimer.MaxMinutes, timer.Minutes);
        Assert.Equal(BreakTimer.MaxMinutes, new BreakTimer(Start, 1000).Minutes);
    }

    [Theory]
    [InlineData(600, "10:00")]
    [InlineData(599.2, "10:00")]
    [InlineData(59, "00:59")]
    [InlineData(0.4, "00:01")]
    [InlineData(0, "00:00")]
    [InlineData(-3, "00:00")]
    [InlineData(5999, "99:59")]
    public void Format_RoundsUpToWholeSeconds(double seconds, string expected) =>
        Assert.Equal(expected, BreakTimer.Format(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("zoom", "Zoom")]
    [InlineData("draw", "Draw")]
    [InlineData("timer", "Timer")]
    [InlineData("snip", "Snip")]
    [InlineData(null, "Zoom")]
    [InlineData("other", "Zoom")]
    public void OverlayModes_MapCommands(string? command, string expected) =>
        Assert.Equal(expected, OverlayModes.FromCommand(command).ToString());
}
