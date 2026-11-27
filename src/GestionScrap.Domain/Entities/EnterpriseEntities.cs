using System.Globalization;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Domain.Entities;

/// <summary>
/// Maestro de vehículos y patentes normalizadas para trazabilidad logística transversal.
/// </summary>
public sealed class Vehiculo
{
    public long Id { get; init; }
    public string PatenteOriginal { get; init; } = string.Empty;
    public string PatenteNormalizada { get; init; } = string.Empty;
    public string TipoVehiculo { get; init; } = "Camión";
    public string Marca { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public int CapacidadCargaKg { get; init; }
    public string RutEntidadAsociada { get; init; } = string.Empty;
    public string NombreEntidadAsociada { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public string Observaciones { get; init; } = string.Empty;
    public DateTime CreadoEnUtc { get; init; } = DateTime.UtcNow;

    public string PatenteClave => string.IsNullOrWhiteSpace(PatenteNormalizada)
        ? PatenteChilena.Normalize(PatenteOriginal)
        : PatenteNormalizada;

    public string EstadoDescripcion => Activo ? "Activo" : "Inactivo";
}

/// <summary>
/// Configuración administrable de tipos de pesaje del negocio.
/// </summary>
public sealed class TipoPesajeConfig
{
    public long Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public TipoPesaje TipoBase { get; init; } = TipoPesaje.RecepcionCarga;
    public bool RequiereDosMediciones { get; init; }
    public bool AfectaInventario { get; init; } = true;
    public int DireccionInventario { get; init; } = 1; // +1 entrada, -1 salida, 0 neutro
    public bool Activo { get; init; } = true;
    public int Orden { get; init; }

    public string ModoMedicion => RequiereDosMediciones ? "Entrada y Salida (2 pasos)" : "Medición Directa (1 paso)";
    public string ImpactoInventario => !AfectaInventario
        ? "No afecta"
        : DireccionInventario > 0 ? "Ingreso (+)" : DireccionInventario < 0 ? "Egreso (-)" : "Neutro";
    public string EstadoDescripcion => Activo ? "Activo" : "Inactivo";
}

/// <summary>
/// Operación comercial o logística ERP (Orden de Compra, Venta, Recepción, Entrega/Despacho, Traslado).
/// </summary>
public sealed class OperacionComercial
{
    public long Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public TipoOperacionComercial TipoOperacion { get; init; } = TipoOperacionComercial.OrdenCompra;
    public DateOnly Fecha { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string RutCliente { get; init; } = string.Empty;
    public string NombreCliente { get; init; } = string.Empty;
    public string RutProveedor { get; init; } = string.Empty;
    public string NombreProveedor { get; init; } = string.Empty;
    public string CodProducto { get; init; } = string.Empty;
    public string NombreProducto { get; init; } = string.Empty;
    public long CantidadEsperadaKg { get; init; }
    public long CantidadRealConfirmadaKg { get; init; }
    public string UnidadMedida { get; init; } = "KG";
    public string Patente { get; init; } = string.Empty;
    public string DocumentoAsociado { get; init; } = string.Empty;
    public EstadoOperacion Estado { get; init; } = EstadoOperacion.Abierta;
    public string Observaciones { get; init; } = string.Empty;
    public string CreadoPor { get; init; } = string.Empty;
    public DateTime CreadoEnUtc { get; init; } = DateTime.UtcNow;

    public string TipoDescripcion => TipoOperacion.ToDisplayName();
    public string EstadoDescripcion => Estado.ToDisplayName();
    public long DiferenciaKg => CantidadRealConfirmadaKg - CantidadEsperadaKg;
}

/// <summary>
/// Asiento inmutable en el libro mayor de inventario de materiales.
/// </summary>
public sealed class InventarioMovimiento
{
    public long Id { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public TipoMovimientoInventario TipoMovimiento { get; init; }
    public string CodProducto { get; init; } = string.Empty;
    public string NombreProducto { get; init; } = string.Empty;
    public long CantidadKg { get; init; } // Positivo = ingreso, Negativo = egreso
    public string UnidadMedida { get; init; } = "KG";
    public long? PesajeId { get; init; }
    public string FolioPesaje { get; init; } = string.Empty;
    public long? OperacionId { get; init; }
    public string CodigoOperacion { get; init; } = string.Empty;
    public string DocumentoReferencia { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public string Observaciones { get; init; } = string.Empty;
    public bool Anulado { get; init; }

    public string FechaHoraLocal => TimestampUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    public string TipoDescripcion => TipoMovimiento.ToDisplayName();
}

/// <summary>
/// Existencia consolidada derivada exclusivamente del libro de movimientos confirmados de inventario.
/// </summary>
public sealed record InventarioExistencia(
    string CodProducto,
    string NombreProducto,
    string GrupoMaterial,
    string UnidadMedida,
    long EntradasKg,
    long SalidasKg,
    long AjustesKg,
    long StockActualKg,
    int CantidadMovimientos,
    DateTime? UltimoMovimientoUtc);

/// <summary>
/// Registro de auditoría inmutable para modificaciones y anulaciones autorizadas de pesajes.
/// </summary>
public sealed class PesajeHistorialCambio
{
    public long Id { get; init; }
    public long PesajeId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Usuario { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public int PesoBrutoAnterior { get; init; }
    public int PesoTaraAnterior { get; init; }
    public int PesoNetoAnterior { get; init; }
    public int PesoBrutoNuevo { get; init; }
    public int PesoTaraNuevo { get; init; }
    public int PesoNetoNuevo { get; init; }
    public EstadoPesaje EstadoAnterior { get; init; }
    public EstadoPesaje EstadoNuevo { get; init; }
    public string Motivo { get; init; } = string.Empty;

    public string FechaHoraLocal => TimestampUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Documento comercial, tributario o logístico asociado a una entidad CRM, operación o pesaje.
/// </summary>
public sealed class DocumentoAsociado
{
    public long Id { get; init; }
    public string RutEntidad { get; init; } = string.Empty;
    public long? OperacionId { get; init; }
    public long? PesajeId { get; init; }
    public string TipoDocumento { get; init; } = "Guía de Despacho";
    public string NumeroDocumento { get; init; } = string.Empty;
    public DateOnly FechaDocumento { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string Observaciones { get; init; } = string.Empty;
    public DateTime CreadoEnUtc { get; init; } = DateTime.UtcNow;
}
