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
    public void Draw_LineIsStraightBetweenEndpoints()
    {
        using var bitmap = RenderShape(StrokeShape.Line, 10, 50, 90, 50, ignored: (50, 30));

        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public void Draw_RectangleOutlinesBoundsInAnyDragDirection()
    {
        using var bitmap = RenderShape(StrokeShape.Rectangle, 80, 80, 20, 20, ignored: (50, 50));

        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 50));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(80, 50));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 20));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 80));
    }

    [Fact]
    public void Draw_EllipseTouchesBoundsMidpointsButNotCorners()
    {
        using var bitmap = RenderShape(StrokeShape.Ellipse, 20, 20, 80, 80, ignored: (22, 22));

        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 50));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 80));
        Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
    }

    [Fact]
    public void Draw_ArrowAddsHeadAtTheEnd()
    {
        using var line = RenderShape(StrokeShape.Line, 10, 50, 90, 50, ignored: (80, 44));
        using var arrow = RenderShape(StrokeShape.Arrow, 10, 50, 90, 50, ignored: (20, 44));

        // A ponta cobre pixels acima da haste perto do fim, mas não perto do início.
        Assert.Equal(SKColors.Red, arrow.GetPixel(80, 44));
        Assert.Equal(SKColors.Red, arrow.GetPixel(50, 50));
    }

    [Fact]
    public void Draw_HighlighterKeepsUniformAlphaWhereTheStrokeOverlapsItself()
    {
        var document = new AnnotationDocument(PenColor.Red, 10f);
        document.SelectColor(PenColor.Red, highlighter: true);
        document.BeginStroke(10, 50, 0.5f, 1f);
        document.ContinueStroke(50, 50, 0.6f);
        document.ContinueStroke(90, 50, 0.7f);
        document.ContinueStroke(50, 52, 0.8f);
        document.EndStroke();

        using var bitmap = Render(document, background: null, Full, transparent: true);

        // Segmentos com pressão se sobrepõem em (50, 50); o alfa não deve acumular.
        Assert.Equal(AnnotationDocument.HighlighterAlpha, bitmap.GetPixel(30, 50).Alpha);
        Assert.Equal(AnnotationDocument.HighlighterAlpha, bitmap.GetPixel(50, 50).Alpha);
    }

    [Fact]
    public void Draw_HighlighterLetsTheBackgroundShowThrough()
    {
        var document = new AnnotationDocument(PenColor.Yellow, 20f);
        document.ToggleBoard(Board.Black);
        document.SelectColor(PenColor.Yellow, highlighter: true);
        document.BeginStroke(10, 50, 1f, 1f);
        document.ContinueStroke(90, 50, 1f);

        using var bitmap = Render(document, background: null, Full, transparent: false);
        var pixel = bitmap.GetPixel(50, 50);

        Assert.InRange(pixel.Red, 80, 110);
        Assert.InRange(pixel.Green, 80, 110);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void Draw_BlurRectangleSoftensOnlyTheContentInside()
    {
        using var background = Quadrants();
        var document = BlurRectangle(strong: false);

        using var bitmap = Render(document, background, Full, transparent: false);

        // Dentro do retângulo, a borda vermelho/verde em x = 50 se mistura; fora dele, continua nítida.
        var inside = bitmap.GetPixel(47, 40);
        Assert.True(inside.Green > 40 && inside.Red < 230, $"Esperava mistura, obtido {inside}");
        Assert.Equal(SKColors.Red, bitmap.GetPixel(47, 10));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(53, 10));
    }

    [Fact]
    public void Draw_StrongBlurSpreadsFurther()
    {
        using var background = Quadrants();

        using var normal = Render(BlurRectangle(strong: false), background, Full, transparent: false);
        using var strong = Render(BlurRectangle(strong: true), background, Full, transparent: false);

        Assert.True(strong.GetPixel(40, 40).Green > normal.GetPixel(40, 40).Green);
    }

    [Fact]
    public void Draw_BlurAlsoSoftensEarlierAnnotations()
    {
        var document = new AnnotationDocument(PenColor.Red, 2f);
        document.ToggleBoard(Board.White);
        document.BeginStroke(50, 10, 1f, 1f, StrokeShape.Line);
        document.ContinueStroke(50, 90, 1f);
        document.EndStroke();
        document.SelectBlur(strong: false);
        document.BeginStroke(30, 30, 1f, 1f, StrokeShape.Rectangle);
        document.ContinueStroke(70, 70, 1f);
        document.EndStroke();

        using var bitmap = Render(document, background: null, Full, transparent: false);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 15));
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(53, 50));
    }

    [Fact]
    public void Draw_TextPaintsGlyphsBelowAndRightOfTheAnchor()
    {
        Assert.SkipWhen(SKTypeface.Default.GlyphCount == 0, "Sem fonte disponível no ambiente.");
        var document = new AnnotationDocument(PenColor.Red, 6f);
        document.BeginText(20, 20, 1f);
        document.AppendText("HH\nHH");
        document.EndText();

        using var bitmap = Render(document, background: null, Full, transparent: true);
        var painted = CountPainted(bitmap, SKRect.Create(20, 20, 80, 80));

        Assert.True(painted > 50, $"Esperava glifos, obtido {painted} pixels");
        Assert.Equal(0, CountPainted(bitmap, SKRect.Create(0, 0, 18, 100)));
        var caret = SceneRenderer.TextCaret(document.Strokes[0]);
        Assert.True(caret.Left > 20 && caret.Top > 20);
    }

    [Fact]
    public void Draw_RejectsEmptyView()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));

        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Draw(surface.Canvas, new AnnotationDocument(),
            null, Full, SKRect.Empty, Size, transparent: true));
    }

    private static AnnotationDocument BlurRectangle(bool strong)
    {
        var document = new AnnotationDocument(PenColor.Red, 2f);
        document.SelectBlur(strong);
        document.BeginStroke(30, 30, 1f, 1f, StrokeShape.Rectangle);
        document.ContinueStroke(70, 70, 1f);
        document.EndStroke();
        return document;
    }

    private static SKBitmap RenderShape(StrokeShape shape, float x1, float y1, float x2, float y2, (int X, int Y) ignored)
    {
        var document = new AnnotationDocument(PenColor.Red, 4f);
        document.BeginStroke(x1, y1, 1f, 1f, shape);
        document.ContinueStroke(x2, y2, 1f);
        document.EndStroke();
        var bitmap = Render(document, background: null, Full, transparent: true);
        Assert.Equal(0, bitmap.GetPixel(ignored.X, ignored.Y).Alpha);
        return bitmap;
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

    private static int CountPainted(SKBitmap bitmap, SKRect area)
    {
        var count = 0;
        for (var y = (int)area.Top; y < (int)area.Bottom && y < bitmap.Height; y++)
        {
            for (var x = (int)area.Left; x < (int)area.Right && x < bitmap.Width; x++)
            {
                count += bitmap.GetPixel(x, y).Alpha > 0 ? 1 : 0;
            }
        }

        return count;
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
