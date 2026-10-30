using System.Diagnostics;
using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.MacOS;

/// <summary>
/// Captura com a ferramenta do sistema <c>screencapture</c>, provisória até a captura com ScreenCaptureKit.
/// A implementação definitiva usa ScreenCaptureKit (F2/F5).
/// </summary>
internal sealed class ScreencaptureToolCapture : IScreenCapture
{
    public string Name => "macOS screencapture";

    public async Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pixelpointer-{Environment.ProcessId}.png");
        try
        {
            using var process = Process.Start(new ProcessStartInfo("/usr/sbin/screencapture", ["-x", "-m", path])
            {
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("Não foi possível executar screencapture.");
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(path))
            {
                throw new InvalidOperationException("screencapture falhou; verifique a permissão de Gravação de Tela.");
            }

            return SKBitmap.Decode(path) ?? throw new InvalidOperationException("PNG inválido.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
