using System.Text;
using PixelPointer.Core.Updates;

namespace PixelPointer.App.Updates;

internal sealed record UpdatePreferencesResult(UpdatePreferences Preferences, string Path, string? Error);

/// <summary>
/// update.json fica ao lado do settings.json, no diretório de configuração do usuário. Diferente do settings.json, é
/// gravado pelo aplicativo: troca atômica por arquivo temporário e recusa em sobrescrever um arquivo inválido ou
/// alterado por outro processo durante a gravação.
/// </summary>
internal static class UpdatePreferencesStore
{
    internal const string FileName = "update.json";

    internal static string GetPath() =>
        Path.Combine(Path.GetDirectoryName(SettingsStore.GetPath())!, FileName);

    internal static UpdatePreferencesResult Load() => Load(GetPath());

    internal static UpdatePreferencesResult Load(string path)
    {
        try
        {
            var bytes = ReadCurrent(path);
            return new(bytes is null ? UpdatePreferences.Default : Parse(bytes), path, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
            or DecoderFallbackException)
        {
            return new(UpdatePreferences.Default, path, ex.Message);
        }
    }

    internal static UpdatePreferences Update(Func<UpdatePreferences, UpdatePreferences> change) => Update(GetPath(), change);

    internal static UpdatePreferences Update(string path, Func<UpdatePreferences, UpdatePreferences> change)
    {
        byte[]? original;
        UpdatePreferences current;
        try
        {
            original = ReadCurrent(path);
            current = original is null ? UpdatePreferences.Default : Parse(original);
        }
        catch (Exception ex) when (ex is InvalidDataException or DecoderFallbackException)
        {
            throw new InvalidOperationException(
                $"{path} é inválido e não será sobrescrito ({ex.Message}); corrija ou remova o arquivo.", ex);
        }

        var updated = change(current);
        if (updated == current)
        {
            return current;
        }

        var directory = Path.GetDirectoryName(path)!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var temporary = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(new UTF8Encoding(false).GetBytes(updated.Serialize()));
                stream.Flush(flushToDisk: true);
            }

            // Conflito: outro processo (ou o usuário) alterou o arquivo depois da leitura.
            var now = ReadCurrent(path);
            if (!(now is null && original is null) && (now is null || original is null || !now.AsSpan().SequenceEqual(original)))
            {
                throw new IOException($"{path} foi alterado durante a gravação; nada foi sobrescrito. Tente novamente.");
            }

            File.Move(temporary, path, overwrite: true);
            return updated;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static UpdatePreferences Parse(byte[] bytes) =>
        UpdatePreferences.Parse(new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('\uFEFF'));

    private static byte[]? ReadCurrent(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[UpdatePreferences.MaxBytes + 1];
            var length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return length > UpdatePreferences.MaxBytes
                ? throw new InvalidDataException($"o arquivo excede {UpdatePreferences.MaxBytes / 1024} KiB")
                : buffer[..length];
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
