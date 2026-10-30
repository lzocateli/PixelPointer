using PixelPointer.App.Linux;

namespace PixelPointer.UnitTests;

public sealed class X11HotkeysTests
{
    [Fact]
    public void KeyEvent_KeycodeOffsetMatchesXlibAbi()
    {
        var expectedOffset = IntPtr.Size == 8 ? 84 : 52;
        Assert.Equal(expectedOffset, X11EventLayout.KeycodeOffset);
    }

    [Fact]
    public void KeyEvent_SizeMatchesXlibAbi() =>
        Assert.Equal(IntPtr.Size == 8 ? 96 : 60,
            System.Runtime.InteropServices.Marshal.SizeOf<X11EventLayout.KeyEvent>());
}
