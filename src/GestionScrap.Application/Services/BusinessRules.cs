using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Application.Services;

/// <summary>
/// Motor de reglas de negocio puras: clasificación jerárquica de materiales, mapeo Firebird WMDBSPC,
/// detección de duplicados, comparativos YoY/MoM, segmentación dinámica y analítica Pareto ABC.
/// </summary>
public static class BusinessRules
{
    private static readonly CultureInfo EsCl = CultureInfo.GetCultureInfo("es-CL");

    public static readonly (string Nombre, string ColorHex)[] DefaultParentGroups =
    [
        ("SCRAP SUPERMERCADO & MIX", "#0891B2"),
        ("SCRAP PESQUERA", "#0B3C5D"),
        ("SCRAP AGRÍCOLA", "#059669"),
        ("SCRAP COLOR Y TRANSPARENTE", "#2563EB"),
        ("SCRAP HDPE / ALTA", "#C9A46E"),
        ("SCRAP STRETCH FILM", "#7C3AED"),
        ("MAQUILADOS Y DESECHOS", "#D97706"),
        ("PELLETS Y OTROS", "#475569")
    ];

    public static double CalculatePercentDelta(long current, long previous)
    {
        if (previous == 0)
        {
            return current > 0 ? 100.0 : 0.0;
        }

        return Math.Round((current - previous) * 100.0 / previous, 1);
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

    public static string ClassifyProductoDefaultGroup(string codProducto, string nomProducto)
    {
        string code = codProducto.Trim().ToUpperInvariant();
        string desc = nomProducto.Trim().ToUpperInvariant();

        if (code == "5300000009" || code == "5300000010" || desc.Contains("SUPERMERCADO", StringComparison.Ordinal) || desc.Contains("MIX POST", StringComparison.Ordinal))
        {
            return "SCRAP SUPERMERCADO & MIX";
        }

        if (desc.Contains("PESQUERA", StringComparison.Ordinal))
        {
            return "SCRAP PESQUERA";
        }

        if (desc.Contains("AGRICOLA", StringComparison.Ordinal) || desc.Contains("AGRÍCOLA", StringComparison.Ordinal) || desc.Contains("MANGA DE RIEGO", StringComparison.Ordinal))
        {
            return "SCRAP AGRÍCOLA";
        }

        if (desc.Contains("HDPE", StringComparison.Ordinal) || desc.Contains("ALTA", StringComparison.Ordinal) || desc.Contains("PLUMILLA", StringComparison.Ordinal))
        {
            return "SCRAP HDPE / ALTA";
        }

        if (desc.Contains("STRETCH", StringComparison.Ordinal))
        {
            return "SCRAP STRETCH FILM";
        }

        if (desc.Contains("MAQUIL", StringComparison.Ordinal) || desc.Contains("DESECHO", StringComparison.Ordinal) ||
            desc.Contains("BASURA", StringComparison.Ordinal) || desc.Contains("MERMA", StringComparison.Ordinal) ||
            desc.Contains("CARTON", StringComparison.Ordinal) || desc.Contains("FIERRO", StringComparison.Ordinal))
        {
            return "MAQUILADOS Y DESECHOS";
        }

        if (desc.Contains("COLOR", StringComparison.Ordinal) || desc.Contains("TRANSP", StringComparison.Ordinal) ||
            desc.Contains("BOBINA", StringComparison.Ordinal) || desc.Contains("PICADO", StringComparison.Ordinal))
        {
            return "SCRAP COLOR Y TRANSPARENTE";
        }

        if (desc.Contains("PELLET", StringComparison.Ordinal) || desc.Contains("INTERNO", StringComparison.Ordinal) ||
            desc.Contains("COAGULANTE", StringComparison.Ordinal) || desc.Contains("IMPORTADO", StringComparison.Ordinal))
        {
            return "PELLETS Y OTROS";
        }

        return "PELLETS Y OTROS";
    }

    public static Dictionary<string, string> BuildProductToRootGroupMap(IReadOnlyList<MaterialGroupNode> nodes)
    {
        Dictionary<long, MaterialGroupNode> byId = nodes.ToDictionary(n => n.Id);
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (MaterialGroupNode node in nodes.Where(n => n.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(n.CodigoProducto)))
        {
            MaterialGroupNode root = node;
            HashSet<long> visited = [root.Id];
            while (root.ParentId.HasValue && byId.TryGetValue(root.ParentId.Value, out MaterialGroupNode? parent) && visited.Add(parent.Id))
            {
                root = parent;
            }

            map[node.CodigoProducto.Trim().ToUpperInvariant()] = root.Nombre;
        }

        return map;
    }

    public static Pesaje MapRemoteToPesaje(
        FirebirdRemotePesaje raw,
        Planta planta,
        IReadOnlyDictionary<string, string>? catalogGroups = null)
    {
        TipoPesaje tipo = TipoPesajeExtensions.FromLegacyValue(raw.TipoPesajeRaw);
        int neto = raw.PesoNeto > 0 ? raw.PesoNeto : Math.Max(0, raw.PesoBruto - raw.PesoTara);
        string codeKey = raw.CodProducto.Trim().ToUpperInvariant();
        string grupo = catalogGroups is not null && catalogGroups.TryGetValue(codeKey, out string? mapped) && !string.IsNullOrWhiteSpace(mapped)
            ? mapped
            : ClassifyProductoDefaultGroup(raw.CodProducto, raw.NomProducto);

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
            Acoplado = raw.Acoplado.Trim().ToUpperInvariant(),
            PesoBruto = raw.PesoBruto,
            PesoTara = raw.PesoTara,
            PesoNeto = neto,
            CodProducto = codeKey,
            NomProducto = string.IsNullOrWhiteSpace(raw.NomProducto) ? codeKey : raw.NomProducto.Trim().ToUpperInvariant(),
            RutCliente = raw.RutCliente.Trim().ToUpperInvariant(),
            NomCliente = string.IsNullOrWhiteSpace(raw.NomCliente) ? raw.RutCliente.Trim() : raw.NomCliente.Trim(),
            Origen = raw.Origen.Trim(),
            Destino = raw.Destino.Trim(),
            GuiaDespacho = raw.GuiaDespacho.Trim(),
            Chofer = raw.Chofer.Trim(),
            Transportista = raw.Transportista.Trim(),
            FechaIngreso = raw.FechaIngreso,
            HoraIngreso = raw.HoraIngreso,
            FechaSalida = raw.FechaSalida,
            HoraSalida = raw.HoraSalida,
            Observacion = raw.Observacion.Trim(),
            GrupoMaterial = grupo,
            EsTraslado = esTraslado
        };
    }

