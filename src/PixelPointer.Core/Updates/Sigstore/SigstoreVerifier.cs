using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace PixelPointer.Core.Updates.Sigstore;

public sealed class SigstoreVerificationException(string message) : Exception(message);

/// <summary>Atestação verificada: emitida pelo signatário autorizado em <see cref="IntegratedTime"/>.</summary>
public sealed record VerifiedProvenance(string Signer, string SourceRef, DateTimeOffset IntegratedTime);

/// <summary>
/// Verificação nativa (BCL, sem rede) de bundles Sigstore v0.2/v0.3 com envelope DSSE, como os emitidos por
/// actions/attest-build-provenance: cadeia Fulcio, SCT do log CT, SET e prova de inclusão do Rekor v1,
/// assinatura DSSE, identidade do workflow e digest do arquivo no statement in-toto. Falha fechada em qualquer etapa.
/// </summary>
public static class SigstoreVerifier
{
    public const int MaxResponseBytes = 4 * 1024 * 1024;

    private const string ExtensionIssuer = "1.3.6.1.4.1.57264.1.8";
    private const string ExtensionRunnerEnvironment = "1.3.6.1.4.1.57264.1.11";
    private const string ExtensionSourceRepositoryUri = "1.3.6.1.4.1.57264.1.12";
    private const string ExtensionSourceRepositoryRef = "1.3.6.1.4.1.57264.1.14";
    private const string ExtensionSourceRepositoryId = "1.3.6.1.4.1.57264.1.15";
    private const string ExtensionSourceRepositoryOwnerId = "1.3.6.1.4.1.57264.1.17";
    private const string ExtensionSct = "1.3.6.1.4.1.11129.2.4.2";
    private const string ExtensionSubjectAlternativeName = "2.5.29.17";
    private const string CodeSigningUsage = "1.3.6.1.5.5.7.3.3";
    private const string InTotoPayloadType = "application/vnd.in-toto+json";
    private const string InTotoStatementV1 = "https://in-toto.io/Statement/v1";

    private static readonly string[] SupportedMediaTypes =
    [
        "application/vnd.dev.sigstore.bundle.v0.3+json",
        "application/vnd.dev.sigstore.bundle+json;version=0.3",
        "application/vnd.dev.sigstore.bundle+json;version=0.2",
    ];

    /// <summary>
    /// Verifica a resposta de GET /repos/{owner}/{repo}/attestations/sha256:{digest}: ao menos um bundle de
    /// proveniência SLSA v1 precisa ser válido para <paramref name="subjectName"/> com <paramref name="sha256"/>.
    /// </summary>
    public static VerifiedProvenance VerifyAttestations(string json, string subjectName, string sha256, ProvenancePolicy policy)
    {
        AttestationsResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(json, SigstoreJsonContext.Default.AttestationsResponse);
        }
        catch (JsonException ex)
        {
            throw new SigstoreVerificationException($"resposta de atestações inválida: {ex.Message}");
        }

        var failures = new List<string>();
        foreach (var bundle in response?.Attestations?.Select(item => item.Bundle) ?? [])
        {
            if (bundle is null)
            {
                continue;
            }

            try
            {
                return VerifyBundle(bundle, subjectName, sha256, policy, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is SigstoreVerificationException or CryptographicException or FormatException
                or AsnContentException or InvalidDataException or JsonException or ArgumentException)
            {
                failures.Add(ex.Message);
            }
        }

        throw new SigstoreVerificationException(failures.Count == 0
            ? "nenhuma atestação publicada para o pacote"
            : $"nenhuma atestação válida ({string.Join("; ", failures.Distinct().Take(3))})");
    }

