using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PixelPointer.Core.Updates;
using PixelPointer.Core.Updates.Sigstore;

namespace PixelPointer.App.Updates;

internal sealed record StagedUpdate(StagedUpdateMarker Marker, string Root, string Application)
{
    public SemanticVersion Version => Marker.Version;
}

/// <summary>
/// Staging e aplicação da atualização. O staging fica em &lt;pai&gt;/.pixelpointer.update, no mesmo volume da instalação:
/// pacote baixado, SHA-256 e proveniência verificados, arquivos extraídos e a versão nova executada (--version e
/// selftest) antes de qualquer troca. A aplicação troca os diretórios e recupera a versão anterior em falha.
/// </summary>
internal static partial class UpdateInstaller
{
    internal const string StageDirectoryName = ".pixelpointer.update";
    internal const string TemporaryPrefix = ".pixelpointer.update.tmp.";
    internal const string PreviousPrefix = ".pixelpointer.update-old.";
    internal const string ApplicationDirectoryName = "app";

    internal static TimeSpan ToolTimeout { get; set; } = TimeSpan.FromSeconds(60);

    internal static string StageRoot(string appDirectory) =>
        Path.Combine(Path.GetDirectoryName(appDirectory)!, StageDirectoryName);

    internal static StagedUpdate? FindStaged(string appDirectory)
    {
        var root = StageRoot(appDirectory);
        var marker = Path.Combine(root, StagedUpdateMarker.FileName);
        try
        {
            if (new DirectoryInfo(root) is not { Exists: true, LinkTarget: null }
                || new FileInfo(marker) is not { Exists: true, LinkTarget: null, Length: <= StagedUpdateMarker.MaxBytes })
            {
                return null;
            }

            var parsed = StagedUpdateMarker.Parse(File.ReadAllText(marker, new UTF8Encoding(false, throwOnInvalidBytes: true)));
            var application = Path.Combine(root, ApplicationDirectoryName);
            return parsed.Target == Path.GetFileName(appDirectory) && Directory.Exists(application)
                ? new StagedUpdate(parsed, root, application)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
            or DecoderFallbackException)
        {
            return null;
        }
    }

    internal static async Task<StagedUpdate> StageAsync(
        UpdateClient client,
        string appDirectory,
        string rid,
        UpdateCandidate candidate,
        Action<string> progress,
        CancellationToken cancellationToken)
    {
        if (candidate is not { Availability: UpdateAvailability.Available, AssetName: { } assetName })
        {
            throw new InvalidOperationException("Não há atualização compatível para preparar.");
        }

        if (FindStaged(appDirectory) is { } existing && existing.Version == candidate.Latest)
        {
            return existing;
        }

        var layout = PackageLayout.For(rid);
        var parent = Path.GetDirectoryName(appDirectory)!;
        var work = Path.Combine(parent, TemporaryPrefix + Guid.NewGuid().ToString("N")[..12]);
        CreatePrivateDirectory(work);
        try
        {
            progress($"Baixando SHA256SUMS da versão {candidate.Latest}");
            var expected = Sha256Sums.FindDigest(
                await client.GetChecksumsAsync(candidate.Latest, cancellationToken).ConfigureAwait(false), assetName);

            progress($"Baixando {assetName} ({candidate.AssetSize / 1024.0 / 1024.0:0.0} MiB)");
            var archive = Path.Combine(work, assetName);
            var actual = await client.DownloadAsync(candidate.Latest, assetName, candidate.AssetSize, archive, cancellationToken)
                .ConfigureAwait(false);
            if (actual != expected)
            {
                throw new InvalidDataException($"SHA-256 de {assetName} não confere com o SHA256SUMS da release.");
            }

            progress("Verificando a proveniência Sigstore do pacote");
            var provenance = SigstoreVerifier.VerifyAttestations(
                await client.GetAttestationsAsync(actual, cancellationToken).ConfigureAwait(false),
                assetName, actual, ProvenancePolicy.PixelPointerReleases);
            DiagnosticLog.Write("UPDATE", $"provenance ok version={candidate.Latest} signer={provenance.Signer}");

            var application = Path.Combine(work, ApplicationDirectoryName);
            PackageExtractor.Extract(archive, layout, application);
            File.Delete(archive);
            File.Copy(Path.Combine(appDirectory, InstallReceipt.FileName), Path.Combine(application, InstallReceipt.FileName));
            SetModes(application, layout);

            progress("Executando --version e selftest da versão nova");
            RequireVersion(Path.Combine(application, layout.Executable), candidate.Latest);
            RequireSelfTest(Path.Combine(application, layout.Executable));

            var marker = new StagedUpdateMarker(candidate.Latest, rid, Path.GetFileName(appDirectory), actual);
            File.WriteAllText(Path.Combine(work, StagedUpdateMarker.FileName), marker.Serialize(), new UTF8Encoding(false));

            var root = StageRoot(appDirectory);
            DeleteDirectory(root);
            Directory.Move(work, root);
            return new StagedUpdate(marker, root, Path.Combine(root, ApplicationDirectoryName));
        }
        catch
        {
            DeleteDirectory(work);
            throw;
        }
    }

