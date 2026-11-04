using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using PixelPointer.Core.Updates;
using PixelPointer.Core.Updates.Sigstore;

namespace PixelPointer.App.Updates;

/// <summary>
/// Acesso HTTPS às releases do GitHub. A resposta de releases/latest só indica a tag; o pacote vem do caminho
/// imutável da tag e só é aceito com SHA256SUMS e atestação Sigstore verificados. Nenhum identificador pessoal ou
/// telemetria é enviado: somente um User-Agent fixo exigido pela API.
/// </summary>
internal sealed class UpdateClient : IDisposable
{
    internal static readonly Uri ApiBase = new("https://api.github.com/repos/lzocateli/PixelPointer/");
    internal static readonly Uri DownloadBase = new("https://github.com/lzocateli/PixelPointer/releases/download/");

    private readonly HttpClient _http;
    private readonly Uri _apiBase;
    private readonly Uri _downloadBase;

    internal UpdateClient()
        : this(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(20),
        }, ApiBase, DownloadBase)
    {
    }

    internal UpdateClient(HttpMessageHandler handler, Uri apiBase, Uri downloadBase)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PixelPointer-Updater", "1"));
        _apiBase = apiBase;
        _downloadBase = downloadBase;
    }

    public void Dispose() => _http.Dispose();

    internal async Task<PublishedRelease> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        var json = await GetTextAsync(new Uri(_apiBase, "releases/latest"), PublishedRelease.MaxBytes, api: true, cancellationToken)
            .ConfigureAwait(false);
        return PublishedRelease.Parse(json);
    }

    internal Task<string> GetChecksumsAsync(SemanticVersion version, CancellationToken cancellationToken) =>
        GetTextAsync(new Uri(_downloadBase, $"v{version}/SHA256SUMS"), Sha256Sums.MaxBytes, api: false, cancellationToken);

    internal Task<string> GetAttestationsAsync(string sha256, CancellationToken cancellationToken) =>
        GetTextAsync(new Uri(_apiBase, $"attestations/sha256:{sha256}"), SigstoreVerifier.MaxResponseBytes, api: true, cancellationToken);

    /// <summary>Baixa o pacote para <paramref name="destination"/> e devolve o SHA-256; o tamanho precisa ser o publicado.</summary>
    internal async Task<string> DownloadAsync(
        SemanticVersion version, string assetName, long expectedSize, string destination, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(new Uri(_downloadBase, $"v{version}/{assetName}"), api: false, cancellationToken)
            .ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is { } length && length != expectedSize)
        {
            throw new InvalidDataException($"{assetName} tem {length} bytes; a release publica {expectedSize}.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > expectedSize)
            {
                throw new InvalidDataException($"{assetName} excede o tamanho publicado na release.");
            }

            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return total == expectedSize
            ? Convert.ToHexStringLower(hash.GetHashAndReset())
            : throw new InvalidDataException($"Download de {assetName} incompleto ({total} de {expectedSize} bytes).");
    }

    private async Task<string> GetTextAsync(Uri uri, int maxBytes, bool api, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(uri, api, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[maxBytes + 1];
        var length = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        if (length > maxBytes)
        {
            throw new InvalidDataException($"Resposta de {uri.Host} maior que o limite aceito.");
        }

        return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, 0, length);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, bool api, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (api)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.RequestMessage?.RequestUri is { } final && final.Scheme != uri.Scheme)
        {
            response.Dispose();
            throw new HttpRequestException($"Redirecionamento recusado para {final.Scheme}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new HttpRequestException(status is 403 or 429
                ? $"{uri.Host} limitou as consultas (HTTP {status}); tente mais tarde."
                : $"{uri.Host} respondeu HTTP {status}.");
        }

        return response;
    }
}
