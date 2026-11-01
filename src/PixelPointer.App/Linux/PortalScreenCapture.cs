using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>Captura pelo portal org.freedesktop.portal.Screenshot (Wayland).</summary>
internal sealed class PortalScreenCapture(PortalClient portal) : IScreenCapture
{
    public string Name => "xdg-desktop-portal Screenshot";

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        Uri uri;
        try
        {
            uri = new Uri(await portal.ScreenshotAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Tmds.DBus.Protocol.DBusErrorReplyException error)
        {
            throw new InvalidOperationException($"Nenhum portal de captura disponível ({error.ErrorName}).", error);
        }

        var path = uri.LocalPath;
        try
        {
            return SKBitmap.Decode(path) ?? throw new InvalidOperationException($"PNG inválido devolvido pelo portal: {path}");
        }
        finally
        {
            // O arquivo foi criado pelo portal para esta solicitação; não deve se acumular na pasta do usuário.
            File.Delete(path);
        }
    }
}
