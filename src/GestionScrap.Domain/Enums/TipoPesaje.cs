namespace GestionScrap.Domain.Enums;

/// <summary>
/// Clasificación del movimiento registrado en báscula.
/// </summary>
public enum TipoPesaje
{
    RecepcionCarga = 1,
    DespachoCarga = 2,
    MovimientoInterno = 3
}

public static class TipoPesajeExtensions
{
    public static string ToLegacyLabel(this TipoPesaje tipo) => tipo switch
    {
        TipoPesaje.RecepcionCarga => "Recepcion de carga",
        TipoPesaje.DespachoCarga => "Despacho de Carga",
        TipoPesaje.MovimientoInterno => "Movimiento Interno",
        _ => "Recepcion de carga"
    };

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

        return TipoPesaje.RecepcionCarga;
    }
}
