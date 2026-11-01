using PixelPointer.App.Hotkeys;
using Tmds.DBus.Protocol;

namespace PixelPointer.UnitTests;

public sealed class HotkeyStartupTests
{
    [Theory]
    [InlineData("org.freedesktop.portal.Error.NotAllowed")]
    [InlineData("org.freedesktop.portal.Error.Failed")]
    [InlineData("org.freedesktop.DBus.Error.AccessDenied")]
    public void PortalRefusal_IsRecoverable(string errorName)
    {
        var error = new DBusErrorReplyException(errorName, "An app id is required");

        Assert.True(HotkeyStartup.IsRecoverable(error));
    }

    [Fact]
    public void ExistingStartupFailures_RemainRecoverable()
    {
        Assert.True(HotkeyStartup.IsRecoverable(new NotSupportedException()));
        Assert.True(HotkeyStartup.IsRecoverable(new InvalidOperationException()));
        Assert.True(HotkeyStartup.IsRecoverable(new OperationCanceledException()));
    }

    [Fact]
    public void UnexpectedFailures_AreNotHidden()
    {
        Assert.False(HotkeyStartup.IsRecoverable(new ArgumentException()));
        Assert.False(HotkeyStartup.IsRecoverable(new IOException()));
        Assert.False(HotkeyStartup.IsRecoverable(new Exception()));
    }
}
