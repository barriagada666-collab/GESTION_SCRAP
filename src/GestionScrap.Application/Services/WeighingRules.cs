using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Application.Services;

/// <summary>
/// Resultado validado del cálculo determinista de pesos industriales (Bruto, Tara, Neto y Unidad).
/// </summary>
public sealed record WeighingCalculationResult(
    int PesoBruto,
    int PesoTara,
    int PesoNeto,
    string UnidadMedida,
    bool UsoAjusteManualAutorizado);

/// <summary>
/// Resultado de evaluación de tolerancia entre peso esperado (orden/guía) y peso neto real en báscula.
/// </summary>
public sealed record WeightDiscrepancyResult(
    bool DentroTolerancia,
    int DiferenciaKg,
    double DiferenciaPct,
    bool RequiereAutorizacion,
    string Resumen);

/// <summary>
/// Impacto determinista de un pesaje confirmado sobre el libro mayor de inventario.
/// </summary>
public sealed record InventoryImpactResult(
    bool AfectaInventario,
    TipoMovimientoInventario TipoMovimiento,
    long CantidadFirmadaKg);

/// <summary>
/// Reglas puras y estrictas de cálculo y validación de pesajes industriales (1 y 2 mediciones,
/// consistencia de unidades, protección contra sobrescritura manual no autorizada y control de tolerancias).
/// </summary>
public static class WeighingRules
{
    private static readonly HashSet<string> SupportedUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "KG",
        "TON"
    };

    public static string NormalizeUnit(string? unit)
    {
        string normalized = string.IsNullOrWhiteSpace(unit) ? "KG" : unit.Trim().ToUpperInvariant();
        if (normalized is "KGS" or "KILO" or "KILOS")
        {
            normalized = "KG";
        }
        else if (normalized is "T" or "TN" or "TONS" or "TONELADA" or "TONELADAS")
        {
            normalized = "TON";
        }

        if (!SupportedUnits.Contains(normalized))
        {
            throw new ArgumentException($"Unidad de medida no válida: '{unit}'. Unidades soportadas: KG o TON.");
        }

        return normalized;
    }

    public static void ValidateUnitConsistency(string? unitWeighing, string? unitMaterial)
    {
        string uPesaje = NormalizeUnit(unitWeighing);
        if (string.IsNullOrWhiteSpace(unitMaterial))
        {
            return;
        }

        string uMat = NormalizeUnit(unitMaterial);
        if (!string.Equals(uPesaje, uMat, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Inconsistencia de unidades de medida: el pesaje usa '{uPesaje}' pero el material maestro está configurado en '{uMat}'.");
        }
    }

    /// <summary>
    /// Valida y calcula un pesaje directo (1 paso): Neto = Bruto - Tara, sin pesos negativos ni inconsistencias.
    /// Impide sobrescribir el peso neto manualmente salvo ajuste autorizado con motivo explícito.
    /// </summary>
    public static WeighingCalculationResult ValidateAndCalculateSingleStep(
        int pesoBruto,
        int pesoTara,
        int? pesoNetoManual = null,
        bool ajusteManualAutorizado = false,
        string? motivoAjuste = null,
        string? unidadMedida = "KG",
        string? unidadMaterial = null)
    {
        string unit = NormalizeUnit(unidadMedida);
        ValidateUnitConsistency(unit, unidadMaterial);

        if (pesoBruto < 0 || pesoTara < 0)
        {
            throw new ArgumentException("Los pesos Bruto y Tara no pueden ser negativos.");
        }

        if (pesoBruto == 0)
        {
            throw new ArgumentException("El peso Bruto debe ser mayor que cero para registrar un pesaje completo.");
        }

        if (pesoBruto < pesoTara)
        {
            throw new ArgumentException($"El peso Bruto ({pesoBruto:N0} {unit}) no puede ser menor que la Tara ({pesoTara:N0} {unit}).");
        }

        int netoCalculado = pesoBruto - pesoTara;
        if (netoCalculado < 0)
        {
            throw new InvalidOperationException("El peso neto calculado no puede ser negativo.");
        }

        int netoFinal = netoCalculado;
        bool usedOverride = false;

        if (pesoNetoManual.HasValue && pesoNetoManual.Value != netoCalculado)
        {
            if (!ajusteManualAutorizado || string.IsNullOrWhiteSpace(motivoAjuste))
            {
                throw new InvalidOperationException(
                    $"No se permite modificar manualmente el peso neto ({pesoNetoManual.Value:N0} vs calculado {netoCalculado:N0} {unit}) sin ajuste autorizado y motivo registrado.");
            }

            if (pesoNetoManual.Value < 0 || pesoNetoManual.Value > pesoBruto)
            {
                throw new ArgumentException($"El peso neto ajustado ({pesoNetoManual.Value:N0} {unit}) debe estar entre 0 y el peso Bruto ({pesoBruto:N0} {unit}).");
            }

            netoFinal = pesoNetoManual.Value;
            usedOverride = true;
        }

        return new WeighingCalculationResult(pesoBruto, pesoTara, netoFinal, unit, usedOverride);
    }

    /// <summary>
    /// Valida el inicio de un pesaje en dos etapas (1ª medición). Nunca calcula peso neto definitivo.
    /// </summary>
    public static string ValidateFirstMeasurement(int primeraMedicionKg, string? unidadMedida = "KG", string? unidadMaterial = null)
    {
        string unit = NormalizeUnit(unidadMedida);
        ValidateUnitConsistency(unit, unidadMaterial);

        if (primeraMedicionKg <= 0)
        {
            throw new ArgumentException($"La primera medición debe ser un valor positivo mayor que cero ({unit}).");
        }

        return unit;
    }

    /// <summary>
    /// Resuelve un pesaje de entrada y salida al registrar la 2ª medición:
    /// Bruto = mayor medición, Tara = menor medición, Neto = Bruto - Tara.
    /// </summary>
    public static WeighingCalculationResult ResolveTwoStepMeasurements(
        int primeraMedicionKg,
        int segundaMedicionKg,
        string? unidadMedida = "KG",
        string? unidadMaterial = null)
    {
        string unit = NormalizeUnit(unidadMedida);
        ValidateUnitConsistency(unit, unidadMaterial);

        if (primeraMedicionKg <= 0 || segundaMedicionKg <= 0)
        {
            throw new ArgumentException($"Tanto la primera como la segunda medición deben ser mayores que cero ({unit}).");
        }

        int bruto = Math.Max(primeraMedicionKg, segundaMedicionKg);
        int tara = Math.Min(primeraMedicionKg, segundaMedicionKg);
        int neto = bruto - tara;

        return new WeighingCalculationResult(bruto, tara, neto, unit, UsoAjusteManualAutorizado: false);
    }

    /// <summary>
    /// Evalúa si existe una diferencia entre peso esperado y real superior a la tolerancia configurada del material.
    /// </summary>
    public static WeightDiscrepancyResult EvaluateWeightDiscrepancy(int pesoNetoRealKg, int? pesoEsperadoKg, double toleranciaPct)
    {
        if (!pesoEsperadoKg.HasValue || pesoEsperadoKg.Value <= 0)
        {
            return new WeightDiscrepancyResult(true, 0, 0.0, false, "Sin peso esperado de referencia.");
        }

        int diffKg = pesoNetoRealKg - pesoEsperadoKg.Value;
        double diffPct = Math.Round(Math.Abs(diffKg) * 100.0 / pesoEsperadoKg.Value, 2);
        double tol = Math.Max(0.0, toleranciaPct);
        bool exceeds = diffPct > tol;

        string summary = exceeds
            ? $"Diferencia de {diffKg:+#,##0;-#,##0;0} Kg ({diffPct:0.##}%) excede la tolerancia permitida ({tol:0.##}%): requiere autorización."
            : $"Diferencia de {diffKg:+#,##0;-#,##0;0} Kg ({diffPct:0.##}%) dentro de la tolerancia ({tol:0.##}%).";

        return new WeightDiscrepancyResult(!exceeds, diffKg, diffPct, exceeds, summary);
    }

    /// <summary>
    /// Determina el tipo y signo del movimiento de inventario para un pesaje confirmado.
    /// </summary>
    public static InventoryImpactResult ResolveInventoryImpact(TipoPesaje tipo, TipoPesajeConfig? config, int pesoNeto)
    {
        if (pesoNeto <= 0)
        {
            return new InventoryImpactResult(false, TipoMovimientoInventario.Entrada, 0);
        }

        if (config is not null)
        {
            if (!config.AfectaInventario || config.DireccionInventario == 0)
            {
                return new InventoryImpactResult(false, TipoMovimientoInventario.Entrada, 0);
            }

            if (config.DireccionInventario > 0)
            {
                TipoMovimientoInventario movIn = tipo == TipoPesaje.RecepcionCarga
                    ? TipoMovimientoInventario.Recepcion
                    : TipoMovimientoInventario.Entrada;
                return new InventoryImpactResult(true, movIn, pesoNeto);
            }

            TipoMovimientoInventario movOut = tipo == TipoPesaje.DespachoCarga
                ? TipoMovimientoInventario.Despacho
                : TipoMovimientoInventario.Salida;
            return new InventoryImpactResult(true, movOut, -pesoNeto);
        }

        if (tipo.EsIngresoInventario())
        {
            TipoMovimientoInventario movIn = tipo == TipoPesaje.RecepcionCarga
                ? TipoMovimientoInventario.Recepcion
                : TipoMovimientoInventario.Entrada;
            return new InventoryImpactResult(true, movIn, pesoNeto);
        }

        if (tipo.EsSalidaInventario())
        {
            TipoMovimientoInventario movOut = tipo == TipoPesaje.DespachoCarga
                ? TipoMovimientoInventario.Despacho
                : TipoMovimientoInventario.Salida;
            return new InventoryImpactResult(true, movOut, -pesoNeto);
        }

        return new InventoryImpactResult(false, TipoMovimientoInventario.Entrada, 0);
    }
}
