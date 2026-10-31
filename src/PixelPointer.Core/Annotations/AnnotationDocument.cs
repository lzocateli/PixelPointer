namespace PixelPointer.Core.Annotations;

public enum PenColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Orange,
    Pink,
}

public enum Board
{
    None,
    White,
    Black,
}

/// <summary>
/// Estado das anotações de uma sobreposição: traços, ferramenta atual e quadro de fundo,
/// independente de janela, entrada e renderização.
/// </summary>
public sealed class AnnotationDocument
{
    public const float MinPenWidth = 1f;
    public const float MaxPenWidth = 60f;
    public const float PenWidthStep = 2f;
    public const float DefaultPenWidth = 6f;

    private readonly List<Stroke> _strokes = [];
    private Stroke? _current;

    public AnnotationDocument(PenColor color = PenColor.Red, float penWidth = DefaultPenWidth)
    {
        if (!float.IsFinite(penWidth) || penWidth < MinPenWidth || penWidth > MaxPenWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(penWidth), penWidth, "Espessura fora dos limites.");
        }

        SelectColor(color);
        PenWidth = penWidth;
    }

    public IReadOnlyList<Stroke> Strokes => _strokes;

    public PenColor Color { get; private set; }

    public uint ColorArgb => ToArgb(Color);

    public float PenWidth { get; private set; }

    public Board Board { get; private set; } = Board.None;

    public bool IsStroking => _current is not null;

    public static uint ToArgb(PenColor color) => color switch
    {
        PenColor.Red => 0xFFFF0000,
        PenColor.Green => 0xFF32CD32,
        PenColor.Blue => 0xFF1E90FF,
        PenColor.Yellow => 0xFFFFFF00,
        PenColor.Orange => 0xFFFFA500,
        PenColor.Pink => 0xFFFF69B4,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };

    public void SelectColor(PenColor color)
    {
        _ = ToArgb(color);
        Color = color;
    }

    /// <summary>Aumenta (positivo) ou reduz (negativo) a espessura em um passo, dentro dos limites.</summary>
    public void StepPenWidth(int direction) =>
        PenWidth = Math.Clamp(PenWidth + (Math.Sign(direction) * PenWidthStep), MinPenWidth, MaxPenWidth);

    /// <summary>Alterna o quadro: escolher o quadro já ativo volta ao fundo capturado.</summary>
    public void ToggleBoard(Board board) => Board = Board == board ? Board.None : board;

    /// <summary>Inicia um traço; <paramref name="viewScale"/> converte a espessura da tela para a imagem.</summary>
    public void BeginStroke(float x, float y, float pressure, float viewScale)
    {
        if (!(viewScale > 0) || !float.IsFinite(viewScale))
        {
            throw new ArgumentOutOfRangeException(nameof(viewScale), viewScale, "A escala da vista deve ser positiva e finita.");
        }

        _current = new Stroke(ColorArgb, PenWidth / viewScale, new StrokePoint(x, y, pressure));
        _strokes.Add(_current);
    }

    public void ContinueStroke(float x, float y, float pressure) => _current?.Add(new StrokePoint(x, y, pressure));

    public void EndStroke() => _current = null;

    /// <summary>Remove o último traço; um traço em andamento é encerrado para não receber pontos depois de removido.</summary>
    public void Undo()
    {
        _current = null;
        if (_strokes.Count > 0)
        {
            _strokes.RemoveAt(_strokes.Count - 1);
        }
    }

    public void Clear()
    {
        _current = null;
        _strokes.Clear();
    }
}
