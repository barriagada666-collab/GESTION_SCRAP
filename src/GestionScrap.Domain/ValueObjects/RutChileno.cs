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

    /// <summary>
    /// Calcula el dígito verificador chileno (Módulo 11) para un cuerpo numérico válido.
    /// </summary>
    public static string ComputeDv(string? bodyDigits)
    {
        string body = Clean(bodyDigits).TrimStart('0');
        if (string.IsNullOrEmpty(body) || !body.All(char.IsAsciiDigit))
        {
            return string.Empty;
        }

        int sum = 0;
        int multiplier = 2;
        for (int i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * multiplier;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        int rem = 11 - (sum % 11);
        return rem switch
        {
            11 => "0",
            10 => "K",
            _ => rem.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Valida sintaxis y dígito verificador chileno cuando se proporciona con guión o formato completo.
    /// </summary>
    public static (bool IsValid, string Body, string Dv, string Formatted, string Message) ValidateAndFormat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (false, string.Empty, string.Empty, string.Empty, "El RUT es obligatorio.");
        }

        string trimmed = raw.Trim().ToUpperInvariant();
        string body;
        string? providedDv = null;

        int dashIdx = trimmed.IndexOf('-');
        if (dashIdx > 0)
        {
            body = Clean(trimmed[..dashIdx]).TrimStart('0');
            providedDv = Clean(trimmed[(dashIdx + 1)..]);
        }
        else
        {
            string cleaned = Clean(trimmed).TrimStart('0');
            if (cleaned.Length < 2)
            {
                return (false, string.Empty, string.Empty, string.Empty, "El RUT ingresado es demasiado corto.");
            }

            body = cleaned[..^1];
            providedDv = cleaned[^1..];
        }

        if (body.Length < 6 || body.Length > 9 || !body.All(char.IsAsciiDigit))
        {
            return (false, string.Empty, string.Empty, string.Empty, "El cuerpo numérico del RUT debe tener entre 6 y 9 dígitos.");
        }

        string expectedDv = ComputeDv(body);
        if (!string.IsNullOrEmpty(providedDv) && !string.Equals(providedDv, expectedDv, StringComparison.OrdinalIgnoreCase))
        {
            return (false, body, expectedDv, $"{body}-{expectedDv}", $"Dígito verificador inválido para el RUT {body} (esperado: {expectedDv}).");
        }

        return (true, body, expectedDv, $"{body}-{expectedDv}", "RUT válido.");
    }
}
