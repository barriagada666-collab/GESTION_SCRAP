using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Application.DTOs;

/// <summary>
/// Ficha 360° unificada de una entidad CRM (Cliente, Proveedor o Ambos) con sus pesajes, operaciones, vehículos y documentos.
/// </summary>
public sealed record EntidadComercialFichaDto(
    Cliente Entidad,
    int TotalPesajesConfirmados,
    long TotalKilosNetos,
    int TotalOperaciones,
    DateOnly? UltimoPesaje,
    IReadOnlyList<Pesaje> PesajesRecientes,
    IReadOnlyList<OperacionComercial> Operaciones,
    IReadOnlyList<Vehiculo> VehiculosAsociados,
    IReadOnlyList<DocumentoAsociado> Documentos);

/// <summary>
/// Historial integral de un material (stock derivado, pesajes y movimientos de inventario).
/// </summary>
public sealed record MaterialHistorialDto(
    Producto Material,
    long StockActualKg,
    long EntradasKg,
    long SalidasKg,
    int TotalPesajesConfirmados,
    long TotalKilosPesados,
    IReadOnlyList<Pesaje> PesajesRecientes,
    IReadOnlyList<InventarioMovimiento> MovimientosInventario);

/// <summary>
/// Historial completo de pesajes de un vehículo/patente normalizada.
/// </summary>
public sealed record VehiculoHistorialDto(
    Vehiculo Vehiculo,
    int TotalPesajesConfirmados,
    long TotalKilosNetos,
    DateOnly? PrimerPesaje,
    DateOnly? UltimoPesaje,
    IReadOnlyList<Pesaje> PesajesRecientes);

/// <summary>
/// Trazabilidad punta a punta de una operación ERP: orden -> pesajes -> movimientos inventario -> documentos.
/// </summary>
public sealed record OperacionTrazabilidadDto(
    OperacionComercial Operacion,
    long TotalPesadoConfirmadoKg,
    long DiferenciaVsEsperadoKg,
    IReadOnlyList<Pesaje> PesajesVinculados,
    IReadOnlyList<InventarioMovimiento> MovimientosInventario,
    IReadOnlyList<DocumentoAsociado> DocumentosAsociados);

