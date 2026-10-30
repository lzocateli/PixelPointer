using PixelPointer.App;

namespace PixelPointer.UnitTests;

public sealed class AppInfoTests
{
    [Fact]
    public void Version_StartsWithVersionPrefix() =>
        Assert.Matches(@"^\d+\.\d+\.\d+([-+].*)?$", AppInfo.Version);
}
