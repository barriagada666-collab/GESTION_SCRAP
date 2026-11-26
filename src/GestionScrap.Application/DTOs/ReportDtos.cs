using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Application.DTOs;

public sealed record GrupoKilosItem(string Grupo, long Kilos);

public sealed record ComprasRowDto(string Codigo, string Descripcion, string RazonSocial, long Kilos);

public sealed record TrasladoRowDto(
    long Folio,
    string Patente,
    string Producto,
    DateOnly FechaEntrada,
    DateOnly FechaSalida,
    int PesoBruto,
    int PesoTara,
    int PesoNeto);

public sealed record ComprasTrasladosResultDto(
    Planta Planta,
    string LabelCompras,
    string LabelTraslados,
    IReadOnlyList<ComprasRowDto> Compras,
    IReadOnlyList<TrasladoRowDto> Traslados,
    long TotalComprasKilos,
    long TotalTrasladosKilos,
    long TotalGeneralKilos);

public sealed record GrupoEspecialItemDto(string Cliente, long Kilos);

public sealed record GrupoEspecialCardDto(
    string Id,
    string Titulo,
    string VistaReferencia,
    IReadOnlyList<GrupoEspecialItemDto> Items,
    long TotalKilos);

public sealed record JcrammDetalleRowDto(
    DateOnly Fecha,
    long Folio,
    string CodProducto,
    string Descripcion,
    string Patente,
    string Proveedor,
    string Ciudad,
    int Kilos);

public sealed record JcrammResumenRowDto(string Producto, long Kilos);

public sealed record JcrammReportDto(
    UltimoFolioJcramm UltimoFolio,
    IReadOnlyList<JcrammDetalleRowDto> Detalle,
    IReadOnlyList<JcrammResumenRowDto> Resumen,
    long TotalKilosDetalle,
    long TotalKilosResumen);

public sealed record PrecioDetalleRowDto(
    long Folio,
    string CodigoProd,
    string Descripcion,
    string RutCliente,
    string Proveedor,
    int Kilos,
    decimal Precio,
    decimal Total,
    string Familia);

public sealed record PrecioFamiliaResumenDto(
    string Grupo,
    long TotalKilos,
    decimal PrecioPromedioPonderado,
    decimal TotalValorizado);

public sealed record InformePreciosResultDto(
    Planta Planta,
    ModoPrecio Modo,
    IReadOnlyList<PrecioDetalleRowDto> Detalle,
    IReadOnlyList<PrecioFamiliaResumenDto> ResumenFamilias,
    long TotalKilos,
    decimal TotalValorizado);

public sealed record PriceAlertDto(
    string TipoAlerta,
    long Folio,
    Planta Planta,
    string Cliente,
    string CodProducto,
    int Kilos,
    string Mensaje);

public sealed record WeekComparisonDto(string Semana, long KilosActual, long KilosAnterior);

public sealed record DashboardSummaryDto(
    string PeriodoActualLabel,
    string PeriodoAnteriorLabel,
    long TotalKilosMesActual,
    long TotalKilosMesAnterior,
    double VariacionPorcentual,
    DateOnly UltimaFechaRegistrada,
    long KilosUltimaJornada,
    int CamionesUltimaJornada,
    IReadOnlyList<GrupoEspecialItemDto> TopProveedores,
    IReadOnlyList<GrupoKilosItem> KilosPorMaterial,
    IReadOnlyList<WeekComparisonDto> ComparativaSemanal,
    IReadOnlyList<PriceAlertDto> AlertasPrecios,
    IReadOnlyList<Pesaje> UltimosFolios);
