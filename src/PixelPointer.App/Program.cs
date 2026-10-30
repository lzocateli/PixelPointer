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
        "install-desktop-file" => InstallDesktopFile(),
        "grant" => Grant(args.Contains("--auto")),
        "selftest" => SelfTest(),
        "--version" => PrintVersion(),
        "help" or "--help" or "-h" => Help(Console.Out),
        _ => UnknownCommand(command),
    };
}
catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or OperationCanceledException)
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
          daemon                   registra Ctrl+1/Ctrl+2 e o socket IPC; Ctrl+C encerra
          zoom | draw              envia o comando ao daemon (ou executa direto)
          install-desktop-file     instala o .desktop exigido pelo registro de app id (Linux)
          grant                    pede a permissão de captura com uma janela focada (GNOME/Wayland)
          selftest                 valida as bibliotecas nativas sem tela (Skia e SDL com driver dummy)
          --version | --help

        Na sobreposição: roda/setas = zoom, clique = desenhar, R G B Y O P = cor,
        Ctrl+roda = espessura, Ctrl+Z = desfazer, E = apagar, W/K = quadro branco/preto,
        Ctrl+S = salvar PNG, botão direito/Esc = sair.
        """);
    return 0;
}

static async Task<int> ProbeAsync()
{
    Console.WriteLine($"SO .............. {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
    Console.WriteLine($"Runtime ......... {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"Native AOT ...... {!File.Exists(Path.Combine(AppContext.BaseDirectory, "pixelpointer.dll"))}");
    Console.WriteLine($"Sessão .......... {SessionInfo.Describe()}");
    Console.WriteLine($"Skia ............ SkiaSharp {typeof(SKBitmap).Assembly.GetName().Version}");

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
        Console.WriteLine($"Registro app id . {(registry is null ? $"ok ({PortalClient.AppId})" : registry)}");
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
        RunOverlay(portal, new HotkeyCommand(mode == OverlayMode.Draw ? "draw" : "zoom"), image, Stopwatch.StartNew());
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

    var ipc = IpcChannel.ServeAsync(Enqueue, cancellation.Token);
    Console.WriteLine($"IPC ............. {IpcChannel.SocketPath}");

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

    cancellation.Token.Register(queue.CompleteAdding);
    InitSdl();
    try
    {
        // A janela SDL precisa da thread principal (exigência do macOS); comandos chegam de outras threads pela fila.
        foreach (var (cmd, received) in queue.GetConsumingEnumerable())
        {
            try
            {
                RunOverlay(portal, cmd, null, received);
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
        SDL_Quit();
        ipc.GetAwaiter().GetResult();
    }

    return 0;
}

static int SendOrRun(string command) =>
    IpcChannel.TrySendAsync(command).GetAwaiter().GetResult()
        ? 0
        : Overlay(null, command == "draw" ? OverlayMode.Draw : OverlayMode.Zoom);

static void RunOverlay(PortalClient? portal, HotkeyCommand command, string? image, Stopwatch received)
{
    var capture = ScreenCaptureFactory.Create(portal);
    var captureStart = received.Elapsed.TotalMilliseconds;
    using var bitmap = image is not null
        ? SKBitmap.Decode(image) ?? throw new InvalidOperationException($"Imagem inválida: {image}")
        : capture.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
    var captureMs = received.Elapsed.TotalMilliseconds - captureStart;

    if (command.ActivationToken is { } token)
    {
        // Com o token do xdg-activation o compositor Wayland entrega o foco à janela nova.
        Environment.SetEnvironmentVariable("XDG_ACTIVATION_TOKEN", token);
    }

    var metrics = new OverlaySession(bitmap, command.Id == "draw" ? OverlayMode.Draw : OverlayMode.Zoom).Run();
    Console.WriteLine($"""
        --- sessão {command.Id} ---
        captura ......... {(image is null ? capture.Name : "arquivo")}: {bitmap.Width}x{bitmap.Height} em {captureMs:0} ms
        janela .......... {metrics.VideoDriver} / renderer {metrics.Renderer}, {metrics.PixelWidth}x{metrics.PixelHeight} px, densidade {metrics.PixelDensity:0.##}
        1º quadro ....... {metrics.WindowToFirstFrameMs:0} ms após criar a janela; {captureMs + metrics.WindowToFirstFrameMs:0} ms após o comando
        quadros ......... {metrics.Frames}, média {metrics.AverageFrameMs:0.0} ms, p95 {metrics.P95FrameMs:0.0} ms (Skia CPU + upload)
        caneta .......... {metrics.PenEvents} eventos, pressão máx. {metrics.MaxPressure:0.00}
        """);
}

static PortalClient ConnectPortal()
{
    var portal = PortalClient.ConnectAsync().GetAwaiter().GetResult();
    // O registro precisa ser a primeira chamada de portal desta conexão.
    if (portal.TryRegisterAppIdAsync().GetAwaiter().GetResult() is { } error)
    {
        Console.Error.WriteLine($"aviso: registro do app id falhou ({error}); rode 'install-desktop-file'.");
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

static int InstallDesktopFile()
{
    if (!OperatingSystem.IsLinux())
    {
        Console.Error.WriteLine("Somente Linux.");
        return 2;
    }

    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, $"{PortalClient.AppId}.desktop");
    File.WriteAllText(path, $"""
        [Desktop Entry]
        Type=Application
        Name=PixelPointer
        Comment=Zoom e anotação de tela para apresentações
        Exec={Environment.ProcessPath} daemon
        Icon=zoom-in
        Terminal=false
        NoDisplay=true
        Categories=Utility;

        """);
    Console.WriteLine($"Instalado: {path}");
    return 0;
}
