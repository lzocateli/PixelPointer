using PixelPointer.App.Linux;

namespace PixelPointer.UnitTests;

public sealed class DesktopIntegrationTests
{
    [Fact]
    public void Apply_InstallsIdempotentlyAndRemovesOnlyUnchangedResources()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Integração desktop específica do Linux.");
        var root = Directory.CreateTempSubdirectory("pixelpointer-desktop-");
        try
        {
            var data = Path.Combine(root.FullName, "data");
            var executable = Path.Combine(root.FullName, "app % directory", "pixelpointer");
            var source = Path.Combine(root.FullName, "icon.svg");
            File.WriteAllText(source, "<svg/>");
            DesktopIntegration.Apply(data, executable, source, remove: false);
            DesktopIntegration.Apply(data, executable, source, remove: false);
            var desktop = Path.Combine(data, "applications", "io.github.lzocateli.PixelPointer.desktop");
            var icon = Path.Combine(data, "icons", "hicolor", "scalable", "apps", "io.github.lzocateli.PixelPointer.svg");
            var original = File.ReadAllText(desktop);
            Assert.Contains("%%", original);
            Assert.Contains("Terminal=false", original);
            Assert.Contains("NoDisplay=false", original);
            Assert.Equal("<svg/>", File.ReadAllText(icon));

            File.WriteAllText(desktop, "preserve");
            Assert.Throws<InvalidOperationException>(() => DesktopIntegration.Apply(data, executable, source, remove: false));
            Assert.Throws<InvalidOperationException>(() => DesktopIntegration.Apply(data, executable, source, remove: true));
            Assert.Equal("preserve", File.ReadAllText(desktop));
            Assert.True(File.Exists(icon));

            var moved = Path.Combine(root.FullName, "nova extração", "pixelpointer");
            File.WriteAllText(desktop, original);
            DesktopIntegration.Apply(data, moved, source, remove: false);
            var relocated = File.ReadAllText(desktop);
            Assert.Contains(moved, relocated);
            Assert.DoesNotContain(executable, relocated);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(desktop)!));

            File.WriteAllText(desktop, original.Replace("Terminal=false", "Terminal=true", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => DesktopIntegration.Apply(data, moved, source, remove: false));

            File.WriteAllText(desktop, original);
            DesktopIntegration.Apply(data, executable, source, remove: true);
            DesktopIntegration.Apply(data, executable, source, remove: true);
            Assert.False(File.Exists(desktop));
            Assert.False(File.Exists(icon));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Apply_RejectsRelativePathsAndSymlinkParents()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Integração desktop específica do Linux.");
        var root = Directory.CreateTempSubdirectory("pixelpointer-desktop-");
        try
        {
            var source = Path.Combine(root.FullName, "icon.svg");
            File.WriteAllText(source, "<svg/>");
            Assert.Throws<InvalidOperationException>(() => DesktopIntegration.Apply("relative", source, source, remove: false));
            if (OperatingSystem.IsLinux())
            {
                var target = Directory.CreateDirectory(Path.Combine(root.FullName, "target"));
                var link = Path.Combine(root.FullName, "linked");
                Directory.CreateSymbolicLink(link, target.FullName);
                Assert.Throws<InvalidOperationException>(() => DesktopIntegration.Apply(link, source, source, remove: false));
                Assert.Empty(target.EnumerateFileSystemInfos());
            }
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
