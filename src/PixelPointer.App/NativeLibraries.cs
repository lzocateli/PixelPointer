using System.Runtime.InteropServices;
using SDL;
using SkiaSharp;

namespace PixelPointer.App;

/// <summary>
/// SDL3 e libSkiaSharp são distribuídas ao lado do executável. Instaladores publicam o comando como link simbólico
/// (Homebrew, winget, /usr/bin do .deb); quando o caminho do processo é um link, as bibliotecas são carregadas do
/// diretório do executável real.
/// </summary>
internal static class NativeLibraries
{
    internal static void Register()
    {
        var directory = ResolveApplicationDirectory(Environment.ProcessPath);
        if (directory is null || PathsEqual(directory, AppContext.BaseDirectory))
        {
            return;
        }

        foreach (var assembly in new[] { typeof(SDL3).Assembly, typeof(SKBitmap).Assembly })
        {
            NativeLibrary.SetDllImportResolver(assembly, (name, _, _) => TryLoad(directory, name));
        }
    }

    /// <summary>Diretório do executável final, seguindo links simbólicos; null quando não há link a seguir.</summary>
    internal static string? ResolveApplicationDirectory(string? processPath)
    {
        if (string.IsNullOrEmpty(processPath))
        {
            return null;
        }

        var target = File.ResolveLinkTarget(processPath, returnFinalTarget: true)?.FullName ?? processPath;
        return Path.GetDirectoryName(Path.GetFullPath(target));
    }

    private static nint TryLoad(string directory, string name)
    {
        foreach (var candidate in GetCandidateFileNames(name))
        {
            if (NativeLibrary.TryLoad(Path.Combine(directory, candidate), out var handle))
            {
                return handle;
            }
        }

        return 0;
    }

    internal static IEnumerable<string> GetCandidateFileNames(string name)
    {
        yield return name;
        if (OperatingSystem.IsWindows())
        {
            yield return $"{name}.dll";
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return $"lib{name}.dylib";
            yield return $"{name}.dylib";
        }
        else
        {
            yield return $"lib{name}.so";
            yield return $"{name}.so";
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
