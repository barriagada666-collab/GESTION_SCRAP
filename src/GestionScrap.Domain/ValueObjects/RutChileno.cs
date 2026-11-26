namespace GestionScrap.Domain.ValueObjects;

/// <summary>
/// Utilidades de normalización y comparación segura de RUT chilenos
/// considerando variantes históricas con o sin dígito verificador y ceros iniciales.
/// </summary>
public static class RutChileno
{
    public static string Clean(string? raw)
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
            if (char.IsAsciiDigit(upper) || upper == 'K')
            {
                buffer[idx++] = upper;
            }
        }
        return new string(buffer[..idx]);
    }

    /// <summary>
    /// Normaliza el cuerpo numérico sin ceros a la izquierda para comparaciones consistentes.
    /// Si se entrega con dígito verificador separado por guión (ej. "76218764-5"), extrae el cuerpo antes del guión.
    /// </summary>
    public static string NormalizeBody(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string trimmed = raw.Trim().ToUpperInvariant();
        int dashIdx = trimmed.IndexOf('-');
        string bodyPart = dashIdx > 0 ? trimmed[..dashIdx] : trimmed;

        string cleaned = Clean(bodyPart).TrimStart('0');
        return cleaned;
    }

    public static bool Matches(string? left, string? right)
    {
        string lFull = Clean(left).TrimStart('0');
        string rFull = Clean(right).TrimStart('0');
        if (string.IsNullOrEmpty(lFull) || string.IsNullOrEmpty(rFull))
        {
            return false;
        }

        if (string.Equals(lFull, rFull, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string lBody = NormalizeBody(left);
        string rBody = NormalizeBody(right);
        if (string.Equals(lBody, rBody, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Handle legacy Placilla RUTs stored as body+DV concatenated (e.g. 150713757 vs 15071375)
        if (lFull.Length == rFull.Length + 1 && lFull.StartsWith(rFull, StringComparison.Ordinal))
        {
            return true;
        }

        if (rFull.Length == lFull.Length + 1 && rFull.StartsWith(lFull, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
