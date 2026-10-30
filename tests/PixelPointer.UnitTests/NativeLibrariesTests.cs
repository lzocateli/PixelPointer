using PixelPointer.App;

namespace PixelPointer.UnitTests;

public sealed class NativeLibrariesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pixelpointer-tests-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ResolveApplicationDirectory_FollowsSymbolicLinkToRealExecutable()
    {
        var appDirectory = Directory.CreateDirectory(Path.Combine(_root, "lib", "pixelpointer")).FullName;
        var binDirectory = Directory.CreateDirectory(Path.Combine(_root, "bin")).FullName;
        var executable = Path.Combine(appDirectory, "pixelpointer");
        File.WriteAllText(executable, string.Empty);
        var link = Path.Combine(binDirectory, "pixelpointer");
        try
        {
            File.CreateSymbolicLink(link, executable);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip($"Sem permissão para criar links simbólicos: {ex.Message}");
        }

        Assert.Equal(appDirectory, NativeLibraries.ResolveApplicationDirectory(link));
    }

    [Fact]
    public void ResolveApplicationDirectory_WithoutLinkReturnsOwnDirectory()
    {
        var executable = Path.Combine(_root, "pixelpointer");
        File.WriteAllText(executable, string.Empty);
        Assert.Equal(_root, NativeLibraries.ResolveApplicationDirectory(executable));
    }

    [Fact]
    public void ResolveApplicationDirectory_WithoutProcessPathReturnsNull() =>
        Assert.Null(NativeLibraries.ResolveApplicationDirectory(null));

    [Fact]
    public void GetCandidateFileNames_IncludesPlatformFileName()
    {
        var expected = OperatingSystem.IsWindows() ? "SDL3.dll" : OperatingSystem.IsMacOS() ? "libSDL3.dylib" : "libSDL3.so";
        Assert.Contains(expected, NativeLibraries.GetCandidateFileNames("SDL3"));
    }
}
