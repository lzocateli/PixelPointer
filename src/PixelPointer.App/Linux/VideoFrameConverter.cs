using System.Runtime.InteropServices;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>Converte um quadro de vídeo bruto do PipeWire em bitmap BGRA opaco.</summary>
internal static class VideoFrameConverter
{
    internal static SKBitmap ToBitmap(ReadOnlySpan<byte> data, SpaVideoInfo info, int stride)
    {
        var rowBytes = checked(info.Width * 4);
        if (stride < rowBytes || data.Length < checked(((long)stride * (info.Height - 1)) + rowBytes))
        {
            throw new InvalidOperationException("Quadro do PipeWire menor que o formato negociado.");
        }

        var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        try
        {
            var swap = info.Format is SpaVideoFormat.RGBx or SpaVideoFormat.RGBA;
            var target = bitmap.GetPixelSpan();
            var targetStride = bitmap.RowBytes;
            for (var y = 0; y < info.Height; y++)
            {
                var source = MemoryMarshal.Cast<byte, uint>(data.Slice(y * stride, rowBytes));
                var row = MemoryMarshal.Cast<byte, uint>(target.Slice(y * targetStride, rowBytes));
                for (var x = 0; x < source.Length; x++)
                {
                    var pixel = source[x];
                    if (swap)
                    {
                        pixel = (pixel & 0x0000FF00u) | ((pixel & 0x000000FFu) << 16) | ((pixel & 0x00FF0000u) >> 16);
                    }

                    // O byte "x" do BGRx/RGBx pode vir zerado; a tela é sempre opaca.
                    row[x] = pixel | 0xFF000000u;
                }
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
