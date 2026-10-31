using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelPointer.App;
using PixelPointer.App.Capture;
using PixelPointer.App.Hotkeys;
using PixelPointer.App.Linux;
using PixelPointer.App.Overlay;
using PixelPointer.App.Windows;
using SDL;
using SkiaSharp;
using static SDL.SDL3;

// Versão inicial, derivada do spike da F0 (docs/f0-relatorio.md): zoom e desenho com atalhos globais e IPC.
// A estrutura definitiva em projetos Core/Rendering/Platform chega na F2 (docs/plano-implementacao.md).
// Main é síncrono de propósito: o SDL precisa da thread principal (exigência do macOS), e um laço de janela rodando
// na continuação de um await ocuparia a thread de leitura do D-Bus, impedindo qualquer resposta dos portais.
NativeLibraries.Register();
var command = args.Length > 0 ? args[0] : "help";
string? Option(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

try
{
    return command switch
    {
        "probe" => ProbeAsync().GetAwaiter().GetResult(),
        "capture" => CaptureAsync(Option("--out") ?? "pixelpointer-capture.png").GetAwaiter().GetResult(),
        "overlay" => Overlay(Option("--image"), Option("--mode") == "draw" ? OverlayMode.Draw : OverlayMode.Zoom),
        "daemon" => Daemon(),
        "zoom" or "draw" => SendOrRun(command),
        "status" => Status(),
        "quit" => Quit(),
        "install-desktop-file" => InstallDesktopFile(),
        "uninstall-desktop-file" => InstallDesktopFile(remove: true),
        "grant" => Grant(args.Contains("--auto")),
        "selftest" => SelfTest(),
        "--version" => PrintVersion(),
        "help" or "--help" or "-h" => Help(Console.Out),
        _ => UnknownCommand(command),
    };
}
catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or OperationCanceledException
    or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"erro: {ex.Message}");
    return 1;
}

static int PrintVersion()
{
    Console.WriteLine(AppInfo.Version);
    return 0;
}

static int SelfTest()
{
    // Usado pelo smoke dos pacotes: prova que libSkiaSharp e SDL3 carregam do diretório do executável.
    using var surface = SKSurface.Create(new SKImageInfo(64, 64));
    surface.Canvas.Clear(SKColors.OrangeRed);
    using var png = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
    SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "dummy");
    SDL_SetHint(SDL_HINT_NO_SIGNAL_HANDLERS, "1");
    if (png is null || png.Size == 0 || !SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
    {
        Console.Error.WriteLine($"selftest falhou: {SDL_GetError()}");
        return 1;
    }

    var v = SDL_GetVersion();
    Console.WriteLine($"ok: SkiaSharp PNG {png.Size} bytes; SDL {v / 1000000}.{v / 1000 % 1000}.{v % 1000} ({SDL_GetCurrentVideoDriver()})");
    SDL_Quit();
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Comando desconhecido: {command}");
    Help(Console.Error);
    return 2;
}

static int Help(TextWriter writer)
{
    writer.WriteLine("""
        pixelpointer <comando>
          probe                    ambiente: SO, sessão, SDL, monitores, portais, AOT
          capture [--out arq.png]  captura a tela e mede a latência
          overlay [--image arq] [--mode zoom|draw]
                                   captura (ou usa a imagem) e abre a sobreposição
          daemon                   registra Ctrl+1/Ctrl+2; Esc fecha só a janela e Ctrl+C encerra
          zoom | draw              envia o comando ao daemon (ou executa direto)
          status                   informa se o daemon está em execução
          quit                     encerra o daemon e a sobreposição ativa
          install-desktop-file     instala a entrada de menu e o ícone do app (Linux)
          uninstall-desktop-file   remove entrada e ícone idênticos aos instalados (Linux)
          grant                    pede permissão via portal (Wayland); não é necessário no X11
          selftest                 valida as bibliotecas nativas sem tela (Skia e SDL com driver dummy)
          --version | --help

        Na sobreposição: roda/setas = zoom, clique = desenhar, R G B Y O P = cor,
        Ctrl+roda = espessura, Ctrl+Z = desfazer, E = apagar, W/K = quadro branco/preto,
        Ctrl+S = salvar PNG, botão direito/Esc = sair.
        Destino do Ctrl+S: Imagens/Screenshots no Windows, preferência de capturas ou
        Área de Trabalho no macOS, PICTURES/Screenshots conforme XDG no Linux.
        O caminho final é informado na janela; arquivos existentes são preservados.
        Configurações opcionais em settings.json (o probe mostra o caminho), lidas a cada
        sobreposição: {"version": 1, "pen": {"color": "red", "width": 6}, "zoom": {"initial": 2}}.
        Arquivo inválido é ignorado com aviso e nunca é regravado.
        """);
    return 0;
}

