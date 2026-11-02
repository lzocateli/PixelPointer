using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>
/// Sessão ScreenCast mantida aberta para gravação: cada captura pede o próximo quadro do PipeWire.
/// Em compositores que só enviam quadros quando a tela muda, repete o último quadro.
/// </summary>
internal sealed class ScreenCastStream : IScreenCapture, IDisposable
{
    private const uint CursorEmbedded = 2;
    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromMilliseconds(500);

    private readonly PortalClient _portal;
    private readonly string _session;
    private readonly PipeWireFrameReader _reader;
    private SKBitmap? _last;
    private int _disposed;

    private ScreenCastStream(PortalClient portal, string session, PipeWireFrameReader reader)
    {
        _portal = portal;
        _session = session;
        _reader = reader;
    }

    public string Name => "xdg-desktop-portal ScreenCast + PipeWire (contínuo)";

    /// <summary>Abre a sessão; o desktop pode pedir a escolha do monitor, reaproveitando o restore token salvo.</summary>
    internal static async Task<ScreenCastStream> OpenAsync(PortalClient portal, RestoreTokenStore? tokens,
        CancellationToken cancellationToken)
    {
        var (capabilities, _) = await portal.GetScreenCastCapabilitiesAsync().ConfigureAwait(false);
        if (capabilities.Version == 0 || !capabilities.SupportsMonitor)
        {
            throw new NotSupportedException("o portal ScreenCast não oferece captura de monitor");
        }

        var persist = capabilities.Version >= 4 && tokens is not null;
        var session = await portal.CreateScreenCastSessionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cursor = (capabilities.AvailableCursorModes & CursorEmbedded) != 0 ? CursorEmbedded : 0;
            await portal.SelectScreenCastSourcesAsync(session, cursor, persist, persist ? tokens!.Read() : null,
                cancellationToken).ConfigureAwait(false);
            var (nodeId, newToken) = await portal.StartScreenCastAsync(session, cancellationToken).ConfigureAwait(false);
            if (persist && RestoreTokenStore.IsValid(newToken))
            {
                try
                {
                    tokens!.Write(newToken!);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"aviso: não foi possível salvar a autorização do ScreenCast ({ex.GetType().Name}).");
                }
            }

            using var remote = await portal.OpenPipeWireRemoteAsync(session).ConfigureAwait(false);
            var reader = PipeWireFrameReader.Start(remote, nodeId);
            reader.KeepLatestFrames();
            return new ScreenCastStream(portal, session, reader);
        }
        catch
        {
            await portal.CloseSessionAsync(session).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        try
        {
            var frame = await _reader.NextFrameAsync()
                .WaitAsync(_last is null ? FirstFrameTimeout : FrameTimeout, cancellationToken).ConfigureAwait(false);
            _last?.Dispose();
            _last = frame;
        }
        catch (TimeoutException) when (_last is not null)
        {
            // Sem mudança na tela o compositor não envia quadro; o último continua válido.
        }

        return _last?.Copy() ?? throw new TimeoutException("O ScreenCast não entregou o primeiro quadro.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _reader.Dispose();
        _portal.CloseSessionAsync(_session).GetAwaiter().GetResult();
        _last?.Dispose();
    }
}