    public static EstadisticaAvanzadaDto ComputeEstadisticas(
        IReadOnlyList<Pesaje> actuales,
        IReadOnlyList<Pesaje> scopeAllDates,
        DateOnly startDate,
        DateOnly endDate)
    {
        long totalKilos = actuales.Sum(p => (long)p.PesoNeto);
        double totalTon = Math.Round(totalKilos / 1000.0, 2);

        long minKg = 0;
        long maxKg = 0;
        double medianKg = 0;
        double stdDev = 0;
        int diasOp = 0;
        double promDiario = 0;
        int patentes = 0;

        if (actuales.Count > 0)
        {
            int[] sorted = [.. actuales.Select(p => p.PesoNeto).OrderBy(x => x)];
            minKg = sorted[0];
            maxKg = sorted[^1];
            int mid = sorted.Length / 2;
            medianKg = sorted.Length % 2 == 1 ? sorted[mid] : Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, 1);

            double mean = totalKilos / (double)sorted.Length;
            double variance = sorted.Sum(x => (x - mean) * (x - mean)) / sorted.Length;
            stdDev = Math.Round(Math.Sqrt(variance), 1);

            diasOp = actuales.Select(p => p.FechaIngreso).Distinct().Count();
            promDiario = diasOp > 0 ? Math.Round(totalKilos / (double)diasOp, 1) : 0.0;
            patentes = actuales
                .Select(p => p.Patente.Trim())
                .Where(pat => !string.IsNullOrEmpty(pat) && pat != "S/P")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        DateOnly yoyStart = startDate.AddYears(-1);
        DateOnly yoyEnd = endDate.AddYears(-1);
        long yoyKilos = scopeAllDates
            .Where(p => p.FechaIngreso >= yoyStart && p.FechaIngreso <= yoyEnd)
            .Sum(p => (long)p.PesoNeto);
        double yoyPct = CalculatePercentDelta(totalKilos, yoyKilos);

        var bestMonth = scopeAllDates
            .GroupBy(p => new DateOnly(p.FechaIngreso.Year, p.FechaIngreso.Month, 1))
            .Select(g => new { Label = g.Key.ToString("MMM yyyy", EsCl), Kilos = g.Sum(x => (long)x.PesoNeto) })
            .OrderByDescending(x => x.Kilos)
            .FirstOrDefault();

        var bestYear = scopeAllDates
            .GroupBy(p => p.FechaIngreso.Year)
            .Select(g => new { Label = g.Key.ToString(CultureInfo.InvariantCulture), Kilos = g.Sum(x => (long)x.PesoNeto) })
            .OrderByDescending(x => x.Kilos)
            .FirstOrDefault();

        return new EstadisticaAvanzadaDto(
            totalTon,
            minKg,
            maxKg,
            medianKg,
            stdDev,
            diasOp,
            promDiario,
            patentes,
            yoyKilos,
            yoyPct,
            bestMonth?.Label ?? "-",
            bestMonth?.Kilos ?? 0,
            bestYear?.Label ?? "-",
            bestYear?.Kilos ?? 0);
    }

