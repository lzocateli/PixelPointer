namespace PixelPointer.Core.Annotations;

/// <summary>Ponto de um traço em coordenadas da imagem; pressão 1 equivale a mouse.</summary>
public readonly record struct StrokePoint(float X, float Y, float Pressure);

/// <summary>Traço com cor ARGB e espessura em coordenadas da imagem.</summary>
public sealed class Stroke
{
    private readonly List<StrokePoint> _points;

    internal Stroke(uint color, float width, StrokePoint first)
    {
        Color = color;
        Width = width;
        _points = [first];
    }

    public uint Color { get; }

    public float Width { get; }

    public IReadOnlyList<StrokePoint> Points => _points;

    public bool HasUniformPressure => _points.TrueForAll(p => p.Pressure >= 0.999f);

    internal void Add(StrokePoint point) => _points.Add(point);
}
