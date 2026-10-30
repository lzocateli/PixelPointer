namespace PixelPointer.App.Linux;

internal static class DesktopIntegration
{
    internal static string GetDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return AbsolutePath(configured);
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(AbsolutePath(home), ".local", "share");
    }

    internal static void Apply(string dataDirectory, string executable, string iconSource, bool remove)
    {
        dataDirectory = AbsolutePath(dataDirectory);
        executable = AbsolutePath(executable);
        iconSource = AbsolutePath(iconSource);
        var desktopPath = Path.Combine(dataDirectory, "applications", $"{PortalClient.AppId}.desktop");
        var iconPath = Path.Combine(dataDirectory, "icons", "hicolor", "scalable", "apps", $"{PortalClient.AppId}.svg");
        ValidatePath(desktopPath);
        ValidatePath(iconPath);
        ValidatePath(iconSource);
        var contents = $"""
            [Desktop Entry]
            Type=Application
            Name=PixelPointer
            Comment=Zoom e anotação de tela para apresentações
            Exec="{EscapeArgument(executable)}" daemon
            Icon={PortalClient.AppId}
            Terminal=false
            NoDisplay=false
            Categories=Utility;

            """;
        var icon = File.ReadAllText(iconSource);
        var resources = new[] { (Path: desktopPath, Contents: contents), (Path: iconPath, Contents: icon) };
        foreach (var resource in resources)
        {
            if (Directory.Exists(resource.Path)
                || (File.Exists(resource.Path) && File.ReadAllText(resource.Path) != resource.Contents))
            {
                throw new InvalidOperationException($"O arquivo existente será preservado porque difere do recurso do PixelPointer: {resource.Path}");
            }
        }

        foreach (var resource in resources)
        {
            if (remove)
            {
                if (File.Exists(resource.Path))
                {
                    File.Delete(resource.Path);
                    Console.WriteLine($"Removido: {resource.Path}");
                }
            }
            else if (!File.Exists(resource.Path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resource.Path)!);
                using var stream = new FileStream(resource.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.Write(resource.Contents);
                Console.WriteLine($"Instalado: {resource.Path}");
            }
            else
            {
                Console.WriteLine($"Já instalado: {resource.Path}");
            }
        }
    }

    private static string EscapeArgument(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal);

    private static string AbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Any(char.IsControl))
        {
            throw new InvalidOperationException("A integração desktop exige caminhos absolutos válidos.");
        }

        return Path.GetFullPath(path);
    }

    private static void ValidatePath(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (new FileInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException($"O recurso desktop não será acessado por link simbólico: {current}");
            }
        }
    }
}
