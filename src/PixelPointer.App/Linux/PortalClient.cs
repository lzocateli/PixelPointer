using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;

namespace PixelPointer.App.Linux;

internal enum AppIdRegistrationStatus
{
    Registered,
    RegistryUnavailable,
    Failed,
}

internal sealed record AppIdRegistrationResult(AppIdRegistrationStatus Status, string? Detail = null);

/// <summary>Cliente mínimo dos portais XDG (Registry, Screenshot, ScreenCast e GlobalShortcuts) sobre o barramento de sessão.</summary>
internal sealed class PortalClient : IDisposable
{
    internal const string AppId = "io.github.lzocateli.PixelPointer";
    private const string Destination = "org.freedesktop.portal.Desktop";
    private const string DesktopPath = "/org/freedesktop/portal/desktop";
    private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";

    private readonly DBusConnection _connection;
    private int _tokenCounter;

    private PortalClient(DBusConnection connection) => _connection = connection;

    internal string UniqueName => _connection.UniqueName ?? string.Empty;

    internal static async Task<PortalClient> ConnectAsync()
    {
        var connection = new DBusConnection(DBusAddress.Session!);
        await connection.ConnectAsync().ConfigureAwait(false);
        return new PortalClient(connection);
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Versão de uma interface do portal, ou 0 quando ela não existe.</summary>
    internal async Task<uint> GetVersionAsync(string portalInterface)
    {
        try
        {
            var message = CreateMessage((ref MessageWriter writer) =>
            {
                writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.DBus.Properties", "Get", "ss");
                writer.WriteString(portalInterface);
                writer.WriteString("version");
            });
            return await _connection.CallMethodAsync(message,
                static (message, _) => message.GetBodyReader().ReadVariantValue().GetUInt32()).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Associa esta conexão a um app id (org.freedesktop.host.portal.Registry, xdg-desktop-portal 1.19+).
    /// Apps fora de sandbox não têm app id; sem ele, permissões e atalhos não ficam associados ao PixelPointer.
    /// </summary>
    internal async Task<AppIdRegistrationResult> TryRegisterAppIdAsync()
    {
        try
        {
            var message = CreateMessage((ref MessageWriter writer) =>
            {
                writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.host.portal.Registry", "Register", "sa{sv}");
                writer.WriteString(AppId);
                var dict = writer.WriteDictionaryStart();
                writer.WriteDictionaryEnd(dict);
            });
            await _connection.CallMethodAsync(message, static (_, _) => 0).ConfigureAwait(false);
            return new AppIdRegistrationResult(AppIdRegistrationStatus.Registered);
        }
        catch (DBusErrorReplyException ex)
        {
            var status = ClassifyRegistryError(ex.ErrorName);
            return new AppIdRegistrationResult(status, $"{ex.ErrorName}: {ex.ErrorMessage}");
        }
    }

    internal static AppIdRegistrationStatus ClassifyRegistryError(string errorName) =>
        errorName is "org.freedesktop.DBus.Error.UnknownMethod" or "org.freedesktop.DBus.Error.UnknownInterface"
            ? AppIdRegistrationStatus.RegistryUnavailable
            : AppIdRegistrationStatus.Failed;

    /// <summary>Captura a tela inteira sem diálogo interativo e devolve o URI do PNG gerado pelo portal.</summary>
    internal async Task<string> ScreenshotAsync(CancellationToken cancellationToken)
    {
        var token = NextToken();
        var results = await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.portal.Screenshot", "Screenshot", "sa{sv}");
            writer.WriteString(string.Empty);
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            writer.WriteDictionaryEntryStart();
            writer.WriteString("interactive");
            writer.WriteVariantBool(false);
            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);
        return results["uri"].GetString();
    }

    /// <summary>Lê uma propriedade uint32 de uma interface do portal, ou 0 quando ausente.</summary>
    internal async Task<uint> GetUInt32PropertyAsync(string portalInterface, string property)
    {
        try
        {
            var message = CreateMessage((ref MessageWriter writer) =>
            {
                writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.DBus.Properties", "Get", "ss");
                writer.WriteString(portalInterface);
                writer.WriteString(property);
            });
            return await _connection.CallMethodAsync(message,
                static (message, _) => message.GetBodyReader().ReadVariantValue().GetUInt32()).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            return 0;
        }
    }

    /// <summary>Cria uma sessão ScreenCast e devolve o handle da sessão.</summary>
    internal async Task<string> CreateScreenCastSessionAsync(CancellationToken cancellationToken)
    {
        var token = NextToken();
        var sessionToken = NextToken();
        var results = await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, ScreenCastInterface, "CreateSession", "a{sv}");
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            WriteStringEntry(ref writer, "session_handle_token", sessionToken);
            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);
        var handle = results["session_handle"];
        return handle.Type == VariantValueType.ObjectPath ? handle.GetObjectPathAsString() : handle.GetString();
    }

