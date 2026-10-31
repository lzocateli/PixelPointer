using PixelPointer.Core.Annotations;
using PixelPointer.Rendering;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class SceneRendererTests
{
    private const int Size = 100;
    private static readonly SKRect Full = SKRect.Create(0, 0, Size, Size);

    [Fact]
    public void Draw_TransparentWithoutBackgroundKeepsAlphaZero()
    {
        using var bitmap = Render(new AnnotationDocument(), background: null, Full, transparent: true);

        Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
    }

    [Fact]
    public void Draw_OpaqueWithoutBackgroundIsBlack()
    {
        using var bitmap = Render(new AnnotationDocument(), background: null, Full, transparent: false);

        Assert.Equal(SKColors.Black, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public void Draw_ZoomedViewMagnifiesTheBackground()
    {
        using var background = Quadrants();

        using var bitmap = Render(new AnnotationDocument(), background, SKRect.Create(50, 0, 50, 50), transparent: false);

        // A vista 2x do quadrante superior direito (verde) ocupa a saída inteira.
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(5, 5));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(94, 94));
    }

    [Theory]
    [InlineData(Board.White, 0xFFFFFFFFu)]
    [InlineData(Board.Black, 0xFF000000u)]
    public void Draw_BoardCoversTheBackground(Board board, uint expected)
    {
        using var background = Quadrants();
        var document = new AnnotationDocument();
        document.ToggleBoard(board);

        using var bitmap = Render(document, background, Full, transparent: false);

        Assert.Equal(new SKColor(expected), bitmap.GetPixel(25, 25));
        Assert.Equal(new SKColor(expected), bitmap.GetPixel(75, 75));
    }

    [Fact]
    public void Draw_StrokeFollowsViewScale()
    {
        var document = new AnnotationDocument(PenColor.Blue, 4f);
        document.BeginStroke(60, 10, 1f, viewScale: 1f);
        document.ContinueStroke(90, 10, 1f);

        using var bitmap = Render(document, background: null, SKRect.Create(50, 0, 50, 50), transparent: true);

        // x 60..90 na imagem vira 20..80 na saída 2x; y 10 vira 20; espessura 4 vira 8.
        Assert.Equal(new SKColor(AnnotationDocument.ToArgb(PenColor.Blue)), bitmap.GetPixel(50, 20));
        Assert.Equal(new SKColor(AnnotationDocument.ToArgb(PenColor.Blue)), bitmap.GetPixel(50, 23));
        Assert.Equal(0, bitmap.GetPixel(50, 30).Alpha);
        Assert.Equal(0, bitmap.GetPixel(10, 20).Alpha);
    }

    [Fact]
    public void Draw_SinglePointProducesADot()
    {
        var document = new AnnotationDocument(PenColor.Red, 10f);
        document.BeginStroke(50, 50, 1f, 1f);

        using var bitmap = Render(document, background: null, Full, transparent: true);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.Equal(0, bitmap.GetPixel(70, 50).Alpha);
    }

    [Fact]
    public void Draw_PressureNarrowsTheStroke()
    {
        var mouse = new AnnotationDocument(PenColor.Red, 20f);
        mouse.BeginStroke(10, 50, 1f, 1f);
        mouse.ContinueStroke(90, 50, 1f);
        var pen = new AnnotationDocument(PenColor.Red, 20f);
        pen.BeginStroke(10, 50, 0.2f, 1f);
        pen.ContinueStroke(90, 50, 0.2f);

        using var full = Render(mouse, background: null, Full, transparent: true);
        using var light = Render(pen, background: null, Full, transparent: true);

        Assert.True(OpaqueRows(light, 50) < OpaqueRows(full, 50) / 2, "Pressão baixa deveria afinar o traço");
    }

    [Fact]
    public void Draw_RejectsEmptyView()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));

        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Draw(surface.Canvas, new AnnotationDocument(),
            null, Full, SKRect.Empty, Size, transparent: true));
    }

    private static SKBitmap Render(AnnotationDocument document, SKImage? background, SKRect view, bool transparent)
    {
        var info = new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        SceneRenderer.Draw(surface.Canvas, document, background, Full, view, Size, transparent);
        var bitmap = new SKBitmap(info);
        Assert.True(surface.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap;
    }

    private static SKImage Quadrants()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        using var paint = new SKPaint();
        foreach (var (rect, color) in new[]
        {
            (SKRect.Create(0, 0, 50, 50), SKColors.Red),
            (SKRect.Create(50, 0, 50, 50), SKColors.Lime),
            (SKRect.Create(0, 50, 50, 50), SKColors.Blue),
            (SKRect.Create(50, 50, 50, 50), SKColors.Yellow),
        })
        {
            paint.Color = color;
            surface.Canvas.DrawRect(rect, paint);
        }

        return surface.Snapshot();
    }

    private static int OpaqueRows(SKBitmap bitmap, int x)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            count += bitmap.GetPixel(x, y).Alpha == 255 ? 1 : 0;
        }

        return count;
    }
}
