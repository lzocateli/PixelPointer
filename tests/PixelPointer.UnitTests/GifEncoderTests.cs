using PixelPointer.App.Recording;
using PixelPointer.Core.Recording;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class GifEncoderTests
{
    [Fact]
    public void Encoder_ProducesAnAnimatedGifThatDecodesToThePaletteColors()
    {
        using var output = new MemoryStream();
        var encoder = new GifEncoder(output, 40, 30);
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue };
        foreach (var color in colors)
        {
            using var frame = new SKBitmap(new SKImageInfo(40, 30, SKColorType.Bgra8888, SKAlphaType.Opaque));
            frame.Erase(color);
            encoder.AddFrame(frame.GetPixelSpan(), frame.RowBytes, 12);
        }

        encoder.Finish();
        output.Position = 0;

        using var codec = SKCodec.Create(output);
        Assert.Equal(SKEncodedImageFormat.Gif, codec.EncodedFormat);
        Assert.Equal(3, codec.FrameCount);
        Assert.Equal(120, codec.FrameInfo[1].Duration);
        for (var i = 0; i < colors.Length; i++)
        {
            Assert.Equal(colors[i], DecodeFrame(codec, i).GetPixel(20, 15));
        }
    }

    [Fact]
    public void Encoder_RoundTripsNoiseThatForcesCodeGrowthAndTableResets()
    {
        const int width = 257;
        const int height = 193;
        var random = new Random(42);
        using var frame = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var pixels = new byte[frame.RowBytes * height];
        random.NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, frame.GetPixels(), pixels.Length);
        using var output = new MemoryStream();
        var encoder = new GifEncoder(output, width, height);
        encoder.AddFrame(frame.GetPixelSpan(), frame.RowBytes, 10);
        encoder.Finish();
        output.Position = 0;

        using var codec = SKCodec.Create(output);
        using var decoded = DecodeFrame(codec, 0);
        var expected = BuildExpectedPalette();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var source = frame.GetPixel(x, y);
                var index = GifEncoder.Quantize(source.Blue, source.Green, source.Red);
                Assert.Equal(expected[index], decoded.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void Encoder_RejectsInvalidFramesAndUseAfterFinish()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new GifEncoder(output, 0, 10));
        var encoder = new GifEncoder(output, 4, 4);

        Assert.Throws<ArgumentException>(() => encoder.AddFrame(new byte[10], 16, 10));
        encoder.Finish();
        Assert.Throws<ObjectDisposedException>(() => encoder.AddFrame(new byte[64], 16, 10));
    }

    [Theory]
    [InlineData(1920, 1080, 1280, 720)]
    [InlineData(3840, 1600, 1280, 533)]
    [InlineData(800, 600, 800, 600)]
    public void ScaledSize_LimitsWidthKeepingAspect(int width, int height, int expectedWidth, int expectedHeight) =>
        Assert.Equal((expectedWidth, expectedHeight), ScreenRecorder.ScaledSize(width, height, ScreenRecorder.MaxWidth));

    private static SKBitmap DecodeFrame(SKCodec codec, int index)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(index));
        Assert.Equal(SKCodecResult.Success, result);
        return bitmap;
    }

    private static SKColor[] BuildExpectedPalette()
    {
        var palette = new SKColor[256];
        for (var r = 0; r < 6; r++)
        {
            for (var g = 0; g < 7; g++)
            {
                for (var b = 0; b < 6; b++)
                {
                    palette[(r * 42) + (g * 6) + b] = new SKColor((byte)(r * 255 / 5), (byte)(g * 255 / 6), (byte)(b * 255 / 5));
                }
            }
        }

        return palette;
    }
}
