namespace PixelPointer.App;

internal static class SnapshotStorage
{
    internal static string GetDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(RequireAbsolutePath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures,
                    Environment.SpecialFolderOption.DoNotVerify), "Imagens"), "Screenshots");
        }

        var home = RequireAbsolutePath(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "diretório pessoal");
        if (OperatingSystem.IsMacOS())
        {
            return ResolveMacDirectory(MacOS.ScreenshotPreferences.GetLocation(),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), home);
        }

        if (OperatingSystem.IsLinux())
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configDirectory = string.IsNullOrEmpty(config)
                ? Path.Combine(home, ".config")
                : RequireAbsolutePath(config, "XDG_CONFIG_HOME");
            var path = Path.Combine(configDirectory, "user-dirs.dirs");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Configure PICTURES em XDG User Directories antes de salvar uma captura.");
            }

            return ResolveLinuxDirectory(File.ReadAllText(path), home);
        }

        throw new NotSupportedException("O salvamento de capturas não está disponível neste sistema.");
    }

    internal static string ResolveMacDirectory(string? location, string desktop, string home)
    {
        if (location is null)
        {
            return RequireAbsolutePath(desktop, "Área de Trabalho");
        }

        return RequireAbsolutePath(location.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(home, location[2..])
            : location, "preferência de capturas do macOS");
    }

    internal static string ResolveLinuxDirectory(string configuration, string home)
    {
        home = RequireAbsolutePath(home, "diretório pessoal");
        string? pictures = null;
        foreach (var line in configuration.Split('\n'))
        {
            var entry = line.Trim();
            if (!entry.StartsWith("XDG_PICTURES_DIR", StringComparison.Ordinal)
                || (entry.Length > 16 && entry[16] is not ('=' or ' ' or '\t')))
            {
                continue;
            }

            if (pictures is not null || !entry[16..].TrimStart().StartsWith('='))
            {
                throw new InvalidOperationException("Configuração XDG_PICTURES_DIR inválida ou duplicada.");
            }

            pictures = ParseXdgValue(entry[16..].TrimStart()[1..].TrimStart(), home);
        }

        if (pictures is null || Path.TrimEndingDirectorySeparator(pictures) == Path.TrimEndingDirectorySeparator(home))
        {
            throw new InvalidOperationException("A pasta PICTURES está ausente ou desabilitada em XDG User Directories.");
        }

        return Path.Combine(pictures, "Screenshots");
    }

    internal static string Save(string directory, Action<Stream> write)
    {
        directory = RequireAbsolutePath(directory, "destino da captura");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"pixelpointer-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(path, options))
        {
            try
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                stream.Dispose();
                File.Delete(path);
                throw;
            }
        }

        return path;
    }

    private static string ParseXdgValue(string value, string home)
    {
        if (!value.StartsWith('"'))
        {
            throw new InvalidOperationException("XDG_PICTURES_DIR deve conter um caminho entre aspas.");
        }

        var result = new System.Text.StringBuilder();
        var index = 1;
        if (value.AsSpan(index).StartsWith("$HOME/"))
        {
            result.Append(home).Append('/');
            index += 6;
        }
        else if (value.AsSpan(index).StartsWith("$HOME\""))
        {
            result.Append(home);
            index += 5;
        }

        for (; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                var remainder = value[(index + 1)..].TrimStart();
                if (remainder.Length != 0 && !remainder.StartsWith('#'))
                {
                    break;
                }

                return RequireAbsolutePath(result.ToString(), "XDG_PICTURES_DIR");
            }

            if (character == '\\')
            {
                if (++index >= value.Length || value[index] is not ('\\' or '"' or '$' or '`'))
                {
                    break;
                }

                result.Append(value[index]);
            }
            else if (character is '$' or '`' || char.IsControl(character))
            {
                break;
            }
            else
            {
                result.Append(character);
            }
        }

        throw new InvalidOperationException("XDG_PICTURES_DIR contém uma expressão inválida; o arquivo não será executado.");
    }

    private static string RequireAbsolutePath(string path, string setting)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Any(char.IsControl))
        {
            throw new InvalidOperationException($"Não foi possível resolver um caminho absoluto válido para {setting}.");
        }

        return Path.GetFullPath(path);
    }
}
