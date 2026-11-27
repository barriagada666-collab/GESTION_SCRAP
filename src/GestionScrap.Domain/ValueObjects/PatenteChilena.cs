namespace GestionScrap.Domain.ValueObjects;

/// <summary>
/// Normalización y comparación consistente de patentes vehiculares evitando duplicados por guiones, espacios o minúsculas.
/// </summary>
public static class PatenteChilena
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[raw.Length];
        int idx = 0;
        foreach (char ch in raw)
        {
            char upper = char.ToUpperInvariant(ch);
            if (char.IsAsciiLetterOrDigit(upper))
            {
                buffer[idx++] = upper;
            }
        }

        return new string(buffer[..idx]);
    }

    public static bool IsValidFormat(string? raw)
    {
        string norm = Normalize(raw);
        return norm.Length is >= 5 and <= 10 && norm != "SP" && norm != "SINPATENTE";
    }

    public static string FormatDisplay(string? raw)
    {
        string norm = Normalize(raw);
        if (norm.Length == 6)
        {
            return $"{norm[..2]}-{norm[2..4]}-{norm[4..6]}";
        }

        return norm;
    }

    public static bool MatchesExact(string? left, string? right)
    {
        string l = Normalize(left);
        string r = Normalize(right);
        return !string.IsNullOrEmpty(l) && string.Equals(l, r, StringComparison.Ordinal);
    }

    public static bool MatchesPartial(string? patente, string? query)
    {
        string q = Normalize(query);
        if (string.IsNullOrEmpty(q))
        {
            return true;
        }

        string p = Normalize(patente);
        return !string.IsNullOrEmpty(p) && p.Contains(q, StringComparison.Ordinal);
    }
}