/// <summary>
/// Solicitud validada para crear, iniciar en 2 pasos o corregir un pesaje industrial.
/// </summary>
public sealed class PesajeRegistroRequestDto
{
    public long Id { get; init; }
    public long Folio { get; init; }
    public string FolioCodigo { get; init; } = string.Empty;
    public Planta Planta { get; init; } = Planta.Placilla;
    public TipoPesaje TipoPesaje { get; init; } = TipoPesaje.RecepcionCarga;
    public long? TipoPesajeConfigId { get; init; }
    public string Patente { get; init; } = string.Empty;
    public string Acoplado { get; init; } = string.Empty;
    public int PesoBruto { get; init; }
    public int PesoTara { get; init; }
    public int? PesoNetoManual { get; init; }
    public int? PrimeraMedicionKg { get; init; }
    public int? PesoEsperadoKg { get; init; }
    public string UnidadMedida { get; init; } = "KG";
    public string CodProducto { get; init; } = string.Empty;
    public string RutEntidad { get; init; } = string.Empty;
    public string NombreEntidad { get; init; } = string.Empty;
    public string Origen { get; init; } = string.Empty;
    public string Destino { get; init; } = string.Empty;
    public string GuiaDespacho { get; init; } = string.Empty;
    public string Chofer { get; init; } = string.Empty;
    public string Transportista { get; init; } = string.Empty;
    public string Bascula { get; init; } = "Báscula Principal";
    public string Operador { get; init; } = string.Empty;
    public DateOnly FechaIngreso { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly HoraIngreso { get; init; } = TimeOnly.FromDateTime(DateTime.Now);
    public DateOnly FechaSalida { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly HoraSalida { get; init; } = TimeOnly.FromDateTime(DateTime.Now);
    public long? OperacionId { get; init; }
    public string Observacion { get; init; } = string.Empty;
    public bool AjusteManualAutorizado { get; init; }
    public bool AutorizacionExcepcionTolerancia { get; init; }
    public string MotivoAjuste { get; init; } = string.Empty;
}

/// <summary>
/// Opción individual para un botón de segmentación multi-selección.
/// </summary>
public sealed record SlicerOptionDto(string Key, string Label, int Count);

/// <summary>
/// Colección de valores disponibles para todos los segmentadores multi-selección del centro de informes.
/// </summary>
public sealed record EnterpriseSlicerFacetsDto(
    IReadOnlyList<SlicerOptionDto> Proveedores,
    IReadOnlyList<SlicerOptionDto> Clientes,
    IReadOnlyList<SlicerOptionDto> Materiales,
    IReadOnlyList<SlicerOptionDto> GruposMaterial,
    IReadOnlyList<SlicerOptionDto> Patentes,
    IReadOnlyList<SlicerOptionDto> TiposPesaje,
    IReadOnlyList<SlicerOptionDto> Anios,
    IReadOnlyList<SlicerOptionDto> Meses,
    IReadOnlyList<SlicerOptionDto> Estados);

/// <summary>
/// Filtro maestro combinado para informes empresariales y búsqueda avanzada de pesajes.
/// Aplica OR dentro de cada categoría multi-selección y AND entre categorías distintas.
/// </summary>
public sealed class EnterpriseReportFilterDto
{
    public DateOnly? Desde { get; init; } = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateOnly? Hasta { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public Planta? Planta { get; init; }
    public string? FolioSearch { get; init; }
    public bool FolioExacto { get; init; }
    public string? PatenteSearch { get; init; }
    public bool PatenteExacta { get; init; }
    public string? RutSearch { get; init; }
    public bool RutExacto { get; init; }
    public string? OperadorSearch { get; init; }
    public bool ExcluirAnulados { get; init; } = true;
    public bool ExcluirTotalesCero { get; init; }

    public IReadOnlySet<string>? ProveedoresRuts { get; init; }
    public IReadOnlySet<string>? ClientesRuts { get; init; }
    public IReadOnlySet<string>? MaterialesCodigos { get; init; }
    public IReadOnlySet<string>? GruposMaterial { get; init; }
    public IReadOnlySet<string>? PatentesNorm { get; init; }
    public IReadOnlySet<TipoPesaje>? TiposPesaje { get; init; }
    public IReadOnlySet<int>? Anios { get; init; }
    public IReadOnlySet<int>? Meses { get; init; }
    public IReadOnlySet<EstadoPesaje>? Estados { get; init; }
}

public sealed record InformePatenteRowDto(
    string Patente,
    string TipoVehiculo,
    string EntidadAsociada,
    int CantidadPesajes,
    long TotalBrutoKg,
    long TotalTaraKg,
    long TotalNetoKg,
    double ParticipacionPct,
    DateOnly PrimerPesaje,
    DateOnly UltimoPesaje,
    string MaterialPrincipal);

public sealed record InformeMaterialRowDto(
    string CodProducto,
    string NombreMaterial,
    string GrupoMaterial,
    string Unidad,
    int CantidadPesajes,
    long TotalBrutoKg,
    long TotalTaraKg,
    long TotalNetoKg,
    double ParticipacionPct,
    long StockActualKg);

public sealed record InformeEntidadRowDto(
    string Rut,
    string Nombre,
    string RolDescripcion,
    string Ciudad,
    int CantidadPesajes,
    long TotalBrutoKg,
    long TotalTaraKg,
    long TotalNetoKg,
    double ParticipacionPct,
    DateOnly UltimoPesaje,
    string MaterialPrincipal);

public sealed record InformeTipoPesajeRowDto(
    TipoPesaje Tipo,
    string NombreTipo,
    int CantidadPesajes,
    long TotalBrutoKg,
    long TotalTaraKg,
    long TotalNetoKg,
    double ParticipacionPct,
    double PromedioNetoKg);

/// <summary>
/// Resultado consolidado del centro de informes empresariales. Todas las vistas y KPIs derivan
/// de la misma fuente filtrada para asegurar 100 % de consistencia entre tablas, gráficos y exportaciones.
/// </summary>
public sealed record EnterpriseReportResultDto(
    string PeriodoLabel,
    string FiltrosActivosResumen,
    int CantidadPesajes,
    long TotalBrutoKg,
    long TotalTaraKg,
    long TotalNetoKg,
    double TotalToneladas,
    double PromedioNetoKg,
    int PatentesUnicas,
    int MaterialesUnicos,
    int EntidadesUnicas,
    IReadOnlyList<InformePatenteRowDto> PorPatente,
    IReadOnlyList<InformeMaterialRowDto> PorMaterial,
    IReadOnlyList<InformeEntidadRowDto> PorProveedor,
    IReadOnlyList<InformeEntidadRowDto> PorCliente,
    IReadOnlyList<InformeEntidadRowDto> PorRut,
    IReadOnlyList<InformeTipoPesajeRowDto> PorTipoPesaje,
    IReadOnlyList<Pesaje> DetallePesajes,
    EnterpriseSlicerFacetsDto Facets,
    IReadOnlyList<BarChartItemDto> TopChartItems);
