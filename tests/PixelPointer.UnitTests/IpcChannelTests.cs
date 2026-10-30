using PixelPointer.App;

namespace PixelPointer.UnitTests;

public sealed class IpcChannelTests
{
    [Theory]
    [InlineData("zoom", true)]
    [InlineData("draw", true)]
    [InlineData("ZOOM", false)]
    [InlineData("ping", false)]
    [InlineData("", false)]
    public void IsKnownCommand_AcceptsOnlyDocumentedCommands(string command, bool expected) =>
        Assert.Equal(expected, IpcChannel.IsKnownCommand(command));

    [Fact]
    public void SocketPath_IsNamedAfterProduct() =>
        Assert.Equal("pixelpointer.sock", Path.GetFileName(IpcChannel.SocketPath));
}
