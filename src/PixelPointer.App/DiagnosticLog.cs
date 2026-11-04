using System.Globalization;

namespace PixelPointer.App;

internal static class DiagnosticLog
{
    private static readonly object Gate = new();
    private static long _sequence;

    internal static void Write(string category, string detail)
    {
        lock (Gate)
        {
            var sequence = ++_sequence;
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            var safeDetail = detail.Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            Console.Error.WriteLine($"[DIAG #{sequence:D6} {timestamp}] {category} {safeDetail}");
        }
    }
}
