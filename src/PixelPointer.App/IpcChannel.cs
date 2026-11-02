using System.Net.Sockets;
using System.Text;
using PixelPointer.App.Hotkeys;

namespace PixelPointer.App;

/// <summary>Canal local protegido da instância em execução.</summary>
internal static class IpcChannel
{
    private const int MaxCommandLength = 64;

    internal static string SocketPath => Path.Combine(GetRuntimeDirectory(), "pixelpointer.sock");

    internal static bool IsKnownCommand(string command) =>
        command is "zoom" or "draw" or "timer" or "status" or "quit";

    internal static async Task ServeAsync(
        Action<HotkeyCommand> onCommand,
        Func<string> getStatus,
        Action onQuit,
        CancellationToken cancellationToken)
    {
        using var instanceLock = AcquireInstanceLock();
        RemoveStaleSocket();

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var bound = false;
        try
        {
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                throw new InvalidOperationException("O caminho do socket IPC já está reservado por outra instância.", ex);
            }

            bound = true;
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            listener.Listen(4);
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    var command = await ReceiveCommandAsync(client, requestTimeout.Token).ConfigureAwait(false);
                    if (command is null || !IsKnownCommand(command))
                    {
                        await SendResponseAsync(client, "unknown\n", requestTimeout.Token).ConfigureAwait(false);
                        continue;
                    }

                    var shouldQuit = command == "quit";
                    var response = command switch
                    {
                        "zoom" or "draw" or "timer" => QueueCommand(command, onCommand),
                        "status" => getStatus(),
                        "quit" => "quitting",
                        _ => "unknown",
                    };
                    await SendResponseAsync(client, response + "\n", requestTimeout.Token).ConfigureAwait(false);
                    if (shouldQuit)
                    {
                        onQuit();
                    }
                }
                catch (SocketException)
                {
                    Console.Error.WriteLine("aviso: o cliente IPC desconectou antes da resposta.");
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Console.Error.WriteLine("aviso: o cliente IPC excedeu o prazo para concluir o comando.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Dispose();
            if (bound)
            {
                File.Delete(SocketPath);
            }
        }
    }

    internal static async Task<bool> TrySendAsync(string command) =>
        await SendCommandAsync(command).ConfigureAwait(false) switch
        {
            "ok" => true,
            null => false,
            _ => throw new InvalidOperationException("O daemon retornou uma resposta IPC inesperada."),
        };

    internal static Task<string?> SendControlCommandAsync(string command) => SendCommandAsync(command);

    private static string QueueCommand(string command, Action<HotkeyCommand> onCommand)
    {
        onCommand(new HotkeyCommand(command, Environment.GetEnvironmentVariable("XDG_ACTIVATION_TOKEN")));
        return "ok";
    }

    private static async Task<string?> SendCommandAsync(string command)
    {
        if (!IsKnownCommand(command))
        {
            throw new ArgumentException("Comando IPC desconhecido.", nameof(command));
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), timeout.Token).ConfigureAwait(false);
            await SendResponseAsync(socket, command + "\n", timeout.Token).ConfigureAwait(false);
            return await ReceiveCommandAsync(socket, timeout.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("O daemon encerrou a conexão sem responder ao comando IPC.");
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressNotAvailable or SocketError.HostNotFound or SocketError.ConnectionRefused
            && !File.Exists(SocketPath))
        {
            return null;
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Não foi possível comunicar com o daemon; verifique seu estado antes de reiniciá-lo.", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException("O daemon não respondeu ao comando IPC no prazo esperado.", ex);
        }
    }

    private static async Task<string?> ReceiveCommandAsync(Socket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxCommandLength + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            var newline = Array.IndexOf(buffer, (byte)'\n', length, read);
            if (newline >= 0)
            {
                return Encoding.UTF8.GetString(buffer, 0, newline);
            }

            length += read;
        }

        return null;
    }

    private static async Task SendResponseAsync(Socket socket, string response, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(response);
        var sent = 0;
        while (sent < bytes.Length)
        {
            sent += await socket.SendAsync(bytes.AsMemory(sent), cancellationToken).ConfigureAwait(false);
        }
    }

    private static FileStream AcquireInstanceLock()
    {
        try
        {
            var lockPath = SocketPath + ".lock";
            if (new FileInfo(lockPath).LinkTarget is not null)
            {
                throw new InvalidOperationException("O caminho de lock do IPC não pode ser um link simbólico.");
            }

            var instanceLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(lockPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                return instanceLock;
            }
            catch
            {
                instanceLock.Dispose();
                throw;
            }
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Já existe uma instância do daemon em execução.", ex);
        }
    }

    private static void RemoveStaleSocket()
    {
        if (new FileInfo(SocketPath).LinkTarget is not null)
        {
            throw new InvalidOperationException("O caminho do socket IPC não pode ser um link simbólico.");
        }

        using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            probe.Connect(new UnixDomainSocketEndPoint(SocketPath));
            throw new InvalidOperationException("Já existe um daemon ativo usando o socket IPC.");
        }
        catch (SocketException ex) when (!OperatingSystem.IsWindows() && ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            // Com o lock da instância retido, nenhum daemon escuta aqui; sockets têm tamanho zero, arquivos com conteúdo são preservados.
            var existing = new FileInfo(SocketPath);
            if (!existing.Exists || existing.Length != 0)
            {
                throw new InvalidOperationException("O caminho do socket IPC existe, mas não é um socket obsoleto.", ex);
            }

            Console.Error.WriteLine("aviso: removendo socket IPC obsoleto de uma execução anterior.");
            existing.Delete();
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressNotAvailable or SocketError.HostNotFound)
        {
            if (File.Exists(SocketPath))
            {
                throw new InvalidOperationException("O caminho do socket IPC existe, mas não é um socket obsoleto.", ex);
            }
        }
        catch (SocketException ex) when (OperatingSystem.IsWindows() && ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Não foi possível verificar com segurança o caminho reservado ao socket IPC.", ex);
        }
    }

    private static string GetRuntimeDirectory()
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            if (!Path.IsPathFullyQualified(runtimeDirectory))
            {
                throw new InvalidOperationException("XDG_RUNTIME_DIR deve ser um caminho absoluto.");
            }

            var normalized = Path.GetFullPath(runtimeDirectory);
            if (!Directory.Exists(normalized) || new DirectoryInfo(normalized).LinkTarget is not null)
            {
                throw new InvalidOperationException("XDG_RUNTIME_DIR deve apontar para um diretório existente que não seja link simbólico.");
            }

            EnsurePrivateDirectory(normalized);
            return normalized;
        }

        var baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new InvalidOperationException("Não foi possível determinar um diretório privado para o IPC.");
        }

        var fallback = OperatingSystem.IsWindows()
            ? Path.Combine(baseDirectory, "PixelPointer", "run")
            : Path.Combine(baseDirectory, ".cache", "pixelpointer", "run");
        var existed = Directory.Exists(fallback);
        Directory.CreateDirectory(fallback);
        if (OperatingSystem.IsLinux() && !existed)
        {
            File.SetUnixFileMode(fallback,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        EnsurePrivateDirectory(fallback);
        return Path.GetFullPath(fallback);
    }

    private static void EnsurePrivateDirectory(string directory)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (new DirectoryInfo(directory).LinkTarget is not null)
        {
            throw new InvalidOperationException("O diretório do IPC não pode ser um link simbólico.");
        }

        var mode = File.GetUnixFileMode(directory);
        var sharedAccess = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        if ((mode & sharedAccess) != 0)
        {
            throw new InvalidOperationException("O diretório do IPC deve ser privado ao usuário (modo 0700).");
        }
    }
}
