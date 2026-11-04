using System.Runtime.InteropServices;
using System.Text;
using PixelPointer.Core.Updates;

namespace PixelPointer.App.Updates;

/// <summary>Coleta fatos do diretório real do executável para classificar a origem da instalação.</summary>
internal static class InstallOriginDetector
{
    internal static string CurrentRid
    {
        get
        {
            var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : "unknown";
            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                var other => other.ToString().ToLowerInvariant(),
            };
            return $"{os}-{architecture}";
        }
    }

    internal static string? ApplicationDirectory =>
        NativeLibraries.ResolveApplicationDirectory(Environment.ProcessPath) is { } directory
            ? Path.TrimEndingDirectorySeparator(directory)
            : null;

    internal static InstallOrigin Detect() =>
        ApplicationDirectory is { } directory
            ? Detect(directory, CurrentRid, Environment.IsPrivilegedProcess)
            : new InstallOrigin(InstallChannel.Unknown, false, "não foi possível determinar o executável em execução");

    internal static InstallOrigin Detect(string appDirectory, string rid, bool isPrivileged)
    {
        var parent = Path.GetDirectoryName(appDirectory);
        if (parent is null || !Path.IsPathFullyQualified(appDirectory) || appDirectory.Any(char.IsControl))
        {
            return new InstallOrigin(InstallChannel.Unknown, false, "diretório do aplicativo inválido");
        }

        if (new DirectoryInfo(appDirectory).LinkTarget is not null)
        {
            return new InstallOrigin(InstallChannel.Unknown, false, "o diretório do aplicativo é um link simbólico");
        }

        var (receipt, receiptError) = ReadReceipt(Path.Combine(appDirectory, InstallReceipt.FileName));
        return InstallOrigin.Classify(new InstallFacts(
            rid,
            appDirectory,
            receipt,
            receiptError,
            isPrivileged,
            IsWritable(appDirectory),
            IsWritable(parent),
            !OperatingSystem.IsWindows() && (IsSharedWritable(appDirectory) || IsSharedWritable(parent)),
            OperatingSystem.IsLinux() && IsDpkgOwned(appDirectory)));
    }

    private static (InstallReceipt? Receipt, string? Error) ReadReceipt(string path)
    {
        try
        {
            if (new FileInfo(path) is not { Exists: true, LinkTarget: null, Length: <= InstallReceipt.MaxBytes })
            {
                return File.Exists(path) ? (null, "recibo do instalador inválido") : (null, null);
            }

            var json = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(path));
            return (InstallReceipt.Parse(json), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
            or DecoderFallbackException)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Prova a escrita criando e removendo um arquivo, sem depender de donos ou ACLs específicos do SO.</summary>
    internal static bool IsWritable(string directory)
    {
        try
        {
            using var probe = new FileStream(Path.Combine(directory, $".pixelpointer.write-probe.{Guid.NewGuid():N}"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSharedWritable(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return (File.GetUnixFileMode(directory) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsDpkgOwned(string appDirectory)
    {
        const string list = "/var/lib/dpkg/info/pixelpointer.list";
        try
        {
            return File.Exists(list) && File.ReadLines(list).Any(line => line == appDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