    public static IReadOnlyList<ComparativoMensualDto> BuildComparativoMensual(
        IReadOnlyList<Pesaje> scopeAllDates,
        bool excluirTotalesCero = false)
    {
        var monthlyDict = scopeAllDates
            .GroupBy(p => (p.FechaIngreso.Year, p.FechaIngreso.Month))
            .ToDictionary(
                g => g.Key,
                g => (Viajes: g.Count(), Kilos: g.Sum(x => (long)x.PesoNeto)));

        List<(int Year, int Month)> orderedKeys = [.. monthlyDict.Keys.OrderBy(k => k.Year).ThenBy(k => k.Month)];
        List<ComparativoMensualDto> rows = new(orderedKeys.Count);

        foreach ((int year, int month) in orderedKeys)
        {
            (int viajes, long kilos) = monthlyDict[(year, month)];
            if (excluirTotalesCero && kilos <= 0)
            {
                continue;
            }

            (int prevY, int prevM) = month == 1 ? (year - 1, 12) : (year, month - 1);
            long prevMonthKilos = monthlyDict.TryGetValue((prevY, prevM), out var pm) ? pm.Kilos : 0L;
            long prevYearKilos = monthlyDict.TryGetValue((year - 1, month), out var py) ? py.Kilos : 0L;

            DateOnly dt = new(year, month, 1);
            rows.Add(new ComparativoMensualDto(
                year,
                month,
                dt.ToString("MMM yyyy", EsCl),
                viajes,
                kilos,
                prevMonthKilos,
                CalculatePercentDelta(kilos, prevMonthKilos),
                prevYearKilos,
                CalculatePercentDelta(kilos, prevYearKilos),
                viajes > 0 ? Math.Round(kilos / (double)viajes, 1) : 0.0));
        }

        return [.. rows.OrderByDescending(r => r.Anio).ThenByDescending(r => r.Mes)];
    }

    public static IReadOnlyList<ComparativoAnualDto> BuildComparativoAnual(
        IReadOnlyList<Pesaje> scopeAllDates,
        bool excluirTotalesCero = false)
    {
        Dictionary<int, (int Viajes, long Kilos, int Prov)> byYear = scopeAllDates
            .GroupBy(p => p.FechaIngreso.Year)
            .ToDictionary(
                g => g.Key,
                g => (
                    g.Count(),
                    g.Sum(x => (long)x.PesoNeto),
                    g.Select(x => RutChileno.NormalizeBody(x.RutCliente)).Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase).Count()));

        long grandTotal = Math.Max(1, byYear.Values.Sum(v => v.Kilos));
        List<ComparativoAnualDto> rows = [];

        foreach (int year in byYear.Keys.OrderByDescending(y => y))
        {
            (int viajes, long kilos, int provs) = byYear[year];
            if (excluirTotalesCero && kilos <= 0)
            {
                continue;
            }

            long prevYearKilos = byYear.TryGetValue(year - 1, out var py) ? py.Kilos : 0L;
            rows.Add(new ComparativoAnualDto(
                year,
                viajes,
                kilos,
                Math.Round(kilos / 1000.0, 2),
                prevYearKilos,
                CalculatePercentDelta(kilos, prevYearKilos),
                Math.Round(kilos * 100.0 / grandTotal, 1),
                viajes > 0 ? Math.Round(kilos / (double)viajes, 1) : 0.0,
                provs));
        }

