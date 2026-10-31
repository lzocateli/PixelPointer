using System.Text;
using PixelPointer.Core.Settings;

namespace PixelPointer.App;

internal enum SettingsStatus
{
    Missing,
    Loaded,
    Invalid,
}

internal sealed record SettingsResult(AppSettings Settings, SettingsStatus Status, string? Path, string? Error);

/// <summary>Lê settings.json do diretório de configuração do usuário; nunca grava nem corrige o arquivo.</summary>
internal static class SettingsStore
{
    internal const string FileName = "settings.json";
    internal const int MaxBytes = 64 * 1024;

    internal static string GetPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(RequireAbsolutePath(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "APPDATA"),
                "PixelPointer", FileName);
        }

        var home = RequireAbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "diretório pessoal");
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "PixelPointer", FileName);
        }

        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var directory = string.IsNullOrEmpty(config) ? Path.Combine(home, ".config") : RequireAbsolutePath(config, "XDG_CONFIG_HOME");
        return Path.Combine(directory, "pixelpointer", FileName);
    }

    internal static SettingsResult Load()
    {
        string path;
        try
        {
            path = GetPath();
        }
        catch (InvalidOperationException ex)
        {
            return new SettingsResult(AppSettings.Default, SettingsStatus.Invalid, null, ex.Message);
        }

        return Load(path);
    }

    internal static SettingsResult Load(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[MaxBytes + 1];
            var length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (length > MaxBytes)
            {
                return Invalid(path, $"o arquivo excede {MaxBytes / 1024} KiB");
            }

            var json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(buffer, 0, length).TrimStart('\uFEFF');
            return new SettingsResult(AppSettings.Parse(json), SettingsStatus.Loaded, path, null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SettingsResult(AppSettings.Default, SettingsStatus.Missing, path, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
            or DecoderFallbackException)
        {
            return Invalid(path, ex.Message);
        }
    }

    private static SettingsResult Invalid(string path, string error) =>
        new(AppSettings.Default, SettingsStatus.Invalid, path, error);

    private static string RequireAbsolutePath(string path, string setting)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Any(char.IsControl))
        {
            throw new InvalidOperationException($"Não foi possível resolver um caminho absoluto válido para {setting}.");
        }

        return Path.GetFullPath(path);
    }
}
