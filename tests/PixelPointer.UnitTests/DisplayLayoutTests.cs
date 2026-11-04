using PixelPointer.App.Capture;
using PixelPointer.App.Linux;
using PixelPointer.App.Overlay;
using PixelPointer.Core.Annotations;
using PixelPointer.Core.Snip;
using PixelPointer.Rendering;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class DisplayLayoutTests
{
    private static readonly DisplayBounds Primary = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(-50, false)]
    [InlineData(50, true)]
    public async Task SnipPreviewAndExport_UseOnlyTheSelectedMonitor(int cursorX, bool blue)
    {
        var left = new DisplayBounds(-100, 0, 100, 100);
        var right = new DisplayBounds(0, 0, 100, 100);
        using var capture = new MonitorScreenCapture(new TwoMonitorCapture(), CursorMonitor.FromPoint(cursorX, 50, [right, left]));
        using var screenshot = await capture.CaptureAsync(CancellationToken.None);
        using var background = SKImage.FromBitmap(screenshot);
        using var preview = new SKBitmap(new SKImageInfo(100, 100));
        using (var canvas = new SKCanvas(preview))
        {
            var wholeMonitor = SKRect.Create(0, 0, screenshot.Width, screenshot.Height);
            SceneRenderer.Draw(canvas, new AnnotationDocument(), background, wholeMonitor, wholeMonitor, 100, false);
        }

        var expected = blue ? SKColors.Blue : SKColors.Lime;
        Assert.Equal(expected, preview.GetPixel(50, 50));
        var region = SnipRegion.FromCorners(10, 20, 40, 60, screenshot.Width, screenshot.Height)!.Value;
        using var subset = new SKBitmap();
        Assert.True(screenshot.ExtractSubset(subset, SKRectI.Create(region.X, region.Y, region.Width, region.Height)));
        using var image = SKImage.FromBitmap(subset);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var exported = SKBitmap.Decode(png);
        Assert.Equal((30, 40), (exported.Width, exported.Height));
        Assert.Equal(expected, exported.GetPixel(0, 0));
        Assert.Equal(expected, exported.GetPixel(29, 39));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1920)]
    public void X11Recording_CapturesTheRequestedRegionWithoutScaling(int monitorX)
    {
        var monitor = new DisplayBounds(monitorX, 0, 1920, 1080);

        Assert.Equal(monitor, X11ScreenCapture.CaptureBounds(monitor, 3840, 1080));
        Assert.Equal(new DisplayBounds(0, 0, 3840, 1080), X11ScreenCapture.CaptureBounds(null, 3840, 1080));
    }

    [Fact]
    public void X11Recording_RejectsAMonitorOutsideTheCurrentDesktop() =>
        Assert.Throws<InvalidOperationException>(() =>
            X11ScreenCapture.CaptureBounds(new DisplayBounds(1920, 0, 1920, 1080), 1920, 1080));

    [Theory]
    [InlineData(100, 0)]
    [InlineData(1919, 0)]
    [InlineData(1920, 1920)]
    [InlineData(2962, 1920)]
    public void CursorMonitor_SelectsByCoordinatesNotPrimaryOrEnumerationOrder(int cursorX, int expectedX)
    {
        var right = new DisplayBounds(1920, 0, 1920, 1080);
        var monitor = CursorMonitor.FromPoint(cursorX, 363, [right, Primary]);

        Assert.Equal(expectedX, monitor.Bounds.X);
    }

    [Fact]
    public void RecordingCrop_SelectsOneMonitorWithNegativeCoordinatesAndScaling()
    {
        var left = new DisplayBounds(-1280, -200, 1280, 1024);

        Assert.Equal(SKRect.Create(2560, 400, 3840, 2160),
            DisplayLayout.RecordingCrop(Primary, [Primary, left], 6400, 2560));
        Assert.Equal(SKRect.Create(0, 0, 2560, 2048),
            DisplayLayout.RecordingCrop(left, [Primary, left], 6400, 2560));
    }

    [Fact]
    public void RecordingCrop_RejectsUnknownTarget() =>
        Assert.Throws<InvalidOperationException>(() => DisplayLayout.RecordingCrop(default, [], 200, 100));

    [Theory]
    [InlineData(-50, false)]
    [InlineData(50, true)]
    public async Task RecordingSource_KeepsOnlySelectedMonitorPixelsAcrossFramesAndDisposesSource(int cursorX, bool blue)
    {
        var left = new DisplayBounds(-100, 0, 100, 100);
        var right = new DisplayBounds(0, 0, 100, 100);
        var source = new TwoMonitorCapture();
        using (var capture = new MonitorScreenCapture(source, CursorMonitor.FromPoint(cursorX, 50, [right, left])))
        {
            for (var frameIndex = 0; frameIndex < 2; frameIndex++)
            {
                using var frame = await capture.CaptureAsync(CancellationToken.None);
                Assert.Equal((100, 100), (frame.Width, frame.Height));
                Assert.Equal(blue ? SKColors.Blue : SKColors.Lime, frame.GetPixel(0, 50));
                Assert.Equal(blue ? SKColors.Blue : SKColors.Lime, frame.GetPixel(99, 50));
            }
        }

        Assert.True(source.Disposed);
    }

    [Fact]
    public void Crop_UsesTheWholeCaptureWhenItMatchesTheWindow() =>
        Assert.Equal(new SKRect(0, 0, 1920, 1080), DisplayLayout.Crop(Primary, [Primary], 1920, 1080, 1920, 1080));

    [Fact]
    public void Crop_OffsetsASecondaryMonitorToTheRight()
    {
        var right = new DisplayBounds(1920, 0, 1920, 1080);

        Assert.Equal(SKRect.Create(1920, 0, 1920, 1080), DisplayLayout.Crop(right, [Primary, right], 3840, 1080, 1920, 1080));
    }

    [Fact]
    public void Crop_MeasuresFromTheDesktopCornerWhenAMonitorIsLeftOfThePrimary()
    {
        // No Windows, monitores \u00e0 esquerda do principal t\u00eam X negativo e a captura come\u00e7a neles.
        var left = new DisplayBounds(-1280, -200, 1280, 1024);

        Assert.Equal(SKRect.Create(1280, 200, 1920, 1080), DisplayLayout.Crop(Primary, [Primary, left], 3200, 1280, 1920, 1080));
        Assert.Equal(SKRect.Create(0, 0, 1280, 1024), DisplayLayout.Crop(left, [Primary, left], 3200, 1280, 1280, 1024));
    }

    [Fact]
    public void Crop_ScalesLogicalBoundsToWindowPixels()
    {
        var right = new DisplayBounds(1920, 0, 1920, 1080);

        Assert.Equal(SKRect.Create(3840, 0, 3840, 2160), DisplayLayout.Crop(right, [Primary, right], 7680, 2160, 3840, 2160));
    }

    [Fact]
    public void Crop_ExtractsOnlyTheSelectedMonitor()
    {
        var desktopInfo = new SKImageInfo(200, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var desktop = new SKBitmap(desktopInfo);
        using (var canvas = new SKCanvas(desktop))
        using (var paint = new SKPaint())
        {
            paint.Color = SKColors.Lime;
            canvas.DrawRect(SKRect.Create(0, 0, 100, 100), paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(SKRect.Create(100, 0, 100, 100), paint);
        }

        using var monitor = DisplayLayout.Crop(desktop, SKRect.Create(100, 0, 100, 100));

        Assert.Equal((100, 100), (monitor.Width, monitor.Height));
        Assert.Equal(SKColors.Blue, monitor.GetPixel(0, 50));
        Assert.Equal(SKColors.Blue, monitor.GetPixel(99, 50));
    }

    [Fact]
    public void Crop_RejectsBoundsOutsideTheDesktop()
    {
        using var desktop = new SKBitmap(new SKImageInfo(200, 100));

        Assert.Throws<InvalidOperationException>(() => DisplayLayout.Crop(desktop, SKRect.Create(100, 0, 200, 100)));
    }

    [Fact]
    public void Crop_RejectsUnknownTargetOnAFullDesktopCapture() =>
        Assert.Throws<InvalidOperationException>(() =>
            DisplayLayout.Crop(new DisplayBounds(0, 0, 0, 0), [], 3840, 1080, 1920, 1080));

    private sealed class TwoMonitorCapture : IScreenCapture, IDisposable
    {
        public string Name => "two-monitor-test";
        internal bool Disposed { get; private set; }

        public Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
        {
            var bitmap = new SKBitmap(new SKImageInfo(200, 100, SKColorType.Bgra8888, SKAlphaType.Opaque));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Lime);
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(SKRect.Create(100, 0, 100, 100), paint);
            return Task.FromResult(bitmap);
        }

        public void Dispose() => Disposed = true;
    }
}
