using System.Runtime.InteropServices;
using TrayLabel = PixelPointer.App.TrayController.TrayLabel;

namespace PixelPointer.UnitTests;

public sealed class TrayLabelTests
{
    [Fact]
    public void AboutEntries_UseExecutableVersionAndEveryDefaultShortcut()
    {
        var entries = PixelPointer.App.TrayController.AboutEntries;
        Assert.Equal($"PixelPointer {PixelPointer.App.AppInfo.Version}", entries[0]);
        foreach (var shortcut in PixelPointer.App.Hotkeys.HotkeyDefaults.Shortcuts)
        {
            Assert.Contains($"{shortcut.Trigger}  —  {shortcut.Description}", entries);
        }

        Assert.Contains("Atalhos globais padrão", entries);
        Assert.Contains("O desktop pode redefinir os atalhos.", entries);
        Assert.Contains("Disponibilidade depende da plataforma e das permissões.", entries);
        Assert.Equal(PixelPointer.App.Hotkeys.HotkeyDefaults.Shortcuts.Count + 4, entries.Count);
    }

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
