namespace PixelPointer.Core.Updates;

/// <summary>Agenda da verificação automática no daemon: 5 min após iniciar, depois a cada 24 h.</summary>
public static class UpdateSchedule
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Próxima consulta automática. Uma última consulta no futuro (relógio ajustado para trás) é ignorada para não
    /// suspender a verificação indefinidamente.
    /// </summary>
    public static DateTimeOffset NextCheck(DateTimeOffset daemonStart, DateTimeOffset? lastCheck, DateTimeOffset now)
    {
        var first = daemonStart + InitialDelay;
        if (lastCheck is not { } last || last > now)
        {
            return first;
        }

        var next = last + Interval;
        return next > first ? next : first;
    }
}
