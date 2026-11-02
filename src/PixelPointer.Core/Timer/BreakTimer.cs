namespace PixelPointer.Core.Timer;

/// <summary>Contagem regressiva de intervalo (Ctrl+3), independente de relógio e de janela.</summary>
public sealed class BreakTimer
{
    public const int DefaultMinutes = 10;
    public const int MinMinutes = 1;
    public const int MaxMinutes = 99;

    private TimeSpan _started;

    public BreakTimer(TimeSpan now, int minutes = DefaultMinutes)
    {
        Minutes = Math.Clamp(minutes, MinMinutes, MaxMinutes);
        _started = now;
    }

    public int Minutes { get; private set; }

    /// <summary>Ajusta a duração e reinicia a contagem a partir de <paramref name="now"/>.</summary>
    public void Adjust(int deltaMinutes, TimeSpan now)
    {
        Minutes = Math.Clamp(Minutes + deltaMinutes, MinMinutes, MaxMinutes);
        _started = now;
    }

    public TimeSpan Remaining(TimeSpan now)
    {
        var remaining = TimeSpan.FromMinutes(Minutes) - (now - _started);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool IsExpired(TimeSpan now) => Remaining(now) == TimeSpan.Zero;

    /// <summary>Formata como MM:SS arredondando para cima, para mostrar 10:00 no início e 00:00 só ao terminar.</summary>
    public static string Format(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return FormattableString.Invariant($"{seconds / 60:00}:{seconds % 60:00}");
    }
}
