namespace PixelPointer.App.Linux;

/// <summary>
/// Guarda o restore token do portal ScreenCast, que evita repetir o diálogo de escolha do monitor.
/// O token é tratado como credencial: arquivo 0600, diretório 0700, nunca registrado em logs.
/// </summary>
internal sealed class RestoreTokenStore(string path)
{
    internal const int MaxLength = 512;

    internal string Path { get; } = path;

    internal static RestoreTokenStore CreateDefault()
    {
        var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        string directory;
        if (!string.IsNullOrEmpty(state))
        {
            if (!System.IO.Path.IsPathFullyQualified(state))
            {
                throw new InvalidOperationException("XDG_STATE_HOME deve ser um caminho absoluto.");
            }

            directory = state;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!System.IO.Path.IsPathFullyQualified(home))
            {
                throw new InvalidOperationException("Não foi possível resolver o diretório pessoal.");
            }

            directory = System.IO.Path.Combine(home, ".local", "state");
        }

        return new RestoreTokenStore(System.IO.Path.Combine(System.IO.Path.GetFullPath(directory), "pixelpointer",
            "screencast-restore-token"));
    }

    /// <summary>Token salvo, ou null quando ausente, ilegível, link simbólico ou malformado.</summary>
    internal string? Read()
    {
        try
        {
            var info = new FileInfo(Path);
            if (!info.Exists || info.LinkTarget is not null || info.Length > MaxLength)
            {
                return null;
            }

            var token = File.ReadAllText(Path).Trim();
            return IsValid(token) ? token : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Substitui o token de forma atômica; tokens inválidos não são gravados.</summary>
    internal void Write(string token)
    {
        if (!IsValid(token))
        {
            throw new ArgumentException("Restore token inválido.", nameof(token));
        }

        var directory = System.IO.Path.GetDirectoryName(Path)!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        if (new DirectoryInfo(directory).LinkTarget is not null || new FileInfo(Path).LinkTarget is not null)
        {
            throw new IOException("O destino do restore token é um link simbólico.");
        }

        var temporary = System.IO.Path.Combine(directory, $".screencast-restore-token.{Environment.ProcessId}.{Guid.NewGuid():N}");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using (var stream = new FileStream(temporary, options))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(token);
            }

            File.Move(temporary, Path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    internal void Delete()
    {
        if (new FileInfo(Path).LinkTarget is null)
        {
            File.Delete(Path);
        }
    }

    internal static bool IsValid(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxLength && token.All(c => c is > ' ' and < (char)0x7F);
}
