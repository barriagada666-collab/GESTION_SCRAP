using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Domain.Entities;

/// <summary>
/// Entidad comercial unificada CRM (Cliente, Proveedor o Ambos) identificada por RUT único sin duplicación.
/// </summary>
public sealed class Cliente
{
    public string Rut { get; init; } = string.Empty;
    public string Digito { get; init; } = string.Empty;
    public string RutNormalizado { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string RazonSocial { get; init; } = string.Empty;
    public string NombreComercial { get; init; } = string.Empty;
    public string Giro { get; init; } = string.Empty;
    public string Comuna { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string ContactoNombre { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string CondicionComercial { get; init; } = string.Empty;
    public string Observaciones { get; init; } = string.Empty;
    public bool EsCliente { get; init; } = true;
    public bool EsProveedor { get; init; } = true;
    public bool Activo { get; init; } = true;
    public Planta? PlantaAsociada { get; init; }
    public DateTime? ActualizadoEnUtc { get; init; }

    public string RutFormateado => string.IsNullOrWhiteSpace(Digito) ? Rut : $"{Rut}-{Digito}";

    public string RutClave => string.IsNullOrWhiteSpace(RutNormalizado)
        ? RutChileno.NormalizeBody(RutFormateado)
        : RutNormalizado;

    public RolEntidadComercial Rol => (EsCliente, EsProveedor) switch
    {
        (true, true) => RolEntidadComercial.Ambos,
        (true, false) => RolEntidadComercial.Cliente,
        _ => RolEntidadComercial.Proveedor
    };

    public string RolDescripcion => Rol.ToDisplayName();
    public string EstadoDescripcion => Activo ? "Activo" : "Inactivo";
}

/// <summary>
/// Registro maestro de material reutilizado en pesajes, árbol de grupos, órdenes ERP e inventario.
/// </summary>
public sealed class Producto
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Grupo { get; init; } = "GENERAL";
    public string UnidadMedida { get; init; } = "KG";
    public string InformacionAdicional { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public double ToleranciaDiferenciaPct { get; init; } = 5.0;
    public DateTime? ActualizadoEnUtc { get; init; }

    public string NombreVisible => string.IsNullOrWhiteSpace(Nombre) ? Descripcion : Nombre;
    public string EstadoDescripcion => Activo ? "Activo" : "Inactivo";
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
