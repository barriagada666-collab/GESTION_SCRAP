using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Application.Services;

/// <summary>
/// Servicio principal de aplicación que coordina consultas, reglas de negocio,
/// control de permisos, exportación Excel, sincronización Firebird y migración MDB.
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

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerDashboard);

        IReadOnlyList<Pesaje> allPesajes = await _repository.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<PrecioProveedor> preciosProv = await _repository.GetPreciosProveedorAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<IndexPrecioPesaje> indexPrecios = await _repository.GetIndexPreciosAsync(ct: ct).ConfigureAwait(false);

        DateOnly maxDate = allPesajes.Count > 0
            ? allPesajes.Max(p => p.FechaIngreso)
            : DateOnly.FromDateTime(DateTime.Today);

        DateOnly currentMonthStart = new(maxDate.Year, maxDate.Month, 1);
        DateOnly currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
        DateOnly prevMonthStart = currentMonthStart.AddMonths(-1);
        DateOnly prevMonthEnd = currentMonthStart.AddDays(-1);

        List<Pesaje> mesActual = [.. allPesajes.Where(p => p.FechaIngreso >= currentMonthStart && p.FechaIngreso <= currentMonthEnd)];
        List<Pesaje> mesAnterior = [.. allPesajes.Where(p => p.FechaIngreso >= prevMonthStart && p.FechaIngreso <= prevMonthEnd)];

        long totalActual = mesActual.Sum(p => (long)p.PesoNeto);
        long totalAnterior = mesAnterior.Sum(p => (long)p.PesoNeto);
        double variacion = totalAnterior == 0
            ? (totalActual > 0 ? 100.0 : 0.0)
            : Math.Round((totalActual - totalAnterior) * 100.0 / totalAnterior, 1);

        List<Pesaje> ultimaJornada = [.. allPesajes.Where(p => p.FechaIngreso == maxDate)];
        long kilosJornada = ultimaJornada.Sum(p => (long)p.PesoNeto);

        List<GrupoEspecialItemDto> topProveedores = [.. mesActual
            .GroupBy(p => p.NomCliente)
            .Select(g => new GrupoEspecialItemDto(g.Key, g.Sum(x => (long)x.PesoNeto)))
            .OrderByDescending(x => x.Kilos)
            .Take(6)];

        List<GrupoKilosItem> materiales = [.. mesActual
            .GroupBy(p => string.IsNullOrWhiteSpace(p.GrupoMaterial) ? "GENERAL" : p.GrupoMaterial)
            .Select(g => new GrupoKilosItem(g.Key, g.Sum(x => (long)x.PesoNeto)))
            .OrderByDescending(x => x.Kilos)];

        WeekComparisonDto[] semanas =
        [
            BuildWeek("Sem 1 (1-7)", 1, 7, mesActual, mesAnterior),
            BuildWeek("Sem 2 (8-14)", 8, 14, mesActual, mesAnterior),
            BuildWeek("Sem 3 (15-21)", 15, 21, mesActual, mesAnterior),
            BuildWeek("Sem 4 (22-31)", 22, 31, mesActual, mesAnterior)
        ];

        List<PriceAlertDto> alertas = [];
        foreach (Pesaje p in mesActual.Where(x => x.Planta is Planta.Placilla or Planta.Santiago && BusinessRules.IsCompra(x)))
        {
            decimal teo = BusinessRules.FindTheoreticalPrice(preciosProv, p.Planta, p.RutCliente, p.CodProducto);
            decimal real = BusinessRules.FindRealPrice(indexPrecios, p.Planta, p.Folio);

            if (teo == 0m && real == 0m)
            {
                alertas.Add(new PriceAlertDto(
                    "SIN_PRECIO",
                    p.Folio,
                    p.Planta,
                    p.NomCliente,
                    p.CodProducto,
                    p.PesoNeto,
                    $"Sin tarifa pactada para {p.CodProducto} en {p.Planta.ToShortTitle()}."));
            }
            else if (real == 0m && teo > 0m)
            {
                alertas.Add(new PriceAlertDto(
                    "PENDIENTE_CONGELAR",
                    p.Folio,
                    p.Planta,
                    p.NomCliente,
                    p.CodProducto,
                    p.PesoNeto,
                    $"Tarifa teórica (${teo:N0}) pendiente de indexar como precio real."));
            }

            if (alertas.Count >= 12)
            {
                break;
            }
        }

        List<Pesaje> ultimosFolios = [.. allPesajes
            .OrderByDescending(p => p.FechaIngreso)
            .ThenByDescending(p => p.Folio)
            .Take(12)];

        return new DashboardSummaryDto(
            currentMonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            prevMonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            totalActual,
            totalAnterior,
            variacion,
            maxDate,
            kilosJornada,
            ultimaJornada.Count,
            topProveedores,
            materiales,
            semanas,
            alertas,
            ultimosFolios);
    }

    private static WeekComparisonDto BuildWeek(string label, int d1, int d2, List<Pesaje> actual, List<Pesaje> anterior) =>
        new(
            label,
            actual.Where(p => p.FechaIngreso.Day >= d1 && p.FechaIngreso.Day <= d2).Sum(p => (long)p.PesoNeto),
            anterior.Where(p => p.FechaIngreso.Day >= d1 && p.FechaIngreso.Day <= d2).Sum(p => (long)p.PesoNeto));

    public async Task<(IReadOnlyList<GrupoKilosItem> Placilla, IReadOnlyList<GrupoKilosItem> Santiago, IReadOnlyList<GrupoKilosItem> Talcahuano)>
        GetGrupoMaterialAsync(DateOnly desde, DateOnly hasta, bool semanal, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(desde: desde, hasta: hasta, ct: ct).ConfigureAwait(false);
        IEnumerable<Pesaje> baseSet = semanal
            ? pesajes.Where(BusinessRules.IsCompra)
            : pesajes.Where(p => p.TipoPesaje == TipoPesaje.RecepcionCarga);

        static List<GrupoKilosItem> Aggregate(IEnumerable<Pesaje> items, Planta planta) =>
        [
            .. items
                .Where(p => p.Planta == planta)
                .GroupBy(p => string.IsNullOrWhiteSpace(p.GrupoMaterial) ? "GENERAL" : p.GrupoMaterial)
                .Select(g => new GrupoKilosItem(g.Key, g.Sum(x => (long)x.PesoNeto)))
                .OrderByDescending(x => x.Kilos)
        ];

        return (Aggregate(baseSet, Planta.Placilla), Aggregate(baseSet, Planta.Santiago), Aggregate(baseSet, Planta.Talcahuano));
    }

    public async Task<ComprasTrasladosResultDto> GetComprasTrasladosAsync(
        Planta planta,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(planta, desde, hasta, ct).ConfigureAwait(false);

        List<ComprasRowDto> compras = [.. pesajes
            .Where(BusinessRules.IsCompra)
            .GroupBy(p => (p.CodProducto, p.NomProducto, p.NomCliente))
            .Select(g => new ComprasRowDto(g.Key.CodProducto, g.Key.NomProducto, g.Key.NomCliente, g.Sum(x => (long)x.PesoNeto)))
            .OrderBy(r => r.Codigo)
            .ThenBy(r => r.RazonSocial)];

        List<TrasladoRowDto> traslados = [.. pesajes
            .Where(BusinessRules.IsTraslado)
            .OrderBy(p => p.Folio)
            .Select(p => new TrasladoRowDto(
                p.Folio,
                p.Patente,
                p.NomProducto,
                p.FechaIngreso,
                p.FechaSalida,
                p.PesoBruto,
                p.PesoTara,
                p.PesoNeto))];

        long totCompras = compras.Sum(c => c.Kilos);
        long totTraslados = traslados.Sum(t => (long)t.PesoNeto);

        (string lblCompras, string lblTraslados) = planta switch
        {
            Planta.Placilla => ("DETALLE COMPRAS PLACILLA", "TRASLADOS BODEGA SANTIAGO (RECEPCIÓN)"),
            Planta.Santiago => ("DETALLE COMPRAS SANTIAGO", "TRASLADOS SCRAP SANTIAGO (A PLACILLA)"),
            Planta.Talcahuano => ("DETALLE COMPRAS TALCAHUANO", "TRASLADOS A SANTIAGO"),
            _ => ("COMPRAS", "TRASLADOS")
        };

        return new ComprasTrasladosResultDto(
            planta,
            lblCompras,
            lblTraslados,
            compras,
            traslados,
            totCompras,
            totTraslados,
            totCompras + totTraslados);
    }

    public async Task<IReadOnlyList<Pesaje>> GetInformeClienteAsync(
        Planta planta,
        bool buscarPorRut,
        string criterio,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(planta, desde, hasta, ct).ConfigureAwait(false);

        if (buscarPorRut)
        {
            if (string.IsNullOrWhiteSpace(criterio))
            {
                return [];
            }

            return [.. pesajes
                .Where(p => p.TipoPesaje == TipoPesaje.RecepcionCarga && RutChileno.Matches(p.RutCliente, criterio))
                .OrderBy(p => p.Folio)];
        }

        if (!long.TryParse(criterio.Trim(), out long folioBuscado))
        {
            return [];
        }

        return [.. pesajes
            .Where(p => p.Folio == folioBuscado)
            .OrderBy(p => p.FechaIngreso)];
    }

    public async Task<IReadOnlyList<GrupoEspecialCardDto>> GetGruposEspecialesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(desde: desde, hasta: hasta, ct: ct).ConfigureAwait(false);

        static GrupoEspecialCardDto BuildCard(string id, string titulo, string vista, IEnumerable<Pesaje> items)
        {
            List<GrupoEspecialItemDto> rows = [.. items
                .GroupBy(p => p.NomCliente)
                .Select(g => new GrupoEspecialItemDto(g.Key, g.Sum(x => (long)x.PesoNeto)))
                .OrderByDescending(x => x.Kilos)];
            return new GrupoEspecialCardDto(id, titulo, vista, rows, rows.Sum(r => r.Kilos));
        }

        return
        [
            BuildCard("ret_stgo", "Retail Santiago", "VISTA_RETAIL_STGO", pesajes.Where(BusinessRules.MatchesRetailStgo)),
            BuildCard("ret_emp", "Retail Empresas", "VISTA_RETAIL_EMPRESAS", pesajes.Where(BusinessRules.MatchesRetailEmpresas)),
            BuildCard("mix_stgo", "Mix Supermercado Santiago", "VISTA_MIX_SUPERMERCADO_STGO", pesajes.Where(BusinessRules.MatchesMixStgo)),
            BuildCard("mix_pl", "Mix Supermercado Placilla", "VISTA_MIX_SUPERMERCADO_PLACILLA", pesajes.Where(BusinessRules.MatchesMixPlacilla)),
            BuildCard("ret_reg", "Retail Regiones (Martino / Johnson)", "VISTA_RETAIL_REGIONES_STGO", pesajes.Where(BusinessRules.MatchesRetailRegiones)),
            BuildCard("agr", "Sector Agrícola", "CONSULTA_GRUPO_AGRICOLA", pesajes.Where(BusinessRules.MatchesAgricola)),
            BuildCard("pesq", "Sector Pesquero", "CONSULTA_GRUPO_PESQUERA", pesajes.Where(BusinessRules.MatchesPesquera)),
            BuildCard("agrosuper", "Grupo Agrosuper Placilla", "VISTA_PSJES_AGROSUPER_PL", pesajes.Where(BusinessRules.MatchesAgrosuper))
        ];
    }

    public async Task<JcrammReportDto> GetInformeJcrammAsync(long? overrideLastFolio = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);

        UltimoFolioJcramm ultimo = await _repository.GetUltimoFolioJcrammAsync(ct).ConfigureAwait(false);
        long folioCorte = overrideLastFolio ?? ultimo.Folio;

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(Planta.Placilla, ct: ct).ConfigureAwait(false);
        IReadOnlyList<Cliente> clientes = await _repository.GetClientesAsync(Planta.Placilla, ct).ConfigureAwait(false);

        List<JcrammDetalleRowDto> detalle = [.. pesajes
            .Where(p => p.Folio > folioCorte && BusinessRules.IsJcrammEligible(p))
            .OrderBy(p => p.Folio)
            .Select(p =>
            {
                string ciudad = p.CiudadCliente ??
                                clientes.FirstOrDefault(c => RutChileno.Matches(c.Rut, p.RutCliente))?.Ciudad ??
                                p.Origen;
                return new JcrammDetalleRowDto(
                    p.FechaIngreso,
                    p.Folio,
                    p.CodProducto,
                    p.NomProducto,
                    p.Patente,
                    p.NomCliente,
                    ciudad,
                    p.PesoNeto);
            })];

        List<JcrammResumenRowDto> resumen = [.. detalle
            .GroupBy(d => d.Descripcion)
            .Select(g => new JcrammResumenRowDto(g.Key, g.Sum(x => (long)x.Kilos)))
            .OrderByDescending(x => x.Kilos)];

        return new JcrammReportDto(
            ultimo,
            detalle,
            resumen,
            detalle.Sum(d => (long)d.Kilos),
            resumen.Sum(r => r.Kilos));
    }

    public async Task CerrarYExportarJcrammAsync(string filePath, JcrammReportDto report, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ExportarExcel);
        _auth.EnsurePermission(PermisoSistema.CerrarFolioJcramm);

        if (report.Detalle.Count == 0)
        {
            throw new InvalidOperationException("No existen pesajes pendientes posteriores al último folio para exportar.");
        }

        _excel.ExportInformeJcramm(
            filePath,
            [.. report.Detalle.Select(d => (d.Fecha, d.Folio, d.CodProducto, d.Descripcion, d.Patente, d.Proveedor, d.Ciudad, d.Kilos))],
            [.. report.Resumen.Select(r => (r.Producto, r.Kilos))],
            report.TotalKilosDetalle,
            report.TotalKilosResumen);

        JcrammDetalleRowDto last = report.Detalle[^1];
        await _repository.SetUltimoFolioJcrammAsync(last.Folio, last.Fecha, Actor, ct).ConfigureAwait(false);
    }

    public async Task<InformePreciosResultDto> GetInformePreciosAsync(
        Planta planta,
        ModoPrecio modo,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        ValidateDateRange(desde, hasta);

        IReadOnlyList<Pesaje> pesajes = await _repository.GetPesajesAsync(planta, desde, hasta, ct).ConfigureAwait(false);
        IReadOnlyList<PrecioProveedor> preciosProv = await _repository.GetPreciosProveedorAsync(planta, ct).ConfigureAwait(false);
        IReadOnlyList<IndexPrecioPesaje> indexPrecios = await _repository.GetIndexPreciosAsync(planta, ct).ConfigureAwait(false);

        List<PrecioDetalleRowDto> detalle = [.. pesajes
            .Where(BusinessRules.IsCompra)
            .OrderBy(p => p.Folio)
            .Select(p =>
            {
                decimal precio = modo == ModoPrecio.Teoricos
                    ? BusinessRules.FindTheoreticalPrice(preciosProv, planta, p.RutCliente, p.CodProducto)
                    : BusinessRules.FindRealPrice(indexPrecios, planta, p.Folio);
                decimal total = p.PesoNeto * precio;
                return new PrecioDetalleRowDto(
                    p.Folio,
                    p.CodProducto,
                    p.NomProducto,
                    p.RutCliente,
                    p.NomCliente,
                    p.PesoNeto,
                    precio,
                    total,
                    p.GrupoMaterial);
            })];

        string[] familiasObjetivo = ["SUPER-BASULIP", "RECAM", "ALTA"];
        List<PrecioFamiliaResumenDto> resumen = [];
        foreach (string fam in familiasObjetivo)
        {
            List<PrecioDetalleRowDto> rows = [.. detalle.Where(r => r.Familia.Contains(fam, StringComparison.OrdinalIgnoreCase))];
            long kilos = rows.Sum(r => (long)r.Kilos);
            decimal totalPesos = rows.Sum(r => r.Total);
            decimal promedio = BusinessRules.CalculateWeightedAverage(totalPesos, kilos);
            resumen.Add(new PrecioFamiliaResumenDto(fam == "SUPER-BASULIP" ? "SUPER BASULIP" : fam, kilos, promedio, totalPesos));
        }

        return new InformePreciosResultDto(
            planta,
            modo,
            detalle,
            resumen,
            detalle.Sum(d => (long)d.Kilos),
            detalle.Sum(d => d.Total));
    }

    public async Task ActualizarPrecioRealIndividualAsync(
        Planta planta,
        ModoPrecio modoActual,
        long folio,
        decimal nuevoPrecio,
        string familia,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.EditarPreciosReales);

        (bool allowed, string msg) = BusinessRules.ValidateCanUpdatePrice(modoActual);
        if (!allowed)
        {
            throw new InvalidOperationException(msg);
        }

        if (nuevoPrecio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoPrecio), "El precio debe ser mayor a 0.");
        }

        await _repository.UpsertIndexPrecioAsync(new IndexPrecioPesaje
        {
            Folio = folio,
            Planta = planta,
            Precio = nuevoPrecio,
            Familia = familia,
            ActualizadoEn = DateTime.UtcNow
        }, Actor, ct).ConfigureAwait(false);
    }

    public async Task<int> CongelarPreciosTeoricosAsync(
        Planta planta,
        ModoPrecio modoActual,
        IEnumerable<PrecioDetalleRowDto> rows,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.CongelarPreciosTeoricos);

        if (modoActual != ModoPrecio.Teoricos)
        {
            throw new InvalidOperationException("Para guardar precios teóricos seleccione la opción 'Precios Teóricos (Pactados)'.");
        }

        List<IndexPrecioPesaje> items = [.. rows
            .Where(r => r.Precio > 0)
            .Select(r => new IndexPrecioPesaje
            {
                Folio = r.Folio,
                Planta = planta,
                Precio = r.Precio,
                Familia = r.Familia,
                ActualizadoEn = DateTime.UtcNow
            })];

        return await _repository.BulkFreezeIndexPreciosAsync(items, Actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrecioProveedor>> GetPreciosProveedorAsync(Planta planta, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        return await _repository.GetPreciosProveedorAsync(planta, ct).ConfigureAwait(false);
    }

    public async Task GuardarPrecioProveedorAsync(
        Planta planta,
        string rut,
        string razonSocial,
        string codProducto,
        decimal precio,
        bool permitirActualizar,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMantenedorPrecios);

        if (string.IsNullOrWhiteSpace(rut) || string.IsNullOrWhiteSpace(codProducto) || precio <= 0)
        {
            throw new ArgumentException("Complete RUT, código de producto y precio mayor a cero.");
        }

        IReadOnlyList<PrecioProveedor> existentes = await _repository.GetPreciosProveedorAsync(planta, ct).ConfigureAwait(false);
        bool exists = BusinessRules.CheckProviderPriceExists(existentes, planta, rut, codProducto);
        if (exists && !permitirActualizar)
        {
            throw new InvalidOperationException("El precio ya está asociado al proveedor y material en esta planta.");
        }

        await _repository.UpsertPrecioProveedorAsync(new PrecioProveedor
        {
            Planta = planta,
            Rut = rut.Trim(),
            RazonSocial = string.IsNullOrWhiteSpace(razonSocial) ? rut.Trim() : razonSocial.Trim(),
            CodigoProducto = codProducto.Trim().ToUpperInvariant(),
            Precio = precio,
            FechaActualizacion = DateOnly.FromDateTime(DateTime.Today)
        }, permitirActualizar, Actor, ct).ConfigureAwait(false);
    }

    public async Task EliminarPrecioProveedorAsync(Planta planta, string rut, string codProducto, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMantenedorPrecios);
        await _repository.DeletePrecioProveedorAsync(planta, rut, codProducto, Actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Cliente>> GetClientesAsync(Planta? planta = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        return await _repository.GetClientesAsync(planta, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ConsultarInformes);
        return await _repository.GetProductosAsync(ct).ConfigureAwait(false);
    }

    public async Task<(bool Success, int PingMs, int TablesCount, int ProceduresCount, string Message)> TestFirebirdConnectionAsync(
        FirebirdPlantaEndpoint endpoint, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarFirebird);
        return await _firebird.TestConnectionAsync(endpoint, ct).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<FirebirdRemotePesaje> Records, bool UsedSimulatedFallback, string StatusMessage)> QueryFirebirdRemoteAsync(
        FirebirdPlantaEndpoint endpoint, DateOnly desde, DateOnly hasta, bool allowFallbackDemo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarFirebird);
        ValidateDateRange(desde, hasta);
        return await _firebird.QueryRemotePesajesAsync(endpoint, desde, hasta, allowFallbackDemo, ct).ConfigureAwait(false);
    }

    public async Task<(int Imported, int Duplicates)> ImportFromFirebirdAsync(
        Planta planta, IEnumerable<FirebirdRemotePesaje> records, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.ImportarFirebird);

        IReadOnlyList<Producto> prods = await _repository.GetProductosAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> groups = prods.ToDictionary(p => p.Codigo.ToUpperInvariant(), p => p.Grupo, StringComparer.OrdinalIgnoreCase);

        List<Pesaje> mapped = [.. records.Select(r => BusinessRules.MapRemoteToPesaje(r, planta, groups))];
        return await _repository.ImportPesajesAsync(mapped, planta, Actor, ct).ConfigureAwait(false);
    }

    public async Task<(bool Success, IReadOnlyList<string> Logs, string? ErrorMessage)> ExecuteFirebirdStoredProceduresAsync(
        FirebirdPlantaEndpoint endpoint, bool allowSimulatedDemo, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.EjecutarProcedimientosFirebird);

        var res = await _firebird.ExecuteStoredProceduresAsync(endpoint, allowSimulatedDemo, progress, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(new AuditLogEntry
        {
            Usuario = Actor,
            Accion = "EJECUCION_PA",
            Entidad = "FIREBIRD",
            Detalle = res.Success
                ? $"Ejecución transaccional PA_INS_PROV_STGO + PA_INS_MASIVO_EXP_SAP exitosa en {endpoint.ServidorIp}."
                : $"Fallo y Rollback en PA Firebird: {res.ErrorMessage}"
        }, ct).ConfigureAwait(false);
        return res;
    }

    public async Task<int> PurgeAllPesajesAsync(string confirmationPhrase, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.PurgarTablasPesaje);

        if (!string.Equals(confirmationPhrase?.Trim(), "ELIMINAR", StringComparison.Ordinal))
        {
            throw new ArgumentException("Debe escribir exactamente la palabra ELIMINAR para confirmar la purga.");
        }

        return await _repository.PurgeAllPesajesAsync(Actor, ct).ConfigureAwait(false);
    }

    public async Task ResetDemoDataAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridad);
        await _repository.ResetDemoDataAsync(Actor, ct).ConfigureAwait(false);
    }

    public async Task<(int PesajesImported, int ClientesImported, int ProductosImported, int PreciosImported, int IndexPreciosImported, IReadOnlyList<string> Warnings)>
        ImportFromAccessMdbAsync(string mdbPath, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.MigrarDesdeMdb);
        return await _mdbMigration.ImportFromMdbAsync(mdbPath, Actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 500, string? accion = null, string? usuario = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerAuditoria);
        return await _repository.GetAuditLogsAsync(limit, accion, usuario, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridad);
        return await _repository.GetUsuariosAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveUsuarioAsync(Usuario usuario, string? plainNewPassword, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridad);

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
            Rol = usuario.Rol,
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
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridad);
        if (_auth.CurrentUser?.Id == id)
        {
            throw new InvalidOperationException("No puede eliminar su propio usuario autenticado.");
        }

        await _repository.DeleteUsuarioAsync(id, Actor, ct).ConfigureAwait(false);
    }

    public async Task<string> BackupDatabaseAsync(string destinationPath, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarSeguridad);
        return await _repository.BackupDatabaseAsync(destinationPath, ct).ConfigureAwait(false);
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
