using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Entities;

/// <summary>
/// Perfil de acceso administrable dinámicamente desde la interfaz de Seguridad y Perfiles.
/// </summary>
public sealed class PerfilUsuario
{
    public long Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public bool EsSistema { get; init; }
    public IReadOnlyCollection<PermisoSistema> Permisos { get; init; } = [];
}

public sealed class Usuario
{
    public long Id { get; init; }
    public string NombreUsuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public long PerfilId { get; init; }
    public string NombrePerfil { get; init; } = "Operador";
    public string PasswordHash { get; init; } = string.Empty;
    public string PasswordSalt { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public int IntentosFallidos { get; init; }
    public DateTime? BloqueadoHastaUtc { get; init; }
    public IReadOnlyCollection<PermisoSistema> PermisosPersonalizados { get; init; } = [];
    public DateTime CreadoEnUtc { get; init; } = DateTime.UtcNow;
}

public sealed class AuditLogEntry
{
    public long Id { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Usuario { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public string RegistroId { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
}

public sealed class FirebirdPlantaEndpoint
{
    public Planta Planta { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ServidorIp { get; set; } = "192.168.2.56";
    public int Puerto { get; set; } = 3050;
    public string RutaArchivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = "SYSDBA";
    public string Password { get; set; } = "masterkey";
    public bool UsarModoLocalSiFallaRed { get; set; }
}

public sealed class FirebirdRemotePesaje
{
    public long Folio { get; init; }
    public string TipoPesajeRaw { get; init; } = "1";
    public string Patente { get; init; } = string.Empty;
    public string Acoplado { get; init; } = string.Empty;
    public int PesoBruto { get; init; }
    public int PesoTara { get; init; }
    public int PesoNeto { get; init; }
    public string CodProducto { get; init; } = string.Empty;
    public string NomProducto { get; init; } = string.Empty;
    public string RutCliente { get; init; } = string.Empty;
    public string NomCliente { get; init; } = string.Empty;
    public string Origen { get; init; } = string.Empty;
    public string Destino { get; init; } = string.Empty;
    public string GuiaDespacho { get; init; } = string.Empty;
    public string Chofer { get; init; } = string.Empty;
    public string Transportista { get; init; } = string.Empty;
    public DateOnly FechaIngreso { get; init; }
    public TimeOnly HoraIngreso { get; init; }
    public DateOnly FechaSalida { get; init; }
    public TimeOnly HoraSalida { get; init; }
    public string Observacion { get; init; } = string.Empty;
}

public sealed record FirebirdExtractPackage(
    IReadOnlyList<FirebirdRemotePesaje> Pesajes,
    IReadOnlyList<Cliente> Clientes,
    IReadOnlyList<Producto> Productos,
    bool UsedSimulatedFallback,
    string StatusMessage);

public sealed class AppSettings
{
    public string SqliteDatabasePath { get; set; } = string.Empty;
    public string LastMdbPath { get; set; } = string.Empty;
    public List<FirebirdPlantaEndpoint> FirebirdEndpoints { get; set; } = [];
}
