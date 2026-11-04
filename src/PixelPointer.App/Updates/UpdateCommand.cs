using System.Globalization;
using PixelPointer.Core.Updates;

namespace PixelPointer.App.Updates;

/// <summary>Subcomandos de 'pixelpointer update'.</summary>
internal static class UpdateCommand
{
    internal const string Usage = """
        pixelpointer update [status]            versão, origem da instalação, preferências e atualização preparada
        pixelpointer update check               consulta agora a release estável mais recente (ignora o intervalo)
        pixelpointer update install             baixa, valida e prepara; aplica já se o daemon não estiver em execução
        pixelpointer update apply               aplica a atualização preparada (recusa com o daemon em execução)
        pixelpointer update auto-check on|off   verificação automática no daemon (padrão: off)
        pixelpointer update auto-install on|off instalação automática; exige auto-check on (padrão: off)
        """;

    internal static int Run(string[] args, Func<bool> daemonRunning)
    {
        var verb = args.Length > 0 ? args[0] : "status";
        try
        {
            return (verb, args.Length) switch
            {
                ("status", 1) or ("status", 0) => Status(),
                ("check", 1) => Check(),
                ("install", 1) => Install(daemonRunning),
                ("apply", 1) => Apply(daemonRunning),
                ("auto-check", 2) when args[1] is "on" or "off" => SetCheck(args[1] == "on"),
                ("auto-install", 2) when args[1] is "on" or "off" => SetInstall(args[1] == "on"),
                ("finish", 3) when args[1] == "--wait-pid" && int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                    => UpdateService.FinishOnWindows(pid),
                ("help" or "--help" or "-h", 1) => PrintUsage(Console.Out, 0),
                _ => PrintUsage(Console.Error, 2),
            };
        }
        catch (Exception ex) when (UpdateService.IsUpdateFailure(ex))
        {
            Console.Error.WriteLine($"erro: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage(TextWriter writer, int code)
    {
        writer.WriteLine(Usage);
        return code;
    }

    private static int Status()
    {
        var status = UpdateService.GetStatus();
        Console.WriteLine($"Versão instalada .. {status.Current}");
        Console.WriteLine($"Origem ............ {status.Origin.Detail}");
        Console.WriteLine($"Autoatualização ... {(status.Origin.CanSelfUpdate ? "disponível" : "indisponível nesta instalação")}");
        if (!string.IsNullOrEmpty(status.Origin.Guidance))
        {
            Console.WriteLine($"Como atualizar .... {status.Origin.Guidance}");
        }

        var preferences = status.Preferences;
        Console.WriteLine($"Verificação auto .. {OnOff(preferences.CheckAutomatically)}");
        Console.WriteLine($"Instalação auto ... {OnOff(preferences.EffectiveInstallAutomatically)}{(preferences.EffectiveInstallAutomatically && !status.Origin.CanSelfUpdate ? " (sem efeito nesta instalação)" : string.Empty)}");
        Console.WriteLine($"Última consulta ... {preferences.LastCheck?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "nunca"}");
        Console.WriteLine($"Preparada ......... {(status.Staged is { } staged ? $"{staged.Version}; aplicada ao encerrar o daemon ou com 'pixelpointer update apply'" : "nenhuma")}");
        if (preferences.LastFailure is { } failure)
        {
            Console.WriteLine($"Última falha ...... {failure}");
        }

        Console.WriteLine($"Preferências ...... {status.PreferencesPath}{(status.PreferencesError is null ? string.Empty : $" (ignoradas: {status.PreferencesError})")}");
        return 0;
    }

    private static int Check()
    {
        using var client = new UpdateClient();
        var candidate = UpdateService.CheckAsync(client, CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine(candidate.Detail);
        if (candidate.Availability == UpdateAvailability.Available)
        {
            var origin = InstallOriginDetector.Detect();
            Console.WriteLine(origin.CanSelfUpdate ? "Para instalar: pixelpointer update install" : origin.Guidance);
        }

        return 0;
    }

    private static int Install(Func<bool> daemonRunning)
    {
        UpdateService.RequireSelfUpdate();
        using var client = new UpdateClient();
        var candidate = UpdateService.CheckAsync(client, CancellationToken.None).GetAwaiter().GetResult();
        if (candidate.Availability != UpdateAvailability.Available)
        {
            Console.WriteLine(candidate.Detail);
            return candidate.Availability == UpdateAvailability.UpToDate ? 0 : 1;
        }

        var staged = UpdateService.StageAsync(client, candidate, message => Console.WriteLine($"{message}..."), CancellationToken.None)
            .GetAwaiter().GetResult();
        Console.WriteLine($"Versão {staged.Version} validada e preparada.");
        if (daemonRunning())
        {
            Console.WriteLine("O daemon está em execução: a atualização será aplicada quando ele encerrar (Sair na tray ou 'pixelpointer quit').");
            return 0;
        }

        Console.WriteLine(UpdateService.ApplyStaged());
        return 0;
    }

    private static int Apply(Func<bool> daemonRunning)
    {
        if (daemonRunning())
        {
            Console.Error.WriteLine("O daemon está em execução; a atualização será aplicada quando ele encerrar. Use 'pixelpointer quit' para encerrá-lo agora.");
            return 1;
        }

        Console.WriteLine(UpdateService.ApplyStaged());
        return 0;
    }

    private static int SetCheck(bool enabled)
    {
        var updated = UpdatePreferencesStore.Update(preferences => preferences.WithCheckAutomatically(enabled));
        Console.WriteLine($"Verificação automática {OnOff(updated.CheckAutomatically)}; instalação automática {OnOff(updated.EffectiveInstallAutomatically)}.");
        return 0;
    }

    private static int SetInstall(bool enabled)
    {
        if (enabled)
        {
            UpdateService.RequireSelfUpdate();
        }

        var updated = UpdatePreferencesStore.Update(preferences => preferences.WithInstallAutomatically(enabled));
        Console.WriteLine($"Instalação automática {OnOff(updated.EffectiveInstallAutomatically)}.");
        return 0;
    }

    private static string OnOff(bool value) => value ? "ativada" : "desativada";
}
