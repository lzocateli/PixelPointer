using System.Runtime.InteropServices;
using TrayLabel = PixelPointer.App.TrayController.TrayLabel;

namespace PixelPointer.UnitTests;

public sealed class TrayLabelTests
{
    [Theory]
    [InlineData("Zoom")]
    [InlineData("Desenhar")]
    [InlineData("Sair")]
    [InlineData("Anotações — ação")]
    public void Label_RemainsValidUtf8AfterTheNativeCallUntilDisposed(string text)
    {
        var label = new TrayLabel(text);
        try
        {
            var retainedPointer = label.DangerousGetHandle();
            Assert.NotEqual(0, retainedPointer);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            for (var index = 0; index < 100; index++)
            {
                using var temporary = new TrayLabel($"temporary-{index}");
                Assert.Equal(text, Marshal.PtrToStringUTF8(retainedPointer));
            }

            var expected = System.Text.Encoding.UTF8.GetBytes(text + "\0");
            var actual = new byte[expected.Length];
            Marshal.Copy(retainedPointer, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
        }
        finally
        {
            label.Dispose();
        }

        Assert.True(label.IsClosed);
        label.Dispose();
        Assert.True(label.IsClosed);
    }
}
