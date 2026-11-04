using PixelPointer.Core.Updates;
using PixelPointer.Core.Updates.Sigstore;

namespace PixelPointer.App.Updates;

internal sealed record UpdateStatus(
    InstallOrigin Origin,
    SemanticVersion Current,
    UpdatePreferences Preferences,
    string PreferencesPath,
    string? PreferencesError,
    StagedUpdate? Staged);

/// <summary>Orquestra verificação, staging e aplicação para a CLI e para o daemon.</summary>
internal static class UpdateService
{
    internal static SemanticVersion CurrentVersion =>
        SemanticVersion.TryParse(AppInfo.Version, out var version) ? version : new SemanticVersion(0, 0, 0, string.Empty);

    internal static bool IsUpdateFailure(Exception ex) =>
        ex is HttpRequestException or InvalidDataException or SigstoreVerificationException or IOException
            or UnauthorizedAccessException or InvalidOperationException or TimeoutException or TaskCanceledException
            or NotSupportedException or System.Security.Cryptography.CryptographicException;

    internal static UpdateStatus GetStatus()
    {
        var preferences = UpdatePreferencesStore.Load();
        var origin = InstallOriginDetector.Detect();
        var staged = origin.CanSelfUpdate && InstallOriginDetector.ApplicationDirectory is { } directory
            ? UpdateInstaller.FindStaged(directory)
            : null;
        return new UpdateStatus(origin, CurrentVersion, preferences.Preferences, preferences.Path, preferences.Error, staged);
    }

    internal static async Task<UpdateCandidate> CheckAsync(UpdateClient client, CancellationToken cancellationToken)
    {
        var release = await client.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        TryUpdatePreferences(preferences => preferences with { LastCheck = DateTimeOffset.UtcNow });
        var candidate = release.Evaluate(CurrentVersion, InstallOriginDetector.CurrentRid);
        DiagnosticLog.Write("UPDATE", $"check result={candidate.Availability} latest={candidate.Latest}");
        return candidate;
    }

    internal static async Task<StagedUpdate> StageAsync(
        UpdateClient client, UpdateCandidate candidate, Action<string> progress, CancellationToken cancellationToken)
    {
        var (origin, directory) = RequireSelfUpdate();
        using var _ = UpdateLock.Acquire(TimeSpan.FromSeconds(5));
        try
        {
            var staged = await UpdateInstaller.StageAsync(client, directory, InstallOriginDetector.CurrentRid, candidate, progress,
                cancellationToken).ConfigureAwait(false);
            DiagnosticLog.Write("UPDATE", $"staged version={staged.Version} origin={origin.Channel}");
            return staged;
        }
        catch (Exception ex) when (IsUpdateFailure(ex) && ex is not OperationCanceledException)
        {
            TryUpdatePreferences(preferences => preferences with { LastFailure = $"Preparação da versão {candidate.Latest}: {ex.Message}" });
            throw;
        }
    }

    /// <summary>
    /// Aplica a atualização preparada. No Linux e no macOS a troca acontece agora; no Windows, a versão nova conclui a troca
    /// depois que este processo encerrar. Devolve a mensagem para o usuário.
    /// </summary>
    internal static string ApplyStaged()
    {
        var (_, directory) = RequireSelfUpdate();
        var rid = InstallOriginDetector.CurrentRid;
        using var _ = UpdateLock.Acquire(TimeSpan.FromSeconds(30));
        var staged = UpdateInstaller.FindStaged(directory) ?? throw new InvalidOperationException("Nenhuma atualização preparada.");
        if (OperatingSystem.IsWindows())
        {
            UpdateInstaller.StartWindowsHelper(staged, rid, Environment.ProcessId);
            DiagnosticLog.Write("UPDATE", $"windows helper started version={staged.Version}");
            return $"A versão {staged.Version} será instalada assim que este processo encerrar; confira com 'pixelpointer --version'.";
        }

        try
        {
            var applied = UpdateInstaller.ApplyInProcess(directory, rid);
            TryUpdatePreferences(preferences => preferences with { AppliedVersion = applied.ToString(), LastFailure = null });
            DiagnosticLog.Write("UPDATE", $"applied version={applied}");
            return $"PixelPointer atualizado para {applied}; a próxima abertura usa a versão nova.";
        }
        catch (Exception ex) when (IsUpdateFailure(ex))
        {
            TryUpdatePreferences(preferences => preferences with { LastFailure = $"Aplicação da versão {staged.Version}: {ex.Message}" });
            DiagnosticLog.Write("UPDATE", $"apply failed version={staged.Version} error={ex.GetType().Name}");
            throw;
        }
    }

