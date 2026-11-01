using PixelPointer.App;

namespace PixelPointer.UnitTests;

public sealed class SetupTests
{
    [Theory]
    [InlineData(true, true, false, true, true, true)]
    [InlineData(true, false, false, true, false, true)]
    [InlineData(false, false, false, false, false, true)]
    [InlineData(true, true, true, true, true, false)]
    public void Policy_OnlyOffersValidExplicitActions(bool linux, bool wayland, bool running,
        bool desktop, bool capture, bool start)
    {
        var policy = new SetupPolicy(linux, wayland, running);

        Assert.True(policy.IsAvailable(SetupAction.Close));
        Assert.True(policy.IsAvailable(SetupAction.Diagnose));
        Assert.Equal(desktop, policy.IsAvailable(SetupAction.RegisterDesktop));
        Assert.Equal(capture, policy.IsAvailable(SetupAction.RequestCapture));
        Assert.Equal(start, policy.IsAvailable(SetupAction.StartDaemon));
        Assert.False(policy.IsAvailable((SetupAction)99));
    }

    [Fact]
    public void DefaultAction_IsSideEffectFreeClose() => Assert.Equal(SetupAction.Close, default);

    [Theory]
    [InlineData(24, 116, "Diagnose")]
    [InlineData(100, 168, "RegisterDesktop")]
    [InlineData(100, 220, "RequestCapture")]
    [InlineData(100, 272, "StartDaemon")]
    [InlineData(100, 324, "Close")]
    public void Window_HitTestMatchesStableActionRows(float x, float y, string expected) =>
        Assert.Equal(Enum.Parse<SetupAction>(expected), SetupWindow.HitTest(x, y));

    [Theory]
    [InlineData(23, 116)]
    [InlineData(536, 116)]
    [InlineData(100, 156)]
    [InlineData(100, 364)]
    public void Window_HitTestRejectsGapsAndOutside(float x, float y) => Assert.Null(SetupWindow.HitTest(x, y));
}
