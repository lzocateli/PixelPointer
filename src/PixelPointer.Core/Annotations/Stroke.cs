namespace PixelPointer.Core.Annotations;

/// <summary>Ponto de um traço em coordenadas da imagem; pressão 1 equivale a mouse.</summary>
public readonly record struct StrokePoint(float X, float Y, float Pressure);

/// <summary>Forma do traço; formas geométricas guardam apenas o ponto inicial e o final.</summary>
public enum StrokeShape
{
    Freehand,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
}

public static class StrokeShapes
{
    /// <summary>Convenção do ZoomIt: Shift linha, Ctrl retângulo, Ctrl+Shift seta, Tab elipse.</summary>
    public static StrokeShape FromModifiers(bool ctrl, bool shift, bool tab) => (ctrl, shift, tab) switch
    {
        (true, true, _) => StrokeShape.Arrow,
        (true, false, _) => StrokeShape.Rectangle,
        (false, true, _) => StrokeShape.Line,
        (false, false, true) => StrokeShape.Ellipse,
        _ => StrokeShape.Freehand,
    };
}

/// <summary>Efeito aplicado ao conteúdo sob o traço em vez de pintar com a cor.</summary>
public enum StrokeEffect
{
    None,
    Blur,
    StrongBlur,
}

/// <summary>Traço com cor ARGB e espessura em coordenadas da imagem.</summary>
public sealed class Stroke
{
    private readonly List<StrokePoint> _points;

    internal Stroke(uint color, float width, StrokePoint first, StrokeShape shape = StrokeShape.Freehand,
        StrokeEffect effect = StrokeEffect.None)
    {
        Color = color;
        Width = width;
        Shape = shape;
        Effect = effect;
        _points = shape == StrokeShape.Freehand ? [first] : [first, first];
    }

    public uint Color { get; }

    public float Width { get; }

    public StrokeShape Shape { get; }

    public StrokeEffect Effect { get; }

    public IReadOnlyList<StrokePoint> Points => _points;

    public bool HasUniformPressure => _points.TrueForAll(p => p.Pressure >= 0.999f);

    internal void Add(StrokePoint point)
    {
        if (Shape == StrokeShape.Freehand)
        {
            _points.Add(point);
        }
        else
        {
            _points[1] = point;
        }
    }
}
