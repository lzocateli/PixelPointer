using System.Runtime.InteropServices;
using SkiaSharp;

namespace PixelPointer.App.MacOS;

internal sealed unsafe partial class ScreenCaptureKitFrameSource : IDisposable
{
    private const string LibraryName = "PixelPointerScreenCaptureKit";
    private readonly nint _handle;
    private SKBitmap? _bitmap;
    private int _disposed;

    private ScreenCaptureKitFrameSource(nint handle, int displayWidth, int displayHeight)
    {
        _handle = handle;
        DisplayWidth = displayWidth;
        DisplayHeight = displayHeight;
    }

    internal int DisplayWidth { get; }

    internal int DisplayHeight { get; }

    internal static ScreenCaptureKitFrameSource Start()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new NotSupportedException("ScreenCaptureKit está disponível somente no macOS.");
        }

        var handle = Native.Create(out var errorCode, out var displayWidth, out var displayHeight);
        return handle != 0
            ? new ScreenCaptureKitFrameSource(handle, displayWidth, displayHeight)
            : throw new InvalidOperationException(
                $"Não foi possível iniciar ScreenCaptureKit (código {errorCode}); confira a permissão de Gravação de Tela.");
    }

    internal SKBitmap? CaptureCrop(int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || x + width > DisplayWidth || y + height > DisplayHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "A região da lupa está fora da tela principal.");
        }

        if (_bitmap is null || _bitmap.Width != width || _bitmap.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        var status = Native.CopyFrame(_handle, (nint)_bitmap.GetPixels(), checked(_bitmap.RowBytes * height),
            _bitmap.RowBytes, x, y, width, height);
        return status switch
        {
            0 => _bitmap,
            1 => null,
            _ => throw new InvalidOperationException($"Não foi possível copiar o quadro do ScreenCaptureKit (código {status})."),
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Native.Destroy(_handle);
        _bitmap?.Dispose();
    }

    private static partial class Native
    {
        [LibraryImport(LibraryName, EntryPoint = "pixelpointer_sck_create")]
        internal static partial nint Create(out int errorCode, out int displayWidth, out int displayHeight);

        [LibraryImport(LibraryName, EntryPoint = "pixelpointer_sck_copy_frame")]
        internal static partial int CopyFrame(nint handle, nint destination, int capacity, int rowBytes,
            int cropX, int cropY, int cropWidth, int cropHeight);

        [LibraryImport(LibraryName, EntryPoint = "pixelpointer_sck_destroy")]
        internal static partial void Destroy(nint handle);
    }
}
