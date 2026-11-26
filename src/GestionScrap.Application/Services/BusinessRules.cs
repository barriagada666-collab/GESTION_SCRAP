using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Application.Services;

/// <summary>
/// Reglas de negocio puras del dominio de pesajes, tarifas, agrupaciones y prevención de duplicados.
/// Consolida y corrige la lógica migrada de los módulos legacy VBA y vistas Access.
/// </summary>
public static class BusinessRules
{
    private static readonly string[] RetailStgoRuts =
        ["07629801", "76579070", "03649695", "13699851", "13681758", "77934420", "07955483", "76005403", "76032472", "05801156", "10894204", "09536956"];

    private static readonly string[] RetailEmpresasRuts =
        ["95293000", "84898000", "91362000", "96529310", "81201000", "89996200"];

    private static readonly string[] RetailRegionesStgoRuts =
        ["76047074", "06959040", "12245169", "09444237", "65313500", "11084756", "76064906", "02925626", "10015029", "12599861", "10038755", "77358680", "10913603", "10803189", "16986312", "15864708"];

    private static readonly string[] MartinoSosRuts = ["12749419", "76029117"];
    private static readonly string[] MartinoSosProds = ["5301003001", "5301003000", "5301002003", "5301002001"];

    private static readonly string[] AgricolaRuts = ["09165318", "07377653", "10286881", "12821515", "76218764"];
    private static readonly string[] AgricolaProds = ["5301001002", "5301002000", "5301002001", "5301002003", "5301003001"];

    private static readonly string[] PesqueraRuts = ["76658930", "11919881", "76029117", "52003786", "12749419", "76064160"];
    private static readonly string[] PesqueraProds = ["5301002000", "5301001000", "5301002002", "5300000006"];

    private static readonly string[] AgrosuperRuts = ["78483600", "77476390"];

    private static readonly string[] JcrammExcludedCodes =
        ["CARTON", "5301001003", "0000000005", "XXXXXX", "5302000002", "PLASTICOS", "TIX-14", "CONO"];

    public static (bool Allowed, string Message) ValidateCanUpdatePrice(ModoPrecio modo)
    {
        if (modo != ModoPrecio.Reales)
        {
            return (false, "Para actualizar el precio individual debe seleccionar la opción 'Precios Reales (Indexados)'.");
        }

        return (true, "Validación exitosa.");
    }

