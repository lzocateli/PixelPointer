namespace PixelPointer.Core.Updates;

/// <summary>Formato do pacote de release por RID, igual ao conferido por install.sh e install.ps1.</summary>
public sealed record PackageLayout(string Rid, string AssetName, string Executable, IReadOnlyList<string> Files)
{
    /// <summary>Limite de tamanho aceito para o pacote baixado.</summary>
    public const long MaxPackageBytes = 128L * 1024 * 1024;

    public static IReadOnlyList<string> SupportedRids { get; } =
        ["linux-x64", "linux-arm64", "osx-arm64", "osx-x64", "win-x64", "win-arm64"];

    public bool IsZip => AssetName.EndsWith(".zip", StringComparison.Ordinal);

    public static PackageLayout For(string rid)
    {
        string[] common = ["pixelpointer.svg", "LICENSE", "THIRD-PARTY-NOTICES.md"];
        return rid switch
        {
            "linux-x64" or "linux-arm64" => new(rid, $"pixelpointer-{rid}.tar.gz", "pixelpointer",
                ["pixelpointer", "libSDL3.so", "libSkiaSharp.so", .. common]),
            "osx-arm64" or "osx-x64" => new(rid, $"pixelpointer-{rid}.tar.gz", "pixelpointer",
                ["pixelpointer", "libSDL3.dylib", "libSkiaSharp.dylib", "libPixelPointerScreenCaptureKit.dylib", .. common]),
            "win-x64" or "win-arm64" => new(rid, $"pixelpointer-{rid}.zip", "pixelpointer.exe",
                ["pixelpointer.exe", "SDL3.dll", "libSkiaSharp.dll", .. common]),
            _ => throw new NotSupportedException($"RID sem pacote de release: {rid}"),
        };
    }
}
