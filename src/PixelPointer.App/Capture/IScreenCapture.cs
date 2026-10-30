using SkiaSharp;

namespace PixelPointer.App.Capture;

/// <summary>Captura da área de trabalho inteira em pixels físicos.</summary>
internal interface IScreenCapture
{
    string Name { get; }

    Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken);
}
