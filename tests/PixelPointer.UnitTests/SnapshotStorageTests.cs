using PixelPointer.App;
using PixelPointer.App.Linux;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class SnapshotStorageTests
{
    [Fact]
    public void OpaqueBgraCapture_RendersPixelsWithUnusedAlphaByte()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var source = new byte[]
        {
            204, 170, 0, 0, 204, 170, 0, 0,
            204, 170, 0, 0, 204, 170, 0, 0,
        };
        var normalized = new byte[source.Length];
        X11ScreenCapture.CopyOpaquePixels(source, normalized);
        System.Runtime.InteropServices.Marshal.Copy(normalized, 0, bitmap.GetPixels(), normalized.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var surface = SKSurface.Create(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        surface.Canvas.Scale(2);
        surface.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        using var encoded = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
        using var decoded = SKBitmap.Decode(encoded);
        Assert.Equal(new SKColor(0, 170, 204), decoded.GetPixel(1, 1));
    }

    [Fact]
    public void Save_PreservesPngPixelsAndAlphaWithoutAWindow()
    {
        var root = Directory.CreateTempSubdirectory("pixelpointer-snapshot-");
        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red };
            surface.Canvas.DrawRect(2, 2, 4, 4, paint);
            using var encoded = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
            var path = SnapshotStorage.Save(root.FullName, encoded.SaveTo);
            using var decoded = SKBitmap.Decode(path);
            Assert.Equal(8, decoded.Width);
            Assert.Equal(8, decoded.Height);
            Assert.Equal(SKColors.Red, decoded.GetPixel(3, 3));
            Assert.Equal((byte)0, decoded.GetPixel(0, 0).Alpha);

            using var icon = AppIcon.CreateBitmap();
            Assert.Equal(AppIcon.Size, icon.Width);
            Assert.Equal((byte)0, icon.GetPixel(0, 0).Alpha);
            Assert.Equal(new SKColor(0x42, 0xD6, 0xC5), icon.GetPixel(9, 9));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetDirectory_ReadsConfiguredXdgDirectoryWithoutDependingOnWorkingDirectory()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolução XDG específica do Linux.");
        var root = Directory.CreateTempSubdirectory("pixelpointer-snapshot-");
        var previousConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", root.FullName);
            Assert.Throws<InvalidOperationException>(SnapshotStorage.GetDirectory);
            var pictures = Path.Combine(root.FullName, "Fotos de férias");
            File.WriteAllText(Path.Combine(root.FullName, "user-dirs.dirs"), $"XDG_PICTURES_DIR=\"{pictures}\"");
            var destination = SnapshotStorage.GetDirectory();
            var file = SnapshotStorage.Save(destination, stream => stream.Write("image"u8));
            Assert.Equal(Path.Combine(pictures, "Screenshots"), Path.GetDirectoryName(file));
            Assert.Equal("image", File.ReadAllText(file));
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "relative");
            Assert.Throws<InvalidOperationException>(SnapshotStorage.GetDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousConfig);
            root.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("XDG_PICTURES_DIR=\"$HOME/Imagens\"", "/home/teste/Imagens/Screenshots")]
    [InlineData("XDG_PICTURES_DIR=\"/mnt/Fotos de férias\" # destino", "/mnt/Fotos de férias/Screenshots")]
    [InlineData("# comentário\nXDG_DESKTOP_DIR=\"$HOME/Desktop\"\n XDG_PICTURES_DIR = \"$HOME/Photos\"\r\n", "/home/teste/Photos/Screenshots")]
    [InlineData("XDG_PICTURES_DIR=\"/mnt/Photos/../Pictures\"", "/mnt/Pictures/Screenshots")]
    [InlineData("XDG_PICTURES_DIR=\"/mnt/\\$literal\"", "/mnt/$literal/Screenshots")]
    public void ResolveLinuxDirectory_UsesConfiguredPictures(string configuration, string expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(expected, SnapshotStorage.ResolveLinuxDirectory(configuration, "/home/teste"));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("XDG_PICTURES_DIR=\"$HOME\"")]
    [InlineData("XDG_PICTURES_DIR=\"$HOME/\"")]
    [InlineData("XDG_PICTURES_DIR=\"\"")]
    [InlineData("XDG_PICTURES_DIR=\"relative/path\"")]
    [InlineData("XDG_PICTURES_DIR=/tmp/Pictures")]
    [InlineData("XDG_PICTURES_DIR=\"$(touch /tmp/never-execute)\"")]
    [InlineData("XDG_PICTURES_DIR=\"`touch /tmp/never-execute`\"")]
    [InlineData("XDG_PICTURES_DIR=\"$OTHER/Pictures\"")]
    [InlineData("XDG_PICTURES_DIR=\"$HOME/Pictures\"; command")]
    [InlineData("XDG_PICTURES_DIR=\"$HOME/Pictures\"\nXDG_PICTURES_DIR=\"/tmp/other\"")]
    [InlineData("XDG_PICTURES_DIR=\"/tmp/unclosed")]
    public void ResolveLinuxDirectory_RejectsMissingDisabledOrExecutableSettings(string configuration)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<InvalidOperationException>(() => SnapshotStorage.ResolveLinuxDirectory(configuration, "/home/teste"));
        }
    }

    [Theory]
    [InlineData(null, "/Users/teste/Desktop")]
    [InlineData("~/Fotos de férias", "/Users/teste/Fotos de férias")]
    [InlineData("/Volumes/Fotos", "/Volumes/Fotos")]
    public void ResolveMacDirectory_UsesPreferenceOrDesktop(string? preference, string expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(expected, SnapshotStorage.ResolveMacDirectory(preference, "/Users/teste/Desktop", "/Users/teste"));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("/tmp/invalid\npath")]
    public void ResolveMacDirectory_RejectsInvalidPreference(string preference) =>
        Assert.Throws<InvalidOperationException>(() => SnapshotStorage.ResolveMacDirectory(preference, "/tmp", "/tmp"));

    [Fact]
    public void Save_CreatesDistinctPrivateFilesAndPreservesExistingContent()
    {
        var root = Directory.CreateTempSubdirectory("pixelpointer-snapshot-");
        try
        {
            var destination = Path.Combine(root.FullName, "Fotos de férias", "Screenshots");
            var first = SnapshotStorage.Save(destination, stream => stream.Write("first"u8));
            var second = SnapshotStorage.Save(destination, stream => stream.Write("second"u8));
            Assert.NotEqual(first, second);
            Assert.Equal("first", File.ReadAllText(first));
            Assert.Equal("second", File.ReadAllText(second));
            Assert.Equal(destination, Path.GetDirectoryName(first));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(first));
            }
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Save_RemovesIncompleteFileAndDoesNotRedirectOnFailure()
    {
        var root = Directory.CreateTempSubdirectory("pixelpointer-snapshot-");
        try
        {
            Assert.Throws<IOException>(() => SnapshotStorage.Save(root.FullName, stream =>
            {
                stream.Write("partial"u8);
                throw new IOException("falha de gravação");
            }));
            Assert.Empty(root.EnumerateFiles());

            var blocked = Path.Combine(root.FullName, "blocked");
            File.WriteAllText(blocked, "preserve");
            Assert.Throws<IOException>(() => SnapshotStorage.Save(blocked, _ => { }));
            Assert.Equal("preserve", File.ReadAllText(blocked));
            Assert.Single(root.EnumerateFiles());
            Assert.Throws<InvalidOperationException>(() => SnapshotStorage.Save("relative", _ => { }));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