        return rows;
    }

    public static IReadOnlyList<SegmentacionItemDto> BuildSegmentacion(
        SegmentacionDimension dimension,
        IReadOnlyList<Pesaje> actuales,
        IReadOnlyList<Pesaje> anteriores,
        IReadOnlyDictionary<string, string> prodToRootGroup,
        bool excluirTotalesCero = false)
    {
        (string Key, string Label) ResolveKey(Pesaje p) => dimension switch
        {
            SegmentacionDimension.Planta => (p.Planta.ToString(), p.Planta.ToShortTitle()),
            SegmentacionDimension.TipoPesaje => (p.TipoPesaje.ToString(), p.TipoPesaje.ToDisplayName()),
            SegmentacionDimension.Proveedor =>
                (string.IsNullOrWhiteSpace(RutChileno.NormalizeBody(p.RutCliente)) ? p.NomCliente.Trim().ToUpperInvariant() : RutChileno.NormalizeBody(p.RutCliente),
                 string.IsNullOrWhiteSpace(p.NomCliente) ? p.RutCliente : $"{p.NomCliente.Trim()} ({p.RutCliente.Trim()})"),
            SegmentacionDimension.Producto =>
                (p.CodProducto.Trim().ToUpperInvariant(), $"{p.CodProducto.Trim()} · {p.NomProducto.Trim()}"),
            SegmentacionDimension.Origen =>
                (string.IsNullOrWhiteSpace(p.Origen) ? "SIN ORIGEN" : p.Origen.Trim().ToUpperInvariant(),
                 string.IsNullOrWhiteSpace(p.Origen) ? "Sin origen registrado" : p.Origen.Trim()),
            SegmentacionDimension.Mes =>
                ($"{p.FechaIngreso.Year:D4}-{p.FechaIngreso.Month:D2}",
                 new DateOnly(p.FechaIngreso.Year, p.FechaIngreso.Month, 1).ToString("MMM yyyy", EsCl)),
            SegmentacionDimension.Anio =>
                (p.FechaIngreso.Year.ToString(CultureInfo.InvariantCulture),
                 p.FechaIngreso.Year.ToString(CultureInfo.InvariantCulture)),
            _ => ResolveMaterialGroupKey(p, prodToRootGroup)
        };

        static (string Key, string Label) ResolveMaterialGroupKey(Pesaje p, IReadOnlyDictionary<string, string> prodMap)
        {
            string code = p.CodProducto.Trim().ToUpperInvariant();
            string grp = prodMap.TryGetValue(code, out string? g) && !string.IsNullOrWhiteSpace(g) ? g : p.GrupoMaterial;
            return (grp.Trim().ToUpperInvariant(), grp.Trim());
        }

        Dictionary<string, long> prevMap = anteriores
            .GroupBy(p => ResolveKey(p).Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.PesoNeto), StringComparer.OrdinalIgnoreCase);

        long totalActual = Math.Max(1, actuales.Sum(p => (long)p.PesoNeto));

        var grouped = actuales
            .GroupBy(p => ResolveKey(p).Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                string label = ResolveKey(g.First()).Label;
                long kilos = g.Sum(x => (long)x.PesoNeto);
                int viajes = g.Count();
                long prevKilos = prevMap.TryGetValue(g.Key, out long pk) ? pk : 0L;
                int provs = g.Select(x => RutChileno.NormalizeBody(x.RutCliente)).Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase).Count();

                return new SegmentacionItemDto(
                    g.Key,
                    label,
                    viajes,
                    kilos,
                    Math.Round(kilos * 100.0 / totalActual, 1),
                    viajes > 0 ? Math.Round(kilos / (double)viajes, 1) : 0.0,
                    prevKilos,
                    CalculatePercentDelta(kilos, prevKilos),
                    provs);
            })
            .Where(s => !excluirTotalesCero || s.KilosActual > 0)
            .OrderByDescending(s => s.KilosActual)
            .ToList();

        return grouped;
    }

    public static GrupoMaterialReportResultDto BuildMaterialTreeReport(
        string periodoLabel,
        IReadOnlyList<MaterialGroupNode> nodes,
        IReadOnlyList<Pesaje> pesajes,
        IReadOnlyList<Producto> catalogProductos,
        long? filterSelectedNodeId = null,
        bool excluirTotalesCero = false)
    {
        IEnumerable<Pesaje> effectivePesajes = excluirTotalesCero ? pesajes.Where(p => p.PesoNeto > 0) : pesajes;
        List<Pesaje> filteredPesajes = [.. effectivePesajes];

        Dictionary<string, (int Viajes, long Kilos)> statsByCode = filteredPesajes
            .GroupBy(p => p.CodProducto.Trim().ToUpperInvariant())
            .ToDictionary(
                g => g.Key,
                g => (g.Count(), g.Sum(x => (long)x.PesoNeto)),
                StringComparer.OrdinalIgnoreCase);

        long totalPeriodKilos = filteredPesajes.Sum(p => (long)p.PesoNeto);
        int totalPeriodViajes = filteredPesajes.Count;

        Dictionary<long, List<MaterialGroupNode>> childrenByParent = nodes
            .GroupBy(n => n.ParentId ?? 0L)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(n => n.Orden).ThenBy(n => n.Nombre, StringComparer.OrdinalIgnoreCase).ToList());

        (int Viajes, long Kilos) ComputeSubtreeStats(long nodeId, HashSet<long> guard)
        {
            if (!guard.Add(nodeId))
            {
                return (0, 0);
            }

            MaterialGroupNode? current = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (current is null)
            {
                return (0, 0);
            }

            int v = 0;
            long k = 0;

            if (current.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(current.CodigoProducto))
            {
                if (statsByCode.TryGetValue(current.CodigoProducto.Trim().ToUpperInvariant(), out (int Viajes, long Kilos) s))
                {
                    v += s.Viajes;
                    k += s.Kilos;
                }
            }

            if (childrenByParent.TryGetValue(nodeId, out List<MaterialGroupNode>? kids))
            {
                foreach (MaterialGroupNode child in kids)
                {
                    (int cv, long ck) = ComputeSubtreeStats(child.Id, guard);
                    v += cv;
                    k += ck;
                }
            }

            return (v, k);
        }

        List<MaterialTreeFlatRowDto> flatRows = [];
        void Traverse(long parentKey, int level)
        {
            if (!childrenByParent.TryGetValue(parentKey, out List<MaterialGroupNode>? list))
            {
                return;
            }

            foreach (MaterialGroupNode node in list)
            {
                (int viajes, long kilos) = ComputeSubtreeStats(node.Id, []);
                if (excluirTotalesCero && kilos <= 0)
                {
                    continue;
                }

                double pct = totalPeriodKilos > 0 ? Math.Round(kilos * 100.0 / totalPeriodKilos, 2) : 0.0;
                double prom = viajes > 0 ? Math.Round((double)kilos / viajes, 1) : 0.0;
                string prefix = level == 0 ? string.Empty : new string(' ', level * 4) + "└─ ";

                flatRows.Add(new MaterialTreeFlatRowDto(
                    node.Id,
                    node.ParentId,
                    level,
                    prefix + node.Nombre,
                    node.Nombre,
                    node.TipoNodo,
                    node.CodigoProducto,
                    node.ColorHex,
                    viajes,
                    kilos,
                    pct,
                    prom));

                Traverse(node.Id, level + 1);
            }
        }

        Traverse(0L, 0);

        HashSet<string> assignedCodes = [.. nodes
            .Where(n => n.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(n.CodigoProducto))
            .Select(n => n.CodigoProducto.Trim().ToUpperInvariant())];

        List<KeyValuePair<string, (int Viajes, long Kilos)>> unassignedWithActivity = [.. statsByCode
            .Where(kv => !assignedCodes.Contains(kv.Key) && (!excluirTotalesCero || kv.Value.Kilos > 0))
            .OrderByDescending(kv => kv.Value.Kilos)];

        int unassignedCatalogCount = catalogProductos.Count(p => !assignedCodes.Contains(p.Codigo.Trim().ToUpperInvariant()));

        if (unassignedWithActivity.Count > 0)
        {
            int unViajes = unassignedWithActivity.Sum(x => x.Value.Viajes);
            long unKilos = unassignedWithActivity.Sum(x => x.Value.Kilos);
            double unPct = totalPeriodKilos > 0 ? Math.Round(unKilos * 100.0 / totalPeriodKilos, 2) : 0.0;
            double unProm = unViajes > 0 ? Math.Round((double)unKilos / unViajes, 1) : 0.0;

            flatRows.Add(new MaterialTreeFlatRowDto(
                -1,
                null,
                0,
                "SIN ASIGNAR EN ÁRBOL",
                "SIN ASIGNAR EN ÁRBOL",
                NodoGrupoTipo.CarpetaGrupo,
                string.Empty,
                "#94A3B8",
                unViajes,
                unKilos,
                unPct,
                unProm));

            foreach (KeyValuePair<string, (int Viajes, long Kilos)> u in unassignedWithActivity)
            {
                string prodName = catalogProductos.FirstOrDefault(p => string.Equals(p.Codigo, u.Key, StringComparison.OrdinalIgnoreCase))?.Descripcion ?? u.Key;
                double pct = totalPeriodKilos > 0 ? Math.Round(u.Value.Kilos * 100.0 / totalPeriodKilos, 2) : 0.0;
                double prom = u.Value.Viajes > 0 ? Math.Round((double)u.Value.Kilos / u.Value.Viajes, 1) : 0.0;
                flatRows.Add(new MaterialTreeFlatRowDto(
                    -1000,
                    -1,
                    1,
                    $"    └─ {u.Key} · {prodName}",
                    $"{u.Key} · {prodName}",
                    NodoGrupoTipo.ProductoAsignado,
                    u.Key,
                    "#94A3B8",
                    u.Value.Viajes,
                    u.Value.Kilos,
                    pct,
                    prom));
            }
        }

        List<GrupoKilosItem> rootSummary = [.. flatRows
            .Where(r => r.Nivel == 0 && (!excluirTotalesCero || r.TotalKilos > 0))
            .OrderByDescending(r => r.TotalKilos)
            .Select(r => new GrupoKilosItem(r.Nombre, r.TotalKilos, r.Viajes, r.Porcentaje))];

        List<BarChartItemDto> selectedProducts = BuildSelectedNodeProductBreakdown(
            filterSelectedNodeId, nodes, childrenByParent, statsByCode, catalogProductos);

        int rootCount = nodes.Count(n => n.ParentId is null && n.TipoNodo == NodoGrupoTipo.CarpetaGrupo);

        return new GrupoMaterialReportResultDto(
            periodoLabel,
            totalPeriodKilos,
            totalPeriodViajes,
            rootCount,
            nodes.Count,
            Math.Max(unassignedCatalogCount, unassignedWithActivity.Count),
            flatRows,
            rootSummary,
            selectedProducts);
    }

    private static List<BarChartItemDto> BuildSelectedNodeProductBreakdown(
        long? selectedNodeId,
        IReadOnlyList<MaterialGroupNode> nodes,
        Dictionary<long, List<MaterialGroupNode>> childrenByParent,
        Dictionary<string, (int Viajes, long Kilos)> statsByCode,
        IReadOnlyList<Producto> catalogProductos)
    {
        HashSet<string> targetCodes = new(StringComparer.OrdinalIgnoreCase);

        if (selectedNodeId.HasValue && selectedNodeId.Value > 0)
        {
            void Collect(long id)
            {
                MaterialGroupNode? n = nodes.FirstOrDefault(x => x.Id == id);
                if (n is null)
                {
                    return;
                }

                if (n.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(n.CodigoProducto))
                {
                    targetCodes.Add(n.CodigoProducto.Trim().ToUpperInvariant());
                }

                if (childrenByParent.TryGetValue(id, out List<MaterialGroupNode>? kids))
                {
                    foreach (MaterialGroupNode k in kids)
                    {
                        Collect(k.Id);
                    }
                }
            }

            Collect(selectedNodeId.Value);
        }
        else
        {
            foreach (string key in statsByCode.Keys)
            {
                targetCodes.Add(key);
            }
        }

        return [.. targetCodes
            .Select(code =>
            {
                long k = statsByCode.TryGetValue(code, out (int Viajes, long Kilos) st) ? st.Kilos : 0L;
                string desc = catalogProductos.FirstOrDefault(p => string.Equals(p.Codigo, code, StringComparison.OrdinalIgnoreCase))?.Descripcion ?? code;
                return new BarChartItemDto($"{code} · {desc}", k);
            })
            .Where(b => b.Value > 0)
            .OrderByDescending(b => b.Value)
            .Take(10)];
    }

    public static AnalisisProveedoresResultDto BuildAnalisisProveedores(
        string periodoLabel,
        DateOnly referenceDate,
        IReadOnlyList<Pesaje> actuales,
        IReadOnlyList<Pesaje> anteriores,
        IReadOnlyList<Cliente> clientes,
        IReadOnlyDictionary<string, string>? productToRootGroup = null,
        bool excluirTotalesCero = false)
    {
        IEnumerable<Pesaje> effectiveActuales = excluirTotalesCero ? actuales.Where(p => p.PesoNeto > 0) : actuales;

        Dictionary<string, Cliente> clientLookup = [];
        foreach (Cliente c in clientes)
        {
            string body = RutChileno.NormalizeBody(c.Rut);
            if (!string.IsNullOrEmpty(body) && !clientLookup.ContainsKey(body))
            {
                clientLookup[body] = c;
            }
        }

        Dictionary<string, long> prevKilosByRut = anteriores
            .GroupBy(p => RutChileno.NormalizeBody(p.RutCliente))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.PesoNeto), StringComparer.OrdinalIgnoreCase);

        var grouped = effectiveActuales
            .GroupBy(p =>
            {
                string body = RutChileno.NormalizeBody(p.RutCliente);
                return string.IsNullOrEmpty(body) ? p.NomCliente.Trim().ToUpperInvariant() : body;
            })
            .Select(g =>
            {
                Pesaje first = g.First();
                string rutRaw = string.IsNullOrWhiteSpace(first.RutCliente) ? g.Key : first.RutCliente.Trim();
                clientLookup.TryGetValue(g.Key, out Cliente? cli);
                string nombre = cli is not null && !string.IsNullOrWhiteSpace(cli.RazonSocial)
                    ? cli.RazonSocial
                    : first.NomCliente;

                long kilos = g.Sum(x => (long)x.PesoNeto);
                int viajes = g.Count();
                DateOnly primer = g.Min(x => x.FechaIngreso);
                DateOnly ultimo = g.Max(x => x.FechaIngreso);

                string prodTop = g
                    .GroupBy(x =>
                    {
                        string code = x.CodProducto.Trim().ToUpperInvariant();
                        return productToRootGroup is not null && productToRootGroup.TryGetValue(code, out string? grp)
                            ? grp
                            : x.NomProducto;
                    })
                    .OrderByDescending(pg => pg.Sum(x => (long)x.PesoNeto))
                    .First().Key;

                prevKilosByRut.TryGetValue(g.Key, out long prevKilos);
                double varPct = CalculatePercentDelta(kilos, prevKilos);

                return new
                {
                    Key = g.Key,
                    Rut = cli?.RutFormateado ?? rutRaw,
                    Nombre = string.IsNullOrWhiteSpace(nombre) ? rutRaw : nombre,
                    Ciudad = cli?.Ciudad ?? first.CiudadCliente ?? first.Origen,
                    Giro = cli?.Giro ?? string.Empty,
                    Telefono = cli?.Telefono ?? string.Empty,
                    Viajes = viajes,
                    Kilos = kilos,
                    PromKg = viajes > 0 ? Math.Round((double)kilos / viajes, 1) : 0.0,
                    Primer = primer,
                    Ultimo = ultimo,
                    DiasInactivo = Math.Max(0, referenceDate.DayNumber - ultimo.DayNumber),
                    PrevKilos = prevKilos,
                    VarPct = varPct,
                    ProdTop = prodTop
                };
            })
            .Where(x => !excluirTotalesCero || x.Kilos > 0)
            .OrderByDescending(x => x.Kilos)
            .ToList();

        long totalKilos = grouped.Sum(x => x.Kilos);
        int totalViajes = grouped.Sum(x => x.Viajes);
        double runningPct = 0.0;

        List<ProveedorRankingRowDto> ranking = new(grouped.Count);
        foreach (var item in grouped)
        {
            double share = totalKilos > 0 ? Math.Round(item.Kilos * 100.0 / totalKilos, 2) : 0.0;
            runningPct = Math.Min(100.0, Math.Round(runningPct + share, 2));
            string abc = runningPct <= 80.0 || ranking.Count == 0
                ? "A"
                : runningPct <= 95.0 ? "B" : "C";

            ranking.Add(new ProveedorRankingRowDto(
                item.Rut,
                item.Nombre,
                item.Ciudad,
                item.Giro,
                item.Telefono,
                item.Viajes,
                item.Kilos,
                share,
                runningPct,
                abc,
                item.PromKg,
                item.Primer,
                item.Ultimo,
                item.DiasInactivo,
                item.PrevKilos,
                item.VarPct,
                item.ProdTop));
        }

        List<BarChartItemDto> paretoTop = [.. ranking.Take(10).Select(r => new BarChartItemDto(r.Nombre, r.TotalKilos))];

        List<GrupoKilosItem> porClase = [.. ranking
            .GroupBy(r => $"Clase {r.ClaseAbc}")
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                long k = g.Sum(x => x.TotalKilos);
                double p = totalKilos > 0 ? Math.Round(k * 100.0 / totalKilos, 1) : 0.0;
                return new GrupoKilosItem($"{g.Key} ({g.Count()} prov.)", k, g.Sum(x => x.Viajes), p);
            })];

        List<ProveedorRankingRowDto> claseA = [.. ranking.Where(r => r.ClaseAbc == "A")];
        double claseAPct = totalKilos > 0 ? Math.Round(claseA.Sum(x => x.TotalKilos) * 100.0 / totalKilos, 1) : 0.0;

        return new AnalisisProveedoresResultDto(
            periodoLabel,
            ranking.Count,
            totalKilos,
            totalViajes,
            totalViajes > 0 ? Math.Round((double)totalKilos / totalViajes, 1) : 0.0,
            ranking.Count > 0 ? ranking[0].Nombre : "Sin datos",
            ranking.Count > 0 ? ranking[0].PorcentajeParticipacion : 0.0,
            claseA.Count,
            claseAPct,
            ranking,
            paretoTop,
            porClase);
    }

    public static ProveedorDetalleResultDto BuildProveedorDetalle(
        ProveedorRankingRowDto resumen,
        IReadOnlyList<Pesaje> entregasProveedor,
        IReadOnlyDictionary<string, string>? productToRootGroup = null,
        bool excluirTotalesCero = false)
    {
        List<Pesaje> filtered = excluirTotalesCero
            ? [.. entregasProveedor.Where(p => p.PesoNeto > 0)]
            : [.. entregasProveedor];

        long totalKilos = filtered.Sum(p => (long)p.PesoNeto);

        List<GrupoKilosItem> mixMateriales = [.. filtered
            .GroupBy(p =>
            {
                string code = p.CodProducto.Trim().ToUpperInvariant();
                return productToRootGroup is not null && productToRootGroup.TryGetValue(code, out string? g) ? g : p.GrupoMaterial;
            })
            .Select(g =>
            {
                long k = g.Sum(x => (long)x.PesoNeto);
                double pct = totalKilos > 0 ? Math.Round(k * 100.0 / totalKilos, 1) : 0.0;
                return new GrupoKilosItem(g.Key, k, g.Count(), pct);
            })
            .Where(x => !excluirTotalesCero || x.Kilos > 0)
            .OrderByDescending(x => x.Kilos)];

        List<BarChartItemDto> productos = [.. filtered
            .GroupBy(p => $"{p.CodProducto} · {p.NomProducto}")
            .Select(g => new BarChartItemDto(g.Key, g.Sum(x => (long)x.PesoNeto)))
            .Where(x => !excluirTotalesCero || x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(8)];

        List<BarChartItemDto> evolucion = [.. filtered
            .GroupBy(p => new DateOnly(p.FechaIngreso.Year, p.FechaIngreso.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new BarChartItemDto(g.Key.ToString("MMM yyyy", EsCl), g.Sum(x => (long)x.PesoNeto)))
            .Where(x => !excluirTotalesCero || x.Value > 0)];

        List<BarChartItemDto> patentes = [.. filtered
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Patente) ? "S/P" : p.Patente)
            .Select(g => new BarChartItemDto($"{g.Key} ({g.Count()} v.)", g.Sum(x => (long)x.PesoNeto)))
            .Where(x => !excluirTotalesCero || x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(8)];

        List<BarChartItemDto> origenes = [.. filtered
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Origen) ? "NO ESPECIFICADO" : p.Origen)
            .Select(g => new BarChartItemDto(g.Key, g.Sum(x => (long)x.PesoNeto)))
            .Where(x => !excluirTotalesCero || x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(6)];

        return new ProveedorDetalleResultDto(
            resumen,
            mixMateriales,
            productos,
            evolucion,
            patentes,
            origenes,
            [.. filtered.OrderByDescending(p => p.FechaIngreso).ThenByDescending(p => p.Folio)]);
    }
}