    /// <summary>Aplica a atualização preparada no próprio processo (Linux e macOS). Devolve a versão instalada.</summary>
    internal static SemanticVersion ApplyInProcess(string appDirectory, string rid)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("No Windows a troca é feita pelo processo auxiliar.");
        }

        var layout = PackageLayout.For(rid);
        var staged = FindStaged(appDirectory) ?? throw new InvalidOperationException("Nenhuma atualização preparada.");
        if (DiscardIfNotNewer(appDirectory, layout, staged))
        {
            throw new InvalidOperationException($"A versão instalada já é {staged.Version} ou mais nova; a atualização preparada foi descartada.");
        }

        RequireVersion(Path.Combine(staged.Application, layout.Executable), staged.Version);
        var installed = Path.Combine(appDirectory, layout.Executable);
        if (TryExchange(staged.Application, appDirectory))
        {
            try
            {
                RequireVersion(installed, staged.Version);
            }
            catch
            {
                if (!TryExchange(staged.Application, appDirectory))
                {
                    throw new IOException($"A versão nova falhou e a anterior ficou em {staged.Application}; restaure-a manualmente.");
                }

                throw;
            }

            DeleteDirectory(staged.Root);
            return staged.Version;
        }

        var previous = Path.Combine(Path.GetDirectoryName(appDirectory)!, PreviousPrefix + Environment.ProcessId);
        Directory.Move(appDirectory, previous);
        try
        {
            Directory.Move(staged.Application, appDirectory);
        }
        catch
        {
            Directory.Move(previous, appDirectory);
            throw;
        }

        try
        {
            RequireVersion(installed, staged.Version);
        }
        catch
        {
            Directory.Move(appDirectory, staged.Application);
            Directory.Move(previous, appDirectory);
            throw;
        }

        DeleteDirectory(previous);
        DeleteDirectory(staged.Root);
        return staged.Version;
    }

    /// <summary>No Windows o executável em uso não pode ser movido: a versão preparada termina a troca após o encerramento.</summary>
    internal static void StartWindowsHelper(StagedUpdate staged, string rid, int waitProcessId)
    {
        var executable = Path.Combine(staged.Application, PackageLayout.For(rid).Executable);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = staged.Root,
        };
        start.ArgumentList.Add("update");
        start.ArgumentList.Add("finish");
        start.ArgumentList.Add("--wait-pid");
        start.ArgumentList.Add(waitProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var _ = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar a conclusão da atualização.");
    }

    /// <summary>
    /// Executado pela versão nova, a partir de &lt;pai&gt;/.pixelpointer.update/app: espera o processo anterior, move a
    /// instalação atual para backup, copia os arquivos validados e restaura o backup em falha. Os caminhos vêm só da
    /// localização do próprio executável e do marcador; nada é aceito da linha de comando além do PID.
    /// </summary>
    internal static SemanticVersion FinishOnWindows(string stagedApplication, string rid, int waitProcessId, TimeSpan timeout)
    {
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(stagedApplication))!;
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(stagedApplication)) != ApplicationDirectoryName
            || Path.GetFileName(root) != StageDirectoryName)
        {
            throw new InvalidOperationException("'update finish' só é executado pela versão preparada.");
        }

        var parent = Path.GetDirectoryName(root)!;
        var marker = StagedUpdateMarker.Parse(File.ReadAllText(Path.Combine(root, StagedUpdateMarker.FileName)));
        var appDirectory = Path.Combine(parent, marker.Target);
        var layout = PackageLayout.For(rid);
        var origin = InstallOriginDetector.Detect(appDirectory, rid, Environment.IsPrivilegedProcess);
        if (marker.Rid != rid || !origin.CanSelfUpdate)
        {
            throw new InvalidOperationException($"A instalação em {appDirectory} não aceita atualização automática ({origin.Detail}).");
        }

        WaitForExit(waitProcessId, timeout);
        var current = ReadVersion(Path.Combine(appDirectory, layout.Executable));
        if (current >= marker.Version)
        {
            throw new InvalidOperationException($"A versão instalada já é {current}; nada foi alterado.");
        }

        var previous = Path.Combine(parent, PreviousPrefix + Environment.ProcessId);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                Directory.Move(appDirectory, previous);
                break;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }

        try
        {
            Directory.CreateDirectory(appDirectory);
            foreach (var name in layout.Files.Append(InstallReceipt.FileName))
            {
                File.Copy(Path.Combine(stagedApplication, name), Path.Combine(appDirectory, name));
            }

            RequireVersion(Path.Combine(appDirectory, layout.Executable), marker.Version);
        }
        catch
        {
            DeleteDirectory(appDirectory);
            Directory.Move(previous, appDirectory);
            throw;
        }

        DeleteDirectory(previous);
        return marker.Version;
    }

    /// <summary>Remove restos de staging, backups e atualizações já instaladas; nunca o diretório em execução.</summary>
    internal static void Cleanup(string appDirectory, SemanticVersion current)
    {
        var parent = Path.GetDirectoryName(appDirectory)!;
        var running = InstallOriginDetector.ApplicationDirectory;
        foreach (var directory in Directory.EnumerateDirectories(parent, ".pixelpointer.update*"))
        {
            var name = Path.GetFileName(directory);
            var isLeftover = name.StartsWith(TemporaryPrefix, StringComparison.Ordinal)
                || name.StartsWith(PreviousPrefix, StringComparison.Ordinal);
            var isStale = name == StageDirectoryName && (FindStaged(appDirectory) is not { } staged || staged.Version <= current);
            if ((isLeftover || isStale) && (running is null || !running.StartsWith(directory, StringComparison.Ordinal)))
            {
                DeleteDirectory(directory);
            }
        }
    }

    internal static SemanticVersion ReadVersion(string executable)
    {
        var (code, output) = Run(executable, "--version");
        return code == 0 && SemanticVersion.TryParse(output.Trim(), out var version)
            ? version
            : throw new InvalidOperationException($"{executable} não informou uma versão válida.");
    }

    private static bool DiscardIfNotNewer(string appDirectory, PackageLayout layout, StagedUpdate staged)
    {
        if (ReadVersion(Path.Combine(appDirectory, layout.Executable)) < staged.Version)
        {
            return false;
        }

        DeleteDirectory(staged.Root);
        return true;
    }

    private static void RequireVersion(string executable, SemanticVersion expected)
    {
        var actual = ReadVersion(executable);
        if (actual != expected)
        {
            throw new InvalidDataException($"O executável informa a versão {actual}, esperada {expected}.");
        }
    }

    private static void RequireSelfTest(string executable)
    {
        var (code, output) = Run(executable, "selftest");
        if (code != 0)
        {
            throw new InvalidOperationException($"selftest da versão nova falhou: {UpdatePreferences.Truncate(output.Trim())}");
        }
    }

    private static (int Code, string Output) Run(string executable, string argument)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException($"Não foi possível executar {executable}.");
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ToolTimeout))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"{executable} {argument} não terminou em {ToolTimeout.TotalSeconds:0} s.");
            }

            process.WaitForExit();
            return (process.ExitCode, output.Result + error.Result);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Não foi possível executar {executable}: {ex.Message}", ex);
        }
    }

    private static void WaitForExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(timeout))
            {
                throw new TimeoutException("O PixelPointer anterior não encerrou a tempo; a atualização continua preparada.");
            }
        }
        catch (ArgumentException)
        {
            // O processo já terminou.
        }
    }

    private static void SetModes(string directory, PackageLayout layout)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode file = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        const UnixFileMode executable = file | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        foreach (var name in layout.Files.Append(InstallReceipt.FileName))
        {
            File.SetUnixFileMode(Path.Combine(directory, name), name == layout.Executable ? executable : file);
        }

        File.SetUnixFileMode(directory, executable);
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    internal static void DeleteDirectory(string path)
    {
        try
        {
            if (new DirectoryInfo(path) is { Exists: true } info)
            {
                if (info.LinkTarget is not null)
                {
                    info.Delete();
                    return;
                }

                info.Delete(recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write("UPDATE", $"cleanup failed path={Path.GetFileName(path)} error={ex.GetType().Name}");
        }
    }

    /// <summary>Troca atômica de dois diretórios (renameat2 no Linux, renamex_np no macOS); false quando indisponível.</summary>
    private static bool TryExchange(string first, string second)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                const int currentDirectory = -100;
                const uint exchange = 2;
                if (RenameAt2(currentDirectory, first, currentDirectory, second, exchange) == 0)
                {
                    return true;
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                const uint swap = 2;
                if (RenameXNp(first, second, swap) == 0)
                {
                    return true;
                }
            }

            DiagnosticLog.Write("UPDATE", $"exchange unavailable errno={Marshal.GetLastPInvokeError()}");
            return false;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport("libc", EntryPoint = "renameat2", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int RenameAt2(int oldDirectory, string oldPath, int newDirectory, string newPath, uint flags);

    [LibraryImport("libc", EntryPoint = "renamex_np", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int RenameXNp(string from, string to, uint flags);
}
