using PixelPointer.Core.Annotations;
using SkiaSharp;

namespace PixelPointer.Rendering;

/// <summary>Desenha fundo, quadro e anotações de uma sobreposição, independente de janela e de backend (CPU ou GPU).</summary>
public static class SceneRenderer
{
    public const float BlurSigma = 6f;
    public const float StrongBlurSigma = 16f;

    /// <summary>
    /// Desenha a cena; <paramref name="view"/> é a região da imagem exibida e <paramref name="outputWidth"/>
    /// a largura de saída em pixels. Com <paramref name="transparent"/>, o fundo sem imagem fica com alfa zero.
    /// </summary>
    public static void Draw(SKCanvas canvas, AnnotationDocument annotations, SKImage? background, SKRect crop,
        SKRect view, int outputWidth, bool transparent)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(annotations);
        if (!(view.Width > 0) || outputWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(view), "A vista e a largura de saída devem ser positivas.");
        }

        canvas.Clear(transparent ? SKColors.Transparent : SKColors.Black);
        canvas.Save();
        canvas.Scale(outputWidth / view.Width);
        canvas.Translate(-view.Left, -view.Top);
        if (annotations.Board != Board.None)
        {
            using var fill = new SKPaint { Color = annotations.Board == Board.White ? SKColors.White : SKColors.Black };
            canvas.DrawRect(crop, fill);
        }
        else if (background is not null)
        {
            canvas.DrawImage(background, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        foreach (var stroke in annotations.Strokes)
        {
            DrawStroke(canvas, stroke);
        }

        canvas.Restore();
    }

    public static void DrawStroke(SKCanvas canvas, Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(stroke);
        if (stroke.Shape == StrokeShape.Text)
        {
            DrawText(canvas, stroke);
            return;
        }

        if (stroke.Effect != StrokeEffect.None)
        {
            DrawBlurStroke(canvas, stroke);
            return;
        }

        var color = new SKColor(stroke.Color);
        if (color.Alpha == 255)
        {
            DrawOpaqueStroke(canvas, stroke, color);
            return;
        }

        // Camada opaca composta uma vez: sobreposições do próprio traço não escurecem o marca-texto.
        using var layer = new SKPaint { Color = SKColors.White.WithAlpha(color.Alpha) };
        canvas.SaveLayer(Bounds(stroke, 0), layer);
        DrawOpaqueStroke(canvas, stroke, color.WithAlpha(255));
        canvas.Restore();
    }

    /// <summary>Retângulo do cursor de digitação ao fim do texto, em coordenadas da imagem.</summary>
    public static SKRect TextCaret(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        using var font = new SKFont(SKTypeface.Default, stroke.Width);
        var lines = stroke.Text.Split('\n');
        var origin = stroke.Points[0];
        var top = origin.Y + (font.Spacing * (lines.Length - 1));
        var x = origin.X + font.MeasureText(lines[^1]);
        return new SKRect(x, top, x + MathF.Max(1f, stroke.Width / 12), top + font.Spacing);
    }

    /// <summary>Texto com o canto superior esquerdo no ponto do traço; cada \n abre uma linha.</summary>
    private static void DrawText(SKCanvas canvas, Stroke stroke)
    {
        var text = stroke.Text;
        if (text.Length == 0)
        {
            return;
        }

        using var font = new SKFont(SKTypeface.Default, stroke.Width);
        using var paint = new SKPaint { Color = new SKColor(stroke.Color), IsAntialias = true };
        var origin = stroke.Points[0];
        var baseline = origin.Y - font.Metrics.Ascent;
        foreach (var line in text.Split('\n'))
        {
            canvas.DrawText(line, origin.X, baseline, SKTextAlign.Left, font, paint);
            baseline += font.Spacing;
        }
    }

    /// <summary>Desfoca o que já foi desenhado sob o traço; retângulo e elipse são preenchidos.</summary>
    private static void DrawBlurStroke(SKCanvas canvas, Stroke stroke)
    {
        var sigma = stroke.Effect == StrokeEffect.StrongBlur ? StrongBlurSigma : BlurSigma;
        var bounds = Bounds(stroke, sigma * 3);
        using var blur = SKImageFilter.CreateBlur(sigma, sigma);
        canvas.SaveLayer(new SKCanvasSaveLayerRec { Bounds = bounds, Backdrop = blur });
        // A máscara cobre a camada inteira: fora do traço o desfoque é descartado.
        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.SaveLayer(bounds, mask);
        DrawOpaqueStroke(canvas, stroke, SKColors.White, fill: true);
        canvas.Restore();
        canvas.Restore();
    }

    private static SKRect Bounds(Stroke stroke, float extra)
    {
        var points = stroke.Points;
        var bounds = new SKRect(points[0].X, points[0].Y, points[0].X, points[0].Y);
        foreach (var point in points)
        {
            bounds.Union(new SKRect(point.X, point.Y, point.X, point.Y));
        }

        // Margem para a espessura e a ponta da seta.
        var margin = (stroke.Width * 5) + 12 + extra;
        bounds.Inflate(margin, margin);
        return bounds;
    }

    private static void DrawOpaqueStroke(SKCanvas canvas, Stroke stroke, SKColor color, bool fill = false)
    {
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = fill && stroke.Shape is StrokeShape.Rectangle or StrokeShape.Ellipse
                ? SKPaintStyle.StrokeAndFill
                : SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = stroke.Width,
        };

        var points = stroke.Points;
        if (stroke.Shape != StrokeShape.Freehand)
        {
            DrawShape(canvas, stroke.Shape, points[0], points[1], paint);
            return;
        }

        if (points.Count == 1)
        {
            canvas.DrawPoint(points[0].X, points[0].Y, paint);
            return;
        }

        if (stroke.HasUniformPressure)
        {
            using var builder = new SKPathBuilder();
            builder.MoveTo(points[0].X, points[0].Y);
            for (var i = 1; i < points.Count; i++)
            {
                builder.LineTo(points[i].X, points[i].Y);
            }

            using var path = builder.Detach();
            canvas.DrawPath(path, paint);
            return;
        }

        // Caneta com pressão: espessura variável por segmento.
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            paint.StrokeWidth = stroke.Width * MathF.Max(0.15f, (a.Pressure + b.Pressure) / 2);
            canvas.DrawLine(a.X, a.Y, b.X, b.Y, paint);
        }
    }

    private static void DrawShape(SKCanvas canvas, StrokeShape shape, StrokePoint start, StrokePoint end, SKPaint paint)
    {
        var bounds = new SKRect(MathF.Min(start.X, end.X), MathF.Min(start.Y, end.Y),
            MathF.Max(start.X, end.X), MathF.Max(start.Y, end.Y));
        switch (shape)
        {
            case StrokeShape.Line:
                canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
                break;
            case StrokeShape.Arrow:
                canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
                DrawArrowHead(canvas, start, end, paint);
                break;
            case StrokeShape.Rectangle:
                canvas.DrawRect(bounds, paint);
                break;
            case StrokeShape.Ellipse:
                canvas.DrawOval(bounds, paint);
                break;
        }
    }

    private static void DrawArrowHead(SKCanvas canvas, StrokePoint start, StrokePoint end, SKPaint paint)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001f)
        {
            return;
        }

        // Ponta proporcional à espessura, sem passar da metade da haste.
        var head = MathF.Min(MathF.Max(paint.StrokeWidth * 4, 12f), length / 2);
        var angle = MathF.Atan2(dy, dx);
        const float spread = MathF.PI / 6;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var side = angle + MathF.PI + (sign * spread);
            canvas.DrawLine(end.X, end.Y, end.X + (head * MathF.Cos(side)), end.Y + (head * MathF.Sin(side)), paint);
        }
    }
}
