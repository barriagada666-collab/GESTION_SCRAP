using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Entities;

public sealed class Cliente
{
    public string Rut { get; init; } = string.Empty;
    public string Digito { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string RazonSocial { get; init; } = string.Empty;
    public string Giro { get; init; } = string.Empty;
    public string Comuna { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
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

/// <summary>
/// Nodo jerárquico editable para organizar Grupos de Material (Nodos Padre / Subgrupos / Productos Hijo).
/// </summary>
public sealed class MaterialGroupNode
{
    public long Id { get; init; }
    public long? ParentId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public NodoGrupoTipo TipoNodo { get; init; } = NodoGrupoTipo.CarpetaGrupo;
    public string CodigoProducto { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#0891B2";
    public int Orden { get; init; }
}
