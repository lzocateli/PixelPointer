using PixelPointer.App.Linux;
using Tmds.DBus.Protocol;

namespace PixelPointer.UnitTests;

public sealed class PortalClientTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(2u, false)]
    [InlineData(3u, true)]
    public void ScreenCastCapabilities_ReadUInt32ValuesAndMonitorSupport(uint sources, bool supportsMonitor)
    {
        var properties = ScreenCastProperties();
        properties["AvailableSourceTypes"] = sources;

        Assert.True(PortalClient.TryReadScreenCastCapabilities(properties, out var capabilities));
        Assert.Equal(new ScreenCastCapabilities(5, sources, 3), capabilities);
        Assert.Equal(supportsMonitor, capabilities.SupportsMonitor);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("AvailableSourceTypes")]
    [InlineData("AvailableCursorModes")]
    public void ScreenCastCapabilities_RejectMissingProperties(string property)
    {
        var properties = ScreenCastProperties();
        properties.Remove(property);

        Assert.False(PortalClient.TryReadScreenCastCapabilities(properties, out var capabilities));
        Assert.Equal(default, capabilities);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("AvailableSourceTypes")]
    [InlineData("AvailableCursorModes")]
    public void ScreenCastCapabilities_RejectUnexpectedTypes(string property)
    {
        var properties = ScreenCastProperties();
        properties[property] = "invalid";

        Assert.False(PortalClient.TryReadScreenCastCapabilities(properties, out var capabilities));
        Assert.Equal(default, capabilities);
    }

    [Fact]
    public void ScreenCastCapabilities_IgnoreOtherProperties()
    {
        var properties = ScreenCastProperties();
        properties["future_property"] = "unknown";

        Assert.True(PortalClient.TryReadScreenCastCapabilities(properties, out var capabilities));
        Assert.Equal(new ScreenCastCapabilities(5, 1, 3), capabilities);
    }

    private static Dictionary<string, VariantValue> ScreenCastProperties() => new()
    {
        ["version"] = 5u,
        ["AvailableSourceTypes"] = 1u,
        ["AvailableCursorModes"] = 3u,
    };

    [Theory]
    [InlineData(0u, 1u, 3u, 1)]
    [InlineData(5u, 0u, 3u, 2)]
    [InlineData(5u, 2u, 3u, 2)]
    [InlineData(5u, 1u, 0u, 3)]
    [InlineData(5u, 3u, 3u, 3)]
    public async Task ScreenCastCapabilities_LegacyPreservesPropertyOrderAndEarlyExit(
        uint version, uint sources, uint cursors, int expectedReads)
    {
        var reads = new List<string>();
        Task<uint> Read(string property)
        {
            reads.Add(property);
            return Task.FromResult(property switch
            {
                "version" => version,
                "AvailableSourceTypes" => sources,
                "AvailableCursorModes" => cursors,
                _ => throw new InvalidOperationException("unexpected property"),
            });
        }

        var capabilities = await PortalClient.ReadLegacyScreenCastCapabilitiesAsync(Read);

        Assert.Equal(new[] { "version", "AvailableSourceTypes", "AvailableCursorModes" }.Take(expectedReads), reads);
        Assert.Equal(version == 0 ? default : new ScreenCastCapabilities(version, sources, expectedReads == 3 ? cursors : 0), capabilities);
    }

    [Fact]
    public async Task ScreenCastCapabilities_LegacyDoesNotSwallowCancellation()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PortalClient.ReadLegacyScreenCastCapabilitiesAsync(
                property => Task.FromException<uint>(new OperationCanceledException())));
    }

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
