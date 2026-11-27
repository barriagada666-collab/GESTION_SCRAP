using GestionScrap.Application.DTOs;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Application.Services;

/// <summary>
/// Servicio principal de aplicación enfocado en Informe Resumen con analítica YoY/MoM y segmentación,
/// Grupos de Material Jerárquicos, Análisis de Proveedores, Importador trazable y Perfiles RBAC.
/// </summary>
public sealed class ScrapApplicationService(
    IScrapRepository repository,
    IAuthService authService,
    IFirebirdSyncService firebirdSync,
    IExcelExportService excelExport,
    IMdbMigrationService mdbMigration)
{
    private readonly IScrapRepository _repository = repository;
    private readonly IAuthService _auth = authService;
    private readonly IFirebirdSyncService _firebird = firebirdSync;
    private readonly IExcelExportService _excel = excelExport;
    private readonly IMdbMigrationService _mdbMigration = mdbMigration;

    private string Actor => _auth.CurrentUser?.NombreUsuario ?? "SISTEMA";

    public Task<InformeResumenDto> GetInformeResumenAsync(
        Planta? planta = null,
        TipoPesaje? tipoPesaje = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken ct = default) =>
        GetInformeResumenAsync(new InformeResumenFilterDto(planta, tipoPesaje, desde, hasta), ct);

    public async Task<InformeResumenDto> GetInformeResumenAsync(
        InformeResumenFilterDto filter,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);

        IReadOnlyList<Pesaje> rawPesajes = await _repository.GetPesajesAsync(filter.Planta, ct: ct).ConfigureAwait(false);
        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> groupMap = BusinessRules.BuildProductToRootGroupMap(nodes);

        HashSet<string>? allowedCodes = null;
        if (filter.GrupoNodeId.HasValue && filter.GrupoNodeId.Value > 0)
        {
            allowedCodes = CollectDescendantCodes(nodes, filter.GrupoNodeId.Value);
        }

        IEnumerable<Pesaje> scoped = rawPesajes;
        if (filter.TipoPesaje.HasValue)
        {
            scoped = scoped.Where(p => p.TipoPesaje == filter.TipoPesaje.Value);
        }

        if (filter.ExcluirTrasladosInternos)
        {
            scoped = scoped.Where(p => !p.EsTraslado);
        }

        if (filter.ExcluirTotalesCero)
        {
            scoped = scoped.Where(p => p.PesoNeto > 0);
        }

        if (allowedCodes is not null)
        {
            scoped = scoped.Where(p => allowedCodes.Contains(p.CodProducto.Trim().ToUpperInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProveedorFiltro))
        {
            string term = filter.ProveedorFiltro.Trim();
            scoped = scoped.Where(p =>
                p.NomCliente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.RutCliente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Patente.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProductoFiltro))
        {
            string term = filter.ProductoFiltro.Trim();
            scoped = scoped.Where(p =>
                p.CodProducto.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.NomProducto.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.OrigenFiltro))
        {
            string term = filter.OrigenFiltro.Trim();
            scoped = scoped.Where(p =>
                p.Origen.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Destino.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        List<Pesaje> allInScope = [.. scoped];

        DateOnly endDate = filter.Hasta ?? (allInScope.Count > 0 ? allInScope.Max(p => p.FechaIngreso) : DateOnly.FromDateTime(DateTime.Today));
        DateOnly startDate = filter.Desde ?? (allInScope.Count > 0 ? new DateOnly(endDate.Year, endDate.Month, 1) : endDate.AddMonths(-1));
        ValidateDateRange(startDate, endDate);

        int rangeDays = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);
        DateOnly prevEnd = startDate.AddDays(-1);
        DateOnly prevStart = prevEnd.AddDays(-(rangeDays - 1));

        List<Pesaje> actuales = [.. allInScope.Where(p => p.FechaIngreso >= startDate && p.FechaIngreso <= endDate)];
        List<Pesaje> anteriores = [.. allInScope.Where(p => p.FechaIngreso >= prevStart && p.FechaIngreso <= prevEnd)];

        long totalKilos = actuales.Sum(p => (long)p.PesoNeto);
        int totalViajes = actuales.Count;
        double promViaje = totalViajes > 0 ? Math.Round((double)totalKilos / totalViajes, 1) : 0.0;
        long totalAnterior = anteriores.Sum(p => (long)p.PesoNeto);
        double variacion = BusinessRules.CalculatePercentDelta(totalKilos, totalAnterior);

        int provActivos = actuales
            .Select(p => RutChileno.NormalizeBody(p.RutCliente))
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        int prodActivos = actuales
            .Select(p => p.CodProducto.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        DateOnly ultimaFecha = actuales.Count > 0
            ? actuales.Max(p => p.FechaIngreso)
            : (allInScope.Count > 0 ? allInScope.Max(p => p.FechaIngreso) : endDate);
        List<Pesaje> ultimaJornada = [.. allInScope.Where(p => p.FechaIngreso == ultimaFecha)];

        List<PlantaResumenItem> porPlanta = [.. Enum.GetValues<Planta>()
            .Select(pl =>
            {
                List<Pesaje> rows = [.. actuales.Where(p => p.Planta == pl)];
                long k = rows.Sum(x => (long)x.PesoNeto);
                double pct = totalKilos > 0 ? Math.Round(k * 100.0 / totalKilos, 1) : 0.0;
                return new PlantaResumenItem(pl, pl.ToShortTitle(), rows.Count, k, pct);
            })
            .Where(x => (!filter.ExcluirTotalesCero && !filter.Planta.HasValue) || x.Kilos > 0 || (x.Viajes > 0 && !filter.ExcluirTotalesCero))];

        List<TipoPesajeResumenItem> porTipo = [.. Enum.GetValues<TipoPesaje>()
            .Select(tp =>
            {
                List<Pesaje> rows = [.. actuales.Where(p => p.TipoPesaje == tp)];
                long k = rows.Sum(x => (long)x.PesoNeto);
                double pct = totalKilos > 0 ? Math.Round(k * 100.0 / totalKilos, 1) : 0.0;
                return new TipoPesajeResumenItem(tp, tp.ToDisplayName(), rows.Count, k, pct);
            })
            .Where(x => !filter.ExcluirTotalesCero || x.Kilos > 0)
            .OrderByDescending(x => x.Kilos)];

        List<GrupoKilosItem> porGrupo = [.. actuales
            .GroupBy(p =>
            {
                string c = p.CodProducto.Trim().ToUpperInvariant();
                return groupMap.TryGetValue(c, out string? gName) ? gName : p.GrupoMaterial;
            })
            .Select(g =>
            {
                long k = g.Sum(x => (long)x.PesoNeto);
                double pct = totalKilos > 0 ? Math.Round(k * 100.0 / totalKilos, 1) : 0.0;
                return new GrupoKilosItem(g.Key, k, g.Count(), pct);
            })
            .Where(x => !filter.ExcluirTotalesCero || x.Kilos > 0)
            .OrderByDescending(x => x.Kilos)];

        List<BarChartItemDto> topProveedores = [.. actuales
            .GroupBy(p => p.NomCliente)
            .Select(g => new BarChartItemDto(g.Key, g.Sum(x => (long)x.PesoNeto)))
            .Where(x => !filter.ExcluirTotalesCero || x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(8)];

        List<WeekComparisonDto> tendencia = BuildTendenciaComparativa(actuales, anteriores, allInScope, startDate, endDate, prevStart, prevEnd, rangeDays);

        List<Pesaje> ordenadosDesc = [.. actuales
            .OrderByDescending(p => p.FechaIngreso)
            .ThenByDescending(p => p.Folio)];

        List<Pesaje> ultimos = [.. ordenadosDesc.Take(40)];

        EstadisticaAvanzadaDto estadisticas = BusinessRules.ComputeEstadisticas(actuales, allInScope, startDate, endDate);
        IReadOnlyList<ComparativoMensualDto> porMes = BusinessRules.BuildComparativoMensual(actuales, filter.ExcluirTotalesCero);
        IReadOnlyList<ComparativoAnualDto> porAnio = BusinessRules.BuildComparativoAnual(allInScope, filter.ExcluirTotalesCero);
        IReadOnlyList<SegmentacionItemDto> segmentacion = BusinessRules.BuildSegmentacion(
            filter.Segmentacion, actuales, anteriores, groupMap, filter.ExcluirTotalesCero);

        return new InformeResumenDto(
            $"{startDate:dd/MM/yyyy} al {endDate:dd/MM/yyyy}",
            $"{prevStart:dd/MM/yyyy} al {prevEnd:dd/MM/yyyy}",
            totalKilos,
            totalViajes,
            promViaje,
            totalAnterior,
            variacion,
            provActivos,
            prodActivos,
            ultimaFecha,
            ultimaJornada.Sum(p => (long)p.PesoNeto),
            ultimaJornada.Count,
            porPlanta,
            porTipo,
            porGrupo,
            tendencia,
            topProveedores,
            ultimos,
            estadisticas,
            porMes,
            porAnio,
            segmentacion,
            ordenadosDesc);
    }

    private static List<WeekComparisonDto> BuildTendenciaComparativa(
        IReadOnlyList<Pesaje> actuales,
        IReadOnlyList<Pesaje> anteriores,
        IReadOnlyList<Pesaje> allInScope,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly prevStart,
        DateOnly prevEnd,
        int rangeDays)
    {
        List<WeekComparisonDto> tendencia = [];
        if (rangeDays <= 62)
        {
            int sliceDays = Math.Max(1, rangeDays / 4);
            for (int i = 0; i < 4; i++)
            {
                DateOnly sA = startDate.AddDays(i * sliceDays);
                DateOnly eA = i == 3 ? endDate : sA.AddDays(sliceDays - 1);
                DateOnly sP = prevStart.AddDays(i * sliceDays);
                DateOnly eP = i == 3 ? prevEnd : sP.AddDays(sliceDays - 1);
                long kA = actuales.Where(p => p.FechaIngreso >= sA && p.FechaIngreso <= eA).Sum(p => (long)p.PesoNeto);
                long kP = anteriores.Where(p => p.FechaIngreso >= sP && p.FechaIngreso <= eP).Sum(p => (long)p.PesoNeto);
                tendencia.Add(new WeekComparisonDto($"{sA:dd/MM}-{eA:dd/MM}", kA, kP));
            }
            return tendencia;
        }

        // En rangos amplios (> 2 meses), muestra últimos 8 meses del rango comparando mismo mes año anterior (YoY)
        Dictionary<(int Year, int Month), long> byMonth = allInScope
            .GroupBy(p => (p.FechaIngreso.Year, p.FechaIngreso.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.PesoNeto));

        List<(int Year, int Month)> monthsInRange = [.. actuales
            .GroupBy(p => (p.FechaIngreso.Year, p.FechaIngreso.Month))
            .Select(g => g.Key)
            .OrderBy(k => k.Year)
            .ThenBy(k => k.Month)
            .TakeLast(8)];

        foreach ((int y, int m) in monthsInRange)
        {
            long kA = byMonth.TryGetValue((y, m), out long cur) ? cur : 0L;
            long kPrevYear = byMonth.TryGetValue((y - 1, m), out long py) ? py : 0L;
            tendencia.Add(new WeekComparisonDto($"{m:D2}/{y % 100:D2}", kA, kPrevYear));
        }

        return tendencia;
    }

    public async Task<IReadOnlyList<MaterialGroupNode>> GetMaterialGroupTreeAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        return await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Producto>> GetUnassignedProductsAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        IReadOnlyList<Producto> prods = await _repository.GetProductosAsync(ct).ConfigureAwait(false);

        HashSet<string> assigned = [.. nodes
            .Where(n => n.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(n.CodigoProducto))
            .Select(n => n.CodigoProducto.Trim().ToUpperInvariant())];

        return [.. prods.Where(p => !assigned.Contains(p.Codigo.Trim().ToUpperInvariant())).OrderBy(p => p.Codigo)];
    }

    public async Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        return await _repository.GetProductosAsync(ct).ConfigureAwait(false);
    }

    public async Task<MaterialGroupNode> SaveMaterialNodeAsync(MaterialGroupNode node, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarGrupoMaterial);
        if (string.IsNullOrWhiteSpace(node.Nombre))
        {
            throw new ArgumentException("El nombre del nodo o grupo de material es obligatorio.");
        }

        return await _repository.SaveMaterialNodeAsync(node, Actor, ct).ConfigureAwait(false);
    }

    public async Task MoveMaterialNodeAsync(long nodeId, long? newParentId, int newOrden = 999, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarGrupoMaterial);
        if (nodeId == newParentId)
        {
            throw new InvalidOperationException("Un nodo no puede moverse dentro de sí mismo.");
        }

        IReadOnlyList<MaterialGroupNode> all = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        if (newParentId.HasValue)
        {
            Dictionary<long, MaterialGroupNode> byId = all.ToDictionary(n => n.Id);
            long? cursor = newParentId;
            while (cursor.HasValue && byId.TryGetValue(cursor.Value, out MaterialGroupNode? p))
            {
                if (p.Id == nodeId)
                {
                    throw new InvalidOperationException("No es válido mover un grupo padre dentro de uno de sus propios hijos.");
                }

                cursor = p.ParentId;
            }
        }

        await _repository.MoveMaterialNodeAsync(nodeId, newParentId, newOrden, Actor, ct).ConfigureAwait(false);
    }

    public async Task ReorderMaterialNodeAsync(long nodeId, bool moveUp, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarGrupoMaterial);
        await _repository.SwapMaterialNodeOrderAsync(nodeId, moveUp, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeleteMaterialNodeAsync(long nodeId, bool deleteSubtree, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarGrupoMaterial);
        await _repository.DeleteMaterialNodeAsync(nodeId, deleteSubtree, Actor, ct).ConfigureAwait(false);
    }

    public async Task<GrupoMaterialReportResultDto> GetGrupoMaterialReportAsync(
        Planta? planta,
        DateOnly desde,
        DateOnly hasta,
        bool soloRecepciones = false,
        long? selectedNodeId = null,
        bool excluirTotalesCero = false,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(planta, desde, hasta, ct).ConfigureAwait(false);
        if (soloRecepciones)
        {
            pesajes = [.. pesajes.Where(p => p.TipoPesaje == TipoPesaje.RecepcionCarga)];
        }

        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        IReadOnlyList<Producto> prods = await _repository.GetProductosAsync(ct).ConfigureAwait(false);

        string label = $"{(planta?.ToShortTitle() ?? "Todas las Plantas")} · {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
        return BusinessRules.BuildMaterialTreeReport(label, nodes, pesajes, prods, selectedNodeId, excluirTotalesCero);
    }

    public async Task<AnalisisProveedoresResultDto> GetAnalisisProveedoresAsync(
        Planta? planta,
        DateOnly desde,
        DateOnly hasta,
        TipoPesaje? tipoPesaje = TipoPesaje.RecepcionCarga,
        long? filterGroupNodeId = null,
        string? searchTerm = null,
        bool excluirTotalesCero = false,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerAnalisisProveedores);
        ValidateDateRange(desde, hasta);

        int days = Math.Max(1, hasta.DayNumber - desde.DayNumber + 1);
        DateOnly prevHasta = desde.AddDays(-1);
        DateOnly prevDesde = prevHasta.AddDays(-(days - 1));

        IReadOnlyList<Pesaje> allWindow = await _repository.GetPesajesAsync(planta, prevDesde, hasta, ct).ConfigureAwait(false);
        IReadOnlyList<Cliente> clientes = await _repository.GetClientesAsync(planta, ct).ConfigureAwait(false);
        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> prodToRoot = BusinessRules.BuildProductToRootGroupMap(nodes);

        HashSet<string>? allowedCodes = null;
        if (filterGroupNodeId.HasValue && filterGroupNodeId.Value > 0)
        {
            allowedCodes = CollectDescendantCodes(nodes, filterGroupNodeId.Value);
        }

        IEnumerable<Pesaje> filteredAll = allWindow;
        if (tipoPesaje.HasValue)
        {
            filteredAll = filteredAll.Where(p => p.TipoPesaje == tipoPesaje.Value);
        }

        if (allowedCodes is not null)
        {
            filteredAll = filteredAll.Where(p => allowedCodes.Contains(p.CodProducto.Trim().ToUpperInvariant()));
        }

        List<Pesaje> allList = [.. filteredAll];
        List<Pesaje> actuales = [.. allList.Where(p => p.FechaIngreso >= desde && p.FechaIngreso <= hasta)];
        List<Pesaje> anteriores = [.. allList.Where(p => p.FechaIngreso >= prevDesde && p.FechaIngreso <= prevHasta)];

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.Trim();
            actuales = [.. actuales.Where(p =>
                p.NomCliente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.RutCliente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Patente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.NomProducto.Contains(term, StringComparison.OrdinalIgnoreCase))];
        }

        string label = $"{(planta?.ToShortTitle() ?? "Todas las Plantas")} · {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
        return BusinessRules.BuildAnalisisProveedores(label, hasta, actuales, anteriores, clientes, prodToRoot, excluirTotalesCero);
    }

    public async Task<ProveedorDetalleResultDto> GetProveedorDetalleAsync(
        ProveedorRankingRowDto proveedor,
        Planta? planta,
        DateOnly desde,
        DateOnly hasta,
        TipoPesaje? tipoPesaje = TipoPesaje.RecepcionCarga,
        bool excluirTotalesCero = false,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerAnalisisProveedores);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(planta, desde, hasta, ct).ConfigureAwait(false);
        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> prodToRoot = BusinessRules.BuildProductToRootGroupMap(nodes);

        List<Pesaje> entregas = [.. pesajes.Where(p =>
            (!tipoPesaje.HasValue || p.TipoPesaje == tipoPesaje.Value) &&
            (RutChileno.Matches(p.RutCliente, proveedor.Rut) ||
             string.Equals(p.NomCliente.Trim(), proveedor.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))];

        return BusinessRules.BuildProveedorDetalle(proveedor, entregas, prodToRoot, excluirTotalesCero);
    }

    private static HashSet<string> CollectDescendantCodes(IReadOnlyList<MaterialGroupNode> nodes, long rootId)
    {
        HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<long, List<MaterialGroupNode>> byParent = nodes
            .GroupBy(n => n.ParentId ?? 0L)
            .ToDictionary(g => g.Key, g => g.ToList());

        void Walk(long id)
        {
            MaterialGroupNode? n = nodes.FirstOrDefault(x => x.Id == id);
            if (n is null)
            {
                return;
            }

            if (n.TipoNodo == NodoGrupoTipo.ProductoAsignado && !string.IsNullOrWhiteSpace(n.CodigoProducto))
            {
                codes.Add(n.CodigoProducto.Trim().ToUpperInvariant());
            }

            if (byParent.TryGetValue(id, out List<MaterialGroupNode>? kids))
            {
                foreach (MaterialGroupNode k in kids)
                {
                    Walk(k.Id);
                }
            }
        }

        Walk(rootId);
        return codes;
    }

    public async Task<(bool Success, int PingMs, int TablesCount, long PesajesCount, string DateBounds, string Message)> TestFirebirdConnectionAsync(
        FirebirdPlantaEndpoint endpoint,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        return await _firebird.TestConnectionAsync(endpoint, ct).ConfigureAwait(false);
    }

    public async Task<FirebirdExtractPackage> QueryFirebirdPackageAsync(
        FirebirdPlantaEndpoint endpoint,
        DateOnly? desde,
        DateOnly? hasta,
        bool allowFallbackDemo,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        if (desde.HasValue && hasta.HasValue)
        {
            ValidateDateRange(desde.Value, hasta.Value);
        }

        return await _firebird.QueryRemotePackageAsync(endpoint, desde, hasta, allowFallbackDemo, ct).ConfigureAwait(false);
    }

    public Task<(int ImportedPesajes, int Duplicates, int ClientesUpserted, int ProductosUpserted)> ImportFirebirdPackageAsync(
        Planta planta,
        FirebirdExtractPackage package,
        CancellationToken ct = default) =>
        ImportFirebirdPackageAsync(planta, package, null, "Todo el historial", false, ct);

    public async Task<(int ImportedPesajes, int Duplicates, int ClientesUpserted, int ProductosUpserted)> ImportFirebirdPackageAsync(
        Planta planta,
        FirebirdExtractPackage package,
        FirebirdPlantaEndpoint? sourceEndpoint,
        string rangoFiltro,
        bool excluirCeroKg,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);

        if (package.Clientes.Count > 0)
        {
            await _repository.UpsertClientesAsync(package.Clientes, Actor, ct).ConfigureAwait(false);
        }

        if (package.Productos.Count > 0)
        {
            await _repository.UpsertProductosAsync(package.Productos, Actor, ct).ConfigureAwait(false);
        }

        IReadOnlyList<MaterialGroupNode> nodes = await _repository.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> groups = BusinessRules.BuildProductToRootGroupMap(nodes);

        IEnumerable<FirebirdRemotePesaje> rawList = excluirCeroKg
            ? package.Pesajes.Where(r => (r.PesoNeto > 0 ? r.PesoNeto : Math.Max(0, r.PesoBruto - r.PesoTara)) > 0)
            : package.Pesajes;

        List<Pesaje> mapped = [.. rawList.Select(r => BusinessRules.MapRemoteToPesaje(r, planta, groups))];
        (int imported, int duplicates) = await _repository.ImportPesajesAsync(mapped, planta, Actor, ct).ConfigureAwait(false);

        long kilosTotal = mapped.Sum(p => (long)p.PesoNeto);
        string origenStr = sourceEndpoint is not null
            ? $"{sourceEndpoint.ServidorIp}:{sourceEndpoint.Puerto} · {sourceEndpoint.RutaArchivo}"
            : $"Firebird ({planta.ToShortTitle()})";

        await _repository.AddImportHistoryAsync(new ImportacionHistorial
        {
            Usuario = Actor,
            TipoOrigen = package.UsedSimulatedFallback ? "Firebird (Demo Local)" : "Firebird TCP",
            OrigenDetalle = origenStr,
            PlantaDestino = planta.ToShortTitle(),
            DestinoDetalle = _repository.DatabasePath,
            RangoFiltro = rangoFiltro,
            LeidosOrigen = package.Pesajes.Count,
            ImportadosNuevos = imported,
            DuplicadosOmitidos = duplicates,
            ClientesSincronizados = package.Clientes.Count,
            ProductosSincronizados = package.Productos.Count,
            KilosImportados = kilosTotal,
            Estado = imported > 0 ? "COMPLETADA" : "SIN NOVEDADES",
            Observacion = package.StatusMessage
        }, ct).ConfigureAwait(false);

        return (imported, duplicates, package.Clientes.Count, package.Productos.Count);
    }

    public async Task<(FirebirdExtractPackage Package, int ImportedPesajes, int Duplicates, int ClientesUpserted, int ProductosUpserted)>
        ImportDirectFromFirebirdAsync(
            FirebirdPlantaEndpoint endpoint,
            DateOnly? desde,
            DateOnly? hasta,
            bool allowFallbackDemo,
            bool excluirCeroKg,
            CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        if (desde.HasValue && hasta.HasValue)
        {
            ValidateDateRange(desde.Value, hasta.Value);
        }

        FirebirdExtractPackage package = await _firebird.QueryRemotePackageAsync(endpoint, desde, hasta, allowFallbackDemo, ct).ConfigureAwait(false);
        string rango = desde.HasValue && hasta.HasValue ? $"{desde.Value:dd/MM/yyyy} - {hasta.Value:dd/MM/yyyy}" : "Todo el historial";
        var res = await ImportFirebirdPackageAsync(endpoint.Planta, package, endpoint, rango, excluirCeroKg, ct).ConfigureAwait(false);
        return (package, res.ImportedPesajes, res.Duplicates, res.ClientesUpserted, res.ProductosUpserted);
    }

    public async Task<(int PesajesImported, int ClientesImported, int ProductosImported, IReadOnlyList<string> Warnings)>
        ImportFromAccessMdbAsync(string mdbPath, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        return await _mdbMigration.ImportFromMdbAsync(mdbPath, Actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportacionHistorial>> GetImportHistoryAsync(int limit = 200, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        return await _repository.GetImportHistoryAsync(limit, ct).ConfigureAwait(false);
    }

    public async Task<DatabaseResumenStatsDto> GetDatabaseResumenStatsAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarDatos);
        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<Cliente> clientes = await _repository.GetClientesAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<Producto> prods = await _repository.GetProductosAsync(ct).ConfigureAwait(false);
        IReadOnlyList<ImportacionHistorial> history = await _repository.GetImportHistoryAsync(50, ct).ConfigureAwait(false);

        ImportacionHistorial? last = history.Count > 0 ? history[0] : null;
        return new DatabaseResumenStatsDto(
            pesajes.Count,
            pesajes.Sum(p => (long)p.PesoNeto),
            clientes.Count,
            prods.Count,
            history.Count,
            pesajes.Count > 0 ? pesajes.Min(p => p.FechaIngreso) : null,
            pesajes.Count > 0 ? pesajes.Max(p => p.FechaIngreso) : null,
            last?.TimestampUtc,
            last is not null ? $"{last.TipoOrigen}: {last.OrigenDetalle}" : "Sin importaciones registradas aún",
            _repository.DatabasePath);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 500, string? accion = null, string? usuario = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerAuditoria);
        return await _repository.GetAuditLogsAsync(limit, accion, usuario, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PerfilUsuario>> GetPerfilesAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);
        return await _repository.GetPerfilesAsync(ct).ConfigureAwait(false);
    }

    public async Task<PerfilUsuario> SavePerfilAsync(PerfilUsuario perfil, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);
        if (string.IsNullOrWhiteSpace(perfil.Nombre))
        {
            throw new ArgumentException("El nombre del perfil es obligatorio.");
        }

        if (perfil.Permisos.Count == 0)
        {
            throw new ArgumentException("Debe asignar al menos un permiso al perfil.");
        }

        return await _repository.SavePerfilAsync(perfil, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeletePerfilAsync(long id, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);
        await _repository.DeletePerfilAsync(id, Actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);
        return await _repository.GetUsuariosAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveUsuarioAsync(Usuario usuario, string? plainNewPassword, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario) || string.IsNullOrWhiteSpace(usuario.NombreCompleto))
        {
            throw new ArgumentException("El nombre de usuario y el nombre completo son obligatorios.");
        }

        string hash = usuario.PasswordHash;
        string salt = usuario.PasswordSalt;

        if (!string.IsNullOrWhiteSpace(plainNewPassword))
        {
            (bool valid, string msg) = PasswordSecurity.ValidateComplexity(plainNewPassword);
            if (!valid)
            {
                throw new ArgumentException(msg, nameof(plainNewPassword));
            }

            (hash, salt) = PasswordSecurity.HashPassword(plainNewPassword);
        }
        else if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
        {
            throw new ArgumentException("Debe asignar una contraseña inicial para el nuevo usuario.");
        }

        Usuario toSave = new()
        {
            Id = usuario.Id,
            NombreUsuario = usuario.NombreUsuario.Trim(),
            NombreCompleto = usuario.NombreCompleto.Trim(),
            PerfilId = usuario.PerfilId,
            NombrePerfil = usuario.NombrePerfil,
            PasswordHash = hash,
            PasswordSalt = salt,
            Activo = usuario.Activo,
            IntentosFallidos = usuario.IntentosFallidos,
            BloqueadoHastaUtc = usuario.BloqueadoHastaUtc,
            PermisosPersonalizados = usuario.PermisosPersonalizados,
            CreadoEnUtc = usuario.CreadoEnUtc
        };

        await _repository.SaveUsuarioAsync(toSave, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeleteUsuarioAsync(long id, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridadPerfiles);
        if (_auth.CurrentUser?.Id == id)
        {
            throw new InvalidOperationException("No puede eliminar su propio usuario autenticado.");
        }

        await _repository.DeleteUsuarioAsync(id, Actor, ct).ConfigureAwait(false);
    }

    public async Task<string> BackupDatabaseAsync(string destinationPath, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.MantenimientoBaseDatos);
        return await _repository.BackupDatabaseAsync(destinationPath, ct).ConfigureAwait(false);
    }

    public async Task<int> PurgeAllPesajesAsync(string confirmationPhrase, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.MantenimientoBaseDatos);

        if (!string.Equals(confirmationPhrase?.Trim(), "ELIMINAR", StringComparison.Ordinal))
        {
            throw new ArgumentException("Debe escribir exactamente la palabra ELIMINAR para confirmar la purga.");
        }

        return await _repository.PurgeAllPesajesAsync(Actor, ct).ConfigureAwait(false);
    }

    public IExcelExportService ExcelExporter
    {
        get
        {
            _auth.EnsurePermission(PermisoSistema.ExportarExcel);
            return _excel;
        }
    }

    private static void ValidateDateRange(DateOnly desde, DateOnly hasta)
    {
        if (desde > hasta)
        {
            throw new ArgumentException("La fecha 'Desde' no puede ser posterior a la fecha 'Hasta'.");
        }
    }
}
