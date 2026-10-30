using System.Reflection;

namespace PixelPointer.App;

internal static class AppInfo
{
    /// <summary>Versão informativa (SemVer, com +commit quando disponível), gerada pelo MSBuild.</summary>
    internal static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
}
