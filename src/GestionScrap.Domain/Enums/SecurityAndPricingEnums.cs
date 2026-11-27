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
    MantenimientoBaseDatos = 9,
    AdministrarCrmEntidades = 10,
    AdministrarMaterialesVehiculos = 11,
    GestionarPesajesOperaciones = 12,
    AutorizarAnularPesajesInventario = 13
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
    Anio = 8,
    Cliente = 9,
    Patente = 10,
    EstadoPesaje = 11
}

/// <summary>
/// Rol comercial dentro del CRM unificado sin duplicación de entidades por RUT.
/// </summary>
public enum RolEntidadComercial
{
    Cliente = 1,
    Proveedor = 2,
    Ambos = 3
}

/// <summary>
/// Ciclo de vida auditable de un pesaje industrial.
/// </summary>
public enum EstadoPesaje
{
    PendienteSegundaMedicion = 1,
    Confirmado = 2,
    Anulado = 3,
    PendienteAutorizacion = 4
}

/// <summary>
/// Tipos de operación comercial y logística integrados en la capa ERP.
/// </summary>
public enum TipoOperacionComercial
{
    OrdenCompra = 1,
    OrdenVenta = 2,
    RecepcionMaterial = 3,
    EntregaDespacho = 4,
    TrasladoInterno = 5
}

/// <summary>
/// Estado operacional de una orden o operación logística ERP.
/// </summary>
public enum EstadoOperacion
{
    Abierta = 1,
    EnProceso = 2,
    Completada = 3,
    Anulada = 4,
    PendienteAutorizacion = 5
}

/// <summary>
/// Naturaleza del movimiento en el libro mayor de inventario.
/// </summary>
public enum TipoMovimientoInventario
{
    Entrada = 1,
    Salida = 2,
    Recepcion = 3,
    Despacho = 4,
    AjustePositivo = 5,
    AjusteNegativo = 6,
    ReversoAnulacion = 7
}

public static class EnterpriseEnumExtensions
{
    public static string ToDisplayName(this RolEntidadComercial rol) => rol switch
    {
        RolEntidadComercial.Cliente => "Cliente",
        RolEntidadComercial.Proveedor => "Proveedor",
        RolEntidadComercial.Ambos => "Cliente y Proveedor",
        _ => "Proveedor"
    };

    public static string ToDisplayName(this EstadoPesaje estado) => estado switch
    {
        EstadoPesaje.PendienteSegundaMedicion => "Pendiente 2ª Medición",
        EstadoPesaje.Confirmado => "Confirmado",
        EstadoPesaje.Anulado => "Anulado",
        EstadoPesaje.PendienteAutorizacion => "Pendiente Autorización",
        _ => "Confirmado"
    };

    public static string ToDisplayName(this TipoOperacionComercial tipo) => tipo switch
    {
        TipoOperacionComercial.OrdenCompra => "Orden de Compra",
        TipoOperacionComercial.OrdenVenta => "Orden de Venta / Despacho",
        TipoOperacionComercial.RecepcionMaterial => "Recepción de Material",
        TipoOperacionComercial.EntregaDespacho => "Entrega / Despacho",
        TipoOperacionComercial.TrasladoInterno => "Traslado Interno",
        _ => "Orden de Compra"
    };

    public static string ToDisplayName(this EstadoOperacion estado) => estado switch
    {
        EstadoOperacion.Abierta => "Abierta",
        EstadoOperacion.EnProceso => "En Proceso",
        EstadoOperacion.Completada => "Completada",
        EstadoOperacion.Anulada => "Anulada",
        EstadoOperacion.PendienteAutorizacion => "Pendiente Autorización",
        _ => "Abierta"
    };

    public static string ToDisplayName(this TipoMovimientoInventario tipo) => tipo switch
    {
        TipoMovimientoInventario.Entrada => "Entrada",
        TipoMovimientoInventario.Salida => "Salida",
        TipoMovimientoInventario.Recepcion => "Recepción",
        TipoMovimientoInventario.Despacho => "Despacho",
        TipoMovimientoInventario.AjustePositivo => "Ajuste (+)",
        TipoMovimientoInventario.AjusteNegativo => "Ajuste (-)",
        TipoMovimientoInventario.ReversoAnulacion => "Reverso por Anulación",
        _ => "Entrada"
    };
}
