namespace PixelPointer.Core.Updates.Sigstore;

/// <summary>Identidade exigida do certificado Fulcio que assinou a atestação de proveniência.</summary>
public sealed record ProvenancePolicy(
    string Issuer,
    string SourceRepositoryUri,
    string SourceRepositoryId,
    string SourceRepositoryOwnerId,
    IReadOnlyList<string> SignerWorkflows,
    IReadOnlyList<string> AllowedRefs,
    string AllowedTagRefPrefix)
{
    public const string SlsaProvenanceV1 = "https://slsa.dev/provenance/v1";

    /// <summary>
    /// Releases do PixelPointer: atestação feita pelo workflow reutilizável gw-github-assets.yml do próprio repositório,
    /// em runner hospedado pelo GitHub, a partir de main ou de uma tag v*. Os identificadores numéricos fixam o
    /// repositório e o dono mesmo após renomeação ou recriação com o mesmo nome.
    /// </summary>
    public static ProvenancePolicy PixelPointerReleases { get; } = new(
        "https://token.actions.githubusercontent.com",
        "https://github.com/lzocateli/PixelPointer",
        "1439215418",
        "16601706",
        ["https://github.com/lzocateli/PixelPointer/.github/workflows/gw-github-assets.yml"],
        ["refs/heads/main"],
        "refs/tags/v");

    public bool IsAllowedRef(string reference) =>
        AllowedRefs.Contains(reference)
        || (reference.StartsWith(AllowedTagRefPrefix, StringComparison.Ordinal)
            && SemanticVersion.TryParse(reference[AllowedTagRefPrefix.Length..], out _));

    public bool IsAllowedSigner(string subjectAlternativeName)
    {
        foreach (var workflow in SignerWorkflows)
        {
            if (subjectAlternativeName.StartsWith(workflow + "@", StringComparison.Ordinal)
                && IsAllowedRef(subjectAlternativeName[(workflow.Length + 1)..]))
            {
                return true;
            }
        }

        return false;
    }
}
