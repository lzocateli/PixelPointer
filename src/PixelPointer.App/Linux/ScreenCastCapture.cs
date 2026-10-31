using System.Diagnostics;
using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>
/// Captura sob demanda pelo portal ScreenCast e PipeWire: abre a sessão, lê o primeiro quadro e encerra tudo,
/// de modo que o indicador de compartilhamento de tela só aparece durante a captura. Quando o ScreenCast não
/// está disponível, usa o portal Screenshot; a recusa do usuário não é contornada.
/// </summary>
internal sealed class ScreenCastCapture(PortalClient portal, RestoreTokenStore? tokens) : IScreenCapture
{
    private const uint SourceMonitor = 1;
    private const uint CursorHidden = 1;
    // Compositores wlroots só entregam quadro após dano na tela; sem ele, volta ao Screenshot sem prender o usuário.
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly PortalScreenCapture _fallback = new(portal);
    private string _name = "xdg-desktop-portal ScreenCast + PipeWire";

    public string Name => _name;

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await CaptureScreenCastAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or NotSupportedException
            or TimeoutException or InvalidOperationException or Tmds.DBus.Protocol.DBusErrorReplyException)
        {
            Console.Error.WriteLine($"informação: ScreenCast indisponível ({ex.Message}); usando o portal Screenshot.");
            _name = _fallback.Name;
            try
            {
                return await _fallback.CaptureAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Tmds.DBus.Protocol.DBusErrorReplyException fallbackError)
            {
                throw new InvalidOperationException($"Nenhum portal de captura disponível ({fallbackError.ErrorName}).", fallbackError);
            }
        }
    }

    private async Task<SKBitmap> CaptureScreenCastAsync(CancellationToken cancellationToken)
    {
        var version = await portal.GetVersionAsync("org.freedesktop.portal.ScreenCast").ConfigureAwait(false);
        if (version == 0)
        {
            throw new NotSupportedException("portal ScreenCast ausente");
        }

        var sources = await portal.GetUInt32PropertyAsync("org.freedesktop.portal.ScreenCast", "AvailableSourceTypes")
            .ConfigureAwait(false);
        if ((sources & SourceMonitor) == 0)
        {
            throw new NotSupportedException("o portal não oferece captura de monitor");
        }

        var cursors = await portal.GetUInt32PropertyAsync("org.freedesktop.portal.ScreenCast", "AvailableCursorModes")
            .ConfigureAwait(false);
        var persist = version >= 4 && tokens is not null;
        var restoreToken = persist ? tokens!.Read() : null;

        var timer = Stopwatch.StartNew();
        var session = await portal.CreateScreenCastSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await portal.SelectScreenCastSourcesAsync(session, (cursors & CursorHidden) != 0 ? CursorHidden : 0, persist,
                restoreToken, cancellationToken).ConfigureAwait(false);
            var (nodeId, newToken) = await portal.StartScreenCastAsync(session, cancellationToken).ConfigureAwait(false);
            SaveToken(newToken);
            var portalMs = timer.Elapsed.TotalMilliseconds;
            using var remote = await portal.OpenPipeWireRemoteAsync(session).ConfigureAwait(false);
            var bitmap = await PipeWireFrameReader.ReadFrameAsync(remote, nodeId, FrameTimeout, cancellationToken)
                .ConfigureAwait(false);
            Console.Error.WriteLine(
                $"ScreenCast: portal {portalMs:0} ms, primeiro quadro {timer.Elapsed.TotalMilliseconds - portalMs:0} ms");
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