static async Task<int> ProbeAsync()
{
    Console.WriteLine($"SO .............. {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
    Console.WriteLine($"Runtime ......... {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"Native AOT ...... {!File.Exists(Path.Combine(AppContext.BaseDirectory, "pixelpointer.dll"))}");
    Console.WriteLine($"Sessão .......... {SessionInfo.Describe()}");
    if (SessionInfo.IsX11)
    {
        Console.WriteLine($"Compositor X11 .. {(X11Capabilities.HasCompositingManager() ? "presente" : "ausente")}");
    }

    Console.WriteLine($"Skia ............ SkiaSharp {typeof(SKBitmap).Assembly.GetName().Version}");
    var settings = SettingsStore.Load();
    Console.WriteLine($"Configurações .. {settings.Status switch
    {
        SettingsStatus.Loaded => "carregadas",
        SettingsStatus.Missing => "ausentes; usando os padrões",
        _ => $"ignoradas ({settings.Error}); usando os padrões",
    }}{(settings.Path is null ? string.Empty : $" em {settings.Path}")}");

    // SDL antes de qualquer await, para rodar na thread principal.
    InitSdl();
    var v = SDL_GetVersion();
    Console.WriteLine($"SDL ............. {v / 1000000}.{v / 1000 % 1000}.{v % 1000}, driver de vídeo {SDL_GetCurrentVideoDriver()}");
    using (var displays = SDL_GetDisplays())
    {
        for (var i = 0; displays is not null && i < displays.Count; i++)
        {
            var id = displays[i];
            var bounds = GetDisplayBounds(id);
            Console.WriteLine($"Monitor {i} ....... {SDL_GetDisplayName(id)}: {bounds.w}x{bounds.h} em ({bounds.x},{bounds.y}), escala {SDL_GetDisplayContentScale(id):0.##}");
        }
    }

    SDL_Quit();

    if (OperatingSystem.IsLinux())
    {
        using var portal = await PortalClient.ConnectAsync();
        var registry = await portal.TryRegisterAppIdAsync();
        Console.WriteLine(registry.Status switch
        {
            AppIdRegistrationStatus.Registered => $"Registro app id . ok ({PortalClient.AppId})",
            AppIdRegistrationStatus.RegistryUnavailable => $"Registro app id . Registry indisponível ({registry.Detail})",
            _ => $"Registro app id . falhou ({registry.Detail})",
        });
        foreach (var name in new[] { "Screenshot", "ScreenCast", "GlobalShortcuts", "RemoteDesktop", "InputCapture" })
        {
            var version = await portal.GetVersionAsync($"org.freedesktop.portal.{name}");
            Console.WriteLine($"Portal {name,-16} {(version == 0 ? "ausente" : $"v{version}")}");
        }
    }

    return 0;
}

static unsafe SDL_Rect GetDisplayBounds(SDL_DisplayID id)
{
    SDL_Rect bounds;
    SDL_GetDisplayBounds(id, &bounds);
    return bounds;
}

static async Task<int> CaptureAsync(string output)
{
    using var portal = SessionInfo.IsWayland ? ConnectPortal() : null;
    var capture = ScreenCaptureFactory.Create(portal);
    var watch = Stopwatch.StartNew();
    using var bitmap = await capture.CaptureAsync(CancellationToken.None);
    var elapsed = watch.Elapsed.TotalMilliseconds;
    using (var file = File.Create(output))
    {
        bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
    }

    Console.WriteLine($"{capture.Name}: {bitmap.Width}x{bitmap.Height} em {elapsed:0} ms -> {Path.GetFullPath(output)}");
    return 0;
}

static int Overlay(string? image, OverlayMode mode)
{
    using var portal = image is null && SessionInfo.IsWayland ? ConnectPortal() : null;
    InitSdl();
    try
    {
        RunOverlay(portal, new HotkeyCommand(mode == OverlayMode.Draw ? "draw" : "zoom"), image,
            Stopwatch.StartNew(), CancellationToken.None);
        return 0;
    }
    finally
    {
        SDL_Quit();
    }
}

static int Daemon()
{
    using var cancellation = new CancellationTokenSource();
    void Stop(PosixSignalContext context)
    {
        context.Cancel = true;
        cancellation.Cancel();
    }

    // SIGTERM chega no logout e via systemd; SIGINT é o Ctrl+C do terminal.
    using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
    using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);

    using var portal = SessionInfo.IsWayland ? ConnectPortal() : null;
    using var queue = new BlockingCollection<(HotkeyCommand Command, Stopwatch Received)>();
    void Enqueue(HotkeyCommand c)
    {
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] comando {c.Id}{(c.ActivationToken is null ? string.Empty : " (com activation token)")}");
        queue.TryAdd((c, Stopwatch.StartNew()));
    }

    var ipc = IpcChannel.ServeAsync(Enqueue, () => "running", cancellation.Cancel, cancellation.Token);
    if (ipc.IsCompleted)
    {
        ipc.GetAwaiter().GetResult();
    }

    Console.WriteLine("IPC ............. ativo");

    try
    {
        using IHotkeyService? hotkeys = OperatingSystem.IsWindows() ? new Win32Hotkeys()
            : OperatingSystem.IsLinux() ? (portal is not null ? new PortalHotkeys(portal) : new X11Hotkeys())
            : null;
        if (hotkeys is null)
        {
            Console.WriteLine("Atalhos ......... ainda não implementados nesta plataforma; use o comando 'zoom'.");
        }
        else
        {
            try
            {
                Console.WriteLine($"Atalhos ......... {hotkeys.Name}: {hotkeys.StartAsync(Enqueue, cancellation.Token).GetAwaiter().GetResult()}");
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or OperationCanceledException)
            {
                Console.WriteLine($"Atalhos ......... indisponíveis ({ex.Message})");
            }
        }

        InitSdl();
        using var tray = TrayController.TryCreate(Enqueue, cancellation.Cancel, out var trayUnavailableReason);
        Console.WriteLine(tray is null
            ? $"Tray ............. indisponível ({trayUnavailableReason}); use 'pixelpointer status' e 'pixelpointer quit'."
            : "Tray ............. registrada (Zoom, Desenhar, Sair); a visibilidade depende do desktop.");

        while (!cancellation.IsCancellationRequested)
        {
            if (ipc.IsCompleted)
            {
                ipc.GetAwaiter().GetResult();
                throw new InvalidOperationException("O serviço IPC encerrou inesperadamente.");
            }

            if (!queue.TryTake(out var item))
            {
                PumpSdlEvents();
                continue;
            }

            try
            {
                RunOverlay(portal, item.Command, null, item.Received, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
            {
                Console.Error.WriteLine($"erro: {ex.Message}");
            }

            // Comandos recebidos durante a sessão não reabrem a sobreposição.
            while (queue.TryTake(out _))
            {
            }
        }
    }
    finally
    {
        cancellation.Cancel();
        SDL_Quit();
        ipc.GetAwaiter().GetResult();
    }

    return 0;
}

static int SendOrRun(string command) =>
    IpcChannel.TrySendAsync(command).GetAwaiter().GetResult()
        ? 0
        : Overlay(null, command == "draw" ? OverlayMode.Draw : OverlayMode.Zoom);

static int Status()
{
    var status = IpcChannel.SendControlCommandAsync("status").GetAwaiter().GetResult();
    if (status == "running")
    {
        Console.WriteLine("Daemon em execução.");
        return 0;
    }

    Console.Error.WriteLine("Daemon não está em execução.");
    return 1;
}

static int Quit()
{
    var response = IpcChannel.SendControlCommandAsync("quit").GetAwaiter().GetResult();
    if (response == "quitting")
    {
        Console.WriteLine("Encerrando daemon.");
        return 0;
    }

    Console.Error.WriteLine("Daemon não está em execução.");
    return 1;
}

static void RunOverlay(
    PortalClient? portal,
    HotkeyCommand command,
    string? image,
    Stopwatch received,
    CancellationToken cancellationToken)
{
    var mode = command.Id == "draw" ? OverlayMode.Draw : OverlayMode.Zoom;
    var hasX11Compositor = SessionInfo.IsX11 && X11Capabilities.HasCompositingManager();
    var transparentDraw = image is null && mode == OverlayMode.Draw && hasX11Compositor;
    if (image is null && mode == OverlayMode.Draw && SessionInfo.IsX11 && !hasX11Compositor)
    {
        Console.Error.WriteLine("informação: sem compositor X11; o desenho usará uma captura congelada, não transparência ao vivo.");
    }

    var capture = transparentDraw ? null : ScreenCaptureFactory.Create(portal);
    var captureStart = received.Elapsed.TotalMilliseconds;
    using var bitmap = image is not null
        ? SKBitmap.Decode(image) ?? throw new InvalidOperationException($"Imagem inválida: {image}")
        : capture?.CaptureAsync(cancellationToken).GetAwaiter().GetResult();
    var captureMs = capture is null ? 0 : received.Elapsed.TotalMilliseconds - captureStart;

    if (command.ActivationToken is { } token)
    {
        // Com o token do xdg-activation o compositor Wayland entrega o foco à janela nova.
        Environment.SetEnvironmentVariable("XDG_ACTIVATION_TOKEN", token);
    }

    var metrics = new OverlaySession(bitmap, mode, transparentDraw, LoadSettings()).Run(cancellationToken);
    Console.WriteLine($"""
        --- sessão {command.Id} ---
        captura ......... {(transparentDraw ? "X11 transparente" : image is null ? capture!.Name : "arquivo")}: {bitmap?.Width ?? metrics.PixelWidth}x{bitmap?.Height ?? metrics.PixelHeight} em {captureMs:0} ms
        janela .......... {metrics.VideoDriver} / renderer {metrics.Renderer}, {metrics.PixelWidth}x{metrics.PixelHeight} px, densidade {metrics.PixelDensity:0.##}
        1º quadro ....... {metrics.WindowToFirstFrameMs:0} ms após criar a janela; {captureMs + metrics.WindowToFirstFrameMs:0} ms após o comando
        quadros ......... {metrics.Frames}, média {metrics.AverageFrameMs:0.0} ms, p95 {metrics.P95FrameMs:0.0} ms (Skia CPU + upload)
        caneta .......... {metrics.PenEvents} eventos, pressão máx. {metrics.MaxPressure:0.00}
        """);
}

static PixelPointer.Core.Settings.AppSettings LoadSettings()
{
    var result = SettingsStore.Load();
    if (result.Status == SettingsStatus.Invalid)
    {
        Console.Error.WriteLine($"aviso: configurações ignoradas ({(result.Path is null ? string.Empty : $"{result.Path}: ")}{result.Error}); usando os padrões.");
    }

    return result.Settings;
}

static unsafe void PumpSdlEvents()
{
    SDL_Event trayEvent;
    SDL_WaitEventTimeout(&trayEvent, 50);
    while (SDL_PollEvent(&trayEvent))
    {
    }
}

static PortalClient ConnectPortal()
{
    var portal = PortalClient.ConnectAsync().GetAwaiter().GetResult();
    // O registro precisa ser a primeira chamada de portal desta conexão.
    var registration = portal.TryRegisterAppIdAsync().GetAwaiter().GetResult();
    if (registration.Status == AppIdRegistrationStatus.Failed)
    {
        Console.Error.WriteLine($"aviso: o registro do app id falhou ({registration.Detail}); verifique a entrada desktop.");
    }
    else if (registration.Status == AppIdRegistrationStatus.RegistryUnavailable)
    {
        Console.Error.WriteLine($"informação: o portal Registry não está disponível ({registration.Detail}); permissões podem não ser associadas ao app id.");
    }

    return portal;
}

static void InitSdl()
{
    SDL_SetAppMetadata("PixelPointer", AppInfo.Version, PortalClient.AppId);
    // Sem isto o SDL captura SIGINT/SIGTERM e o Ctrl+C do daemon deixa de funcionar.
    SDL_SetHint(SDL_HINT_NO_SIGNAL_HANDLERS, "1");
    if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
    {
        throw new InvalidOperationException($"SDL_Init: {SDL_GetError()}");
    }
}

static int Grant(bool auto)
{
    if (OperatingSystem.IsLinux() && SessionInfo.IsX11)
    {
        Console.WriteLine("Captura X11 usa XGetImage; não é necessário solicitar permissão ao portal.");
        return 0;
    }

    if (!OperatingSystem.IsLinux() || !SessionInfo.IsWayland)
    {
        Console.Error.WriteLine("A solicitação de permissão via portal está disponível somente em sessões Linux Wayland.");
        return 2;
    }

    using var portal = ConnectPortal();
    InitSdl();
    try
    {
        Console.WriteLine($"Permissão de captura: {PermissionWindow.Run(portal, auto)}");
        return 0;
    }
    finally
    {
        SDL_Quit();
    }
}

static int InstallDesktopFile(bool remove = false)
{
    if (!OperatingSystem.IsLinux())
    {
        Console.Error.WriteLine("Somente Linux.");
        return 2;
    }

    var executable = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(executable) || executable.Any(char.IsControl))
    {
        Console.Error.WriteLine("Não foi possível determinar um caminho seguro para iniciar o daemon pelo desktop.");
        return 1;
    }

    DesktopIntegration.Apply(DesktopIntegration.GetDataDirectory(), executable,
        Path.Combine(AppContext.BaseDirectory, "pixelpointer.svg"), remove);
    return 0;
}
