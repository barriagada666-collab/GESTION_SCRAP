using System.Reflection;

namespace GestionScrap.Domain;

/// <summary>
/// Metadatos centralizados de versión y producto leídos dinámicamente desde los atributos del ensamblado
/// (definidos en <c>Directory.Build.props</c>), evitando versiones escritas manualmente en la interfaz.
/// </summary>
public static class AppMetadata
{
    private static readonly Assembly EntryOrDomainAssembly = Assembly.GetEntryAssembly() ?? typeof(AppMetadata).Assembly;

    public static string ProductName =>
        typeof(AppMetadata).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Gestión de Scrap";

    public static string CompanyName =>
        typeof(AppMetadata).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Cambiaso Hnos. S.A.C.";

    public static string Version
    {
        get
        {
            string? info = typeof(AppMetadata).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plusIdx = info.IndexOf('+');
                return plusIdx > 0 ? info[..plusIdx] : info;
            }

            Version? asmVer = typeof(AppMetadata).Assembly.GetName().Version;
            return asmVer is not null ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : "4.1.0";
        }
    }

    public static string ShortVersion
    {
        get
        {
            Version? asmVer = typeof(AppMetadata).Assembly.GetName().Version;
            return asmVer is not null ? $"v{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : $"v{Version}";
        }
    }

    public static string ReleaseDate =>
        typeof(AppMetadata).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "ReleaseDate", StringComparison.OrdinalIgnoreCase))?.Value
        ?? "26-11-2026";

    public static string ExecutableName => EntryOrDomainAssembly.GetName().Name ?? "GestionScrap";
}
