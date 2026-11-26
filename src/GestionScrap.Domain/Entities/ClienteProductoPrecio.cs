using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Entities;

public sealed class Cliente
{
    public string Rut { get; init; } = string.Empty;
    public string Digito { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string RazonSocial { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public Planta? PlantaAsociada { get; init; }

    public string RutFormateado => string.IsNullOrWhiteSpace(Digito) ? Rut : $"{Rut}-{Digito}";
}

public sealed class Producto
{
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Grupo { get; init; } = "GENERAL";
    public string UnidadMedida { get; init; } = "KG";
}

public sealed class PrecioProveedor
{
    public long Indice { get; init; }
    public Planta Planta { get; init; }
    public string Rut { get; init; } = string.Empty;
    public string RazonSocial { get; init; } = string.Empty;
    public string CodigoProducto { get; init; } = string.Empty;
    public decimal Precio { get; init; }
    public DateOnly FechaActualizacion { get; init; }
}

public sealed class IndexPrecioPesaje
{
    public long Folio { get; init; }
    public Planta Planta { get; init; }
    public decimal Precio { get; init; }
    public string Familia { get; init; } = string.Empty;
    public DateTime ActualizadoEn { get; init; }
}

public sealed class UltimoFolioJcramm
{
    public long Folio { get; init; }
    public DateOnly Fecha { get; init; }
}
