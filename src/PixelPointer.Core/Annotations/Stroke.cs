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
    /// <summary>Texto ancorado no primeiro ponto; a espessura é o tamanho da fonte.</summary>
    Text,
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
    private readonly System.Text.StringBuilder _text = new();

    internal Stroke(uint color, float width, StrokePoint first, StrokeShape shape = StrokeShape.Freehand,
        StrokeEffect effect = StrokeEffect.None)
    {
        Color = color;
        Width = width;
        Shape = shape;
        Effect = effect;
        _points = shape is StrokeShape.Freehand or StrokeShape.Text ? [first] : [first, first];
    }

    public uint Color { get; }

    public float Width { get; }

    public StrokeShape Shape { get; }

    public StrokeEffect Effect { get; }

    public string Text => _text.ToString();

    public IReadOnlyList<StrokePoint> Points => _points;

    public bool HasUniformPressure => _points.TrueForAll(p => p.Pressure >= 0.999f);

    internal void Add(StrokePoint point)
    {
        if (Shape == StrokeShape.Freehand)
        {
            _points.Add(point);
        }
        else if (Shape != StrokeShape.Text)
        {
            _points[1] = point;
        }
    }

    internal void AppendText(string text) => _text.Append(text);

    /// <summary>Remove o último caractere, sem separar pares substitutos.</summary>
    internal void RemoveLastCharacter()
    {
        if (_text.Length == 0)
        {
            return;
        }

        var count = _text.Length >= 2 && char.IsLowSurrogate(_text[^1]) && char.IsHighSurrogate(_text[^2]) ? 2 : 1;
        _text.Remove(_text.Length - count, count);
    }
}