    public static decimal CalculateWeightedAverage(decimal totalPesos, long totalKilos)
    {
        if (totalKilos <= 0 || totalPesos <= 0)
        {
            return 0m;
        }

        return Math.Round(totalPesos / totalKilos, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal FindTheoreticalPrice(
        IEnumerable<PrecioProveedor> precios,
        Planta planta,
        string rut,
        string codProducto)
    {
        string cleanCod = codProducto.Trim().ToUpperInvariant();
        PrecioProveedor? found = precios.FirstOrDefault(p =>
            p.Planta == planta &&
            RutChileno.Matches(p.Rut, rut) &&
            string.Equals(p.CodigoProducto.Trim(), cleanCod, StringComparison.OrdinalIgnoreCase));

        return found?.Precio ?? 0m;
    }

    public static decimal FindRealPrice(
        IEnumerable<IndexPrecioPesaje> indexPrecios,
        Planta planta,
        long folio)
    {
        IndexPrecioPesaje? found = indexPrecios.FirstOrDefault(ip => ip.Planta == planta && ip.Folio == folio);
        return found?.Precio ?? 0m;
    }

    public static bool CheckProviderPriceExists(
        IEnumerable<PrecioProveedor> precios,
        Planta planta,
        string rut,
        string codProducto)
    {
        string cleanCod = codProducto.Trim().ToUpperInvariant();
        return precios.Any(p =>
            p.Planta == planta &&
            RutChileno.Matches(p.Rut, rut) &&
            string.Equals(p.CodigoProducto.Trim(), cleanCod, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDuplicatePesaje(
        IEnumerable<Pesaje> existingPesajes,
        long folio,
        Planta planta,
        TimeOnly horaIngreso)
    {
        if (planta == Planta.Talcahuano)
        {
            return existingPesajes.Any(p =>
                p.Planta == Planta.Talcahuano &&
                p.Folio == folio &&
                p.HoraIngreso == horaIngreso);
        }

        return existingPesajes.Any(p => p.Planta == planta && p.Folio == folio);
    }

    public static bool IsDateInRange(DateOnly fecha, DateOnly desde, DateOnly hasta) =>
        fecha >= desde && fecha <= hasta;

    public static bool IsExcludedInternalTransferClient(Pesaje p)
    {
        if (p.Planta == Planta.Placilla)
        {
            return RutChileno.Matches(p.RutCliente, "15071375") ||
                   p.NomCliente.Contains("BODEGA SAN BERNARDO", StringComparison.OrdinalIgnoreCase) ||
                   p.NomCliente.Contains("BODEGA CENTRAL SCRAP CAMBIASO", StringComparison.OrdinalIgnoreCase);
        }

        if (p.Planta == Planta.Santiago)
        {
            return RutChileno.Matches(p.RutCliente, "13856570") ||
                   p.NomCliente.Equals("SCRAP SANTIAGO", StringComparison.OrdinalIgnoreCase) ||
                   p.NomCliente.Contains("BODEGA CENTRAL SCRAP CAMBIASO", StringComparison.OrdinalIgnoreCase);
        }

        return RutChileno.Matches(p.RutCliente, "96781200");
    }

    public static bool IsCompra(Pesaje p) =>
        !p.EsTraslado &&
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        !IsExcludedInternalTransferClient(p);

    public static bool IsTraslado(Pesaje p)
    {
        if (p.EsTraslado || p.TipoPesaje == TipoPesaje.MovimientoInterno || p.TipoPesaje == TipoPesaje.DespachoCarga)
        {
            return true;
        }

        return IsExcludedInternalTransferClient(p);
    }

    public static bool IsJcrammEligible(Pesaje p)
    {
        string code = p.CodProducto.Trim().ToUpperInvariant();
        if (JcrammExcludedCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return p.TipoPesaje == TipoPesaje.RecepcionCarga;
    }

    public static string ClassifyGrupoMaterial(
        string codProducto,
        string nomProducto,
        IReadOnlyDictionary<string, string>? catalogGroups = null)
    {
        string code = codProducto.Trim().ToUpperInvariant();
        if (catalogGroups is not null && catalogGroups.TryGetValue(code, out string? catGroup) && !string.IsNullOrWhiteSpace(catGroup))
        {
            return catGroup.Trim().ToUpperInvariant();
        }

        string desc = nomProducto.Trim().ToUpperInvariant();
        if (desc.Contains("SUPER-BASULIP", StringComparison.Ordinal) || desc.Contains("(SUP)", StringComparison.Ordinal))
        {
            return "SUPER-BASULIP";
        }
        if (desc.Contains("RECAM", StringComparison.Ordinal) || desc.Contains("TRANSPARENTE", StringComparison.Ordinal))
        {
            return "RECAM";
        }
        if (desc.Contains("HDPE", StringComparison.Ordinal) || desc.Contains("ALTA", StringComparison.Ordinal))
        {
            return "ALTA";
        }
        if (desc.Contains("MIX", StringComparison.Ordinal))
        {
            return "MIX SUPERMERCADO";
        }
        if (desc.Contains("STRETCH", StringComparison.Ordinal))
        {
            return "STRETCH";
        }
        if (desc.Contains("MAQUIL", StringComparison.Ordinal) || desc.Contains("MERMA", StringComparison.Ordinal) || desc.Contains("CARTON", StringComparison.Ordinal))
        {
            return "MAQUILADO";
        }
        if (desc.Contains("BASURA", StringComparison.Ordinal))
        {
            return "BASURA";
        }
        if (desc.Contains("SCRAP", StringComparison.Ordinal))
        {
            return "SUPER-BASULIP";
        }
        return "GENERAL";
    }

    public static Pesaje MapRemoteToPesaje(
        FirebirdRemotePesaje raw,
        Planta planta,
        IReadOnlyDictionary<string, string>? catalogGroups = null)
    {
        TipoPesaje tipo = TipoPesajeExtensions.FromLegacyValue(raw.TipoPesajeRaw);
        int neto = raw.PesoNeto > 0 ? raw.PesoNeto : Math.Max(0, raw.PesoBruto - raw.PesoTara);
        string grupo = ClassifyGrupoMaterial(raw.CodProducto, raw.NomProducto, catalogGroups);
        bool esTraslado = tipo != TipoPesaje.RecepcionCarga ||
                          RutChileno.Matches(raw.RutCliente, "15071375") ||
                          RutChileno.Matches(raw.RutCliente, "13856570") ||
                          RutChileno.Matches(raw.RutCliente, "96781200");

        return new Pesaje
        {
            Folio = raw.Folio,
            Planta = planta,
            TipoPesaje = tipo,
            Patente = string.IsNullOrWhiteSpace(raw.Patente) ? "S/P" : raw.Patente.Trim().ToUpperInvariant(),
            PesoBruto = raw.PesoBruto,
            PesoTara = raw.PesoTara,
            PesoNeto = neto,
            CodProducto = raw.CodProducto.Trim().ToUpperInvariant(),
            NomProducto = string.IsNullOrWhiteSpace(raw.NomProducto) ? raw.CodProducto : raw.NomProducto.Trim().ToUpperInvariant(),
            RutCliente = raw.RutCliente.Trim().ToUpperInvariant(),
            NomCliente = string.IsNullOrWhiteSpace(raw.NomCliente) ? raw.RutCliente : raw.NomCliente.Trim(),
            Origen = raw.Origen.Trim(),
            Destino = raw.Destino.Trim(),
            GuiaDespacho = raw.GuiaDespacho.Trim(),
            FechaIngreso = raw.FechaIngreso,
            HoraIngreso = raw.HoraIngreso,
            FechaSalida = raw.FechaSalida,
            HoraSalida = raw.HoraSalida,
            Observacion = raw.Observacion.Trim(),
            GrupoMaterial = grupo,
            EsTraslado = esTraslado
        };
    }

    public static bool MatchesRetailStgo(Pesaje p) =>
        p.Planta == Planta.Santiago &&
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        (RetailStgoRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) ||
         p.NomCliente.Contains("Retail", StringComparison.OrdinalIgnoreCase) ||
         p.NomCliente.Contains("Cencosud", StringComparison.OrdinalIgnoreCase) ||
         p.NomCliente.Contains("Walmart", StringComparison.OrdinalIgnoreCase) ||
         p.NomCliente.Contains("SMU", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesRetailEmpresas(Pesaje p) =>
        p.Planta == Planta.Santiago &&
        (RetailEmpresasRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) ||
         p.NomCliente.Contains("Carozzi", StringComparison.OrdinalIgnoreCase) ||
         p.NomCliente.Contains("Empresas", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesMixStgo(Pesaje p) =>
        p.Planta == Planta.Santiago &&
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        (string.Equals(p.CodProducto.Trim(), "5300000009", StringComparison.OrdinalIgnoreCase) ||
         p.GrupoMaterial.Contains("MIX", StringComparison.OrdinalIgnoreCase) ||
         p.NomProducto.Contains("MIX", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesMixPlacilla(Pesaje p) =>
        p.Planta == Planta.Placilla &&
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        (string.Equals(p.CodProducto.Trim(), "5300000009", StringComparison.OrdinalIgnoreCase) ||
         p.GrupoMaterial.Contains("MIX", StringComparison.OrdinalIgnoreCase) ||
         p.NomProducto.Contains("MIX", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesRetailRegiones(Pesaje p)
    {
        if (p.TipoPesaje != TipoPesaje.RecepcionCarga)
        {
            return false;
        }

        if (p.Planta == Planta.Santiago && RetailRegionesStgoRuts.Any(r => RutChileno.Matches(p.RutCliente, r)))
        {
            return true;
        }

        if (p.Planta == Planta.Placilla &&
            MartinoSosRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) &&
            MartinoSosProds.Contains(p.CodProducto.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Planta == Planta.Placilla && RutChileno.Matches(p.RutCliente, "11548096"))
        {
            return true;
        }

        return p.NomCliente.Contains("Martino", StringComparison.OrdinalIgnoreCase) ||
               p.NomCliente.Contains("Johnson", StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesAgricola(Pesaje p) =>
        p.Planta == Planta.Placilla &&
        ((AgricolaRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) &&
          AgricolaProds.Contains(p.CodProducto.Trim(), StringComparer.OrdinalIgnoreCase)) ||
         p.NomCliente.Contains("Agricola", StringComparison.OrdinalIgnoreCase) ||
         p.NomCliente.Contains("Agrícola", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesPesquera(Pesaje p) =>
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        ((p.Planta == Planta.Placilla &&
          PesqueraRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) &&
          PesqueraProds.Contains(p.CodProducto.Trim(), StringComparer.OrdinalIgnoreCase)) ||
         p.NomCliente.Contains("Pesquera", StringComparison.OrdinalIgnoreCase) ||
         p.NomProducto.Contains("PESQUERA", StringComparison.OrdinalIgnoreCase));

    public static bool MatchesAgrosuper(Pesaje p) =>
        p.Planta == Planta.Placilla &&
        p.TipoPesaje == TipoPesaje.RecepcionCarga &&
        (AgrosuperRuts.Any(r => RutChileno.Matches(p.RutCliente, r)) ||
         p.NomCliente.Contains("Agrosuper", StringComparison.OrdinalIgnoreCase));
}
