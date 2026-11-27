using System.Globalization;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Entities;

/// <summary>
/// Registro integral de pesaje industrial, comercial y logístico (importado desde báscula Firebird/Access
/// o administrado con flujo de 1 o 2 mediciones, trazabilidad ERP e inventario).
/// </summary>
public sealed class Pesaje
{
    public long Id { get; init; }
    public long Folio { get; init; }
    public string FolioCodigo { get; init; } = string.Empty;
    public Planta Planta { get; init; }
    public TipoPesaje TipoPesaje { get; init; }
    public string Patente { get; init; } = string.Empty;
    public string Acoplado { get; init; } = string.Empty;
    public int PesoBruto { get; init; }
    public int PesoTara { get; init; }
    public int PesoNeto { get; init; }
    public string UnidadMedida { get; init; } = "KG";
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
    public string GrupoMaterial { get; init; } = "GENERAL";
    public bool EsTraslado { get; init; }
    public string? CiudadCliente { get; init; }

    // Campos ERP / Pesajes Industriales v5.0
    public string Bascula { get; init; } = string.Empty;
    public string Operador { get; init; } = string.Empty;
    public EstadoPesaje Estado { get; init; } = EstadoPesaje.Confirmado;
    public long? TipoPesajeConfigId { get; init; }
    public long? OperacionId { get; init; }
    public long? VehiculoId { get; init; }
    public int? PrimeraMedicionKg { get; init; }
    public DateTime? PrimeraMedicionUtc { get; init; }
    public int? SegundaMedicionKg { get; init; }
    public DateTime? SegundaMedicionUtc { get; init; }
    public int? PesoEsperadoKg { get; init; }
    public bool AjusteManualAutorizado { get; init; }
    public string MotivoAjusteOAnulacion { get; init; } = string.Empty;
    public string ModificadoPor { get; init; } = string.Empty;
    public DateTime? ModificadoEnUtc { get; init; }

    public string FolioDisplay => string.IsNullOrWhiteSpace(FolioCodigo)
        ? Folio.ToString(CultureInfo.InvariantCulture)
        : FolioCodigo;

    public string TipoPesajeDisplay => TipoPesaje.ToDisplayName();
    public string EstadoDisplay => Estado.ToDisplayName();
}
