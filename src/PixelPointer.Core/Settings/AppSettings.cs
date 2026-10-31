using System.Text.Json;
using System.Text.Json.Serialization;
using PixelPointer.Core.Annotations;

namespace PixelPointer.Core.Settings;

/// <summary>Preferências do usuário aplicadas a cada sobreposição.</summary>
public sealed record AppSettings(PenColor PenColor, float PenWidth, float InitialZoom)
{
    public const int CurrentVersion = 1;
    public const float MinZoom = 1f;
    public const float MaxZoom = 16f;

    public static AppSettings Default { get; } = new(PenColor.Red, AnnotationDocument.DefaultPenWidth, 2f);

    /// <summary>Interpreta o JSON de configurações; campos ausentes usam o padrão e valores inválidos são rejeitados.</summary>
    public static AppSettings Parse(string json)
    {
        SettingsFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON inválido: {ex.Message}", ex);
        }

        if (file is null)
        {
            throw new InvalidDataException("O arquivo deve conter um objeto JSON.");
        }

        if (file.Version is { } version && version != CurrentVersion)
        {
            throw new InvalidDataException($"Versão {version} não suportada; use \"version\": {CurrentVersion}.");
        }

        return new AppSettings(
            file.Pen?.Color is { } color ? ParseColor(color) : Default.PenColor,
            file.Pen?.Width is { } width
                ? RequireRange(width, AnnotationDocument.MinPenWidth, AnnotationDocument.MaxPenWidth, "pen.width")
                : Default.PenWidth,
            file.Zoom?.Initial is { } zoom ? RequireRange(zoom, MinZoom, MaxZoom, "zoom.initial") : Default.InitialZoom);
    }

    private static PenColor ParseColor(string value) => value switch
    {
        "red" => PenColor.Red,
        "green" => PenColor.Green,
        "blue" => PenColor.Blue,
        "yellow" => PenColor.Yellow,
        "orange" => PenColor.Orange,
        "pink" => PenColor.Pink,
        _ => throw new InvalidDataException(
            $"pen.color \"{value}\" inválida; use red, green, blue, yellow, orange ou pink."),
    };

    private static float RequireRange(float value, float min, float max, string name) =>
        float.IsFinite(value) && value >= min && value <= max
            ? value
            : throw new InvalidDataException($"{name} deve estar entre {min} e {max}.");
}

internal sealed class SettingsFile
{
    public int? Version { get; set; }

    public PenSection? Pen { get; set; }

    public ZoomSection? Zoom { get; set; }
}

internal sealed class PenSection
{
    public string? Color { get; set; }

    public float? Width { get; set; }
}

internal sealed class ZoomSection
{
    public float? Initial { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SettingsFile))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