    internal static VerifiedProvenance VerifyBundle(
        SigstoreBundle bundle, string subjectName, string sha256, ProvenancePolicy policy, DateTimeOffset now)
    {
        if (bundle.MediaType is null || !SupportedMediaTypes.Contains(bundle.MediaType))
        {
            throw Fail($"formato de bundle não suportado ({bundle.MediaType})");
        }

        var envelope = bundle.DsseEnvelope ?? throw Fail("bundle sem envelope DSSE");
        var payload = Convert.FromBase64String(envelope.Payload ?? throw Fail("envelope sem payload"));
        var statement = ParseStatement(envelope, payload);
        if (statement.PredicateType != ProvenancePolicy.SlsaProvenanceV1)
        {
            throw Fail($"predicado {statement.PredicateType} não é proveniência SLSA v1");
        }

        RequireSubject(statement, subjectName, sha256);

        var material = bundle.VerificationMaterial ?? throw Fail("bundle sem material de verificação");
        var leafBase64 = material.Certificate?.RawBytesValue
            ?? material.X509CertificateChain?.Certificates?.FirstOrDefault()?.RawBytesValue
            ?? throw Fail("bundle sem certificado");
        var leafDer = Convert.FromBase64String(leafBase64);
        using var leaf = X509CertificateLoader.LoadCertificate(leafDer);

        if (material.TlogEntries is not [var entry])
        {
            throw Fail("o bundle deve conter exatamente uma entrada do log de transparência");
        }

        var integratedTime = VerifyTransparencyLog(entry);
        if (integratedTime > now.AddMinutes(10))
        {
            throw Fail("entrada do log com horário no futuro");
        }

        VerifyCertificate(leaf, integratedTime);
        var (signer, sourceRef) = VerifyIdentity(leaf, policy);

        var signatures = envelope.Signatures ?? [];
        if (signatures is not [{ Sig: { } signatureBase64 }])
        {
            throw Fail("o envelope DSSE deve conter exatamente uma assinatura");
        }

        var signature = Convert.FromBase64String(signatureBase64);
        using (var key = leaf.GetECDsaPublicKey() ?? throw Fail("certificado sem chave ECDSA"))
        {
            if (!key.VerifyData(PreAuthenticationEncoding(envelope.PayloadType!, payload), signature,
                HashFor(key), DSASignatureFormat.Rfc3279DerSequence))
            {
                throw Fail("assinatura DSSE inválida");
            }
        }

        VerifyLogBody(entry, payload, signatureBase64, leafDer);
        return new VerifiedProvenance(signer, sourceRef, integratedTime);
    }

