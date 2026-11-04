using PixelPointer.Core.Updates;

namespace PixelPointer.App.Updates;

/// <summary>
/// Verificação em segundo plano no daemon: nunca abre janelas nem interrompe sobreposições, gravação ou Live Zoom.
/// O status é lido pela thread principal (tray e console) por <see cref="StatusVersion"/>.
/// </summary>
internal sealed class UpdateWorker : IDisposable
{
    private static readonly TimeSpan PreferencesRefresh = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _trigger = new(0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private readonly Task _loop;
    private UpdatePreferences _preferences;
    private string _status;
    private int _statusVersion;
    private int _manualRequested;
    private DateTimeOffset? _lastAttempt;

    internal UpdateWorker()
    {
        Origin = InstallOriginDetector.Detect();
        _preferences = UpdatePreferencesStore.Load().Preferences;
        _status = InitialStatus();
        _loop = Task.Run(RunAsync);
    }

    internal InstallOrigin Origin { get; }

    internal UpdatePreferences Preferences => Volatile.Read(ref _preferences);

    internal string Status => Volatile.Read(ref _status);

    internal int StatusVersion => Volatile.Read(ref _statusVersion);

    /// <summary>Alterna uma preferência pela tray (thread principal) e devolve o estado gravado.</summary>
    internal UpdatePreferences Change(Func<UpdatePreferences, UpdatePreferences> change)
    {
        try
        {
            Volatile.Write(ref _preferences, UpdatePreferencesStore.Update(change));
            _trigger.Release();
        }
        catch (Exception ex) when (UpdateService.IsUpdateFailure(ex))
        {
            SetStatus($"Preferência não alterada: {ex.Message}");
        }

        return Preferences;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
        _trigger.Dispose();
    }

    private string InitialStatus()
    {
        var current = UpdateService.CurrentVersion;
        if (Origin.CanSelfUpdate && InstallOriginDetector.ApplicationDirectory is { } directory
            && UpdateInstaller.FindStaged(directory) is { } staged)
        {
            return $"Versão {staged.Version} pronta; será instalada ao encerrar o PixelPointer";
        }

        return $"Versão {current}";
    }

    private async Task RunAsync()
    {
        var token = _cancellation.Token;
        while (!token.IsCancellationRequested)
        {
            var preferences = RefreshPreferences();
            var wait = PreferencesRefresh;
            if (preferences.CheckAutomatically)
            {
                var due = NextAutomaticCheck(preferences) - DateTimeOffset.UtcNow;
                wait = due < TimeSpan.Zero ? TimeSpan.Zero : due < wait ? due : wait;
            }

            try
            {
                await _trigger.WaitAsync(wait, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var manual = Interlocked.Exchange(ref _manualRequested, 0) == 1;
            preferences = RefreshPreferences();
            if (manual || (preferences.CheckAutomatically && DateTimeOffset.UtcNow >= NextAutomaticCheck(preferences)))
            {
                _lastAttempt = DateTimeOffset.UtcNow;
                await CheckAsync(preferences, token).ConfigureAwait(false);
            }
        }
    }

    // Falhas de rede não avançam LastCheck: a próxima tentativa automática espera RetryDelay.
    private DateTimeOffset NextAutomaticCheck(UpdatePreferences preferences)
    {
        var due = UpdateSchedule.NextCheck(_started, preferences.LastCheck, DateTimeOffset.UtcNow);
        return _lastAttempt is { } attempt && attempt + RetryDelay > due ? attempt + RetryDelay : due;
    }

    /// <summary>Pedido explícito de "Verificar agora" pela tray; ignora o intervalo.</summary>
    internal void RequestCheck()
    {
        Interlocked.Exchange(ref _manualRequested, 1);
        _trigger.Release();
    }

    private UpdatePreferences RefreshPreferences()
    {
        var loaded = UpdatePreferencesStore.Load();
        if (loaded.Error is null && loaded.Preferences != Preferences)
        {
            Volatile.Write(ref _preferences, loaded.Preferences);
            Interlocked.Increment(ref _statusVersion);
        }

        return Preferences;
    }

    private async Task CheckAsync(UpdatePreferences preferences, CancellationToken token)
    {
        SetStatus("Verificando atualizações...");
        try
        {
            using var client = new UpdateClient();
            var candidate = await UpdateService.CheckAsync(client, token).ConfigureAwait(false);
            RefreshPreferences();
            if (candidate.Availability != UpdateAvailability.Available)
            {
                SetStatus(candidate.Availability == UpdateAvailability.UpToDate ? $"Versão {candidate.Current}: atualizada" : candidate.Detail);
                return;
            }

            if (Origin.CanSelfUpdate && InstallOriginDetector.ApplicationDirectory is { } directory
                && UpdateInstaller.FindStaged(directory) is { } staged && staged.Version == candidate.Latest)
            {
                SetStatus($"Versão {staged.Version} pronta; será instalada ao encerrar o PixelPointer");
                return;
            }

            if (!preferences.EffectiveInstallAutomatically || !Origin.CanSelfUpdate)
            {
                SetStatus(Origin.CanSelfUpdate
                    ? $"Versão {candidate.Latest} disponível; use 'pixelpointer update install'"
                    : $"Versão {candidate.Latest} disponível. {Origin.Guidance}");
                return;
            }

            var prepared = await UpdateService.StageAsync(client, candidate, message => SetStatus($"{message}..."), token)
                .ConfigureAwait(false);
            SetStatus($"Versão {prepared.Version} pronta; será instalada ao encerrar o PixelPointer");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (UpdateService.IsUpdateFailure(ex))
        {
            DiagnosticLog.Write("UPDATE", $"background check failed error={ex.GetType().Name}");
            SetStatus($"Atualização não verificada: {UpdatePreferences.Truncate(ex.Message)}");
        }
    }

    private void SetStatus(string status)
    {
        Volatile.Write(ref _status, status);
        Interlocked.Increment(ref _statusVersion);
    }
}
