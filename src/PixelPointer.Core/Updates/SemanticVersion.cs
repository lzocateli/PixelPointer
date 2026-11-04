using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PixelPointer.Core.Updates;

/// <summary>Versão SemVer 2.0 (MAJOR.MINOR.PATCH[-pre]); metadados de build (+...) são ignorados na comparação.</summary>
public sealed record SemanticVersion(int Major, int Minor, int Patch, string Prerelease) : IComparable<SemanticVersion>
{
    public bool IsPrerelease => Prerelease.Length > 0;

    public static SemanticVersion Parse(string value) =>
        TryParse(value, out var version) ? version : throw new FormatException($"Versão inválida: {value}");

    public static bool TryParse(string? value, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        var text = value;
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            if (!IsValidIdentifiers(text[(plus + 1)..], numericRules: false))
            {
                return false;
            }

            text = text[..plus];
        }

        var prerelease = string.Empty;
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            prerelease = text[(dash + 1)..];
            if (!IsValidIdentifiers(prerelease, numericRules: true))
            {
                return false;
            }

            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length != 3 || !TryParseNumber(parts[0], out var major) || !TryParseNumber(parts[1], out var minor)
            || !TryParseNumber(parts[2], out var patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result == 0)
        {
            result = Minor.CompareTo(other.Minor);
        }

        if (result == 0)
        {
            result = Patch.CompareTo(other.Patch);
        }

        return result != 0 ? Math.Sign(result) : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public override string ToString() =>
        IsPrerelease ? $"{Major}.{Minor}.{Patch}-{Prerelease}" : $"{Major}.{Minor}.{Patch}";

    private static int ComparePrerelease(string left, string right)
    {
        // Sem sufixo é maior que qualquer pré-lançamento da mesma versão.
        if (left.Length == 0 || right.Length == 0)
        {
            return left.Length == right.Length ? 0 : left.Length == 0 ? 1 : -1;
        }

        var a = left.Split('.');
        var b = right.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNumeric = int.TryParse(a[i], NumberStyles.None, CultureInfo.InvariantCulture, out var x);
            var bNumeric = int.TryParse(b[i], NumberStyles.None, CultureInfo.InvariantCulture, out var y);
            var result = (aNumeric, bNumeric) switch
            {
                (true, true) => x.CompareTo(y),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (result != 0)
            {
                return Math.Sign(result);
            }
        }

        return Math.Sign(a.Length.CompareTo(b.Length));
    }

    private static bool TryParseNumber(string text, out int value)
    {
        value = 0;
        return text.Length is > 0 and <= 9 && (text.Length == 1 || text[0] != '0') && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsValidIdentifiers(string text, bool numericRules)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0 || !identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }

            if (numericRules && identifier.Length > 1 && identifier[0] == '0' && identifier.All(char.IsAsciiDigit))
            {
                return false;
            }
        }

        return true;
    }
}
