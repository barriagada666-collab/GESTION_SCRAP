namespace GestionScrap.Domain.Enums;

/// <summary>
/// Perfiles de seguridad y roles operativos del sistema.
/// </summary>
public enum RolUsuario
{
    Administrador = 1,
    Supervisor = 2,
    Operador = 3,
    Auditor = 4
}

/// <summary>
/// Permisos granulares validados tanto en interfaz como en capa de aplicación.
/// </summary>
public enum PermisoSistema
{
    VerDashboard = 1,
    ConsultarInformes = 2,
    ExportarExcel = 3,
    EditarPreciosReales = 4,
    CongelarPreciosTeoricos = 5,
    AdministrarMantenedorPrecios = 6,
    CerrarFolioJcramm = 7,
    ImportarFirebird = 8,
    EjecutarProcedimientosFirebird = 9,
    PurgarTablasPesaje = 10,
    MigrarDesdeMdb = 11,
    AdministrarSeguridad = 12,
    VerAuditoria = 13
}

public enum ModoPrecio
{
    Teoricos = 1,
    Reales = 2
}

public enum FiltroInformePrecio
{
    Todos = 1,
    Resumen = 2
}
