using System.Net.Sockets;
using System.Text;
using PixelPointer.App.Hotkeys;

namespace PixelPointer.App;

/// <summary>
/// Canal local da instância em execução: <c>pixelpointer zoom</c> envia o comando por socket Unix
/// (também disponível no Windows 10+). É o caminho para atalhos do desktop quando não há portal.
/// </summary>
internal static class IpcChannel
{
    internal static string SocketPath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime ? runtime : Path.GetTempPath(),
        "pixelpointer.sock");

    internal static bool IsKnownCommand(string command) => command is "zoom" or "draw";

    internal static async Task ServeAsync(Action<HotkeyCommand> onCommand, CancellationToken cancellationToken)
    {
        File.Delete(SocketPath);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        listener.Listen(4);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[256];
                var read = await client.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                var command = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                if (IsKnownCommand(command))
                {
                    onCommand(new HotkeyCommand(command, Environment.GetEnvironmentVariable("XDG_ACTIVATION_TOKEN")));
                    await client.SendAsync("ok\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await client.SendAsync("unknown\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            File.Delete(SocketPath);
        }
    }

    /// <summary>Envia o comando à instância em execução; false quando não há nenhuma.</summary>
    internal static async Task<bool> TrySendAsync(string command)
    {
        if (!File.Exists(SocketPath))
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath)).ConfigureAwait(false);
            await socket.SendAsync(Encoding.UTF8.GetBytes(command)).ConfigureAwait(false);
            var buffer = new byte[16];
            var read = await socket.ReceiveAsync(buffer).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buffer, 0, read).StartsWith("ok", StringComparison.Ordinal);
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
