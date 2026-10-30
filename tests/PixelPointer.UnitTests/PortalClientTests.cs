using PixelPointer.App.Linux;

namespace PixelPointer.UnitTests;

public sealed class PortalClientTests
{
    [Theory]
    [InlineData(":1.17078", "pixelpointer42_1", "/org/freedesktop/portal/desktop/request/1_17078/pixelpointer42_1")]
    [InlineData(":1.2", "t", "/org/freedesktop/portal/desktop/request/1_2/t")]
    public void GetRequestPath_FollowsXdgSpecification(string uniqueName, string token, string expected) =>
        Assert.Equal(expected, PortalClient.GetRequestPath(uniqueName, token));

    [Fact]
    public void AppId_IsReverseDnsUsedByDesktopFile() =>
        Assert.Equal("io.github.lzocateli.PixelPointer", PortalClient.AppId);

    [Theory]
    [InlineData("org.freedesktop.DBus.Error.UnknownMethod", "RegistryUnavailable")]
    [InlineData("org.freedesktop.DBus.Error.UnknownInterface", "RegistryUnavailable")]
    [InlineData("org.freedesktop.DBus.Error.AccessDenied", "Failed")]
    [InlineData("org.freedesktop.portal.Error.Failed", "Failed")]
    public void ClassifyRegistryError_DistinguishesUnsupportedRegistryFromFailures(
        string errorName, string expected) =>
        Assert.Equal(Enum.Parse<AppIdRegistrationStatus>(expected), PortalClient.ClassifyRegistryError(errorName));
}
