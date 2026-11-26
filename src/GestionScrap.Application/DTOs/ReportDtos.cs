using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Application.DTOs;

public sealed record BarChartItemDto(string Label, long Value);

public sealed record GrupoKilosItem(string Grupo, long Kilos, int Viajes = 0, double Porcentaje = 0.0);

public sealed record WeekComparisonDto(string Periodo, long KilosActual, long KilosAnterior);

public sealed record PlantaResumenItem(Planta Planta, string NombrePlanta, int Viajes, long Kilos, double Porcentaje);

public sealed record TipoPesajeResumenItem(TipoPesaje Tipo, string NombreTipo, int Viajes, long Kilos, double Porcentaje);

public sealed record InformeResumenDto(
    string PeriodoActualLabel,
    string PeriodoAnteriorLabel,
    long TotalKilos,
    int TotalViajes,
    double PromedioKilosViaje,
    long TotalKilosAnterior,
    double VariacionPorcentual,
    int ProveedoresActivos,
    int ProductosActivos,
    DateOnly UltimaFecha,
    long KilosUltimaFecha,
    int ViajesUltimaFecha,
    IReadOnlyList<PlantaResumenItem> PorPlanta,
    IReadOnlyList<TipoPesajeResumenItem> PorTipo,
    IReadOnlyList<GrupoKilosItem> PorGrupo,
    IReadOnlyList<WeekComparisonDto> Tendencia,
    IReadOnlyList<BarChartItemDto> TopProveedores,
    IReadOnlyList<Pesaje> UltimosFolios);

public sealed record MaterialTreeFlatRowDto(
    long NodeId,
    long? ParentId,
    int Nivel,
    string NombreConSangria,
    string Nombre,
    NodoGrupoTipo TipoNodo,
    string CodigoProducto,
    string ColorHex,
    int Viajes,
    long TotalKilos,
    double Porcentaje,
    double PromedioKgViaje);

public sealed record GrupoMaterialReportResultDto(
    string PeriodoLabel,
    long TotalKilos,
    int TotalViajes,
    int GruposPadreCount,
    int NodosTotalesCount,
    int ProductosSinClasificarCount,
    IReadOnlyList<MaterialTreeFlatRowDto> Filas,
    IReadOnlyList<GrupoKilosItem> ConsolidadoPadre,
    IReadOnlyList<BarChartItemDto> DetalleProductosSeleccionados);

public sealed record ProveedorRankingRowDto(
    string Rut,
    string Nombre,
    string Ciudad,
    string Giro,
    string Telefono,
    int Viajes,
    long TotalKilos,
    double PorcentajeParticipacion,
    double PorcentajeAcumulado,
    string ClaseAbc,
    double PromedioKgViaje,
    DateOnly PrimerEnvio,
    DateOnly UltimoEnvio,
    int DiasInactivo,
    long KilosPeriodoAnterior,
    double VariacionPorcentual,
    string ProductoPrincipal);

public sealed record AnalisisProveedoresResultDto(
    string PeriodoLabel,
    int TotalProveedoresActivos,
    long TotalKilos,
    int TotalViajes,
    double PromedioGeneralKgViaje,
    string ProveedorLiderNombre,
    double ProveedorLiderParticipacion,
    int ProveedoresClaseACount,
    double ConcentracionClaseAPorcentaje,
    IReadOnlyList<ProveedorRankingRowDto> Ranking,
    IReadOnlyList<BarChartItemDto> TopProveedoresPareto,
    IReadOnlyList<GrupoKilosItem> DistribucionPorClaseAbc);

public sealed record ProveedorDetalleResultDto(
    ProveedorRankingRowDto Resumen,
    IReadOnlyList<GrupoKilosItem> MixMateriales,
    IReadOnlyList<BarChartItemDto> ProductosPrincipales,
    IReadOnlyList<BarChartItemDto> EvolucionMensual,
    IReadOnlyList<BarChartItemDto> PatentesFrecuentes,
    IReadOnlyList<BarChartItemDto> OrigenesFrecuentes,
    IReadOnlyList<Pesaje> Entregas);