    internal static byte[] PreAuthenticationEncoding(string payloadType, byte[] payload)
    {
        var typeBytes = Encoding.UTF8.GetBytes(payloadType);
        var header = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture,
            $"DSSEv1 {typeBytes.Length} {payloadType} {payload.Length} "));
        return [.. header, .. payload];
    }

    private static InTotoStatement ParseStatement(DsseEnvelope envelope, byte[] payload)
    {
        if (envelope.PayloadType != InTotoPayloadType)
        {
            throw Fail($"tipo de payload {envelope.PayloadType} não suportado");
        }

        var statement = JsonSerializer.Deserialize(payload, SigstoreJsonContext.Default.InTotoStatement)
            ?? throw Fail("statement in-toto vazio");
        return statement.Type == InTotoStatementV1 ? statement : throw Fail($"statement {statement.Type} não suportado");
    }

    private static void RequireSubject(InTotoStatement statement, string subjectName, string sha256)
    {
        foreach (var subject in statement.Subject ?? [])
        {
            if (subject.Name == subjectName && subject.Digest is { } digest && digest.TryGetValue("sha256", out var value)
                && string.Equals(value, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw Fail($"a atestação não cobre {subjectName} com o SHA-256 baixado");
    }

    private static DateTimeOffset VerifyTransparencyLog(TlogEntry entry)
    {
        if (entry.KindVersion is not { Kind: "dsse", Version: "0.0.1" })
        {
            throw Fail($"entrada do log {entry.KindVersion?.Kind} {entry.KindVersion?.Version} não suportada");
        }

        var rekorSpki = Convert.FromBase64String(SigstoreTrustRoot.RekorPublicKey);
        var keyId = SHA256.HashData(rekorSpki);
        if (!Convert.FromBase64String(entry.LogId?.KeyId ?? string.Empty).AsSpan().SequenceEqual(keyId))
        {
            throw Fail("entrada de um log de transparência não confiável");
        }

        var body = entry.CanonicalizedBody ?? throw Fail("entrada do log sem corpo");
        var bodyBytes = Convert.FromBase64String(body);
        var logIndex = ParseInt64(entry.LogIndex, "logIndex");
        var integratedSeconds = ParseInt64(entry.IntegratedTime, "integratedTime");
        var integratedTime = DateTimeOffset.FromUnixTimeSeconds(integratedSeconds);
        if (integratedTime < SigstoreTrustRoot.RekorValidFrom)
        {
            throw Fail("entrada do log anterior à validade da chave do Rekor");
        }

        using var rekor = ECDsa.Create();
        rekor.ImportSubjectPublicKeyInfo(rekorSpki, out _);

        // SET: o Rekor assina o JSON canônico {body, integratedTime, logID, logIndex}, com chaves em ordem alfabética.
        var promise = entry.InclusionPromise?.SignedEntryTimestamp ?? throw Fail("entrada do log sem SET");
        var canonical = string.Create(CultureInfo.InvariantCulture,
            $"{{\"body\":\"{body}\",\"integratedTime\":{integratedSeconds},\"logID\":\"{Convert.ToHexStringLower(keyId)}\",\"logIndex\":{logIndex}}}");
        if (!rekor.VerifyData(Encoding.UTF8.GetBytes(canonical), Convert.FromBase64String(promise),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
        {
            throw Fail("SET do Rekor inválido");
        }

        var proof = entry.InclusionProof ?? throw Fail("entrada do log sem prova de inclusão");
        var proofIndex = ParseInt64(proof.LogIndex, "inclusionProof.logIndex");
        var treeSize = ParseInt64(proof.TreeSize, "inclusionProof.treeSize");
        var rootHash = Convert.FromBase64String(proof.RootHash ?? throw Fail("prova sem rootHash"));
        var hashes = (proof.Hashes ?? []).Select(Convert.FromBase64String).ToList();
        var leafHash = SHA256.HashData([0, .. bodyBytes]);
        if (!MerkleRoot(proofIndex, treeSize, leafHash, hashes).AsSpan().SequenceEqual(rootHash))
        {
            throw Fail("prova de inclusão do Rekor inválida");
        }

        VerifyCheckpoint(proof.Checkpoint?.Envelope ?? throw Fail("prova sem checkpoint"), treeSize, rootHash, keyId, rekor);
        return integratedTime;
    }

    internal static byte[] MerkleRoot(long index, long size, byte[] leafHash, IReadOnlyList<byte[]> proof)
    {
        if (index < 0 || size <= 0 || index >= size)
        {
            throw Fail("índice fora da árvore do log");
        }

        // RFC 6962/9162: caminho interno até a borda e, depois, só irmãos à esquerda.
        var inner = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)(index ^ (size - 1)));
        var border = System.Numerics.BitOperations.PopCount((ulong)index >> inner);
        if (proof.Count != inner + border || proof.Any(hash => hash.Length != 32))
        {
            throw Fail("prova de inclusão com tamanho incorreto");
        }

        var hash = leafHash;
        for (var i = 0; i < inner; i++)
        {
            hash = ((index >> i) & 1) == 0 ? HashChildren(hash, proof[i]) : HashChildren(proof[i], hash);
        }

        for (var i = inner; i < proof.Count; i++)
        {
            hash = HashChildren(proof[i], hash);
        }

        return hash;
    }

    private static byte[] HashChildren(byte[] left, byte[] right) => SHA256.HashData([1, .. left, .. right]);

    private static void VerifyCheckpoint(string envelope, long treeSize, byte[] rootHash, byte[] keyId, ECDsa rekor)
    {
        var separator = envelope.IndexOf("\n\n", StringComparison.Ordinal);
        if (separator < 0)
        {
            throw Fail("checkpoint sem assinatura");
        }

        var note = envelope[..(separator + 1)];
        var lines = note.Split('\n');
        if (lines.Length < 4 || !lines[0].StartsWith(SigstoreTrustRoot.RekorCheckpointOrigin + " - ", StringComparison.Ordinal)
            || lines[1] != treeSize.ToString(CultureInfo.InvariantCulture) || lines[2] != Convert.ToBase64String(rootHash))
        {
            throw Fail("checkpoint não corresponde à prova de inclusão");
        }

        var noteBytes = Encoding.UTF8.GetBytes(note);
        foreach (var line in envelope[(separator + 2)..].Split('\n'))
        {
            var prefix = $"\u2014 {SigstoreTrustRoot.RekorCheckpointOrigin} ";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var signed = Convert.FromBase64String(line[prefix.Length..]);
            if (signed.Length > 4 && signed.AsSpan(0, 4).SequenceEqual(keyId.AsSpan(0, 4))
                && rekor.VerifyData(noteBytes, signed.AsSpan(4), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
            {
                return;
            }
        }

        throw Fail("checkpoint sem assinatura válida do Rekor");
    }

    private static void VerifyCertificate(X509Certificate2 leaf, DateTimeOffset integratedTime)
    {
        using var intermediate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(SigstoreTrustRoot.FulcioIntermediate));
        using var root = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(SigstoreTrustRoot.FulcioRoot));
        if (integratedTime < SigstoreTrustRoot.FulcioValidFrom)
        {
            throw Fail("certificado anterior à validade da CA Fulcio");
        }

        foreach (var certificate in new[] { leaf, intermediate, root })
        {
            if (integratedTime < certificate.NotBefore.ToUniversalTime() || integratedTime > certificate.NotAfter.ToUniversalTime())
            {
                throw Fail("certificado fora da validade no horário registrado no log");
            }
        }

        if (!leaf.IssuerName.RawData.AsSpan().SequenceEqual(intermediate.SubjectName.RawData))
        {
            throw Fail("certificado não emitido pela CA Fulcio");
        }

        using (var key = intermediate.GetECDsaPublicKey()!)
        {
            VerifyCertificateSignature(leaf.RawData, key);
        }

        using (var key = root.GetECDsaPublicKey()!)
        {
            VerifyCertificateSignature(intermediate.RawData, key);
        }

        var usage = leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (usage is null || !usage.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>().Any(oid => oid.Value == CodeSigningUsage))
        {
            throw Fail("certificado sem uso para assinatura de código");
        }

        VerifySct(leaf, intermediate);
    }

    private static void VerifyCertificateSignature(byte[] certificate, ECDsa issuerKey)
    {
        var reader = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence();
        var tbs = reader.ReadEncodedValue();
        var algorithm = reader.ReadSequence().ReadObjectIdentifier();
        var signature = reader.ReadBitString(out _);
        var hash = algorithm switch
        {
            "1.2.840.10045.4.3.2" => HashAlgorithmName.SHA256,
            "1.2.840.10045.4.3.3" => HashAlgorithmName.SHA384,
            _ => throw Fail($"algoritmo de assinatura do certificado não suportado ({algorithm})"),
        };
        if (!issuerKey.VerifyData(tbs.Span, signature, hash, DSASignatureFormat.Rfc3279DerSequence))
        {
            throw Fail("assinatura do certificado inválida");
        }
    }

    private static void VerifySct(X509Certificate2 leaf, X509Certificate2 issuer)
    {
        var extension = leaf.Extensions[ExtensionSct] ?? throw Fail("certificado sem SCT do log CT");
        var list = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadOctetString();
        var ctSpki = Convert.FromBase64String(SigstoreTrustRoot.CtLogPublicKey);
        var ctLogId = SHA256.HashData(ctSpki);
        using var ctKey = ECDsa.Create();
        ctKey.ImportSubjectPublicKeyInfo(ctSpki, out _);

        var tbs = PrecertificateTbs(leaf.RawData);
        var issuerKeyHash = SHA256.HashData(issuer.PublicKey.ExportSubjectPublicKeyInfo());
        var span = list.AsSpan();
        var total = BinaryPrimitives.ReadUInt16BigEndian(span);
        span = span.Slice(2, total);
        while (span.Length >= 2)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(span);
            var sct = span.Slice(2, length);
            span = span[(2 + length)..];
            if (sct.Length < 47 || sct[0] != 0 || !sct.Slice(1, 32).SequenceEqual(ctLogId))
            {
                continue;
            }

            var timestamp = sct.Slice(33, 8);
            var extensionsLength = BinaryPrimitives.ReadUInt16BigEndian(sct[41..]);
            var extensions = sct.Slice(43, extensionsLength);
            var rest = sct[(43 + extensionsLength)..];
            if (rest.Length < 4 || rest[0] != 4 || rest[1] != 3)
            {
                continue;
            }

            if (DateTimeOffset.FromUnixTimeMilliseconds((long)BinaryPrimitives.ReadUInt64BigEndian(timestamp)) < SigstoreTrustRoot.CtLogValidFrom)
            {
                continue;
            }

            var signature = rest.Slice(4, BinaryPrimitives.ReadUInt16BigEndian(rest[2..]));
            var signed = new List<byte>(tbs.Length + 64) { 0, 0 };
            signed.AddRange(timestamp.ToArray());
            signed.AddRange([0, 1]);
            signed.AddRange(issuerKeyHash);
            signed.AddRange([(byte)(tbs.Length >> 16), (byte)(tbs.Length >> 8), (byte)tbs.Length]);
            signed.AddRange(tbs);
            signed.AddRange([(byte)(extensions.Length >> 8), (byte)extensions.Length]);
            signed.AddRange(extensions.ToArray());
            if (ctKey.VerifyData(signed.ToArray(), signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
            {
                return;
            }
        }

        throw Fail("certificado sem SCT válido do log CT");
    }

    /// <summary>TBSCertificate sem a extensão de SCTs, como foi assinado pelo log CT no pré-certificado.</summary>
    private static byte[] PrecertificateTbs(byte[] certificate)
    {
        var tbs = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence().ReadSequence();
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            var extensionsTag = new Asn1Tag(TagClass.ContextSpecific, 3, isConstructed: true);
            while (tbs.HasData)
            {
                if (!tbs.PeekTag().HasSameClassAndValue(extensionsTag))
                {
                    writer.WriteEncodedValue(tbs.ReadEncodedValue().Span);
                    continue;
                }

                var extensions = tbs.ReadSequence(extensionsTag).ReadSequence();
                using (writer.PushSequence(extensionsTag))
                using (writer.PushSequence())
                {
                    while (extensions.HasData)
                    {
                        var encoded = extensions.ReadEncodedValue();
                        var oid = new AsnReader(encoded, AsnEncodingRules.DER).ReadSequence().ReadObjectIdentifier();
                        if (oid != ExtensionSct)
                        {
                            writer.WriteEncodedValue(encoded.Span);
                        }
                    }
                }
            }
        }

        return writer.Encode();
    }

    private static (string Signer, string SourceRef) VerifyIdentity(X509Certificate2 leaf, ProvenancePolicy policy)
    {
        Require(leaf, ExtensionIssuer, policy.Issuer, "emissor OIDC");
        Require(leaf, ExtensionSourceRepositoryUri, policy.SourceRepositoryUri, "repositório");
        Require(leaf, ExtensionSourceRepositoryId, policy.SourceRepositoryId, "identificador do repositório");
        Require(leaf, ExtensionSourceRepositoryOwnerId, policy.SourceRepositoryOwnerId, "identificador do dono");
        Require(leaf, ExtensionRunnerEnvironment, "github-hosted", "ambiente do runner");

        var sourceRef = ReadUtf8Extension(leaf, ExtensionSourceRepositoryRef);
        if (sourceRef is null || !policy.IsAllowedRef(sourceRef))
        {
            throw Fail($"referência de origem não autorizada ({sourceRef})");
        }

        var signer = SubjectAlternativeNameUris(leaf).SingleOrDefault()
            ?? throw Fail("certificado sem identidade de workflow");
        if (!policy.IsAllowedSigner(signer))
        {
            throw Fail($"workflow signatário não autorizado ({signer})");
        }

        return (signer, sourceRef);
    }

    private static void Require(X509Certificate2 leaf, string oid, string expected, string name)
    {
        var value = ReadUtf8Extension(leaf, oid);
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw Fail($"{name} do certificado não autorizado ({value})");
        }
    }

    private static string? ReadUtf8Extension(X509Certificate2 certificate, string oid) =>
        certificate.Extensions[oid] is { } extension
            ? new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadCharacterString(UniversalTagNumber.UTF8String)
            : null;

    private static List<string> SubjectAlternativeNameUris(X509Certificate2 certificate)
    {
        var uris = new List<string>();
        if (certificate.Extensions[ExtensionSubjectAlternativeName] is not { } extension)
        {
            return uris;
        }

        var names = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        var uriTag = new Asn1Tag(TagClass.ContextSpecific, 6);
        while (names.HasData)
        {
            if (names.PeekTag().HasSameClassAndValue(uriTag))
            {
                uris.Add(names.ReadCharacterString(UniversalTagNumber.IA5String, uriTag));
            }
            else
            {
                names.ReadEncodedValue();
            }
        }

        return uris;
    }

    private static void VerifyLogBody(TlogEntry entry, byte[] payload, string signature, byte[] leafDer)
    {
        var body = JsonSerializer.Deserialize(Convert.FromBase64String(entry.CanonicalizedBody!),
            SigstoreJsonContext.Default.RekorDsseBody) ?? throw Fail("corpo do log vazio");
        if (body is not { Kind: "dsse", ApiVersion: "0.0.1", Spec: { } spec })
        {
            throw Fail("corpo do log não é uma entrada dsse 0.0.1");
        }

        if (spec.PayloadHash is not { Algorithm: "sha256", Value: { } payloadHash }
            || !string.Equals(payloadHash, Convert.ToHexStringLower(SHA256.HashData(payload)), StringComparison.Ordinal))
        {
            throw Fail("o log registra outro payload");
        }

        if (spec.Signatures is not [{ Signature: { } logged, Verifier: { } verifier }] || logged != signature)
        {
            throw Fail("o log registra outra assinatura");
        }

        var pem = Encoding.UTF8.GetString(Convert.FromBase64String(verifier));
        var fields = PemEncoding.Find(pem);
        if (pem[fields.Label] != "CERTIFICATE" || !Convert.FromBase64String(pem[fields.Base64Data]).AsSpan().SequenceEqual(leafDer))
        {
            throw Fail("o log registra outro certificado");
        }
    }

    private static HashAlgorithmName HashFor(ECDsa key) => key.KeySize switch
    {
        256 => HashAlgorithmName.SHA256,
        384 => HashAlgorithmName.SHA384,
        _ => throw Fail($"curva ECDSA de {key.KeySize} bits não suportada"),
    };

    private static long ParseInt64(string? value, string name) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw Fail($"{name} inválido");

    private static SigstoreVerificationException Fail(string message) => new(message);
}
