using PixelPointer.Core.Annotations;

namespace PixelPointer.UnitTests;

public sealed class AnnotationDocumentTests
{
    [Fact]
    public void BeginStroke_UsesCurrentToolAndConvertsWidthToImageScale()
    {
        var document = new AnnotationDocument();
        document.SelectColor(PenColor.Blue);

        document.BeginStroke(10, 20, 0.5f, viewScale: 2f);

        var stroke = Assert.Single(document.Strokes);
        Assert.Equal(0xFF1E90FFu, stroke.Color);
        Assert.Equal(AnnotationDocument.DefaultPenWidth / 2f, stroke.Width);
        Assert.Equal(new StrokePoint(10, 20, 0.5f), Assert.Single(stroke.Points));
        Assert.True(document.IsStroking);
    }

    [Fact]
    public void ContinueStroke_AddsPointsOnlyWhileStroking()
    {
        var document = new AnnotationDocument();
        document.ContinueStroke(1, 1, 1f);
        Assert.Empty(document.Strokes);

        document.BeginStroke(0, 0, 1f, 1f);
        document.ContinueStroke(5, 5, 1f);
        document.EndStroke();
        document.ContinueStroke(9, 9, 1f);

        Assert.Equal([new StrokePoint(0, 0, 1f), new StrokePoint(5, 5, 1f)], Assert.Single(document.Strokes).Points);
        Assert.False(document.IsStroking);
    }

    [Fact]
    public void ToolChanges_DoNotAlterExistingStrokes()
    {
        var document = new AnnotationDocument();
        document.BeginStroke(0, 0, 1f, 1f);
        document.EndStroke();

        document.SelectColor(PenColor.Yellow);
        document.StepPenWidth(1);
        document.BeginStroke(0, 0, 1f, 1f);

        Assert.Equal(AnnotationDocument.ToArgb(PenColor.Red), document.Strokes[0].Color);
        Assert.Equal(AnnotationDocument.DefaultPenWidth, document.Strokes[0].Width);
        Assert.Equal(AnnotationDocument.ToArgb(PenColor.Yellow), document.Strokes[1].Color);
        Assert.Equal(AnnotationDocument.DefaultPenWidth + AnnotationDocument.PenWidthStep, document.Strokes[1].Width);
    }

    [Fact]
    public void Undo_RemovesLastStrokeAndStopsTheActiveOne()
    {
        var document = new AnnotationDocument();
        document.BeginStroke(0, 0, 1f, 1f);
        document.EndStroke();
        document.BeginStroke(1, 1, 1f, 1f);

        document.Undo();
        document.ContinueStroke(2, 2, 1f);

        Assert.False(document.IsStroking);
        Assert.Equal([new StrokePoint(0, 0, 1f)], Assert.Single(document.Strokes).Points);

        document.Undo();
        document.Undo();
        Assert.Empty(document.Strokes);
    }

    [Fact]
    public void Clear_RemovesAllStrokesAndStopsTheActiveOne()
    {
        var document = new AnnotationDocument();
        document.BeginStroke(0, 0, 1f, 1f);
        document.EndStroke();
        document.BeginStroke(1, 1, 1f, 1f);

        document.Clear();
        document.ContinueStroke(2, 2, 1f);

        Assert.Empty(document.Strokes);
        Assert.False(document.IsStroking);
    }

    [Theory]
    [InlineData(1, 100, AnnotationDocument.MaxPenWidth)]
    [InlineData(-1, 100, AnnotationDocument.MinPenWidth)]
    [InlineData(1, 1, AnnotationDocument.DefaultPenWidth + AnnotationDocument.PenWidthStep)]
    [InlineData(-1, 1, AnnotationDocument.DefaultPenWidth - AnnotationDocument.PenWidthStep)]
    [InlineData(0, 5, AnnotationDocument.DefaultPenWidth)]
    public void StepPenWidth_StaysWithinLimits(int direction, int times, float expected)
    {
        var document = new AnnotationDocument();
        for (var i = 0; i < times; i++)
        {
            document.StepPenWidth(direction);
        }

        Assert.Equal(expected, document.PenWidth);
    }

    [Fact]
    public void ToggleBoard_SwitchesAndReturnsToCapture()
    {
        var document = new AnnotationDocument();

        document.ToggleBoard(Board.White);
        Assert.Equal(Board.White, document.Board);
        document.ToggleBoard(Board.Black);
        Assert.Equal(Board.Black, document.Board);
        document.ToggleBoard(Board.Black);
        Assert.Equal(Board.None, document.Board);
    }

    [Theory]
    [InlineData(PenColor.Red, 0xFFFF0000u)]
    [InlineData(PenColor.Green, 0xFF32CD32u)]
    [InlineData(PenColor.Blue, 0xFF1E90FFu)]
    [InlineData(PenColor.Yellow, 0xFFFFFF00u)]
    [InlineData(PenColor.Orange, 0xFFFFA500u)]
    [InlineData(PenColor.Pink, 0xFFFF69B4u)]
    public void Palette_IsOpaqueAndStable(PenColor color, uint expected) =>
        Assert.Equal(expected, AnnotationDocument.ToArgb(color));

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void BeginStroke_RejectsInvalidScale(float scale)
    {
        var document = new AnnotationDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.BeginStroke(0, 0, 1f, scale));
        Assert.Empty(document.Strokes);
    }

    [Fact]
    public void SelectColor_RejectsUnknownValue()
    {
        var document = new AnnotationDocument();

        Assert.Throws<ArgumentOutOfRangeException>(() => document.SelectColor((PenColor)42));
        Assert.Equal(PenColor.Red, document.Color);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(0.5f, false)]
    public void HasUniformPressure_DistinguishesMouseFromPen(float pressure, bool expected)
    {
        var document = new AnnotationDocument();
        document.BeginStroke(0, 0, 1f, 1f);
        document.ContinueStroke(1, 1, pressure);

        Assert.Equal(expected, document.Strokes[0].HasUniformPressure);
    }
}
