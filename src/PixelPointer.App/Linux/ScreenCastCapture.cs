using System.Diagnostics;
using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>
/// Captura sob demanda pelo portal ScreenCast e PipeWire: abre a sessão, lê o primeiro quadro e encerra tudo,
/// de modo que o indicador de compartilhamento de tela só aparece durante a captura. Quando o ScreenCast não
/// está disponível, usa o portal Screenshot; a recusa do usuário não é contornada.
/// </summary>
internal sealed class ScreenCastCapture : IScreenCapture
{
    private const uint CursorHidden = 1;
    // Compositores wlroots só entregam quadro após dano na tela; sem ele, volta ao Screenshot sem prender o usuário.
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly IScreenCapture _preferred;
    private readonly IScreenCapture _fallback;
    private string _name;

    internal ScreenCastCapture(PortalClient portal, RestoreTokenStore? tokens)
        : this(new ScreenCastSource(portal, tokens), new PortalScreenCapture(portal))
    {
    }

    internal ScreenCastCapture(IScreenCapture preferred, IScreenCapture fallback)
    {
        ArgumentNullException.ThrowIfNull(preferred);
        ArgumentNullException.ThrowIfNull(fallback);
        _preferred = preferred;
        _fallback = fallback;
        _name = preferred.Name;
    }

    public string Name => _name;

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bitmap = await _preferred.CaptureAsync(cancellationToken).ConfigureAwait(false);
            _name = _preferred.Name;
            return bitmap;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or NotSupportedException
            or TimeoutException or InvalidOperationException or Tmds.DBus.Protocol.DBusErrorReplyException)
        {
            Console.Error.WriteLine($"informação: ScreenCast indisponível ({ex.Message}); usando o portal Screenshot.");
            try
            {
                var bitmap = await _fallback.CaptureAsync(cancellationToken).ConfigureAwait(false);
                _name = _fallback.Name;
                return bitmap;
            }
            catch (Tmds.DBus.Protocol.DBusErrorReplyException fallbackError)
            {
                throw new InvalidOperationException($"Nenhum portal de captura disponível ({fallbackError.ErrorName}).", fallbackError);
            }
        }
    }

    internal static string FormatTimings(double sessionCreatedMs, double sourcesSelectedMs, double sessionStartedMs,
        double portalMs, double remoteOpenedMs, double frameReadyMs, bool restoreRequested) =>
        FormattableString.Invariant($"ScreenCast: portal {portalMs:0} ms, primeiro quadro {frameReadyMs - portalMs:0} ms (sessao {sessionCreatedMs:0} ms; fontes {sourcesSelectedMs - sessionCreatedMs:0} ms; inicio {sessionStartedMs - sourcesSelectedMs:0} ms; persistencia {portalMs - sessionStartedMs:0} ms; remoto {remoteOpenedMs - portalMs:0} ms; leitura {frameReadyMs - remoteOpenedMs:0} ms; restore solicitado {(restoreRequested ? "sim" : "nao")})");

    private sealed class ScreenCastSource(PortalClient portal, RestoreTokenStore? tokens) : IScreenCapture
    {
        public string Name => "xdg-desktop-portal ScreenCast + PipeWire";

        public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
        {
            var capabilitiesStarted = Stopwatch.GetTimestamp();
            var (capabilities, grouped) = await portal.GetScreenCastCapabilitiesAsync().ConfigureAwait(false);
            var capabilitiesMs = Stopwatch.GetElapsedTime(capabilitiesStarted).TotalMilliseconds;
            if (capabilities.Version == 0)
            {
                throw new NotSupportedException("portal ScreenCast ausente");
            }

            if (!capabilities.SupportsMonitor)
            {
                throw new NotSupportedException("o portal não oferece captura de monitor");
            }

            var cursors = capabilities.AvailableCursorModes;
            var persist = capabilities.Version >= 4 && tokens is not null;
            var restoreToken = persist ? tokens!.Read() : null;

            var timer = Stopwatch.StartNew();
            var session = await portal.CreateScreenCastSessionAsync(cancellationToken).ConfigureAwait(false);
            var sessionCreatedMs = timer.Elapsed.TotalMilliseconds;
            try
            {
                await portal.SelectScreenCastSourcesAsync(session, (cursors & CursorHidden) != 0 ? CursorHidden : 0, persist,
                    restoreToken, cancellationToken).ConfigureAwait(false);
                var sourcesSelectedMs = timer.Elapsed.TotalMilliseconds;
                var (nodeId, newToken) = await portal.StartScreenCastAsync(session, cancellationToken).ConfigureAwait(false);
                var sessionStartedMs = timer.Elapsed.TotalMilliseconds;
                SaveToken(newToken);
                var portalMs = timer.Elapsed.TotalMilliseconds;
                using var remote = await portal.OpenPipeWireRemoteAsync(session).ConfigureAwait(false);
                var remoteOpenedMs = timer.Elapsed.TotalMilliseconds;
                var bitmap = await PipeWireFrameReader.ReadFrameAsync(remote, nodeId, FrameTimeout, cancellationToken)
                    .ConfigureAwait(false);
                var frameReadyMs = timer.Elapsed.TotalMilliseconds;
                Console.Error.WriteLine(FormatTimings(sessionCreatedMs, sourcesSelectedMs, sessionStartedMs, portalMs,
                    remoteOpenedMs, frameReadyMs, restoreToken is not null));
                Console.Error.WriteLine(FormattableString.Invariant(
                    $"ScreenCast capacidades: {capabilitiesMs:0} ms; consulta {(grouped ? "GetAll" : "Get")}"));
                return bitmap;
            }
            finally
            {
                await portal.CloseSessionAsync(session).ConfigureAwait(false);
            }
        }

        private void SaveToken(string? token)
        {
            if (tokens is null || !RestoreTokenStore.IsValid(token))
            {
                return;
            }

            try
            {
                tokens.Write(token!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Sem o token o diálogo apenas reaparece na próxima captura; o conteúdo do token nunca é exibido.
                Console.Error.WriteLine($"aviso: não foi possível salvar a autorização do ScreenCast ({ex.GetType().Name}).");
            }
        }
    }
}
