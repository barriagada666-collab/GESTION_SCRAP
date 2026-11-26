namespace GestionScrap.Domain.Enums;

/// <summary>
/// Permisos granulares del sistema validados en interfaz gráfica y capa de aplicación.
/// </summary>
public enum PermisoSistema
{
    VerInformeResumen = 1,
    VerGrupoMaterial = 2,
    AdministrarGrupoMaterial = 3,
    VerAnalisisProveedores = 4,
    ImportarDatos = 5,
    ExportarExcel = 6,
    AdministrarSeguridadPerfiles = 7,
    VerAuditoria = 8,
    MantenimientoBaseDatos = 9
}

/// <summary>
/// Tipo de nodo dentro de la jerarquía editable de Grupos de Material.
/// </summary>
public enum NodoGrupoTipo
{
    CarpetaGrupo = 1,
    ProductoAsignado = 2
}
