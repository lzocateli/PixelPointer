using System.Text.RegularExpressions;

namespace PixelPointer.Core.Updates;

/// <summary>Leitura do SHA256SUMS da release, com a mesma regra dos instaladores: exatamente uma entrada por nome.</summary>
public static partial class Sha256Sums
{
    public const int MaxBytes = 64 * 1024;

    public static string FindDigest(string content, string fileName)
    {
        string? found = null;
        foreach (var line in content.Split('\n'))
        {
            var match = EntryPattern().Match(line.TrimEnd('\r'));
            if (!match.Success || !string.Equals(match.Groups[2].Value, fileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidDataException($"SHA256SUMS contém mais de uma entrada para {fileName}.");
            }

            found = match.Groups[1].Value;
        }

        return found ?? throw new InvalidDataException($"SHA256SUMS não contém uma entrada válida para {fileName}.");
    }

    [GeneratedRegex("^([0-9a-f]{64})  (.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex EntryPattern();
}
