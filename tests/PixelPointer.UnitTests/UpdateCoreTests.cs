using PixelPointer.Core.Updates;

namespace PixelPointer.UnitTests;

public sealed class UpdateCoreTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, "")]
    [InlineData("0.3.1+5e4b296abc", 0, 3, 1, "")]
    [InlineData("0.3.2-rc.1", 0, 3, 2, "rc.1")]
    [InlineData("10.20.30-alpha.beta-1+build.7", 10, 20, 30, "alpha.beta-1")]
    public void SemanticVersionParsesSemVer(string text, int major, int minor, int patch, string prerelease) =>
        Assert.Equal(new SemanticVersion(major, minor, patch, prerelease), SemanticVersion.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-a..b")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3;id")]
    [InlineData("9999999999.0.0")]
    public void SemanticVersionRejectsInvalidText(string text) => Assert.False(SemanticVersion.TryParse(text, out _));

    [Theory]
    [InlineData("0.3.2-rc.1", "0.3.2")]
    [InlineData("0.3.1", "0.3.2-rc.1")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.9.9", "1.10.0")]
    public void SemanticVersionOrdersByPrecedence(string lower, string higher)
    {
        Assert.True(SemanticVersion.Parse(lower) < SemanticVersion.Parse(higher));
        Assert.True(SemanticVersion.Parse(higher) > SemanticVersion.Parse(lower));
    }

    [Fact]
    public void BuildMetadataDoesNotChangeEquality() =>
        Assert.Equal(SemanticVersion.Parse("0.4.0"), SemanticVersion.Parse("0.4.0+abc"));

    [Fact]
    public void PreferencesStartDisabled()
    {
        var preferences = UpdatePreferences.Parse("""{"version": 1}""");

        Assert.False(preferences.CheckAutomatically);
        Assert.False(preferences.EffectiveInstallAutomatically);
        Assert.Equal(UpdatePreferences.Default, preferences);
    }

    [Fact]
    public void InstallWithoutCheckIsIgnoredWhenReading() =>
        Assert.False(UpdatePreferences.Parse("""{"version": 1, "installAutomatically": true}""").EffectiveInstallAutomatically);

    [Fact]
    public void DisablingCheckAlsoDisablesInstall()
    {
        var enabled = UpdatePreferences.Default.WithCheckAutomatically(true).WithInstallAutomatically(true);

        var disabled = enabled.WithCheckAutomatically(false);

        Assert.True(enabled.EffectiveInstallAutomatically);
        Assert.False(disabled.InstallAutomatically);
        Assert.False(disabled.WithCheckAutomatically(true).InstallAutomatically);
    }

    [Fact]
    public void InstallCannotBeEnabledWhileCheckIsOff() =>
        Assert.Throws<InvalidOperationException>(() => UpdatePreferences.Default.WithInstallAutomatically(true));

    [Fact]
    public void PreferencesRoundTrip()
    {
        var preferences = new UpdatePreferences(true, true, new DateTimeOffset(2026, 11, 4, 21, 0, 0, TimeSpan.Zero), "0.4.0", "falha\nem linha");

        var parsed = UpdatePreferences.Parse(preferences.Serialize());

        Assert.Equal(preferences with { LastFailure = "falha em linha" }, parsed);
    }

    [Theory]
    [InlineData("""{"version": 2}""")]
    [InlineData("""{}""")]
    [InlineData("""{"version": 1, "token": "x"}""")]
    [InlineData("""{"version": 1, "appliedVersion": "latest"}""")]
    [InlineData("""[]""")]
    public void InvalidPreferencesAreRejected(string json) =>
        Assert.Throws<InvalidDataException>(() => UpdatePreferences.Parse(json));

    [Fact]
    public void FirstAutomaticCheckWaitsFiveMinutesAfterStart()
    {
        var start = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(start.AddMinutes(5), UpdateSchedule.NextCheck(start, null, start));
        Assert.Equal(start.AddMinutes(5), UpdateSchedule.NextCheck(start, start.AddDays(-3), start));
    }

    [Fact]
    public void AutomaticChecksRepeatEvery24Hours()
    {
        var start = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var last = start.AddHours(-1);

        Assert.Equal(last.AddHours(24), UpdateSchedule.NextCheck(start, last, start));
    }

    [Fact]
    public void LastCheckInTheFutureIsIgnored()
    {
        var start = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(start.AddMinutes(5), UpdateSchedule.NextCheck(start, start.AddDays(30), start));
    }

    private const string Release = """
        {"tag_name": "v0.4.0", "draft": false, "prerelease": false, "body": "ignorado",
         "assets": [{"name": "SHA256SUMS", "size": 1503}, {"name": "pixelpointer-linux-x64.tar.gz", "size": 8787898},
                    {"name": "pixelpointer-win-x64.zip", "size": 8549826}]}
        """;

    [Fact]
    public void ReleaseWithPackageForTheRidIsAvailable()
    {
        var candidate = PublishedRelease.Parse(Release).Evaluate(SemanticVersion.Parse("0.3.1"), "linux-x64");

        Assert.Equal(UpdateAvailability.Available, candidate.Availability);
        Assert.Equal("pixelpointer-linux-x64.tar.gz", candidate.AssetName);
        Assert.Equal(8787898, candidate.AssetSize);
    }

    [Fact]
    public void ReleaseWithoutPackageForTheRidIsIncompatible() =>
        Assert.Equal(UpdateAvailability.Incompatible,
            PublishedRelease.Parse(Release).Evaluate(SemanticVersion.Parse("0.3.1"), "osx-arm64").Availability);

    [Theory]
    [InlineData("0.4.0")]
    [InlineData("0.4.1-rc.1")]
    [InlineData("1.0.0")]
    public void SameOrNewerInstalledVersionIsUpToDate(string installed) =>
        Assert.Equal(UpdateAvailability.UpToDate,
            PublishedRelease.Parse(Release).Evaluate(SemanticVersion.Parse(installed), "linux-x64").Availability);

    [Fact]
    public void StableReleaseUpdatesAPreviewOfTheSameVersion() =>
        Assert.Equal(UpdateAvailability.Available,
            PublishedRelease.Parse(Release).Evaluate(SemanticVersion.Parse("0.4.0-rc.1"), "win-x64").Availability);

    [Theory]
    [InlineData("""{"tag_name": "v0.4.0-rc.1", "prerelease": false}""")]
    [InlineData("""{"tag_name": "v0.4.0", "prerelease": true}""")]
    [InlineData("""{"tag_name": "v0.4.0", "draft": true}""")]
    [InlineData("""{"tag_name": "0.4.0"}""")]
    [InlineData("""{"tag_name": "v0.4.0+meta"}""")]
    [InlineData("""{"name": "sem tag"}""")]
    [InlineData("""not json""")]
    public void OnlyStablePublishedReleasesAreAccepted(string json) =>
        Assert.Throws<InvalidDataException>(() => PublishedRelease.Parse(json));

    [Fact]
    public void ChecksumsReturnTheSingleEntry()
    {
        var digest = new string('a', 64);

        Assert.Equal(digest, Sha256Sums.FindDigest($"{new string('b', 64)}  other.zip\r\n{digest}  pixelpointer-linux-x64.tar.gz\n",
            "pixelpointer-linux-x64.tar.gz"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("aaaa  pixelpointer-linux-x64.tar.gz")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA  pixelpointer-linux-x64.tar.gz")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa pixelpointer-linux-x64.tar.gz")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  pixelpointer-linux-x64.tar.gz\nbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb  pixelpointer-linux-x64.tar.gz")]
    public void ChecksumsWithoutASingleValidEntryAreRejected(string content) =>
        Assert.Throws<InvalidDataException>(() => Sha256Sums.FindDigest(content, "pixelpointer-linux-x64.tar.gz"));

    [Theory]
    [InlineData("linux-arm64", "pixelpointer-linux-arm64.tar.gz", "libSkiaSharp.so")]
    [InlineData("osx-x64", "pixelpointer-osx-x64.tar.gz", "libPixelPointerScreenCaptureKit.dylib")]
    [InlineData("win-arm64", "pixelpointer-win-arm64.zip", "SDL3.dll")]
    public void PackageLayoutMatchesTheInstallers(string rid, string asset, string library)
    {
        var layout = PackageLayout.For(rid);

        Assert.Equal(asset, layout.AssetName);
        Assert.Contains(library, layout.Files);
        Assert.Contains(layout.Executable, layout.Files);
        Assert.Contains("THIRD-PARTY-NOTICES.md", layout.Files);
    }

    [Fact]
    public void ReceiptWrittenByTheInstallersIsAccepted()
    {
        var receipt = InstallReceipt.Parse("""{"version": 1, "installer": "install.sh", "scope": "user", "rid": "linux-x64"}""");

        Assert.Equal(new InstallReceipt("install.sh", "user", "linux-x64"), receipt);
    }

    [Theory]
    [InlineData("""{"version": 2, "installer": "install.sh", "scope": "user", "rid": "linux-x64"}""")]
    [InlineData("""{"version": 1, "installer": "brew", "scope": "user", "rid": "linux-x64"}""")]
    [InlineData("""{"version": 1, "installer": "install.sh", "scope": "system", "rid": "linux-x64"}""")]
    [InlineData("""{"version": 1, "installer": "install.sh", "scope": "user", "rid": "linux-riscv64"}""")]
    [InlineData("""{"version": 1""")]
    public void InvalidReceiptsAreRejected(string json) =>
        Assert.Throws<InvalidDataException>(() => InstallReceipt.Parse(json));

    private static InstallFacts UserInstall(string directory = "/home/ana/.local/lib/pixelpointer", string rid = "linux-x64") =>
        new(rid, directory, new InstallReceipt("install.sh", "user", rid), null, false, true, true, false, false);

    [Fact]
    public void UserInstallationCanSelfUpdate()
    {
        var origin = InstallOrigin.Classify(UserInstall());

        Assert.Equal(InstallChannel.UserInstaller, origin.Channel);
        Assert.True(origin.CanSelfUpdate);
        Assert.Empty(origin.Guidance);
    }

    [Theory]
    [InlineData("privileged", "privilégios elevados")]
    [InlineData("readonly", "sem permissão de escrita")]
    [InlineData("parent", "sem permissão de escrita")]
    [InlineData("shared", "grupo ou outros")]
    public void UserInstallationWithoutSafeWriteAccessOnlyNotifies(string fact, string detail)
    {
        var facts = UserInstall();
        facts = fact switch
        {
            "privileged" => facts with { IsPrivileged = true },
            "readonly" => facts with { AppDirectoryWritable = false },
            "parent" => facts with { ParentWritable = false },
            _ => facts with { SharedWritable = true },
        };

        var origin = InstallOrigin.Classify(facts);

        Assert.False(origin.CanSelfUpdate);
        Assert.Contains(detail, origin.Detail, StringComparison.Ordinal);
        Assert.NotEmpty(origin.Guidance);
    }

    [Theory]
    [InlineData("/usr/lib/pixelpointer", "linux-x64", false, InstallChannel.Deb, "apt install")]
    [InlineData("/opt/custom/pixelpointer", "linux-x64", true, InstallChannel.Deb, "apt install")]
    [InlineData("/opt/homebrew/Cellar/pixelpointer/0.3.1/libexec", "osx-arm64", false, InstallChannel.Homebrew, "brew upgrade")]
    [InlineData("/home/linuxbrew/.linuxbrew/Cellar/pixelpointer/0.3.1/libexec", "linux-x64", false, InstallChannel.Homebrew, "brew upgrade")]
    [InlineData(@"C:\Users\ana\AppData\Local\Microsoft\WinGet\Packages\lzocateli.PixelPointer_Microsoft.Winget.Source_8wekyb3d8bbwe", "win-x64", false, InstallChannel.Winget, "winget upgrade")]
    public void PackageManagersAreNeverReplaced(string directory, string rid, bool dpkg, InstallChannel channel, string guidance)
    {
        // Mesmo com um recibo copiado para o diretório, o gerenciador prevalece.
        var origin = InstallOrigin.Classify(UserInstall(directory, rid) with { DpkgOwned = dpkg });

        Assert.Equal(channel, origin.Channel);
        Assert.False(origin.CanSelfUpdate);
        Assert.Contains(guidance, origin.Guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalInstallerOnlyNotifies()
    {
        var origin = InstallOrigin.Classify(UserInstall("/usr/local/lib/pixelpointer") with
        {
            Receipt = new InstallReceipt("install.sh", "global", "linux-x64"),
        });

        Assert.Equal(InstallChannel.GlobalInstaller, origin.Channel);
        Assert.False(origin.CanSelfUpdate);
        Assert.Contains("sudo", origin.Guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingReceiptIsUnknownOrigin()
    {
        var origin = InstallOrigin.Classify(UserInstall() with { Receipt = null });

        Assert.Equal(InstallChannel.Unknown, origin.Channel);
        Assert.False(origin.CanSelfUpdate);
    }

    [Fact]
    public void ReceiptForAnotherRidIsUnknownOrigin() =>
        Assert.Equal(InstallChannel.Unknown, InstallOrigin.Classify(UserInstall() with
        {
            Receipt = new InstallReceipt("install.sh", "user", "linux-arm64"),
        }).Channel);

    [Fact]
    public void MarkerRoundTrips()
    {
        var marker = new StagedUpdateMarker(SemanticVersion.Parse("0.4.0"), "linux-x64", "pixelpointer", new string('c', 64));

        Assert.Equal(marker, StagedUpdateMarker.Parse(marker.Serialize()));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../pixelpointer")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("C:")]
    [InlineData("")]
    public void MarkerTargetMustBeASinglePathComponent(string target) =>
        Assert.Throws<InvalidDataException>(() => StagedUpdateMarker.Parse(
            $$"""{"version": 1, "update": "0.4.0", "rid": "linux-x64", "target": "{{target.Replace(@"\", @"\\", StringComparison.Ordinal)}}", "sha256": "{{new string('c', 64)}}"}"""));
}
