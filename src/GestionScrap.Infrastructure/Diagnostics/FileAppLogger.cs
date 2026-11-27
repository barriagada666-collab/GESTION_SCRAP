using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GestionScrap.Domain;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Diagnostics;

/// <summary>
/// Registrador estructurado de eventos y errores con rotación diaria por fecha,
/// redacción automática de credenciales/secretos y retención controlada (30 días).
/// </summary>
public sealed partial class FileAppLogger : IAppLogger
{
    private const int RetentionDays = 30;
    private readonly string _logDirectory;
    private readonly Lock _syncRoot = new();

    public FileAppLogger(string? logDirectory = null)
    {
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GestionScrap",
                "logs")
            : logDirectory;
    }

    public string LogDirectory => _logDirectory;

    public string CurrentLogFilePath =>
        Path.Combine(_logDirectory, $"gestionscrap_{DateTime.Now:yyyy-MM-dd}.log");

    public void Log(
        DiagnosticLogLevel level,
        string component,
        string message,
        string? context = null,
        Exception? exception = null,
        string? remediation = null,
        string? verification = null)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            StringBuilder sb = new(256);
            sb.Append('[')
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
              .Append("] [")
              .Append(level.ToString().ToUpperInvariant())
              .Append("] [")
              .Append(AppMetadata.ShortVersion)
              .Append("] [")
              .Append(Sanitize(string.IsNullOrWhiteSpace(component) ? "GENERAL" : component.Trim()))
              .Append("] ")
              .AppendLine(Sanitize(message));

            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.Append("  Contexto: ").AppendLine(Sanitize(context));
            }

            if (exception is not null)
            {
                sb.Append("  Excepcion: ")
                  .Append(exception.GetType().FullName)
                  .Append(": ")
                  .AppendLine(Sanitize(exception.Message));

                if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                {
                    sb.AppendLine("  StackTrace:");
                    foreach (string line in exception.StackTrace.Split(Environment.NewLine))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            sb.Append("    ").AppendLine(Sanitize(line.Trim()));
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(remediation))
            {
                sb.Append("  Correccion: ").AppendLine(Sanitize(remediation));
            }

            if (!string.IsNullOrWhiteSpace(verification))
            {
                sb.Append("  Verificacion: ").AppendLine(Sanitize(verification));
            }

            sb.AppendLine(new string('-', 76));

            lock (_syncRoot)
            {
                File.AppendAllText(CurrentLogFilePath, sb.ToString(), Encoding.UTF8);
                CleanupExpiredLogs();
            }
        }
        catch
        {
            // El subsistema de diagnóstico nunca debe provocar fallos secundarios en la aplicación.
        }
    }

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string cleaned = PasswordKeyValueRegex().Replace(input, "$1=[REDACTED]");
        cleaned = BearerTokenRegex().Replace(cleaned, "Bearer [REDACTED]");
        cleaned = GitHubTokenRegex().Replace(cleaned, "[REDACTED_TOKEN]");
        cleaned = DpapiSecretRegex().Replace(cleaned, "DPAPI:[REDACTED]");
        return cleaned;
    }

    private void CleanupExpiredLogs()
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (string file in Directory.EnumerateFiles(_logDirectory, "gestionscrap_*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Limpieza best-effort
            }
        }
    }

    [GeneratedRegex(@"(?i)\b(password|pwd|pass|contrase[ñn]a|secret|token|apikey|api_key)\s*[:=]\s*[^;\r\n""'&]+")]
    private static partial Regex PasswordKeyValueRegex();

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9\-._~+/]+=*")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\b(?:ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b")]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"DPAPI:[A-Za-z0-9+/=]+")]
    private static partial Regex DpapiSecretRegex();
}