    /// <summary>Seleciona um único monitor; com <paramref name="persist"/>, pede um restore token reutilizável.</summary>
    internal async Task SelectScreenCastSourcesAsync(string session, uint cursorMode, bool persist, string? restoreToken,
        CancellationToken cancellationToken)
    {
        var token = NextToken();
        await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, ScreenCastInterface, "SelectSources", "oa{sv}");
            writer.WriteObjectPath(session);
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            WriteUInt32Entry(ref writer, "types", 1);
            writer.WriteDictionaryEntryStart();
            writer.WriteString("multiple");
            writer.WriteVariantBool(false);
            if (cursorMode != 0)
            {
                WriteUInt32Entry(ref writer, "cursor_mode", cursorMode);
            }

            if (persist)
            {
                WriteUInt32Entry(ref writer, "persist_mode", 2);
                if (restoreToken is not null)
                {
                    WriteStringEntry(ref writer, "restore_token", restoreToken);
                }
            }

            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Inicia a sessão; o desktop pode pedir confirmação. Devolve o nó PipeWire e o novo restore token.</summary>
    internal async Task<(uint NodeId, string? RestoreToken)> StartScreenCastAsync(string session,
        CancellationToken cancellationToken)
    {
        var token = NextToken();
        var results = await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, ScreenCastInterface, "Start", "osa{sv}");
            writer.WriteObjectPath(session);
            writer.WriteString(string.Empty);
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);

        if (!results.TryGetValue("streams", out var streams) || streams.Count != 1)
        {
            throw new InvalidOperationException("O portal ScreenCast não devolveu exatamente um monitor.");
        }

        var nodeId = streams.GetItem(0).GetItem(0).GetUInt32();
        var restore = results.TryGetValue("restore_token", out var value) ? value.GetString() : null;
        return (nodeId, restore);
    }

    /// <summary>Abre o socket PipeWire restrito aos streams da sessão.</summary>
    internal async Task<SafeFileHandle> OpenPipeWireRemoteAsync(string session)
    {
        var message = CreateMessage((ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, ScreenCastInterface, "OpenPipeWireRemote", "oa{sv}");
            writer.WriteObjectPath(session);
            var dict = writer.WriteDictionaryStart();
            writer.WriteDictionaryEnd(dict);
        });
        return await _connection.CallMethodAsync(message,
            static (message, _) => message.GetBodyReader().ReadHandle<SafeFileHandle>()
                ?? throw new InvalidOperationException("O portal não devolveu o socket do PipeWire."))
            .ConfigureAwait(false);
    }

    /// <summary>Encerra uma sessão de portal; falhas são ignoradas porque a sessão também termina com a conexão.</summary>
    internal async Task CloseSessionAsync(string session)
    {
        try
        {
            var message = CreateMessage((ref MessageWriter writer) =>
                writer.WriteMethodCallHeader(Destination, session, "org.freedesktop.portal.Session", "Close"));
            await _connection.CallMethodAsync(message).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
        }
    }

    /// <summary>Cria uma sessão GlobalShortcuts e devolve o handle da sessão.</summary>
    internal async Task<string> CreateShortcutSessionAsync(CancellationToken cancellationToken)
    {
        var token = NextToken();
        var sessionToken = NextToken();
        var results = await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.portal.GlobalShortcuts", "CreateSession", "a{sv}");
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            WriteStringEntry(ref writer, "session_handle_token", sessionToken);
            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);
        var handle = results["session_handle"];
        return handle.Type == VariantValueType.ObjectPath ? handle.GetObjectPathAsString() : handle.GetString();
    }

    /// <summary>Pede ao desktop para vincular os atalhos; o desktop pode mostrar um diálogo de confirmação.</summary>
    internal async Task<IReadOnlyList<(string Id, string Trigger)>> BindShortcutsAsync(
        string sessionHandle, IReadOnlyList<(string Id, string Description, string PreferredTrigger)> shortcuts,
        CancellationToken cancellationToken)
    {
        var token = NextToken();
        var results = await RequestAsync(token, (ref MessageWriter writer) =>
        {
            writer.WriteMethodCallHeader(Destination, DesktopPath, "org.freedesktop.portal.GlobalShortcuts", "BindShortcuts", "oa(sa{sv})sa{sv}");
            writer.WriteObjectPath(sessionHandle);
            var array = writer.WriteArrayStart(DBusType.Struct);
            foreach (var (id, description, trigger) in shortcuts)
            {
                writer.WriteStructureStart();
                writer.WriteString(id);
                var props = writer.WriteDictionaryStart();
                WriteStringEntry(ref writer, "description", description);
                WriteStringEntry(ref writer, "preferred_trigger", trigger);
                writer.WriteDictionaryEnd(props);
            }

            writer.WriteArrayEnd(array);
            writer.WriteString(string.Empty);
            var dict = writer.WriteDictionaryStart();
            WriteStringEntry(ref writer, "handle_token", token);
            writer.WriteDictionaryEnd(dict);
        }, cancellationToken).ConfigureAwait(false);

        var bound = new List<(string, string)>();
        if (results.TryGetValue("shortcuts", out var list))
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list.GetItem(i);
                var id = item.GetItem(0).GetString();
                var props = item.GetItem(1);
                var trigger = string.Empty;
                for (var j = 0; j < props.Count; j++)
                {
                    var entry = props.GetDictionaryEntry(j);
                    if (entry.Key.GetString() == "trigger_description")
                    {
                        trigger = entry.Value.GetString();
                    }
                }

                bound.Add((id, trigger));
            }
        }

        return bound;
    }

    /// <summary>Observa o sinal Activated dos atalhos globais.</summary>
    internal ValueTask<IDisposable> WatchShortcutActivatedAsync(Action<string, string?> onActivated) =>
        _connection.WatchSignalAsync(Destination, DesktopPath, "org.freedesktop.portal.GlobalShortcuts", "Activated",
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                reader.ReadObjectPathAsString();
                var id = reader.ReadString();
                reader.ReadUInt64();
                var options = reader.ReadDictionaryOfStringToVariantValue();
                string? activationToken = options.TryGetValue("activation_token", out var t) ? t.GetString() : null;
                return (id, activationToken);
            },
            notification =>
            {
                if (notification.HasValue)
                {
                    onActivated(notification.Value.id, notification.Value.activationToken);
                }
            },
            ObserverFlags.None);

    /// <summary>Caminho do objeto Request que o portal cria para <paramref name="token"/> (especificação XDG).</summary>
    internal static string GetRequestPath(string uniqueName, string token) =>
        $"{DesktopPath}/request/{uniqueName.TrimStart(':').Replace('.', '_')}/{token}";

    private string NextToken() => $"pixelpointer{Environment.ProcessId}_{Interlocked.Increment(ref _tokenCounter)}";

    /// <summary>
    /// Padrão Request/Response dos portais: assina o sinal Response no caminho previsto antes de chamar o método,
    /// para não perder uma resposta imediata.
    /// </summary>
    private async Task<Dictionary<string, VariantValue>> RequestAsync(string token, WriteMessage writeCall,
        CancellationToken cancellationToken)
    {
        var requestPath = GetRequestPath(UniqueName, token);
        var completion = new TaskCompletionSource<(uint Code, Dictionary<string, VariantValue> Results)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var watch = await _connection.WatchSignalAsync(Destination, requestPath, "org.freedesktop.portal.Request", "Response",
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                return (reader.ReadUInt32(), reader.ReadDictionaryOfStringToVariantValue());
            },
            notification =>
            {
                if (notification.HasValue)
                {
                    completion.TrySetResult(notification.Value);
                }
                else if (notification.CompletionReason is { } ex)
                {
                    completion.TrySetException(ex);
                }
            },
            ObserverFlags.None).ConfigureAwait(false);

        await _connection.CallMethodAsync(CreateMessage(writeCall),
            static (message, _) => message.GetBodyReader().ReadObjectPathAsString()).ConfigureAwait(false);

        await using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var (code, results) = await completion.Task.ConfigureAwait(false);
        return code switch
        {
            0 => results,
            1 => throw new OperationCanceledException("O usuário cancelou a solicitação do portal."),
            _ => throw new InvalidOperationException($"O portal recusou a solicitação (código {code})."),
        };
    }

    /// <summary>MessageWriter é um ref struct mutável: precisa ser passado por referência, nunca por cópia.</summary>
    private delegate void WriteMessage(ref MessageWriter writer);

    private MessageBuffer CreateMessage(WriteMessage write)
    {
        using var writer = _connection.GetMessageWriter();
        write(ref Unsafe.AsRef(in writer));
        return writer.CreateMessage();
    }

    private static void WriteStringEntry(ref MessageWriter writer, string key, string value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantString(value);
    }

    private static void WriteUInt32Entry(ref MessageWriter writer, string key, uint value)
    {
        writer.WriteDictionaryEntryStart();
        writer.WriteString(key);
        writer.WriteVariantUInt32(value);
    }
}
