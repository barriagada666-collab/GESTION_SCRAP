namespace GestionScrap.Domain.Enums;

/// <summary>
/// Clasificación operacional del movimiento registrado en báscula (compatibilidad 100 % con códigos históricos 1/2/3).
/// </summary>
public enum TipoPesaje
{
    RecepcionCarga = 1,
    DespachoCarga = 2,
    MovimientoInterno = 3,
    Entrada = 4,
    Salida = 5,
    Carga = 6,
    Descarga = 7,
    Individual = 8,
    EntradaSalida = 9
}

public static class TipoPesajeExtensions
{
    public static string ToDisplayName(this TipoPesaje tipo) => tipo switch
    {
        TipoPesaje.RecepcionCarga => "Recepción de Carga",
        TipoPesaje.DespachoCarga => "Despacho de Carga",
        TipoPesaje.MovimientoInterno => "Movimiento Interno",
        TipoPesaje.Entrada => "Pesaje de Entrada",
        TipoPesaje.Salida => "Pesaje de Salida",
        TipoPesaje.Carga => "Pesaje de Carga",
        TipoPesaje.Descarga => "Pesaje de Descarga",
        TipoPesaje.Individual => "Pesaje Individual",
        TipoPesaje.EntradaSalida => "Entrada y Salida (2 Mediciones)",
        _ => "Recepción de Carga"
    };

    public static string ToLegacyLabel(this TipoPesaje tipo) => tipo switch
    {
        TipoPesaje.RecepcionCarga => "Recepcion de carga",
        TipoPesaje.DespachoCarga => "Despacho de Carga",
        TipoPesaje.MovimientoInterno => "Movimiento Interno",
        _ => tipo.ToDisplayName()
    };

    public static bool EsIngresoInventario(this TipoPesaje tipo) => tipo is
        TipoPesaje.RecepcionCarga or
        TipoPesaje.Entrada or
        TipoPesaje.Descarga or
        TipoPesaje.EntradaSalida;

    public static bool EsSalidaInventario(this TipoPesaje tipo) => tipo is
        TipoPesaje.DespachoCarga or
        TipoPesaje.Salida or
        TipoPesaje.Carga;

    public static TipoPesaje FromLegacyValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TipoPesaje.RecepcionCarga;
        }

        string v = raw.Trim().ToUpperInvariant();
        if (v == "2" || v.Contains("DESPACHO", StringComparison.Ordinal))
        {
            return TipoPesaje.DespachoCarga;
        }

        if (v == "3" || v.Contains("INTERNO", StringComparison.Ordinal))
        {
            return TipoPesaje.MovimientoInterno;
        }

        if (v == "4" || v == "ENTRADA")
        {
            return TipoPesaje.Entrada;
        }

        if (v == "5" || v == "SALIDA")
        {
            return TipoPesaje.Salida;
        }

        if (v == "6" || v == "CARGA")
        {
            return TipoPesaje.Carga;
        }

        if (v == "7" || v == "DESCARGA")
        {
            return TipoPesaje.Descarga;
        }

        if (v == "8" || v == "INDIVIDUAL")
        {
            return TipoPesaje.Individual;
        }

        if (v == "9" || v.Contains("ENTRADA_SALIDA", StringComparison.Ordinal))
        {
            return TipoPesaje.EntradaSalida;
        }

        return TipoPesaje.RecepcionCarga;
    }
}
