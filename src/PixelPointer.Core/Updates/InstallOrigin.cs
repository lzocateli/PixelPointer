using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelPointer.Core.Updates;

public enum InstallChannel
{
    /// <summary>install.sh ou install.ps1 em diretório do usuário.</summary>
    UserInstaller,

    /// <summary>install.sh com --global ou instalação por root.</summary>
    GlobalInstaller,
    Deb,
    Homebrew,
    Winget,

    /// <summary>Instalação portátil, de desenvolvimento ou sem recibo válido.</summary>
    Unknown,
}

/// <summary>Recibo gravado pelos instaladores próprios ao lado do executável.</summary>
public sealed record InstallReceipt(string Installer, string Scope, string Rid)
{
    public const string FileName = "install-receipt.json";
    public const int CurrentVersion = 1;
    public const int MaxBytes = 4 * 1024;

    public static InstallReceipt Parse(string json)
    {
        ReceiptFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, ReceiptJsonContext.Default.ReceiptFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"recibo inválido: {ex.Message}", ex);
        }

        if (file is null || file.Version != CurrentVersion)
        {
            throw new InvalidDataException($"recibo sem \"version\": {CurrentVersion}");
        }

        if (file.Installer is not ("install.sh" or "install.ps1"))
        {
            throw new InvalidDataException("recibo com instalador desconhecido");
        }

        if (file.Scope is not ("user" or "global"))
        {
            throw new InvalidDataException("recibo com escopo desconhecido");
        }

        if (file.Rid is not { } rid || !PackageLayout.SupportedRids.Contains(rid))
        {
            throw new InvalidDataException("recibo com RID desconhecido");
        }

        return new InstallReceipt(file.Installer, file.Scope, rid);
    }
}

/// <summary>Fatos observados pelo aplicativo sobre o diretório em que está instalado.</summary>
public sealed record InstallFacts(
    string Rid,
    string AppDirectory,
    InstallReceipt? Receipt,
    string? ReceiptError,
    bool IsPrivileged,
    bool AppDirectoryWritable,
    bool ParentWritable,
    bool SharedWritable,
    bool DpkgOwned);

/// <summary>Origem detectada e se o atualizador pode substituir a instalação.</summary>
public sealed record InstallOrigin(InstallChannel Channel, bool CanSelfUpdate, string Detail)
{
    /// <summary>Orientação de atualização para canais que o atualizador não substitui.</summary>
    public string Guidance => Channel switch
    {
        InstallChannel.Deb => "Baixe o novo .deb da release e atualize com 'sudo apt install ./pixelpointer-<rid>.deb'.",
        InstallChannel.Homebrew => "Atualize com 'brew upgrade pixelpointer'.",
        InstallChannel.Winget => "Atualize com 'winget upgrade lzocateli.PixelPointer'.",
        InstallChannel.GlobalInstaller => "Reexecute o instalador com os mesmos privilégios usados na instalação (por exemplo, sudo sh install.sh --global).",
        InstallChannel.UserInstaller when !CanSelfUpdate => "Reexecute install.sh ou install.ps1 com o mesmo usuário e destino da instalação.",
        InstallChannel.Unknown => "Reinstale com install.sh ou install.ps1 para habilitar a atualização automática, ou atualize pelo mesmo meio usado na instalação.",
        _ => string.Empty,
    };

    public static InstallOrigin Classify(InstallFacts facts)
    {
        var directory = facts.AppDirectory.Replace('\\', '/').TrimEnd('/');
        if (directory.Contains("/Cellar/pixelpointer/", StringComparison.Ordinal)
            || directory.StartsWith("/opt/homebrew/", StringComparison.Ordinal)
            || directory.StartsWith("/home/linuxbrew/.linuxbrew/", StringComparison.Ordinal))
        {
            return new(InstallChannel.Homebrew, false, "instalado pelo Homebrew");
        }

        if (facts.Rid.StartsWith("linux-", StringComparison.Ordinal)
            && (facts.DpkgOwned || string.Equals(directory, "/usr/lib/pixelpointer", StringComparison.Ordinal)))
        {
            return new(InstallChannel.Deb, false, "instalado pelo pacote .deb");
        }

        if (facts.Rid.StartsWith("win-", StringComparison.Ordinal)
            && directory.Contains("/WinGet/Packages/", StringComparison.OrdinalIgnoreCase))
        {
            return new(InstallChannel.Winget, false, "instalado pelo winget");
        }

        if (facts.Receipt is not { } receipt)
        {
            return new(InstallChannel.Unknown, false, facts.ReceiptError is null
                ? "origem desconhecida: instalação portátil ou sem recibo do instalador"
                : $"origem desconhecida: {facts.ReceiptError}");
        }

        if (!string.Equals(receipt.Rid, facts.Rid, StringComparison.Ordinal))
        {
            return new(InstallChannel.Unknown, false, $"recibo para {receipt.Rid}, mas o executável é {facts.Rid}");
        }

        if (receipt.Scope == "global")
        {
            return new(InstallChannel.GlobalInstaller, false, "instalação global; exigiria privilégios elevados");
        }

        if (facts.IsPrivileged)
        {
            return new(InstallChannel.UserInstaller, false, "processo com privilégios elevados; o atualizador não roda elevado");
        }

        if (!facts.AppDirectoryWritable || !facts.ParentWritable)
        {
            return new(InstallChannel.UserInstaller, false, "diretório da instalação sem permissão de escrita para o usuário atual");
        }

        if (facts.SharedWritable)
        {
            return new(InstallChannel.UserInstaller, false, "diretório da instalação gravável por grupo ou outros usuários");
        }

        return new(InstallChannel.UserInstaller, true, $"instalado por {receipt.Installer} no diretório do usuário");
    }
}

internal sealed class ReceiptFile
{
    public int? Version { get; set; }

    public string? Installer { get; set; }

    public string? Scope { get; set; }

    public string? Rid { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReceiptFile))]
internal sealed partial class ReceiptJsonContext : JsonSerializerContext;
