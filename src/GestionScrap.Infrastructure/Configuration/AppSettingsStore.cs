using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Infrastructure.Configuration;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public AppSettingsStore(string? customSettingsPath = null)
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestionScrap");
        _settingsFilePath = string.IsNullOrWhiteSpace(customSettingsPath)
            ? Path.Combine(baseDir, "settings.json")
            : customSettingsPath;
    }

    public string SettingsDirectory => Path.GetDirectoryName(_settingsFilePath)!;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
                StoredSettings? dto = JsonSerializer.Deserialize<StoredSettings>(json, JsonOptions);
                if (dto is not null)
                {
                    AppSettings settings = new()
                    {
                        SqliteDatabasePath = string.IsNullOrWhiteSpace(dto.SqliteDatabasePath)
                            ? GetDefaultSqlitePath()
                            : dto.SqliteDatabasePath,
                        LastMdbPath = dto.LastMdbPath ?? string.Empty,
                        FirebirdEndpoints = [.. (dto.FirebirdEndpoints ?? []).Select(e => new FirebirdPlantaEndpoint
                        {
                            Planta = e.Planta,
                            Nombre = e.Nombre,
                            ServidorIp = e.ServidorIp,
                            Puerto = e.Puerto <= 0 ? 3050 : e.Puerto,
                            RutaArchivo = e.RutaArchivo,
                            Usuario = e.Usuario,
                            Password = UnprotectSecret(e.ProtectedPassword),
                            UsarModoLocalSiFallaRed = e.UsarModoLocalSiFallaRed
                        })]
                    };

                    if (settings.FirebirdEndpoints.Count == 0)
                    {
                        settings.FirebirdEndpoints = CreateDefaultEndpoints();
                    }

                    return settings;
                }
            }
        }
        catch
        {
            // Fallback to safe defaults if settings file is corrupted
        }

        return new AppSettings
        {
            SqliteDatabasePath = GetDefaultSqlitePath(),
            FirebirdEndpoints = CreateDefaultEndpoints()
        };
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        StoredSettings dto = new()
        {
            SqliteDatabasePath = string.IsNullOrWhiteSpace(settings.SqliteDatabasePath)
                ? GetDefaultSqlitePath()
                : settings.SqliteDatabasePath,
            LastMdbPath = settings.LastMdbPath ?? string.Empty,
            FirebirdEndpoints = [.. settings.FirebirdEndpoints.Select(e => new StoredEndpoint
            {
                Planta = e.Planta,
                Nombre = e.Nombre,
                ServidorIp = e.ServidorIp,
                Puerto = e.Puerto <= 0 ? 3050 : e.Puerto,
                RutaArchivo = e.RutaArchivo,
                Usuario = e.Usuario,
                ProtectedPassword = ProtectSecret(e.Password),
                UsarModoLocalSiFallaRed = e.UsarModoLocalSiFallaRed
            })]
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(_settingsFilePath, json, Encoding.UTF8);
    }

    private string GetDefaultSqlitePath() => Path.Combine(SettingsDirectory, "gestion_scrap.db");

    public static List<FirebirdPlantaEndpoint> CreateDefaultEndpoints() =>
    [
        new FirebirdPlantaEndpoint
        {
            Planta = Planta.Placilla,
            Nombre = "Firebird Placilla 1",
            ServidorIp = "192.168.2.56",
            Puerto = 3050,
            RutaArchivo = @"C:\BDWM\placilla\WMDBSPC.GDB",
            Usuario = "SYSDBA",
            Password = string.Empty,
            UsarModoLocalSiFallaRed = true
        },
        new FirebirdPlantaEndpoint
        {
            Planta = Planta.Santiago,
            Nombre = "Firebird Santiago",
            ServidorIp = "192.168.2.56",
            Puerto = 3050,
            RutaArchivo = @"C:\BDWM\placilla2\WMDBSPC.GDB",
            Usuario = "SYSDBA",
            Password = string.Empty,
            UsarModoLocalSiFallaRed = true
        },
        new FirebirdPlantaEndpoint
        {
            Planta = Planta.Talcahuano,
            Nombre = "Firebird Talcahuano",
            ServidorIp = "192.168.2.56",
            Puerto = 3050,
            RutaArchivo = @"C:\BDWM\thno\PWAREHOUSE.FDB",
            Usuario = "SYSDBA",
            Password = string.Empty,
            UsarModoLocalSiFallaRed = true
        }
    ];

    private static string ProtectSecret(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        if (OperatingSystem.IsWindows())
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return "DPAPI:" + Convert.ToBase64String(protectedData);
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    private static string UnprotectSecret(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return string.Empty;
        }

        try
        {
            if (stored.StartsWith("DPAPI:", StringComparison.Ordinal) && OperatingSystem.IsWindows())
            {
                byte[] cipher = Convert.FromBase64String(stored["DPAPI:".Length..]);
                byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(stored));
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class StoredSettings
    {
        public string? SqliteDatabasePath { get; set; }
        public string? LastMdbPath { get; set; }
        public List<StoredEndpoint>? FirebirdEndpoints { get; set; }
    }

    private sealed class StoredEndpoint
    {
        public Planta Planta { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ServidorIp { get; set; } = string.Empty;
        public int Puerto { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string ProtectedPassword { get; set; } = string.Empty;
        public bool UsarModoLocalSiFallaRed { get; set; }
    }
}
