using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPointer.Core.Updates;

public enum UpdateAvailability
{
    UpToDate,
    Available,
    Incompatible,
}

/// <summary>Resultado da comparação entre a versão instalada e a release publicada.</summary>
public sealed record UpdateCandidate(
    UpdateAvailability Availability,
    SemanticVersion Current,
    SemanticVersion Latest,
    string? AssetName,
    long AssetSize,
    string Detail);

/// <summary>Release estável publicada, lida da resposta de releases/latest. Serve só para descobrir a tag.</summary>
public sealed record PublishedRelease(SemanticVersion Version, IReadOnlyDictionary<string, long> Assets)
{
    public const int MaxBytes = 1024 * 1024;

    /// <summary>Interpreta a release; somente estáveis publicadas são aceitas (o canal preview não é oferecido).</summary>
    public static PublishedRelease Parse(string json)
    {
        GitHubRelease? release;
        try
        {
            release = JsonSerializer.Deserialize(json, ReleaseJsonContext.Default.GitHubRelease);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Resposta de release inválida: {ex.Message}", ex);
        }

        if (release?.TagName is not { } tag || !tag.StartsWith('v') || !SemanticVersion.TryParse(tag[1..], out var version)
            || tag.Contains('+', StringComparison.Ordinal))
        {
            throw new InvalidDataException("A release não informa uma tag vX.Y.Z válida.");
        }

        if (release.Draft || release.Prerelease || version.IsPrerelease)
        {
            throw new InvalidDataException($"A release {tag} não é estável.");
        }

        var assets = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var asset in release.Assets ?? [])
        {
            if (asset.Name is { Length: > 0 } name && asset.Size is > 0)
            {
                assets[name] = asset.Size.Value;
            }
        }

        return new PublishedRelease(version, assets);
    }

    public UpdateCandidate Evaluate(SemanticVersion current, string rid)
    {
        if (Version <= current)
        {
            return new(UpdateAvailability.UpToDate, current, Version, null, 0, $"A versão {current} é a mais recente.");
        }

        if (!PackageLayout.SupportedRids.Contains(rid))
        {
            return new(UpdateAvailability.Incompatible, current, Version, null, 0,
                $"A versão {Version} não publica pacote para {rid}.");
        }

        var layout = PackageLayout.For(rid);
        if (!Assets.TryGetValue(layout.AssetName, out var size) || !Assets.ContainsKey("SHA256SUMS"))
        {
            return new(UpdateAvailability.Incompatible, current, Version, null, 0,
                $"A versão {Version} não contém {layout.AssetName} e SHA256SUMS.");
        }

        if (size > PackageLayout.MaxPackageBytes)
        {
            return new(UpdateAvailability.Incompatible, current, Version, null, 0,
                $"O pacote {layout.AssetName} excede o limite aceito.");
        }

        return new(UpdateAvailability.Available, current, Version, layout.AssetName, size,
            $"Versão {Version} disponível (instalada: {current}).");
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class ReleaseJsonContext : JsonSerializerContext;
