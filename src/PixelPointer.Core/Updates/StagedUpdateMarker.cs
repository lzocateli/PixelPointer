using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPointer.Core.Updates;

/// <summary>Marcador staged.json de uma atualização baixada, validada e pronta para aplicar.</summary>
public sealed record StagedUpdateMarker(SemanticVersion Version, string Rid, string Target, string Sha256)
{
    public const string FileName = "staged.json";
    public const int MaxBytes = 4 * 1024;

    public static StagedUpdateMarker Parse(string json)
    {
        MarkerFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, MarkerJsonContext.Default.MarkerFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"marcador inválido: {ex.Message}", ex);
        }

        if (file is not { Version: 1, Update: { } update, Rid: { } rid, Target: { } target, Sha256: { } sha256 }
            || !SemanticVersion.TryParse(update, out var version)
            || !PackageLayout.SupportedRids.Contains(rid)
            || !IsPlainName(target)
            || sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigitLower))
        {
            throw new InvalidDataException("marcador de atualização inválido");
        }

        return new StagedUpdateMarker(version, rid, target, sha256);
    }

    public string Serialize() =>
        JsonSerializer.Serialize(new MarkerFile
        {
            Version = 1,
            Update = Version.ToString(),
            Rid = Rid,
            Target = Target,
            Sha256 = Sha256,
        }, MarkerJsonContext.Default.MarkerFile) + "\n";

    /// <summary>Nome de um único componente de caminho, sem separadores nem referências relativas.</summary>
    public static bool IsPlainName(string name) =>
        name.Length is > 0 and <= 255 && name is not "." and not ".." && name.Trim() == name
        && name.IndexOfAny(['/', '\\', ':']) < 0 && !name.Any(char.IsControl);
}

internal sealed class MarkerFile
{
    public int? Version { get; set; }

    public string? Update { get; set; }

    public string? Rid { get; set; }

    public string? Target { get; set; }

    public string? Sha256 { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(MarkerFile))]
internal sealed partial class MarkerJsonContext : JsonSerializerContext;