    /// <summary>Executado no Windows pela versão preparada ('update finish').</summary>
    internal static int FinishOnWindows(int waitProcessId)
    {
        if (!OperatingSystem.IsWindows() || InstallOriginDetector.ApplicationDirectory is not { } staged)
        {
            Console.Error.WriteLine("'update finish' é interno e só é usado no Windows.");
            return 2;
        }

        try
        {
            using var _ = UpdateLock.Acquire(TimeSpan.FromMinutes(2));
            var applied = UpdateInstaller.FinishOnWindows(staged, InstallOriginDetector.CurrentRid, waitProcessId, TimeSpan.FromMinutes(2));
            TryUpdatePreferences(preferences => preferences with { AppliedVersion = applied.ToString(), LastFailure = null });
            DiagnosticLog.Write("UPDATE", $"applied version={applied}");
            return 0;
        }
        catch (Exception ex) when (IsUpdateFailure(ex) || ex is ArgumentException)
        {
            TryUpdatePreferences(preferences => preferences with { LastFailure = $"Conclusão da atualização: {ex.Message}" });
            DiagnosticLog.Write("UPDATE", $"finish failed error={ex.GetType().Name}");
            return 1;
        }
    }

    /// <summary>Limpeza de restos e aviso do resultado da última atualização, na abertura do daemon.</summary>
    internal static string? OnDaemonStart()
    {
        if (InstallOriginDetector.ApplicationDirectory is { } directory && InstallOriginDetector.Detect().CanSelfUpdate)
        {
            try
            {
                using var _ = UpdateLock.Acquire(TimeSpan.Zero);
                UpdateInstaller.Cleanup(directory, CurrentVersion);
            }
            catch (Exception ex) when (IsUpdateFailure(ex))
            {
                DiagnosticLog.Write("UPDATE", $"startup cleanup skipped error={ex.GetType().Name}");
            }
        }

        var preferences = UpdatePreferencesStore.Load().Preferences;
        string? notice = null;
        if (preferences.AppliedVersion is { } applied && SemanticVersion.TryParse(applied, out var version))
        {
            notice = version == CurrentVersion
                ? $"Atualizado para {applied}."
                : $"A atualização para {applied} foi registrada, mas esta é a versão {CurrentVersion}.";
        }

        if (preferences.LastFailure is { } failure)
        {
            notice = notice is null ? $"Última atualização não concluída: {failure}" : $"{notice} {failure}";
        }

        if (notice is not null)
        {
            TryUpdatePreferences(current => current with { AppliedVersion = null, LastFailure = null });
        }

        return notice;
    }

    internal static (InstallOrigin Origin, string Directory) RequireSelfUpdate()
    {
        var origin = InstallOriginDetector.Detect();
        if (!origin.CanSelfUpdate || InstallOriginDetector.ApplicationDirectory is not { } directory)
        {
            throw new InvalidOperationException($"Esta instalação não é atualizada pelo PixelPointer ({origin.Detail}). {origin.Guidance}".TrimEnd());
        }

        return (origin, directory);
    }

    internal static void TryUpdatePreferences(Func<UpdatePreferences, UpdatePreferences> change)
    {
        try
        {
            UpdatePreferencesStore.Update(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            DiagnosticLog.Write("UPDATE", $"preferences not saved error={ex.GetType().Name}");
        }
    }
}

/// <summary>Trava por usuário entre CLI, daemon e o auxiliar do Windows durante staging, aplicação e limpeza.</summary>
internal static class UpdateLock
{
    internal static FileStream Acquire(TimeSpan wait)
    {
        var path = Path.Combine(Path.GetDirectoryName(UpdatePreferencesStore.GetPath())!, "update.lock");
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("Outra operação de atualização do PixelPointer está em andamento.", ex);
            }
        }
    }
}
