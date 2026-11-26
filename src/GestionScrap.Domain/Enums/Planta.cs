namespace GestionScrap.Domain.Enums;

/// <summary>
/// Plantas operativas del sistema de pesajes de scrap.
/// </summary>
public enum Planta
{
    Placilla = 1,
    Santiago = 2,
    Talcahuano = 3
}

public static class PlantaExtensions
{
    public static string ToDisplayName(this Planta planta) => planta switch
    {
        Planta.Placilla => "PLACILLA",
        Planta.Santiago => "SANTIAGO",
        Planta.Talcahuano => "TALCAHUANO",
        _ => "PLACILLA"
    };

    public static string ToShortTitle(this Planta planta) => planta switch
    {
        Planta.Placilla => "Placilla",
        Planta.Santiago => "Santiago",
        Planta.Talcahuano => "Talcahuano",
        _ => "Placilla"
    };

    public static Planta ParseFlexible(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Planta.Placilla;
        }

        string v = value.Trim().ToUpperInvariant();
        if (v.Contains("STGO", StringComparison.Ordinal) || v.Contains("SANTIAGO", StringComparison.Ordinal))
        {
            return Planta.Santiago;
        }

        if (v.Contains("THNO", StringComparison.Ordinal) || v.Contains("TALCAHUANO", StringComparison.Ordinal))
        {
            return Planta.Talcahuano;
        }

        return Planta.Placilla;
    }
}
