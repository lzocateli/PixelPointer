using System.Text.Json.Nodes;
using PixelPointer.Core.Updates.Sigstore;

namespace PixelPointer.UnitTests;

/// <summary>Bundles reais da release v0.3.1 (API de atestações do GitHub), verificados sem rede.</summary>
public sealed class SigstoreVerifierTests
{
    private const string LinuxX64 = "pixelpointer-linux-x64.tar.gz";
    private const string LinuxX64Digest = "1053451b85d92df6e972a17d4a083638aa20b01d61860ff843e37e5ea62d5c40";
    private const string LinuxArm64Digest = "38cb0dd3e8bfc5994b6316e862f90d6a34e632f91c634120b874105ea9a8c809";

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "attestations-v0.3.1-linux-x64.json"));

    private static ProvenancePolicy Policy => ProvenancePolicy.PixelPointerReleases;

    [Fact]
    public void ValidProvenanceIsAccepted()
    {
        var result = SigstoreVerifier.VerifyAttestations(Fixture(), LinuxX64, LinuxX64Digest, Policy);

        Assert.Equal("https://github.com/lzocateli/PixelPointer/.github/workflows/gw-github-assets.yml@refs/heads/main", result.Signer);
        Assert.Equal("refs/heads/main", result.SourceRef);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793805932), result.IntegratedTime);
    }

    [Fact]
    public void ProvenanceCoversOtherAssetsOfTheSameRelease() =>
        SigstoreVerifier.VerifyAttestations(Fixture(), "pixelpointer-linux-arm64.tar.gz", LinuxArm64Digest, Policy);

    [Fact]
    public void DigestDifferentFromTheDownloadIsRejected() =>
        AssertRejected(Fixture(), digest: new string('0', 64), expected: "não cobre");

    [Fact]
    public void DigestOfAnotherAssetUnderTheRequestedNameIsRejected() =>
        AssertRejected(Fixture(), digest: LinuxArm64Digest, expected: "não cobre");

    [Theory]
    [InlineData("repository")]
    [InlineData("workflow")]
    [InlineData("ref")]
    [InlineData("issuer")]
    public void OtherIdentityIsRejected(string field)
    {
        var policy = field switch
        {
            "repository" => Policy with { SourceRepositoryId = "1" },
            "workflow" => Policy with { SignerWorkflows = ["https://github.com/lzocateli/PixelPointer/.github/workflows/publish.yml"] },
            "ref" => Policy with { AllowedRefs = ["refs/heads/release"] },
            _ => Policy with { Issuer = "https://example.invalid" },
        };

        var error = Assert.Throws<SigstoreVerificationException>(() =>
            SigstoreVerifier.VerifyAttestations(Fixture(), LinuxX64, LinuxX64Digest, policy));
        Assert.Contains("não autorizad", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TamperedPayloadFailsTheDsseSignature()
    {
        var json = Mutate(bundle =>
        {
            var envelope = bundle["dsseEnvelope"]!;
            var statement = JsonNode.Parse(Convert.FromBase64String(envelope["payload"]!.GetValue<string>()))!;
            var subject = statement["subject"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == LinuxX64)!;
            subject["digest"]!["sha256"] = new string('a', 64);
            envelope["payload"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(statement.ToJsonString()));
        });

        AssertRejected(json, LinuxX64, new string('a', 64), "assinatura DSSE inválida");
    }

    [Fact]
    public void TamperedIntegratedTimeFailsTheSet() =>
        AssertRejected(Mutate(bundle => Entry(bundle)["integratedTime"] = "1793805933"), expected: "SET do Rekor inválido");

    [Fact]
    public void TamperedInclusionProofIsRejected() =>
        AssertRejected(Mutate(bundle => Entry(bundle)["inclusionProof"]!["hashes"]![0] = Convert.ToBase64String(new byte[32])),
            expected: "prova de inclusão");

    [Fact]
    public void CheckpointForAnotherTreeIsRejected() =>
        AssertRejected(Mutate(bundle =>
        {
            var checkpoint = Entry(bundle)["inclusionProof"]!["checkpoint"]!;
            checkpoint["envelope"] = checkpoint["envelope"]!.GetValue<string>().Replace("\n3569162966\n", "\n3569162967\n", StringComparison.Ordinal);
        }), expected: "checkpoint");

    [Fact]
    public void UntrustedLogIsRejected() =>
        AssertRejected(Mutate(bundle => Entry(bundle)["logId"]!["keyId"] = Convert.ToBase64String(new byte[32])),
            expected: "não confiável");

    [Fact]
    public void SbomAttestationAloneIsNotProvenance()
    {
        var root = JsonNode.Parse(Fixture())!;
        root["attestations"]!.AsArray().RemoveAt(0);

        AssertRejected(root.ToJsonString(), expected: "proveniência SLSA");
    }

    [Fact]
    public void MissingAttestationsAreRejected() =>
        AssertRejected("""{"attestations":[]}""", expected: "nenhuma atestação publicada");

    [Fact]
    public void MerkleRootOfSingleLeafTreeIsTheLeaf()
    {
        var leaf = new byte[32];
        leaf[0] = 7;

        Assert.Equal(leaf, SigstoreVerifier.MerkleRoot(0, 1, leaf, []));
        Assert.Throws<SigstoreVerificationException>(() => SigstoreVerifier.MerkleRoot(1, 1, leaf, []));
    }

    [Fact]
    public void PreAuthenticationEncodingFollowsDsse() =>
        Assert.Equal("DSSEv1 4 type 3 abc"u8.ToArray(), SigstoreVerifier.PreAuthenticationEncoding("type", "abc"u8.ToArray()));

    private static JsonNode Entry(JsonNode bundle) => bundle["verificationMaterial"]!["tlogEntries"]![0]!;

    // Altera somente o bundle de proveniência e remove o de SBOM, para que a falha observada seja a do caso.
    private static string Mutate(Action<JsonNode> change)
    {
        var root = JsonNode.Parse(Fixture())!;
        var attestations = root["attestations"]!.AsArray();
        attestations.RemoveAt(1);
        change(attestations[0]!["bundle"]!);
        return root.ToJsonString();
    }

    private static void AssertRejected(string json, string name = LinuxX64, string digest = LinuxX64Digest, string expected = "")
    {
        var error = Assert.Throws<SigstoreVerificationException>(() =>
            SigstoreVerifier.VerifyAttestations(json, name, digest, Policy));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }
}
