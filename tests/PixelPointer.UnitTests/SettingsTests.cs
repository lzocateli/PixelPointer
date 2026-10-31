using PixelPointer.App;
using PixelPointer.Core.Annotations;
using PixelPointer.Core.Settings;

namespace PixelPointer.UnitTests;

public sealed class SettingsTests
{
    [Fact]
    public void Parse_EmptyObjectUsesDefaults() => Assert.Equal(AppSettings.Default, AppSettings.Parse("{}"));

    [Fact]
    public void Parse_ReadsAllFieldsWithCommentsAndTrailingCommas()
    {
        var settings = AppSettings.Parse("""
            {
              // preferências de apresentação
              "version": 1,
              "pen": { "color": "blue", "width": 12.5, },
              "zoom": { "initial": 3 },
            }
            """);

        Assert.Equal(new AppSettings(PenColor.Blue, 12.5f, 3f), settings);
    }

    [Fact]
    public void Parse_PartialSectionKeepsOtherDefaults() =>
        Assert.Equal(AppSettings.Default with { PenColor = PenColor.Pink },
            AppSettings.Parse("""{"pen": {"color": "pink"}}"""));

    [Theory]
    [InlineData("""{"version": 2}""", "Versão 2")]
    [InlineData("""{"pen": {"color": "purple"}}""", "pen.color")]
    [InlineData("""{"pen": {"color": "Red"}}""", "pen.color")]
    [InlineData("""{"pen": {"width": 0}}""", "pen.width")]
    [InlineData("""{"pen": {"width": 61}}""", "pen.width")]
    [InlineData("""{"zoom": {"initial": 0.5}}""", "zoom.initial")]
    [InlineData("""{"zoom": {"initial": 17}}""", "zoom.initial")]
    [InlineData("""{"zoom": {"initial": "2"}}""", "JSON inválido")]
    [InlineData("""{"zoom": {"inital": 2}}""", "JSON inválido")]
    [InlineData("""{"theme": "dark"}""", "JSON inválido")]
    [InlineData("""{"pen": """, "JSON inválido")]
    [InlineData("[]", "JSON inválido")]
    [InlineData("null", "objeto JSON")]
    public void Parse_RejectsInvalidContent(string json, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => AppSettings.Parse(json));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_StartsWithConfiguredTool()
    {
        var document = new AnnotationDocument(PenColor.Orange, 20f);

        Assert.Equal(PenColor.Orange, document.Color);
        Assert.Equal(20f, document.PenWidth);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnnotationDocument(PenColor.Red, 0f));
    }

    [Fact]
    public void Load_MissingFileUsesDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "pixelpointer", SettingsStore.FileName);

        var result = SettingsStore.Load(path);

        Assert.Equal(SettingsStatus.Missing, result.Status);
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void Load_ValidFileIsApplied()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "configuração com espaço", SettingsStore.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "\uFEFF{\"pen\": {\"color\": \"green\"}}");

        var result = SettingsStore.Load(path);

        Assert.Equal(SettingsStatus.Loaded, result.Status);
        Assert.Equal(PenColor.Green, result.Settings.PenColor);
    }

    [Fact]
    public void Load_InvalidFileFallsBackWithoutModifyingIt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, SettingsStore.FileName);
        const string content = """{"pen": {"width": 999}}""";
        File.WriteAllText(path, content);

        var result = SettingsStore.Load(path);

        Assert.Equal(SettingsStatus.Invalid, result.Status);
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.Contains("pen.width", result.Error, StringComparison.Ordinal);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void Load_RejectsOversizedAndNonUtf8Files()
    {
        using var directory = new TemporaryDirectory();
        var large = Path.Combine(directory.Path, "large.json");
        File.WriteAllText(large, "{" + new string(' ', SettingsStore.MaxBytes) + "}");
        var binary = Path.Combine(directory.Path, "binary.json");
        File.WriteAllBytes(binary, [0x7B, 0xFF, 0xFE, 0x7D]);

        Assert.Equal(SettingsStatus.Invalid, SettingsStore.Load(large).Status);
        Assert.Equal(SettingsStatus.Invalid, SettingsStore.Load(binary).Status);
    }

    [Fact]
    public void Load_DirectoryInPlaceOfFileIsInvalid()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, SettingsStore.FileName);
        Directory.CreateDirectory(path);

        Assert.Equal(SettingsStatus.Invalid, SettingsStore.Load(path).Status);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("pixelpointer-settings-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
