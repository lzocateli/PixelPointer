using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using PixelPointer.App.Updates;
using PixelPointer.Core.Updates;
using PixelPointer.Core.Updates.Sigstore;

namespace PixelPointer.UnitTests;

/// <summary>Atualizador contra filesystem real em diretórios temporários e HTTP simulado em memória.</summary>
public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pixelpointer-update-test-").FullName;

    // Pai da instalação simulada: só a instalação e o staging do atualizador vivem aqui.
    private string Parent => Path.Combine(_root, "lib");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void PreferencesStoreStartsWithDefaultsAndPersistsChanges()
    {
        var path = Path.Combine(_root, "config", "update.json");

        Assert.Equal(UpdatePreferences.Default, UpdatePreferencesStore.Load(path).Preferences);
        UpdatePreferencesStore.Update(path, current => current.WithCheckAutomatically(true));

        var loaded = UpdatePreferencesStore.Load(path);
        Assert.Null(loaded.Error);
        Assert.True(loaded.Preferences.CheckAutomatically);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void InvalidPreferencesFileIsNeverOverwritten()
    {
        var path = Path.Combine(_root, "update.json");
        File.WriteAllText(path, "{ editado à mão");

        Assert.NotNull(UpdatePreferencesStore.Load(path).Error);
        Assert.Throws<InvalidOperationException>(() => UpdatePreferencesStore.Update(path, current => current.WithCheckAutomatically(true)));
        Assert.Equal("{ editado à mão", File.ReadAllText(path));
    }

    [Fact]
    public void ConcurrentChangeIsReportedWithoutOverwriting()
    {
        var path = Path.Combine(_root, "update.json");
        UpdatePreferencesStore.Update(path, current => current.WithCheckAutomatically(true));
        var concurrent = new UpdatePreferences(CheckAutomatically: false, LastFailure: "outro processo").Serialize();

        Assert.Throws<IOException>(() => UpdatePreferencesStore.Update(path, current =>
        {
            File.WriteAllText(path, concurrent);
            return current.WithCheckAutomatically(false);
        }));

        Assert.Equal(concurrent, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void ReceiptInWritableUserDirectoryAllowsSelfUpdate()
    {
        var app = CreateInstall("0.3.1");

        var origin = InstallOriginDetector.Detect(app, "linux-x64", isPrivileged: false);

        Assert.True(origin.CanSelfUpdate, origin.Detail);
        Assert.Empty(Directory.GetFiles(app, ".pixelpointer.write-probe.*"));
    }

    [Fact]
    public void DirectoryWithoutReceiptIsUnknown()
    {
        var app = CreateInstall("0.3.1");
        File.Delete(Path.Combine(app, InstallReceipt.FileName));

        Assert.Equal(InstallChannel.Unknown, InstallOriginDetector.Detect(app, "linux-x64", false).Channel);
    }

    [Fact]
    public void PrivilegedProcessNeverSelfUpdates() =>
        Assert.False(InstallOriginDetector.Detect(CreateInstall("0.3.1"), "linux-x64", isPrivileged: true).CanSelfUpdate);

    [Fact]
    public void GroupWritableInstallationOnlyNotifies()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Modo Unix.");
        var app = CreateInstall("0.3.1");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(app, File.GetUnixFileMode(app) | UnixFileMode.GroupWrite);
        }

        var origin = InstallOriginDetector.Detect(app, "linux-x64", false);

        Assert.False(origin.CanSelfUpdate);
        Assert.Contains("grupo", origin.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SymlinkedApplicationDirectoryIsUnknown()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Links simbólicos exigem privilégio no Windows.");
        var app = CreateInstall("0.3.1");
        var link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, app);

        Assert.Equal(InstallChannel.Unknown, InstallOriginDetector.Detect(link, "linux-x64", false).Channel);
    }

    [Fact]
    public void ExtractorCopiesOnlyExpectedRegularFiles()
    {
        var layout = PackageLayout.For("linux-x64");
        var archive = CreateTarGz(layout.Files.Select(name => (name, "conteúdo " + name))
            .Append(("../fora", "escape")).Append(("extra/arquivo", "ignorado")));
        var destination = Path.Combine(_root, "out");

        PackageExtractor.Extract(archive, layout, destination);

        Assert.Equal(layout.Files.Order(StringComparer.Ordinal), Directory.GetFiles(destination).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(_root, "fora")));
        Assert.Equal("conteúdo libSDL3.so", File.ReadAllText(Path.Combine(destination, "libSDL3.so")));
    }

    [Fact]
    public void ExtractorRejectsLinkWithExpectedName()
    {
        var layout = PackageLayout.For("linux-x64");
        var path = Path.Combine(_root, "link.tar.gz");
        using (var file = File.Create(path))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var tar = new TarWriter(gzip))
        {
            foreach (var name in layout.Files.Where(name => name != "libSDL3.so"))
            {
                WriteEntry(tar, name, "x");
            }

            tar.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "libSDL3.so") { LinkName = "/etc/passwd" });
        }

        Assert.Throws<InvalidDataException>(() => PackageExtractor.Extract(path, layout, Path.Combine(_root, "out")));
    }

    [Fact]
    public void ExtractorRejectsMissingAndDuplicatedFiles()
    {
        var layout = PackageLayout.For("win-x64");
        var missing = CreateZip(layout.Files.Skip(1).Select(name => (name, "x")));
        var duplicated = CreateZip(layout.Files.Append(layout.Files[0]).Select(name => (name, "x")), "dup.zip");

        Assert.Throws<InvalidDataException>(() => PackageExtractor.Extract(missing, layout, Path.Combine(_root, "a")));
        Assert.Throws<InvalidDataException>(() => PackageExtractor.Extract(duplicated, layout, Path.Combine(_root, "b")));
    }

    [Fact]
    public async Task ChecksumMismatchLeavesTheInstallationUntouched()
    {
        var app = CreateInstall("0.3.1");
        var package = File.ReadAllBytes(CreateTarGz(PackageLayout.For("linux-x64").Files.Select(name => (name, "x"))));
        using var client = Client(package, sums: $"{new string('0', 64)}  pixelpointer-linux-x64.tar.gz\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.StageAsync(client, app, "linux-x64",
            Candidate(package.Length), _ => { }, TestContext.Current.CancellationToken));

        AssertOnlyInstallRemains(app, "0.3.1");
    }

    [Fact]
    public async Task PackageWithoutAcceptedProvenanceIsNeverExtractedOrExecuted()
    {
        var app = CreateInstall("0.3.1");
        var package = File.ReadAllBytes(CreateTarGz(PackageLayout.For("linux-x64").Files.Select(name => (name, "x"))));
        var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(package));
        var attestations = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "attestations-v0.3.1-linux-x64.json"));
        using var client = Client(package, sums: $"{digest}  pixelpointer-linux-x64.tar.gz\n", attestations: attestations);

        await Assert.ThrowsAsync<SigstoreVerificationException>(() => UpdateInstaller.StageAsync(client, app, "linux-x64",
            Candidate(package.Length), _ => { }, TestContext.Current.CancellationToken));

        AssertOnlyInstallRemains(app, "0.3.1");
    }

    [Fact]
    public async Task SizeDifferentFromTheReleaseIsRejected()
    {
        var app = CreateInstall("0.3.1");
        var package = new byte[100];
        var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(package));
        using var client = Client(package, sums: $"{digest}  pixelpointer-linux-x64.tar.gz\n");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.StageAsync(client, app, "linux-x64",
            Candidate(99), _ => { }, TestContext.Current.CancellationToken));

        Assert.Contains("a release publica 99", error.Message, StringComparison.Ordinal);

        AssertOnlyInstallRemains(app, "0.3.1");
    }

    [Fact]
    public async Task RateLimitIsReportedAsActionableError()
    {
        using var client = new UpdateClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)),
            new Uri("https://api.example/"), new Uri("https://download.example/"));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestReleaseAsync(TestContext.Current.CancellationToken));

        Assert.Contains("tente mais tarde", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySwapsToTheStagedVersionAndRemovesTheStage()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Troca no próprio processo é do Linux e do macOS.");
        var app = CreateInstall("0.3.1");
        Stage(app, "0.4.0");

        var applied = UpdateInstaller.ApplyInProcess(app, "linux-x64");

        Assert.Equal(SemanticVersion.Parse("0.4.0"), applied);
        AssertOnlyInstallRemains(app, "0.4.0");
        Assert.True(File.Exists(Path.Combine(app, InstallReceipt.FileName)));
    }

    [Fact]
    public void FailedVerificationAfterSwapRestoresThePreviousVersion()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Troca no próprio processo é do Linux e do macOS.");
        var app = CreateInstall("0.3.1");
        // Responde 0.4.0 só enquanto está no staging; depois da troca falha, simulando uma instalação corrompida.
        Stage(app, "0.4.0", script: """
            #!/bin/sh
            case "$0" in */.pixelpointer.update/*) echo 0.4.0 ;; *) echo quebrado; exit 3 ;; esac
            """);

        Assert.ThrowsAny<Exception>(() => UpdateInstaller.ApplyInProcess(app, "linux-x64"));

        Assert.Equal("0.3.1", RunVersion(app));
        Assert.NotNull(UpdateInstaller.FindStaged(app));
    }

    [Fact]
    public void StagedVersionNotNewerIsDiscarded()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Troca no próprio processo é do Linux e do macOS.");
        var app = CreateInstall("0.4.0");
        Stage(app, "0.3.9");

        var error = Assert.Throws<InvalidOperationException>(() => UpdateInstaller.ApplyInProcess(app, "linux-x64"));

        Assert.Contains("descartada", error.Message, StringComparison.Ordinal);
        AssertOnlyInstallRemains(app, "0.4.0");
    }

    [Fact]
    public void CleanupRemovesLeftoversAndInstalledStages()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Usa scripts de shell como executável.");
        var app = CreateInstall("0.4.0");
        Stage(app, "0.4.0");
        Directory.CreateDirectory(Path.Combine(Parent, UpdateInstaller.TemporaryPrefix + "abc"));
        Directory.CreateDirectory(Path.Combine(Parent, UpdateInstaller.PreviousPrefix + "123"));
        Directory.CreateDirectory(Path.Combine(Parent, "outro-aplicativo"));

        UpdateInstaller.Cleanup(app, SemanticVersion.Parse("0.4.0"));

        Assert.Equal(["outro-aplicativo", "pixelpointer"], Directory.GetDirectories(Parent).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CleanupKeepsANewerStagedUpdate()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Usa scripts de shell como executável.");
        var app = CreateInstall("0.3.1");
        Stage(app, "0.4.0");

        UpdateInstaller.Cleanup(app, SemanticVersion.Parse("0.3.1"));

        Assert.Equal(SemanticVersion.Parse("0.4.0"), UpdateInstaller.FindStaged(app)?.Version);
    }

    private string CreateInstall(string version)
    {
        var app = Path.Combine(Parent, "pixelpointer");
        Directory.CreateDirectory(app);
        WriteApplication(app, version);
        return app;
    }

    private static void WriteApplication(string directory, string version, string? script = null)
    {
        foreach (var name in PackageLayout.For("linux-x64").Files)
        {
            File.WriteAllText(Path.Combine(directory, name), name);
        }

        var executable = Path.Combine(directory, "pixelpointer");
        File.WriteAllText(executable, (script ?? $"#!/bin/sh\necho {version}+abc\n").Replace("\r\n", "\n", StringComparison.Ordinal));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, (UnixFileMode)0b111_101_101);
            File.SetUnixFileMode(directory, (UnixFileMode)0b111_101_101);
        }

        File.WriteAllText(Path.Combine(directory, InstallReceipt.FileName),
            """{"version": 1, "installer": "install.sh", "scope": "user", "rid": "linux-x64"}""");
    }

    private void Stage(string app, string version, string? script = null)
    {
        var root = UpdateInstaller.StageRoot(app);
        var staged = Path.Combine(root, UpdateInstaller.ApplicationDirectoryName);
        Directory.CreateDirectory(staged);
        WriteApplication(staged, version, script);
        File.WriteAllText(Path.Combine(root, StagedUpdateMarker.FileName),
            new StagedUpdateMarker(SemanticVersion.Parse(version), "linux-x64", "pixelpointer", new string('d', 64)).Serialize());
    }

    private static string RunVersion(string app) =>
        UpdateInstaller.ReadVersion(Path.Combine(app, "pixelpointer")).ToString();

    private void AssertOnlyInstallRemains(string app, string version)
    {
        Assert.Equal(["pixelpointer"], Directory.GetFileSystemEntries(Parent).Select(Path.GetFileName));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(version, RunVersion(app));
        }
    }

    private static UpdateCandidate Candidate(long size) =>
        new(UpdateAvailability.Available, SemanticVersion.Parse("0.3.1"), SemanticVersion.Parse("0.4.0"),
            "pixelpointer-linux-x64.tar.gz", size, "teste");

    private static UpdateClient Client(byte[] package, string sums, string attestations = """{"attestations":[]}""") =>
        new(new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/v0.4.0/SHA256SUMS" => Text(sums),
            "/v0.4.0/pixelpointer-linux-x64.tar.gz" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) },
            var path when path.StartsWith("/attestations/sha256:", StringComparison.Ordinal) => Text(attestations),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        }), new Uri("https://api.example/"), new Uri("https://download.example/"));

    private static HttpResponseMessage Text(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8) };

    private string CreateTarGz(IEnumerable<(string Name, string Content)> entries)
    {
        var path = Path.Combine(_root, $"package-{Guid.NewGuid():N}.tar.gz");
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        using var tar = new TarWriter(gzip);
        foreach (var (name, content) in entries)
        {
            WriteEntry(tar, name, content);
        }

        return path;
    }

    private static void WriteEntry(TarWriter tar, string name, string content) =>
        tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) });

    private string CreateZip(IEnumerable<(string Name, string Content)> entries, string name = "package.zip")
    {
        var path = Path.Combine(_root, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }

        return path;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
