using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPointer.Core.Updates;

/// <summary>
/// Preferências e estado do atualizador por usuário, gravados pelo aplicativo em update.json.
/// A instalação automática só vale com a verificação automática ativada.
/// </summary>
public sealed record UpdatePreferences(
    bool CheckAutomatically = false,
    bool InstallAutomatically = false,
    DateTimeOffset? LastCheck = null,
    string? AppliedVersion = null,
    string? LastFailure = null)
{
    public const int CurrentVersion = 1;
    public const int MaxBytes = 16 * 1024;
    public const int MaxMessageLength = 512;

    public static UpdatePreferences Default { get; } = new();

    /// <summary>Instalação automática efetiva: exige a verificação automática.</summary>
    public bool EffectiveInstallAutomatically => CheckAutomatically && InstallAutomatically;

    /// <summary>Desativar a verificação automática desativa também a instalação automática.</summary>
    public UpdatePreferences WithCheckAutomatically(bool enabled) =>
        this with { CheckAutomatically = enabled, InstallAutomatically = enabled && InstallAutomatically };

    public UpdatePreferences WithInstallAutomatically(bool enabled)
    {
        if (enabled && !CheckAutomatically)
        {
            throw new InvalidOperationException(
                "Ative primeiro a verificação automática; a instalação automática depende dela.");
        }

        return this with { InstallAutomatically = enabled };
    }

    public static UpdatePreferences Parse(string json)
    {
        UpdateFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON inválido: {ex.Message}", ex);
        }

        if (file is null)
        {
            throw new InvalidDataException("O arquivo deve conter um objeto JSON.");
        }

        if (file.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Versão {file.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "ausente"} não suportada; use \"version\": {CurrentVersion}.");
        }

        if (file.AppliedVersion is { } applied && !SemanticVersion.TryParse(applied, out _))
        {
            throw new InvalidDataException("appliedVersion deve ser uma versão SemVer.");
        }

        var check = file.CheckAutomatically ?? false;
        return new UpdatePreferences(
            check,
            check && (file.InstallAutomatically ?? false),
            file.LastCheck,
            file.AppliedVersion,
            Truncate(file.LastFailure));
    }

    public string Serialize() =>
        JsonSerializer.Serialize(new UpdateFile
        {
            Version = CurrentVersion,
            CheckAutomatically = CheckAutomatically,
            InstallAutomatically = EffectiveInstallAutomatically,
            LastCheck = LastCheck,
            AppliedVersion = AppliedVersion,
            LastFailure = Truncate(LastFailure),
        }, UpdateJsonContext.Default.UpdateFile) + "\n";

    public static string? Truncate(string? message)
    {
        if (message is null)
        {
            return null;
        }

        var clean = new StringBuilder(Math.Min(message.Length, MaxMessageLength));
        foreach (var c in message)
        {
            if (clean.Length == MaxMessageLength)
            {
                break;
            }

            clean.Append(char.IsControl(c) ? ' ' : c);
        }

        return clean.ToString();
    }
}

internal sealed class UpdateFile
{
    public int? Version { get; set; }

    public bool? CheckAutomatically { get; set; }

    public bool? InstallAutomatically { get; set; }

    public DateTimeOffset? LastCheck { get; set; }

    public string? AppliedVersion { get; set; }

    public string? LastFailure { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(UpdateFile))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
