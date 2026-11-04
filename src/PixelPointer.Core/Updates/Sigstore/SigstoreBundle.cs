using System.Text.Json.Serialization;

namespace PixelPointer.Core.Updates.Sigstore;

// Modelo mínimo do bundle Sigstore (protobuf-specs, JSON) devolvido pela API de atestações do GitHub.
// Inteiros de 64 bits chegam como string, conforme o mapeamento JSON do protobuf.
internal sealed class AttestationsResponse
{
    public List<AttestationItem>? Attestations { get; set; }
}

internal sealed class AttestationItem
{
    public SigstoreBundle? Bundle { get; set; }
}

internal sealed class SigstoreBundle
{
    public string? MediaType { get; set; }

    public VerificationMaterial? VerificationMaterial { get; set; }

    public DsseEnvelope? DsseEnvelope { get; set; }
}

internal sealed class VerificationMaterial
{
    public RawBytes? Certificate { get; set; }

    public CertificateChain? X509CertificateChain { get; set; }

    public List<TlogEntry>? TlogEntries { get; set; }
}

internal sealed class CertificateChain
{
    public List<RawBytes>? Certificates { get; set; }
}

internal sealed class RawBytes
{
    [JsonPropertyName("rawBytes")]
    public string? RawBytesValue { get; set; }
}

internal sealed class TlogEntry
{
    public string? LogIndex { get; set; }

    public LogId? LogId { get; set; }

    public KindVersion? KindVersion { get; set; }

    public string? IntegratedTime { get; set; }

    public InclusionPromise? InclusionPromise { get; set; }

    public InclusionProof? InclusionProof { get; set; }

    public string? CanonicalizedBody { get; set; }
}

internal sealed class LogId
{
    public string? KeyId { get; set; }
}

internal sealed class KindVersion
{
    public string? Kind { get; set; }

    public string? Version { get; set; }
}

internal sealed class InclusionPromise
{
    public string? SignedEntryTimestamp { get; set; }
}

internal sealed class InclusionProof
{
    public string? LogIndex { get; set; }

    public string? RootHash { get; set; }

    public string? TreeSize { get; set; }

    public List<string>? Hashes { get; set; }

    public Checkpoint? Checkpoint { get; set; }
}

internal sealed class Checkpoint
{
    public string? Envelope { get; set; }
}

internal sealed class DsseEnvelope
{
    public string? Payload { get; set; }

    public string? PayloadType { get; set; }

    public List<DsseSignature>? Signatures { get; set; }
}

internal sealed class DsseSignature
{
    public string? Sig { get; set; }
}

// Corpo da entrada "dsse" 0.0.1 do Rekor.
internal sealed class RekorDsseBody
{
    public string? ApiVersion { get; set; }

    public string? Kind { get; set; }

    public RekorDsseSpec? Spec { get; set; }
}

internal sealed class RekorDsseSpec
{
    public RekorHash? PayloadHash { get; set; }

    public List<RekorSignature>? Signatures { get; set; }
}

internal sealed class RekorHash
{
    public string? Algorithm { get; set; }

    public string? Value { get; set; }
}

internal sealed class RekorSignature
{
    public string? Signature { get; set; }

    public string? Verifier { get; set; }
}

// Statement in-toto v1.
internal sealed class InTotoStatement
{
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    public List<InTotoSubject>? Subject { get; set; }

    public string? PredicateType { get; set; }
}

internal sealed class InTotoSubject
{
    public string? Name { get; set; }

    public Dictionary<string, string>? Digest { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AttestationsResponse))]
[JsonSerializable(typeof(RekorDsseBody))]
[JsonSerializable(typeof(InTotoStatement))]
internal sealed partial class SigstoreJsonContext : JsonSerializerContext;
