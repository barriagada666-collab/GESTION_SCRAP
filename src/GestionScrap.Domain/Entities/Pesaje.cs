using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Entities;

/// <summary>
/// Registro consolidado de pesaje de camión en báscula.
/// </summary>
public sealed class Pesaje
{
    public long Id { get; init; }
    public long Folio { get; init; }
    public Planta Planta { get; init; }
    public TipoPesaje TipoPesaje { get; init; }
    public string Patente { get; init; } = string.Empty;
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
    public DateOnly FechaIngreso { get; init; }
    public TimeOnly HoraIngreso { get; init; }
    public DateOnly FechaSalida { get; init; }
    public TimeOnly HoraSalida { get; init; }
    public string Observacion { get; init; } = string.Empty;
    public string GrupoMaterial { get; init; } = "GENERAL";
    public bool EsTraslado { get; init; }
    public string? CiudadCliente { get; init; }
}
