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

/// <summary>
/// Dimensión de segmentación dinámica para análisis de datos e informes personalizados.
/// </summary>
public enum SegmentacionDimension
{
    GrupoMaterial = 1,
    Planta = 2,
    TipoPesaje = 3,
    Proveedor = 4,
    Producto = 5,
    Origen = 6,
    Mes = 7,
    Anio = 8
}
