using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Application.Services;

/// <summary>
/// Servicio de aplicación integral CRM + ERP + Pesajes Industriales (1 y 2 mediciones) + Flota +
/// Inventario (Libro Mayor Inmutable) + Centro de Informes Empresariales con segmentadores multi-selección.
/// </summary>
public sealed class EnterpriseApplicationService(
    IScrapRepository scrapRepository,
    IEnterpriseRepository enterpriseRepository,
    IAuthService authService,
    IExcelExportService excelExport,
    IAppLogger? logger = null)
{
    private static readonly CultureInfo EsCl = CultureInfo.GetCultureInfo("es-CL");

    private readonly IScrapRepository _scrapRepo = scrapRepository;
    private readonly IEnterpriseRepository _repo = enterpriseRepository;
    private readonly IAuthService _auth = authService;
    private readonly IExcelExportService _excel = excelExport;
    private readonly IAppLogger _logger = logger ?? NullAppLogger.Instance;

    private string Actor => _auth.CurrentUser?.NombreUsuario ?? "SISTEMA";

    // =========================================================================
    // 1. CRM UNIFICADO (CLIENTES / PROVEEDORES POR RUT SIN DUPLICADOS)
    // =========================================================================

    public async Task<IReadOnlyList<Cliente>> GetEntidadesComercialesAsync(
        RolEntidadComercial? rol = null,
        bool? soloActivos = null,
        string? search = null,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetEntidadesComercialesAsync(rol, soloActivos, search, ct).ConfigureAwait(false);
    }

    public async Task<Cliente> SaveEntidadComercialAsync(Cliente entidad, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarCrmEntidades);

        if (!entidad.EsCliente && !entidad.EsProveedor)
        {
            throw new ArgumentException("La entidad debe tener al menos un rol comercial: Cliente, Proveedor o Ambos.");
        }

        string rawRut = string.IsNullOrWhiteSpace(entidad.Digito)
            ? entidad.Rut
            : $"{entidad.Rut}-{entidad.Digito}";

        (bool isValid, string body, string dv, _, string msg) = RutChileno.ValidateAndFormat(rawRut);
        if (!isValid)
        {
            throw new ArgumentException(msg);
        }

        if (string.IsNullOrWhiteSpace(entidad.RazonSocial) && string.IsNullOrWhiteSpace(entidad.Nombre))
        {
            throw new ArgumentException("Debe indicar la Razón Social o Nombre de la entidad comercial.");
        }

        Cliente normalized = new()
        {
            Rut = body,
            Digito = dv,
            RutNormalizado = body,
            Nombre = string.IsNullOrWhiteSpace(entidad.Nombre) ? entidad.RazonSocial.Trim() : entidad.Nombre.Trim(),
            RazonSocial = string.IsNullOrWhiteSpace(entidad.RazonSocial) ? entidad.Nombre.Trim() : entidad.RazonSocial.Trim(),
            NombreComercial = string.IsNullOrWhiteSpace(entidad.NombreComercial) ? entidad.Nombre.Trim() : entidad.NombreComercial.Trim(),
            Giro = entidad.Giro.Trim(),
            Comuna = entidad.Comuna.Trim(),
            Ciudad = entidad.Ciudad.Trim(),
            Direccion = entidad.Direccion.Trim(),
            Telefono = entidad.Telefono.Trim(),
            ContactoNombre = entidad.ContactoNombre.Trim(),
            Email = entidad.Email.Trim(),
            CondicionComercial = entidad.CondicionComercial.Trim(),
            Observaciones = entidad.Observaciones.Trim(),
            EsCliente = entidad.EsCliente,
            EsProveedor = entidad.EsProveedor,
            Activo = entidad.Activo,
            PlantaAsociada = entidad.PlantaAsociada
        };

        return await _repo.SaveEntidadComercialAsync(normalized, Actor, ct).ConfigureAwait(false);
    }

    public async Task SetEntidadActivoAsync(string rut, bool activo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarCrmEntidades);
        await _repo.SetEntidadActivoAsync(rut, activo, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeleteEntidadSafelyAsync(string rut, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarCrmEntidades);
        await _repo.DeleteEntidadSafelyAsync(rut, Actor, ct).ConfigureAwait(false);
    }

    public async Task<EntidadComercialFichaDto> GetEntidadFicha360Async(string rut, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        Cliente entidad = await _repo.GetEntidadByRutAsync(rut, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró la entidad comercial con RUT '{rut}'.");

        IReadOnlyList<Pesaje> allPesajes = await _scrapRepo.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        List<Pesaje> matchedPesajes = [.. allPesajes
            .Where(p => RutChileno.Matches(p.RutCliente, entidad.RutFormateado))
            .OrderByDescending(p => p.FechaIngreso)
            .ThenByDescending(p => p.Folio)];

        List<Pesaje> confirmed = [.. matchedPesajes.Where(p => p.Estado == EstadoPesaje.Confirmado)];

        IReadOnlyList<OperacionComercial> allOps = await _repo.GetOperacionesAsync(ct: ct).ConfigureAwait(false);
        List<OperacionComercial> matchedOps = [.. allOps.Where(o =>
            RutChileno.Matches(o.RutCliente, entidad.RutFormateado) ||
            RutChileno.Matches(o.RutProveedor, entidad.RutFormateado))];

        IReadOnlyList<Vehiculo> allVeh = await _repo.GetVehiculosAsync(ct: ct).ConfigureAwait(false);
        List<Vehiculo> matchedVeh = [.. allVeh.Where(v => RutChileno.Matches(v.RutEntidadAsociada, entidad.RutFormateado))];

        IReadOnlyList<DocumentoAsociado> docs = await _repo.GetDocumentosAsync(rutEntidad: entidad.RutFormateado, ct: ct).ConfigureAwait(false);

        return new EntidadComercialFichaDto(
            entidad,
            confirmed.Count,
            confirmed.Sum(p => (long)p.PesoNeto),
            matchedOps.Count,
            confirmed.Count > 0 ? confirmed.Max(p => p.FechaIngreso) : null,
            [.. matchedPesajes.Take(200)],
            matchedOps,
            matchedVeh,
            docs);
    }

    public async Task<DocumentoAsociado> SaveDocumentoAsync(DocumentoAsociado doc, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarCrmEntidades);
        return await _repo.SaveDocumentoAsync(doc, Actor, ct).ConfigureAwait(false);
    }

    // =========================================================================
    // 2. MATERIALES (CRUD COMPLETO, FILTROS COMBINADOS E HISTORIAL)
    // =========================================================================

    public async Task<IReadOnlyList<Producto>> GetMaterialesAsync(
        bool? soloActivos = null,
        string? search = null,
        string? grupo = null,
        string? unidad = null,
        string? proveedorRut = null,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        IReadOnlyList<Producto> all = await _repo.GetMaterialesAsync(soloActivos, ct).ConfigureAwait(false);
        IEnumerable<Producto> query = all;

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(p =>
                p.Codigo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.NombreVisible.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Descripcion.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.InformacionAdicional.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(grupo))
        {
            string g = grupo.Trim();
            query = query.Where(p => string.Equals(p.Grupo, g, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(unidad))
        {
            string u = WeighingRules.NormalizeUnit(unidad);
            query = query.Where(p => string.Equals(p.UnidadMedida, u, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(proveedorRut))
        {
            IReadOnlyList<Pesaje> pesajes = await _scrapRepo.GetPesajesAsync(ct: ct).ConfigureAwait(false);
            HashSet<string> codesBySupplier = [.. pesajes
                .Where(p => RutChileno.Matches(p.RutCliente, proveedorRut))
                .Select(p => p.CodProducto.Trim().ToUpperInvariant())];
            query = query.Where(p => codesBySupplier.Contains(p.Codigo.Trim().ToUpperInvariant()));
        }

        return [.. query];
    }

    public async Task<Producto> SaveMaterialAsync(Producto material, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        string unit = WeighingRules.NormalizeUnit(material.UnidadMedida);
        Producto clean = new()
        {
            Codigo = material.Codigo.Trim().ToUpperInvariant(),
            Nombre = string.IsNullOrWhiteSpace(material.Nombre) ? material.Descripcion.Trim().ToUpperInvariant() : material.Nombre.Trim().ToUpperInvariant(),
            Descripcion = string.IsNullOrWhiteSpace(material.Descripcion) ? material.Nombre.Trim().ToUpperInvariant() : material.Descripcion.Trim().ToUpperInvariant(),
            Grupo = string.IsNullOrWhiteSpace(material.Grupo) ? "GENERAL" : material.Grupo.Trim().ToUpperInvariant(),
            UnidadMedida = unit,
            InformacionAdicional = material.InformacionAdicional.Trim(),
            Activo = material.Activo,
            ToleranciaDiferenciaPct = Math.Clamp(material.ToleranciaDiferenciaPct, 0.0, 100.0)
        };

        return await _repo.SaveMaterialAsync(clean, Actor, ct).ConfigureAwait(false);
    }

    public async Task SetMaterialActivoAsync(string codigo, bool activo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        await _repo.SetMaterialActivoAsync(codigo, activo, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeleteMaterialSafelyAsync(string codigo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        await _repo.DeleteMaterialSafelyAsync(codigo, Actor, ct).ConfigureAwait(false);
    }

    public async Task<MaterialHistorialDto> GetMaterialHistorialAsync(string codigo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerGrupoMaterial);
        Producto mat = await _repo.GetMaterialByCodigoAsync(codigo, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Material '{codigo}' no encontrado.");

        IReadOnlyList<Pesaje> allPesajes = await _scrapRepo.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        List<Pesaje> matPesajes = [.. allPesajes
            .Where(p => string.Equals(p.CodProducto.Trim(), mat.Codigo, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.FechaIngreso)
            .ThenByDescending(p => p.Folio)];
        List<Pesaje> confirmed = [.. matPesajes.Where(p => p.Estado == EstadoPesaje.Confirmado)];

        IReadOnlyList<InventarioExistencia> stockAll = await _repo.GetExistenciasInventarioAsync(ct).ConfigureAwait(false);
        InventarioExistencia? st = stockAll.FirstOrDefault(s => string.Equals(s.CodProducto, mat.Codigo, StringComparison.OrdinalIgnoreCase));
        IReadOnlyList<InventarioMovimiento> movs = await _repo.GetMovimientosInventarioAsync(codProducto: mat.Codigo, limit: 200, ct: ct).ConfigureAwait(false);

        return new MaterialHistorialDto(
            mat,
            st?.StockActualKg ?? 0,
            st?.EntradasKg ?? 0,
            st?.SalidasKg ?? 0,
            confirmed.Count,
            confirmed.Sum(p => (long)p.PesoNeto),
            [.. matPesajes.Take(200)],
            movs);
    }

    // =========================================================================
    // 3. VEHÍCULOS Y PATENTES (NORMALIZACIÓN + HISTORIAL DE PESAJES)
    // =========================================================================

    public async Task<IReadOnlyList<Vehiculo>> GetVehiculosAsync(bool? soloActivos = null, string? search = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetVehiculosAsync(soloActivos, search, ct).ConfigureAwait(false);
    }

    public async Task<Vehiculo> SaveVehiculoAsync(Vehiculo vehiculo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        return await _repo.SaveVehiculoAsync(vehiculo, Actor, ct).ConfigureAwait(false);
    }

    public async Task SetVehiculoActivoAsync(long id, bool activo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        await _repo.SetVehiculoActivoAsync(id, activo, Actor, ct).ConfigureAwait(false);
    }

    public async Task DeleteVehiculoSafelyAsync(long id, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AdministrarMaterialesVehiculos);
        await _repo.DeleteVehiculoSafelyAsync(id, Actor, ct).ConfigureAwait(false);
    }

    public async Task<VehiculoHistorialDto> GetVehiculoHistorialAsync(string patente, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        Vehiculo veh = await _repo.GetVehiculoByPatenteAsync(patente, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró un vehículo registrado para la patente '{patente}'.");

        IReadOnlyList<Pesaje> all = await _scrapRepo.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        List<Pesaje> matched = [.. all
            .Where(p => PatenteChilena.MatchesExact(p.Patente, veh.PatenteNormalizada))
            .OrderByDescending(p => p.FechaIngreso)
            .ThenByDescending(p => p.Folio)];
        List<Pesaje> confirmed = [.. matched.Where(p => p.Estado == EstadoPesaje.Confirmado)];

        return new VehiculoHistorialDto(
            veh,
            confirmed.Count,
            confirmed.Sum(p => (long)p.PesoNeto),
            confirmed.Count > 0 ? confirmed.Min(p => p.FechaIngreso) : null,
            confirmed.Count > 0 ? confirmed.Max(p => p.FechaIngreso) : null,
            [.. matched.Take(200)]);
    }

    // =========================================================================
    // 4. TIPOS DE PESAJE CONFIGURABLES
    // =========================================================================

    public async Task<IReadOnlyList<TipoPesajeConfig>> GetTiposPesajeConfigAsync(bool? soloActivos = null, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetTiposPesajeConfigAsync(soloActivos, ct).ConfigureAwait(false);
    }

    public async Task<TipoPesajeConfig> SaveTipoPesajeConfigAsync(TipoPesajeConfig config, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);
        return await _repo.SaveTipoPesajeConfigAsync(config, Actor, ct).ConfigureAwait(false);
    }

    public async Task SetTipoPesajeConfigActivoAsync(long id, bool activo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);
        await _repo.SetTipoPesajeConfigActivoAsync(id, activo, Actor, ct).ConfigureAwait(false);
    }

    // =========================================================================
    // 5. PESAJES INDUSTRIALES (1 PASO, 2 MEDICIONES, CORRECCIÓN Y ANULACIÓN)
    // =========================================================================

    public async Task<Pesaje> RegistrarPesajeCompletoAsync(PesajeRegistroRequestDto req, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);

        if (req.AjusteManualAutorizado || req.AutorizacionExcepcionTolerancia)
        {
            _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        }

        (Producto mat, Vehiculo veh, Cliente? ent, TipoPesajeConfig? cfg, string grupoMat) =
            await ValidateAndResolveWeighingReferencesAsync(req, ct).ConfigureAwait(false);

        WeighingCalculationResult calc = WeighingRules.ValidateAndCalculateSingleStep(
            req.PesoBruto,
            req.PesoTara,
            req.PesoNetoManual,
            req.AjusteManualAutorizado,
            req.MotivoAjuste,
            req.UnidadMedida,
            mat.UnidadMedida);

        int? pesoEsperado = req.PesoEsperadoKg;
        if (!pesoEsperado.HasValue && req.OperacionId.HasValue)
        {
            OperacionComercial? op = await _repo.GetOperacionByIdAsync(req.OperacionId.Value, ct).ConfigureAwait(false);
            if (op is not null && op.CantidadEsperadaKg > 0 && op.CantidadEsperadaKg <= int.MaxValue)
            {
                pesoEsperado = (int)op.CantidadEsperadaKg;
            }
        }

        WeightDiscrepancyResult disc = WeighingRules.EvaluateWeightDiscrepancy(calc.PesoNeto, pesoEsperado, mat.ToleranciaDiferenciaPct);
        EstadoPesaje estado = disc.RequiereAutorizacion && !req.AutorizacionExcepcionTolerancia
            ? EstadoPesaje.PendienteAutorizacion
            : EstadoPesaje.Confirmado;

        TipoPesaje tipo = cfg?.TipoBase ?? req.TipoPesaje;
        string motivoFinal = disc.RequiereAutorizacion && !req.AutorizacionExcepcionTolerancia
            ? disc.Resumen
            : req.MotivoAjuste.Trim();

        Pesaje entity = BuildPesajeEntity(
            req,
            tipo,
            mat,
            veh,
            ent,
            cfg,
            grupoMat,
            calc.PesoBruto,
            calc.PesoTara,
            calc.PesoNeto,
            calc.UnidadMedida,
            estado,
            primeraKg: calc.PesoBruto,
            primeraUtc: DateTime.UtcNow,
            segundaKg: calc.PesoTara,
            segundaUtc: DateTime.UtcNow,
            pesoEsperado: pesoEsperado,
            ajusteManual: calc.UsoAjusteManualAutorizado,
            motivo: motivoFinal);

        Pesaje saved = await _repo.SavePesajeIndustrialAsync(entity, Actor, "PESAJE_CREAR_COMPLETO", motivoFinal, ct).ConfigureAwait(false);

        if (saved.Estado == EstadoPesaje.Confirmado)
        {
            await PostInventoryIfApplicableAsync(saved, cfg, ct).ConfigureAwait(false);
        }

        return saved;
    }

    public async Task<Pesaje> IniciarPesajeDosMedicionesAsync(PesajeRegistroRequestDto req, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);

        (Producto mat, Vehiculo veh, Cliente? ent, TipoPesajeConfig? cfg, string grupoMat) =
            await ValidateAndResolveWeighingReferencesAsync(req, ct).ConfigureAwait(false);

        int primeraMedicion = req.PrimeraMedicionKg ?? req.PesoBruto;
        string unit = WeighingRules.ValidateFirstMeasurement(primeraMedicion, req.UnidadMedida, mat.UnidadMedida);
        TipoPesaje tipo = cfg?.TipoBase ?? (req.TipoPesaje == TipoPesaje.RecepcionCarga ? TipoPesaje.EntradaSalida : req.TipoPesaje);

        int? pesoEsperado = req.PesoEsperadoKg;
        if (!pesoEsperado.HasValue && req.OperacionId.HasValue)
        {
            OperacionComercial? op = await _repo.GetOperacionByIdAsync(req.OperacionId.Value, ct).ConfigureAwait(false);
            if (op is not null && op.CantidadEsperadaKg > 0 && op.CantidadEsperadaKg <= int.MaxValue)
            {
                pesoEsperado = (int)op.CantidadEsperadaKg;
            }
        }

        // Regla estricta: NO calcula peso neto definitivo ni impacta inventario hasta la 2ª medición.
        Pesaje pending = BuildPesajeEntity(
            req,
            tipo,
            mat,
            veh,
            ent,
            cfg,
            grupoMat,
            pesoBruto: primeraMedicion,
            pesoTara: 0,
            pesoNeto: 0,
            unidad: unit,
            estado: EstadoPesaje.PendienteSegundaMedicion,
            primeraKg: primeraMedicion,
            primeraUtc: DateTime.UtcNow,
            segundaKg: null,
            segundaUtc: null,
            pesoEsperado: pesoEsperado,
            ajusteManual: false,
            motivo: "1ª medición registrada; pendiente de 2ª medición para calcular peso neto.");

        return await _repo.SavePesajeIndustrialAsync(pending, Actor, "PESAJE_INICIAR_1RA_MEDICION", "Registro de primera medición.", ct).ConfigureAwait(false);
    }

    public async Task<Pesaje> CompletarSegundaMedicionAsync(
        long pesajeId,
        int segundaMedicionKg,
        DateOnly fechaSalida,
        TimeOnly horaSalida,
        string? observacion = null,
        bool autorizacionExcepcionTolerancia = false,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);
        if (autorizacionExcepcionTolerancia)
        {
            _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        }

        Pesaje existing = await _repo.GetPesajeByIdAsync(pesajeId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró el pesaje #{pesajeId}.");

        if (existing.Estado == EstadoPesaje.Anulado)
        {
            throw new InvalidOperationException("No se puede completar la segunda medición de un pesaje anulado.");
        }

        if (existing.Estado != EstadoPesaje.PendienteSegundaMedicion)
        {
            throw new InvalidOperationException($"El pesaje Folio {existing.FolioDisplay} ya fue cerrado (estado actual: {existing.EstadoDisplay}). No se permiten mediciones duplicadas.");
        }

        int primeraKg = existing.PrimeraMedicionKg ?? existing.PesoBruto;
        DateTime primeraUtc = existing.PrimeraMedicionUtc ?? DateTime.UtcNow;

        Producto? mat = await _repo.GetMaterialByCodigoAsync(existing.CodProducto, ct).ConfigureAwait(false);
        WeighingCalculationResult calc = WeighingRules.ResolveTwoStepMeasurements(
            primeraKg,
            segundaMedicionKg,
            existing.UnidadMedida,
            mat?.UnidadMedida);

        double tol = mat?.ToleranciaDiferenciaPct ?? 5.0;
        WeightDiscrepancyResult disc = WeighingRules.EvaluateWeightDiscrepancy(calc.PesoNeto, existing.PesoEsperadoKg, tol);
        EstadoPesaje newState = disc.RequiereAutorizacion && !autorizacionExcepcionTolerancia
            ? EstadoPesaje.PendienteAutorizacion
            : EstadoPesaje.Confirmado;

        TipoPesajeConfig? cfg = existing.TipoPesajeConfigId.HasValue
            ? (await _repo.GetTiposPesajeConfigAsync(ct: ct).ConfigureAwait(false)).FirstOrDefault(t => t.Id == existing.TipoPesajeConfigId.Value)
            : null;

        string mergedObs = string.IsNullOrWhiteSpace(observacion)
            ? existing.Observacion
            : string.IsNullOrWhiteSpace(existing.Observacion) ? observacion.Trim() : $"{existing.Observacion} | {observacion.Trim()}";

        Pesaje completed = new()
        {
            Id = existing.Id,
            Folio = existing.Folio,
            FolioCodigo = existing.FolioCodigo,
            Planta = existing.Planta,
            TipoPesaje = existing.TipoPesaje,
            Patente = existing.Patente,
            Acoplado = existing.Acoplado,
            PesoBruto = calc.PesoBruto,
            PesoTara = calc.PesoTara,
            PesoNeto = calc.PesoNeto,
            UnidadMedida = calc.UnidadMedida,
            CodProducto = existing.CodProducto,
            NomProducto = existing.NomProducto,
            RutCliente = existing.RutCliente,
            NomCliente = existing.NomCliente,
            Origen = existing.Origen,
            Destino = existing.Destino,
            GuiaDespacho = existing.GuiaDespacho,
            Chofer = existing.Chofer,
            Transportista = existing.Transportista,
            FechaIngreso = existing.FechaIngreso,
            HoraIngreso = existing.HoraIngreso,
            FechaSalida = fechaSalida,
            HoraSalida = horaSalida,
            Observacion = mergedObs,
            GrupoMaterial = existing.GrupoMaterial,
            EsTraslado = existing.EsTraslado,
            CiudadCliente = existing.CiudadCliente,
            Bascula = existing.Bascula,
            Operador = existing.Operador,
            Estado = newState,
            TipoPesajeConfigId = existing.TipoPesajeConfigId,
            OperacionId = existing.OperacionId,
            VehiculoId = existing.VehiculoId,
            PrimeraMedicionKg = primeraKg,
            PrimeraMedicionUtc = primeraUtc,
            SegundaMedicionKg = segundaMedicionKg,
            SegundaMedicionUtc = DateTime.UtcNow,
            PesoEsperadoKg = existing.PesoEsperadoKg,
            AjusteManualAutorizado = existing.AjusteManualAutorizado,
            MotivoAjusteOAnulacion = disc.RequiereAutorizacion && !autorizacionExcepcionTolerancia ? disc.Resumen : "2ª medición completada."
        };

        Pesaje saved = await _repo.SavePesajeIndustrialAsync(completed, Actor, "PESAJE_COMPLETAR_2DA_MEDICION", completed.MotivoAjusteOAnulacion, ct).ConfigureAwait(false);

        if (saved.Estado == EstadoPesaje.Confirmado)
        {
            await PostInventoryIfApplicableAsync(saved, cfg, ct).ConfigureAwait(false);
        }

        return saved;
    }

    public async Task<Pesaje> AutorizarPesajePendienteAsync(long pesajeId, string motivo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("Debe ingresar el motivo de autorización del pesaje.");
        }

        Pesaje existing = await _repo.GetPesajeByIdAsync(pesajeId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró el pesaje #{pesajeId}.");

        if (existing.Estado != EstadoPesaje.PendienteAutorizacion)
        {
            throw new InvalidOperationException($"El pesaje Folio {existing.FolioDisplay} no está pendiente de autorización (estado: {existing.EstadoDisplay}).");
        }

        Pesaje approved = CloneWithState(existing, EstadoPesaje.Confirmado, $"Autorizado: {motivo.Trim()}");
        Pesaje saved = await _repo.SavePesajeIndustrialAsync(approved, Actor, "PESAJE_AUTORIZAR", motivo.Trim(), ct).ConfigureAwait(false);

        TipoPesajeConfig? cfg = saved.TipoPesajeConfigId.HasValue
            ? (await _repo.GetTiposPesajeConfigAsync(ct: ct).ConfigureAwait(false)).FirstOrDefault(t => t.Id == saved.TipoPesajeConfigId.Value)
            : null;
        await PostInventoryIfApplicableAsync(saved, cfg, ct).ConfigureAwait(false);

        return saved;
    }

    public async Task<Pesaje> ModificarPesajeAutorizadoAsync(PesajeRegistroRequestDto req, string motivo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        if (req.Id <= 0)
        {
            throw new ArgumentException("Debe indicar el ID del pesaje a modificar.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("Debe indicar un motivo auditable para modificar un pesaje registrado.");
        }

        Pesaje existing = await _repo.GetPesajeByIdAsync(req.Id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró el pesaje #{req.Id}.");

        if (existing.Estado == EstadoPesaje.Anulado)
        {
            throw new InvalidOperationException("No se puede modificar un pesaje anulado. Registre un nuevo pesaje si corresponde.");
        }

        (Producto mat, Vehiculo veh, Cliente? ent, TipoPesajeConfig? cfg, string grupoMat) =
            await ValidateAndResolveWeighingReferencesAsync(req, ct).ConfigureAwait(false);

        WeighingCalculationResult calc = WeighingRules.ValidateAndCalculateSingleStep(
            req.PesoBruto,
            req.PesoTara,
            req.PesoNetoManual,
            req.AjusteManualAutorizado,
            string.IsNullOrWhiteSpace(req.MotivoAjuste) ? motivo : req.MotivoAjuste,
            req.UnidadMedida,
            mat.UnidadMedida);

        TipoPesaje tipo = cfg?.TipoBase ?? req.TipoPesaje;

        Pesaje updated = BuildPesajeEntity(
            req,
            tipo,
            mat,
            veh,
            ent,
            cfg,
            grupoMat,
            calc.PesoBruto,
            calc.PesoTara,
            calc.PesoNeto,
            calc.UnidadMedida,
            EstadoPesaje.Confirmado,
            primeraKg: existing.PrimeraMedicionKg ?? calc.PesoBruto,
            primeraUtc: existing.PrimeraMedicionUtc ?? DateTime.UtcNow,
            segundaKg: existing.SegundaMedicionKg ?? calc.PesoTara,
            segundaUtc: existing.SegundaMedicionUtc ?? DateTime.UtcNow,
            pesoEsperado: req.PesoEsperadoKg ?? existing.PesoEsperadoKg,
            ajusteManual: calc.UsoAjusteManualAutorizado,
            motivo: motivo.Trim(),
            existingId: existing.Id,
            existingFolio: existing.Folio,
            existingFolioCodigo: string.IsNullOrWhiteSpace(req.FolioCodigo) ? existing.FolioDisplay : req.FolioCodigo);

        Pesaje saved = await _repo.SavePesajeIndustrialAsync(updated, Actor, "PESAJE_CORRECCION_AUTORIZADA", motivo.Trim(), ct).ConfigureAwait(false);
        await PostInventoryIfApplicableAsync(saved, cfg, ct).ConfigureAwait(false);

        return saved;
    }

    public async Task<Pesaje> AnularPesajeAsync(long pesajeId, string motivo, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("Debe indicar el motivo obligatorio para anular el pesaje.");
        }

        Pesaje existing = await _repo.GetPesajeByIdAsync(pesajeId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se encontró el pesaje #{pesajeId}.");

        if (existing.Estado == EstadoPesaje.Anulado)
        {
            return existing;
        }

        Pesaje annulled = CloneWithState(existing, EstadoPesaje.Anulado, $"Anulado: {motivo.Trim()}");
        Pesaje saved = await _repo.SavePesajeIndustrialAsync(annulled, Actor, "PESAJE_ANULAR", motivo.Trim(), ct).ConfigureAwait(false);

        // Reverso automático e idempotente en el libro de inventario (sin eliminar historial)
        await _repo.RevertirInventarioParaPesajeAnuladoAsync(saved, motivo.Trim(), Actor, ct).ConfigureAwait(false);
        _logger.Log(DiagnosticLogLevel.Warning, nameof(EnterpriseApplicationService),
            $"Pesaje Folio {saved.FolioDisplay} anulado por {Actor}. Motivo: {motivo.Trim()}");

        return saved;
    }

    public async Task<IReadOnlyList<PesajeHistorialCambio>> GetPesajeHistorialAsync(long pesajeId, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetPesajeHistorialAsync(pesajeId, ct).ConfigureAwait(false);
    }

    // =========================================================================
    // 6. OPERACIONES ERP Y TRAZABILIDAD COMPLETA
    // =========================================================================

    public async Task<IReadOnlyList<OperacionComercial>> GetOperacionesAsync(
        TipoOperacionComercial? tipo = null,
        EstadoOperacion? estado = null,
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetOperacionesAsync(tipo, estado, desde, hasta, ct).ConfigureAwait(false);
    }

    public async Task<OperacionComercial> SaveOperacionAsync(OperacionComercial op, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);
        if (string.IsNullOrWhiteSpace(op.CodProducto))
        {
            throw new ArgumentException("Debe seleccionar un material para la operación comercial.");
        }

        Producto mat = await _repo.GetMaterialByCodigoAsync(op.CodProducto, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"El material '{op.CodProducto}' no existe en el catálogo maestro.");

        string unit = WeighingRules.NormalizeUnit(op.UnidadMedida);
        WeighingRules.ValidateUnitConsistency(unit, mat.UnidadMedida);

        OperacionComercial normalized = new()
        {
            Id = op.Id,
            Codigo = op.Codigo,
            TipoOperacion = op.TipoOperacion,
            Fecha = op.Fecha,
            RutCliente = op.RutCliente.Trim(),
            NombreCliente = op.NombreCliente.Trim(),
            RutProveedor = op.RutProveedor.Trim(),
            NombreProveedor = op.NombreProveedor.Trim(),
            CodProducto = mat.Codigo,
            NombreProducto = mat.NombreVisible,
            CantidadEsperadaKg = op.CantidadEsperadaKg,
            CantidadRealConfirmadaKg = op.CantidadRealConfirmadaKg,
            UnidadMedida = unit,
            Patente = op.Patente.Trim(),
            DocumentoAsociado = op.DocumentoAsociado.Trim(),
            Estado = op.Estado,
            Observaciones = op.Observaciones.Trim(),
            CreadoPor = string.IsNullOrWhiteSpace(op.CreadoPor) ? Actor : op.CreadoPor,
            CreadoEnUtc = op.CreadoEnUtc
        };

        return await _repo.SaveOperacionAsync(normalized, Actor, ct).ConfigureAwait(false);
    }

    public async Task UpdateOperacionEstadoAsync(long id, EstadoOperacion nuevoEstado, string motivo, CancellationToken ct = default)
    {
        if (nuevoEstado == EstadoOperacion.Anulada)
        {
            _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                throw new ArgumentException("Debe ingresar el motivo de anulación de la operación.");
            }
        }
        else
        {
            _auth.EnsurePermission(PermisoSistema.GestionarPesajesOperaciones);
        }

        await _repo.UpdateOperacionEstadoAsync(id, nuevoEstado, motivo, Actor, ct).ConfigureAwait(false);
    }

    public async Task<OperacionTrazabilidadDto> GetOperacionTrazabilidadAsync(long operacionId, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        OperacionComercial op = await _repo.GetOperacionByIdAsync(operacionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Operación #{operacionId} no encontrada.");

        IReadOnlyList<Pesaje> allPes = await _scrapRepo.GetPesajesAsync(ct: ct).ConfigureAwait(false);
        List<Pesaje> linkedPes = [.. allPes.Where(p => p.OperacionId == operacionId).OrderByDescending(p => p.FechaIngreso).ThenByDescending(p => p.Folio)];
        IReadOnlyList<InventarioMovimiento> movs = await _repo.GetMovimientosInventarioAsync(operacionId: operacionId, limit: 500, ct: ct).ConfigureAwait(false);
        IReadOnlyList<DocumentoAsociado> docs = await _repo.GetDocumentosAsync(operacionId: operacionId, ct: ct).ConfigureAwait(false);

        long totalConf = linkedPes.Where(p => p.Estado == EstadoPesaje.Confirmado).Sum(p => (long)p.PesoNeto);
        return new OperacionTrazabilidadDto(op, totalConf, totalConf - op.CantidadEsperadaKg, linkedPes, movs, docs);
    }

    // =========================================================================
    // 7. INVENTARIO (STOCK DERIVADO Y AJUSTES AUTORIZADOS)
    // =========================================================================

    public async Task<IReadOnlyList<InventarioExistencia>> GetExistenciasInventarioAsync(CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetExistenciasInventarioAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InventarioMovimiento>> GetMovimientosInventarioAsync(
        string? codProducto = null,
        long? operacionId = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        return await _repo.GetMovimientosInventarioAsync(codProducto, operacionId, limit, ct).ConfigureAwait(false);
    }

    public async Task<InventarioMovimiento> RegistrarAjusteInventarioAsync(
        string codProducto,
        long cantidadFirmadaKg,
        string documentoRef,
        string motivo,
        CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.AutorizarAnularPesajesInventario);
        return await _repo.RegistrarAjusteInventarioAsync(codProducto, cantidadFirmadaKg, documentoRef, motivo, Actor, ct).ConfigureAwait(false);
    }

    // =========================================================================
    // 8. CENTRO DE INFORMES EMPRESARIALES Y SEGMENTADORES MULTI-SELECCIÓN
    // =========================================================================

    public async Task<EnterpriseReportResultDto> GetEnterpriseReportAsync(EnterpriseReportFilterDto filter, CancellationToken ct = default)
    {
        _auth.EnsurePermission(PermisoSistema.VerInformeResumen);
        if (filter.Desde.HasValue && filter.Hasta.HasValue && filter.Desde.Value > filter.Hasta.Value)
        {
            throw new ArgumentException("La fecha 'Desde' no puede ser posterior a la fecha 'Hasta'.");
        }

        IReadOnlyList<Pesaje> allPesajes = await _scrapRepo.GetPesajesAsync(filter.Planta, ct: ct).ConfigureAwait(false);
        IReadOnlyList<Cliente> entidades = await _repo.GetEntidadesComercialesAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<Producto> materiales = await _repo.GetMaterialesAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<Vehiculo> vehiculos = await _repo.GetVehiculosAsync(ct: ct).ConfigureAwait(false);
        IReadOnlyList<MaterialGroupNode> nodes = await _scrapRepo.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        IReadOnlyList<InventarioExistencia> existencias = await _repo.GetExistenciasInventarioAsync(ct).ConfigureAwait(false);

        Dictionary<string, string> prodToGroup = BusinessRules.BuildProductToRootGroupMap(nodes);
        Dictionary<string, Cliente> entityByRut = BuildEntityLookup(entidades);
        Dictionary<string, Producto> matByCode = materiales.ToDictionary(m => m.Codigo.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Vehiculo> vehByPat = vehiculos.ToDictionary(v => v.PatenteNormalizada, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> stockByCode = existencias.ToDictionary(e => e.CodProducto.Trim().ToUpperInvariant(), e => e.StockActualKg, StringComparer.OrdinalIgnoreCase);

        // Construir facetas para los 9 botones de segmentación multi-selección
        EnterpriseSlicerFacetsDto facets = BuildSlicerFacets(allPesajes, entityByRut, prodToGroup);

        // Filtrado unificado (OR dentro de cada categoría multi-selección, AND entre categorías)
        List<Pesaje> filtered = ApplyEnterpriseFilters(allPesajes, filter, entityByRut, prodToGroup);

        long totalBruto = filtered.Sum(p => (long)p.PesoBruto);
        long totalTara = filtered.Sum(p => (long)p.PesoTara);
        long totalNeto = filtered.Sum(p => (long)p.PesoNeto);
        int cant = filtered.Count;
        double promNeto = cant > 0 ? Math.Round(totalNeto / (double)cant, 1) : 0.0;

        // 1. Informe por Patente
        List<InformePatenteRowDto> porPatente = [.. filtered
            .GroupBy(p =>
            {
                string norm = PatenteChilena.Normalize(p.Patente);
                return string.IsNullOrEmpty(norm) ? "SIN PATENTE" : norm;
            })
            .Select(g =>
            {
                long neto = g.Sum(x => (long)x.PesoNeto);
                vehByPat.TryGetValue(g.Key, out Vehiculo? veh);
                string patDisplay = g.Key == "SIN PATENTE" ? "S/P" : (veh?.PatenteOriginal ?? PatenteChilena.FormatDisplay(g.Key));
                string matTop = g.GroupBy(x => x.NomProducto).OrderByDescending(mg => mg.Sum(z => (long)z.PesoNeto)).First().Key;
                string entAsoc = !string.IsNullOrWhiteSpace(veh?.NombreEntidadAsociada)
                    ? veh.NombreEntidadAsociada
                    : g.GroupBy(x => x.NomCliente).OrderByDescending(eg => eg.Count()).First().Key;
                return new InformePatenteRowDto(
                    patDisplay,
                    veh?.TipoVehiculo ?? "Vehículo",
                    entAsoc,
                    g.Count(),
                    g.Sum(x => (long)x.PesoBruto),
                    g.Sum(x => (long)x.PesoTara),
                    neto,
                    totalNeto > 0 ? Math.Round(neto * 100.0 / totalNeto, 2) : 0.0,
                    g.Min(x => x.FechaIngreso),
                    g.Max(x => x.FechaIngreso),
                    matTop);
            })
            .Where(r => !filter.ExcluirTotalesCero || r.TotalNetoKg > 0)
            .OrderByDescending(r => r.TotalNetoKg)];

        // 2. Informe por Material
        List<InformeMaterialRowDto> porMaterial = [.. filtered
            .GroupBy(p => p.CodProducto.Trim().ToUpperInvariant())
            .Select(g =>
            {
                Pesaje first = g.First();
                long neto = g.Sum(x => (long)x.PesoNeto);
                matByCode.TryGetValue(g.Key, out Producto? mat);
                string grp = prodToGroup.TryGetValue(g.Key, out string? root) ? root : (mat?.Grupo ?? first.GrupoMaterial);
                stockByCode.TryGetValue(g.Key, out long stock);
                return new InformeMaterialRowDto(
                    g.Key,
                    mat?.NombreVisible ?? first.NomProducto,
                    grp,
                    mat?.UnidadMedida ?? first.UnidadMedida,
                    g.Count(),
                    g.Sum(x => (long)x.PesoBruto),
                    g.Sum(x => (long)x.PesoTara),
                    neto,
                    totalNeto > 0 ? Math.Round(neto * 100.0 / totalNeto, 2) : 0.0,
                    stock);
            })
            .Where(r => !filter.ExcluirTotalesCero || r.TotalNetoKg > 0)
            .OrderByDescending(r => r.TotalNetoKg)];

        // 3. Informe por RUT (identificando rol de la entidad: Cliente, Proveedor o Ambos)
        List<InformeEntidadRowDto> porRut = BuildEntityReportRows(filtered, entityByRut, totalNeto, filter.ExcluirTotalesCero);

        // 4. Informe por Proveedor
        List<Pesaje> pesajesProv = [.. filtered.Where(p => IsProveedorWeighing(p, entityByRut))];
        long totalNetoProv = pesajesProv.Sum(p => (long)p.PesoNeto);
        List<InformeEntidadRowDto> porProveedor = BuildEntityReportRows(pesajesProv, entityByRut, totalNetoProv, filter.ExcluirTotalesCero);

        // 5. Informe por Cliente
        List<Pesaje> pesajesCli = [.. filtered.Where(p => IsClienteWeighing(p, entityByRut))];
        long totalNetoCli = pesajesCli.Sum(p => (long)p.PesoNeto);
        List<InformeEntidadRowDto> porCliente = BuildEntityReportRows(pesajesCli, entityByRut, totalNetoCli, filter.ExcluirTotalesCero);

        // 6. Informe por Tipo de Pesaje
        List<InformeTipoPesajeRowDto> porTipo = [.. filtered
            .GroupBy(p => p.TipoPesaje)
            .Select(g =>
            {
                long neto = g.Sum(x => (long)x.PesoNeto);
                int count = g.Count();
                return new InformeTipoPesajeRowDto(
                    g.Key,
                    g.Key.ToDisplayName(),
                    count,
                    g.Sum(x => (long)x.PesoBruto),
                    g.Sum(x => (long)x.PesoTara),
                    neto,
                    totalNeto > 0 ? Math.Round(neto * 100.0 / totalNeto, 2) : 0.0,
                    count > 0 ? Math.Round(neto / (double)count, 1) : 0.0);
            })
            .Where(r => !filter.ExcluirTotalesCero || r.TotalNetoKg > 0)
            .OrderByDescending(r => r.TotalNetoKg)];

        List<BarChartItemDto> topChart = [.. porMaterial.Take(10).Select(m => new BarChartItemDto($"{m.CodProducto} · {m.NombreMaterial}", m.TotalNetoKg))];

        string periodoLabel = filter.Desde.HasValue && filter.Hasta.HasValue
            ? $"{filter.Desde.Value:dd/MM/yyyy} al {filter.Hasta.Value:dd/MM/yyyy}"
            : "Todo el historial";
        string filtrosResumen = BuildActiveFiltersSummary(filter);

        return new EnterpriseReportResultDto(
            periodoLabel,
            filtrosResumen,
            cant,
            totalBruto,
            totalTara,
            totalNeto,
            Math.Round(totalNeto / 1000.0, 2),
            promNeto,
            porPatente.Count,
            porMaterial.Count,
            porRut.Count,
            porPatente,
            porMaterial,
            porProveedor,
            porCliente,
            porRut,
            porTipo,
            filtered,
            facets,
            topChart);
    }

    public string ExportEnterpriseReportExcel(string filePath, EnterpriseReportResultDto report, IReadOnlyList<Pesaje>? selectedRows = null)
    {
        _auth.EnsurePermission(PermisoSistema.ExportarExcel);
        IReadOnlyList<Pesaje> detalle = selectedRows is { Count: > 0 } ? selectedRows : report.DetallePesajes;

        List<(string Indicador, string Valor, string Detalle)> kpis =
        [
            ("Período Analizado", report.PeriodoLabel, report.FiltrosActivosResumen),
            ("Cantidad de Pesajes", report.CantidadPesajes.ToString("N0", EsCl), "Registros incluidos en el filtro activo"),
            ("Total Peso Bruto (Kg)", report.TotalBrutoKg.ToString("N0", EsCl), "Suma consistente en todas las vistas"),
            ("Total Peso Tara (Kg)", report.TotalTaraKg.ToString("N0", EsCl), "Suma consistente en todas las vistas"),
            ("Total Peso Neto (Kg)", report.TotalNetoKg.ToString("N0", EsCl), $"{report.TotalToneladas:N2} toneladas"),
            ("Promedio Neto / Pesaje (Kg)", report.PromedioNetoKg.ToString("N1", EsCl), $"{report.PatentesUnicas} patentes · {report.MaterialesUnicos} materiales · {report.EntidadesUnicas} RUTs")
        ];

        List<ExportTableSection> sections =
        [
            new("Por Patente", "INFORME CONSOLIDADO POR PATENTE",
                ["Patente", "Tipo Vehículo", "Entidad Asociada", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "1er Pesaje", "Último Pesaje", "Material Principal"],
                [.. report.PorPatente.Select(r => new object?[] { r.Patente, r.TipoVehiculo, r.EntidadAsociada, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.PrimerPesaje, r.UltimoPesaje, r.MaterialPrincipal })]),
            new("Por Material", "INFORME CONSOLIDADO POR MATERIAL",
                ["Código", "Material", "Grupo", "Unidad", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "Stock Actual Kg"],
                [.. report.PorMaterial.Select(r => new object?[] { r.CodProducto, r.NombreMaterial, r.GrupoMaterial, r.Unidad, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.StockActualKg })]),
            new("Por Proveedor", "INFORME CONSOLIDADO POR PROVEEDOR",
                ["RUT", "Proveedor", "Rol CRM", "Ciudad", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "Último Pesaje", "Material Principal"],
                [.. report.PorProveedor.Select(r => new object?[] { r.Rut, r.Nombre, r.RolDescripcion, r.Ciudad, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.UltimoPesaje, r.MaterialPrincipal })]),
            new("Por Cliente", "INFORME CONSOLIDADO POR CLIENTE",
                ["RUT", "Cliente", "Rol CRM", "Ciudad", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "Último Pesaje", "Material Principal"],
                [.. report.PorCliente.Select(r => new object?[] { r.Rut, r.Nombre, r.RolDescripcion, r.Ciudad, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.UltimoPesaje, r.MaterialPrincipal })]),
            new("Por RUT", "INFORME CONSOLIDADO POR RUT (ROL UNIFICADO)",
                ["RUT", "Razón Social", "Rol CRM", "Ciudad", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "Último Pesaje", "Material Principal"],
                [.. report.PorRut.Select(r => new object?[] { r.Rut, r.Nombre, r.RolDescripcion, r.Ciudad, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.UltimoPesaje, r.MaterialPrincipal })]),
            new("Por Tipo Pesaje", "INFORME CONSOLIDADO POR TIPO DE PESAJE",
                ["Tipo de Pesaje", "Pesajes", "Bruto Kg", "Tara Kg", "Neto Kg", "% Partic.", "Promedio Kg"],
                [.. report.PorTipoPesaje.Select(r => new object?[] { r.NombreTipo, r.CantidadPesajes, r.TotalBrutoKg, r.TotalTaraKg, r.TotalNetoKg, r.ParticipacionPct, r.PromedioNetoKg })]),
            new("Detalle Pesajes", $"DETALLE DE PESAJES ({detalle.Count:N0} REGISTROS)",
                ["Folio", "Planta", "Fecha", "Hora", "Tipo Pesaje", "Estado", "Patente", "RUT", "Entidad", "Código", "Material", "Grupo", "Bruto", "Tara", "Neto", "Unidad", "Báscula", "Operador", "Documento", "Observación"],
                [.. detalle.Select(p => new object?[] { p.FolioDisplay, p.Planta.ToShortTitle(), p.FechaIngreso, p.HoraIngreso.ToString("HH:mm", CultureInfo.InvariantCulture), p.TipoPesajeDisplay, p.EstadoDisplay, p.Patente, p.RutCliente, p.NomCliente, p.CodProducto, p.NomProducto, p.GrupoMaterial, p.PesoBruto, p.PesoTara, p.PesoNeto, p.UnidadMedida, p.Bascula, p.Operador, p.GuiaDespacho, p.Observacion })])
        ];

        return _excel.ExportReportePersonalizado(
            filePath,
            new ReportePersonalizadoPayload(
                "CAMBIASO HNOS. · CENTRO DE INFORMES EMPRESARIALES DE PESAJES",
                $"Período: {report.PeriodoLabel} · {report.FiltrosActivosResumen}",
                kpis,
                sections));
    }

    // =========================================================================
    // MÉTODOS AUXILIARES PRIVADOS
    // =========================================================================

    private async Task<(Producto Material, Vehiculo Vehiculo, Cliente? Entidad, TipoPesajeConfig? Config, string GrupoMaterial)>
        ValidateAndResolveWeighingReferencesAsync(PesajeRegistroRequestDto req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CodProducto))
        {
            throw new ArgumentException("Debe seleccionar un material válido del catálogo maestro.");
        }

        Producto mat = await _repo.GetMaterialByCodigoAsync(req.CodProducto, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"El material '{req.CodProducto}' no existe en el catálogo maestro.");
        if (!mat.Activo)
        {
            throw new InvalidOperationException($"El material '{mat.Codigo} · {mat.NombreVisible}' se encuentra inactivo y no puede recibir nuevos pesajes.");
        }

        if (string.IsNullOrWhiteSpace(req.Patente))
        {
            throw new ArgumentException("La patente del vehículo es obligatoria.");
        }

        Vehiculo veh = await _repo.GetVehiculoByPatenteAsync(req.Patente, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"El vehículo con patente '{req.Patente}' no está registrado en el maestro de flota. Regístrelo primero en Vehículos y Patentes.");
        if (!veh.Activo)
        {
            throw new InvalidOperationException($"El vehículo '{veh.PatenteOriginal}' se encuentra inactivo.");
        }

        Cliente? ent = null;
        if (!string.IsNullOrWhiteSpace(req.RutEntidad))
        {
            ent = await _repo.GetEntidadByRutAsync(req.RutEntidad, ct).ConfigureAwait(false);
        }

        TipoPesajeConfig? cfg = null;
        IReadOnlyList<TipoPesajeConfig> configs = await _repo.GetTiposPesajeConfigAsync(ct: ct).ConfigureAwait(false);
        if (req.TipoPesajeConfigId.HasValue)
        {
            cfg = configs.FirstOrDefault(c => c.Id == req.TipoPesajeConfigId.Value);
        }

        cfg ??= configs.FirstOrDefault(c => c.TipoBase == req.TipoPesaje && c.Activo);

        IReadOnlyList<MaterialGroupNode> nodes = await _scrapRepo.GetMaterialNodesAsync(ct).ConfigureAwait(false);
        Dictionary<string, string> prodToGroup = BusinessRules.BuildProductToRootGroupMap(nodes);
        string grupo = prodToGroup.TryGetValue(mat.Codigo, out string? rootGrp) ? rootGrp : mat.Grupo;

        return (mat, veh, ent, cfg, grupo);
    }

    private Pesaje BuildPesajeEntity(
        PesajeRegistroRequestDto req,
        TipoPesaje tipo,
        Producto mat,
        Vehiculo veh,
        Cliente? ent,
        TipoPesajeConfig? cfg,
        string grupoMat,
        int pesoBruto,
        int pesoTara,
        int pesoNeto,
        string unidad,
        EstadoPesaje estado,
        int? primeraKg,
        DateTime? primeraUtc,
        int? segundaKg,
        DateTime? segundaUtc,
        int? pesoEsperado,
        bool ajusteManual,
        string motivo,
        long? existingId = null,
        long? existingFolio = null,
        string? existingFolioCodigo = null)
    {
        string rutFinal = ent?.RutFormateado ?? req.RutEntidad.Trim();
        string nomFinal = ent?.RazonSocial ?? (string.IsNullOrWhiteSpace(req.NombreEntidad) ? rutFinal : req.NombreEntidad.Trim());

        return new Pesaje
        {
            Id = existingId ?? req.Id,
            Folio = existingFolio ?? req.Folio,
            FolioCodigo = existingFolioCodigo ?? req.FolioCodigo.Trim(),
            Planta = req.Planta,
            TipoPesaje = tipo,
            Patente = veh.PatenteOriginal,
            Acoplado = req.Acoplado.Trim().ToUpperInvariant(),
            PesoBruto = pesoBruto,
            PesoTara = pesoTara,
            PesoNeto = pesoNeto,
            UnidadMedida = unidad,
            CodProducto = mat.Codigo,
            NomProducto = mat.NombreVisible,
            RutCliente = rutFinal,
            NomCliente = nomFinal,
            Origen = req.Origen.Trim(),
            Destino = req.Destino.Trim(),
            GuiaDespacho = req.GuiaDespacho.Trim(),
            Chofer = req.Chofer.Trim(),
            Transportista = req.Transportista.Trim(),
            FechaIngreso = req.FechaIngreso,
            HoraIngreso = req.HoraIngreso,
            FechaSalida = req.FechaSalida,
            HoraSalida = req.HoraSalida,
            Observacion = req.Observacion.Trim(),
            GrupoMaterial = grupoMat,
            EsTraslado = tipo == TipoPesaje.MovimientoInterno,
            CiudadCliente = ent?.Ciudad,
            Bascula = string.IsNullOrWhiteSpace(req.Bascula) ? "Báscula Principal" : req.Bascula.Trim(),
            Operador = string.IsNullOrWhiteSpace(req.Operador) ? Actor : req.Operador.Trim(),
            Estado = estado,
            TipoPesajeConfigId = cfg?.Id,
            OperacionId = req.OperacionId,
            VehiculoId = veh.Id,
            PrimeraMedicionKg = primeraKg,
            PrimeraMedicionUtc = primeraUtc,
            SegundaMedicionKg = segundaKg,
            SegundaMedicionUtc = segundaUtc,
            PesoEsperadoKg = pesoEsperado,
            AjusteManualAutorizado = ajusteManual,
            MotivoAjusteOAnulacion = motivo
        };
    }

    private static Pesaje CloneWithState(Pesaje source, EstadoPesaje newState, string motivo) => new()
    {
        Id = source.Id,
        Folio = source.Folio,
        FolioCodigo = source.FolioCodigo,
        Planta = source.Planta,
        TipoPesaje = source.TipoPesaje,
        Patente = source.Patente,
        Acoplado = source.Acoplado,
        PesoBruto = source.PesoBruto,
        PesoTara = source.PesoTara,
        PesoNeto = source.PesoNeto,
        UnidadMedida = source.UnidadMedida,
        CodProducto = source.CodProducto,
        NomProducto = source.NomProducto,
        RutCliente = source.RutCliente,
        NomCliente = source.NomCliente,
        Origen = source.Origen,
        Destino = source.Destino,
        GuiaDespacho = source.GuiaDespacho,
        Chofer = source.Chofer,
        Transportista = source.Transportista,
        FechaIngreso = source.FechaIngreso,
        HoraIngreso = source.HoraIngreso,
        FechaSalida = source.FechaSalida,
        HoraSalida = source.HoraSalida,
        Observacion = source.Observacion,
        GrupoMaterial = source.GrupoMaterial,
        EsTraslado = source.EsTraslado,
        CiudadCliente = source.CiudadCliente,
        Bascula = source.Bascula,
        Operador = source.Operador,
        Estado = newState,
        TipoPesajeConfigId = source.TipoPesajeConfigId,
        OperacionId = source.OperacionId,
        VehiculoId = source.VehiculoId,
        PrimeraMedicionKg = source.PrimeraMedicionKg,
        PrimeraMedicionUtc = source.PrimeraMedicionUtc,
        SegundaMedicionKg = source.SegundaMedicionKg,
        SegundaMedicionUtc = source.SegundaMedicionUtc,
        PesoEsperadoKg = source.PesoEsperadoKg,
        AjusteManualAutorizado = source.AjusteManualAutorizado,
        MotivoAjusteOAnulacion = motivo
    };

    private async Task PostInventoryIfApplicableAsync(Pesaje confirmedPesaje, TipoPesajeConfig? cfg, CancellationToken ct)
    {
        InventoryImpactResult impact = WeighingRules.ResolveInventoryImpact(confirmedPesaje.TipoPesaje, cfg, confirmedPesaje.PesoNeto);
        if (!impact.AfectaInventario)
        {
            return;
        }

        await _repo.PostInventarioParaPesajeConfirmadoAsync(
            confirmedPesaje,
            impact.TipoMovimiento,
            impact.CantidadFirmadaKg,
            Actor,
            ct).ConfigureAwait(false);
    }

    private static Dictionary<string, Cliente> BuildEntityLookup(IReadOnlyList<Cliente> entidades)
    {
        Dictionary<string, Cliente> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (Cliente c in entidades)
        {
            string key = c.RutClave;
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
            {
                map[key] = c;
            }
        }

        return map;
    }

    private static bool IsProveedorWeighing(Pesaje p, Dictionary<string, Cliente> entityByRut)
    {
        string body = RutChileno.NormalizeBody(p.RutCliente);
        if (!string.IsNullOrEmpty(body) && entityByRut.TryGetValue(body, out Cliente? ent))
        {
            return ent.EsProveedor;
        }

        return p.TipoPesaje.EsIngresoInventario() || p.TipoPesaje == TipoPesaje.MovimientoInterno;
    }

    private static bool IsClienteWeighing(Pesaje p, Dictionary<string, Cliente> entityByRut)
    {
        string body = RutChileno.NormalizeBody(p.RutCliente);
        if (!string.IsNullOrEmpty(body) && entityByRut.TryGetValue(body, out Cliente? ent))
        {
            return ent.EsCliente;
        }

        return p.TipoPesaje.EsSalidaInventario();
    }

    private static List<Pesaje> ApplyEnterpriseFilters(
        IReadOnlyList<Pesaje> allPesajes,
        EnterpriseReportFilterDto f,
        Dictionary<string, Cliente> entityByRut,
        Dictionary<string, string> prodToGroup)
    {
        IEnumerable<Pesaje> q = allPesajes;

        if (f.Desde.HasValue)
        {
            q = q.Where(p => p.FechaIngreso >= f.Desde.Value);
        }

        if (f.Hasta.HasValue)
        {
            q = q.Where(p => p.FechaIngreso <= f.Hasta.Value);
        }

        if (f.Estados is { Count: > 0 })
        {
            q = q.Where(p => f.Estados.Contains(p.Estado));
        }
        else if (f.ExcluirAnulados)
        {
            q = q.Where(p => p.Estado != EstadoPesaje.Anulado);
        }

        if (f.ExcluirTotalesCero)
        {
            q = q.Where(p => p.PesoNeto > 0);
        }

        if (!string.IsNullOrWhiteSpace(f.FolioSearch))
        {
            string term = f.FolioSearch.Trim();
            q = f.FolioExacto
                ? q.Where(p => string.Equals(p.FolioDisplay, term, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(p.Folio.ToString(CultureInfo.InvariantCulture), term, StringComparison.OrdinalIgnoreCase))
                : q.Where(p => p.FolioDisplay.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               p.Folio.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(f.PatenteSearch))
        {
            string term = f.PatenteSearch.Trim();
            q = f.PatenteExacta
                ? q.Where(p => PatenteChilena.MatchesExact(p.Patente, term))
                : q.Where(p => PatenteChilena.MatchesPartial(p.Patente, term));
        }

        if (!string.IsNullOrWhiteSpace(f.RutSearch))
        {
            string term = f.RutSearch.Trim();
            string cleanTerm = RutChileno.Clean(term).TrimStart('0');
            q = f.RutExacto
                ? q.Where(p => RutChileno.Matches(p.RutCliente, term))
                : q.Where(p => p.RutCliente.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               (!string.IsNullOrEmpty(cleanTerm) && RutChileno.Clean(p.RutCliente).Contains(cleanTerm, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(f.OperadorSearch))
        {
            string term = f.OperadorSearch.Trim();
            q = q.Where(p => p.Operador.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                             p.Bascula.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Segmentadores multi-selección (OR dentro de cada categoría, AND entre categorías)
        if (f.ProveedoresRuts is { Count: > 0 })
        {
            q = q.Where(p => IsProveedorWeighing(p, entityByRut) && f.ProveedoresRuts.Contains(NormalizeEntityKey(p)));
        }

        if (f.ClientesRuts is { Count: > 0 })
        {
            q = q.Where(p => IsClienteWeighing(p, entityByRut) && f.ClientesRuts.Contains(NormalizeEntityKey(p)));
        }

        if (f.MaterialesCodigos is { Count: > 0 })
        {
            q = q.Where(p => f.MaterialesCodigos.Contains(p.CodProducto.Trim().ToUpperInvariant()));
        }

        if (f.GruposMaterial is { Count: > 0 })
        {
            q = q.Where(p =>
            {
                string code = p.CodProducto.Trim().ToUpperInvariant();
                string grp = prodToGroup.TryGetValue(code, out string? g) ? g : p.GrupoMaterial;
                return f.GruposMaterial.Contains(grp.Trim().ToUpperInvariant());
            });
        }

        if (f.PatentesNorm is { Count: > 0 })
        {
            q = q.Where(p => f.PatentesNorm.Contains(PatenteChilena.Normalize(p.Patente)));
        }

        if (f.TiposPesaje is { Count: > 0 })
        {
            q = q.Where(p => f.TiposPesaje.Contains(p.TipoPesaje));
        }

        if (f.Anios is { Count: > 0 })
        {
            q = q.Where(p => f.Anios.Contains(p.FechaIngreso.Year));
        }

        if (f.Meses is { Count: > 0 })
        {
            q = q.Where(p => f.Meses.Contains(p.FechaIngreso.Month));
        }

        return [.. q.OrderByDescending(p => p.FechaIngreso).ThenByDescending(p => p.Folio)];
    }

    private static EnterpriseSlicerFacetsDto BuildSlicerFacets(
        IReadOnlyList<Pesaje> allPesajes,
        Dictionary<string, Cliente> entityByRut,
        Dictionary<string, string> prodToGroup)
    {
        List<SlicerOptionDto> proveedores = [.. allPesajes
            .Where(p => IsProveedorWeighing(p, entityByRut))
            .GroupBy(NormalizeEntityKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                Pesaje f = g.First();
                entityByRut.TryGetValue(g.Key, out Cliente? ent);
                string label = ent is not null ? $"{ent.RazonSocial} ({ent.RutFormateado})" : $"{f.NomCliente} ({f.RutCliente})";
                return new SlicerOptionDto(g.Key, label.Trim(), g.Count());
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)];

        List<SlicerOptionDto> clientes = [.. allPesajes
            .Where(p => IsClienteWeighing(p, entityByRut))
            .GroupBy(NormalizeEntityKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                Pesaje f = g.First();
                entityByRut.TryGetValue(g.Key, out Cliente? ent);
                string label = ent is not null ? $"{ent.RazonSocial} ({ent.RutFormateado})" : $"{f.NomCliente} ({f.RutCliente})";
                return new SlicerOptionDto(g.Key, label.Trim(), g.Count());
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)];

        List<SlicerOptionDto> materiales = [.. allPesajes
            .GroupBy(p => p.CodProducto.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SlicerOptionDto(g.Key, $"{g.Key} · {g.First().NomProducto}", g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)];

        List<SlicerOptionDto> grupos = [.. allPesajes
            .GroupBy(p =>
            {
                string c = p.CodProducto.Trim().ToUpperInvariant();
                return (prodToGroup.TryGetValue(c, out string? g) ? g : p.GrupoMaterial).Trim().ToUpperInvariant();
            }, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SlicerOptionDto(g.Key, g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)];

        List<SlicerOptionDto> patentes = [.. allPesajes
            .Select(p => PatenteChilena.Normalize(p.Patente))
            .Where(PatenteChilena.IsValidFormat)
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SlicerOptionDto(g.Key, PatenteChilena.FormatDisplay(g.Key), g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)];

        List<SlicerOptionDto> tipos = [.. Enum.GetValues<TipoPesaje>()
            .Select(t => new SlicerOptionDto(((int)t).ToString(CultureInfo.InvariantCulture), t.ToDisplayName(), allPesajes.Count(p => p.TipoPesaje == t)))
            .OrderByDescending(x => x.Count)];

        List<SlicerOptionDto> anios = [.. allPesajes
            .GroupBy(p => p.FechaIngreso.Year)
            .Select(g => new SlicerOptionDto(g.Key.ToString(CultureInfo.InvariantCulture), g.Key.ToString(CultureInfo.InvariantCulture), g.Count()))
            .OrderByDescending(x => x.Key, StringComparer.Ordinal)];

        List<SlicerOptionDto> meses = [.. Enumerable.Range(1, 12)
            .Select(m => new SlicerOptionDto(
                m.ToString(CultureInfo.InvariantCulture),
                new DateOnly(2026, m, 1).ToString("MMMM", EsCl).ToUpperInvariant(),
                allPesajes.Count(p => p.FechaIngreso.Month == m)))];

        List<SlicerOptionDto> estados = [.. Enum.GetValues<EstadoPesaje>()
            .Select(e => new SlicerOptionDto(((int)e).ToString(CultureInfo.InvariantCulture), e.ToDisplayName(), allPesajes.Count(p => p.Estado == e)))];

        return new EnterpriseSlicerFacetsDto(proveedores, clientes, materiales, grupos, patentes, tipos, anios, meses, estados);
    }

    private static List<InformeEntidadRowDto> BuildEntityReportRows(
        IReadOnlyList<Pesaje> source,
        Dictionary<string, Cliente> entityByRut,
        long denominatorNeto,
        bool excluirCero)
    {
        return [.. source
            .GroupBy(NormalizeEntityKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                Pesaje first = g.First();
                entityByRut.TryGetValue(g.Key, out Cliente? ent);
                long neto = g.Sum(x => (long)x.PesoNeto);
                string rol = ent is not null
                    ? ent.RolDescripcion
                    : (first.TipoPesaje.EsSalidaInventario() ? "Cliente" : "Proveedor");
                string matTop = g.GroupBy(x => x.NomProducto).OrderByDescending(mg => mg.Sum(z => (long)z.PesoNeto)).First().Key;
                return new InformeEntidadRowDto(
                    ent?.RutFormateado ?? first.RutCliente,
                    ent?.RazonSocial ?? first.NomCliente,
                    rol,
                    ent?.Ciudad ?? first.CiudadCliente ?? first.Origen,
                    g.Count(),
                    g.Sum(x => (long)x.PesoBruto),
                    g.Sum(x => (long)x.PesoTara),
                    neto,
                    denominatorNeto > 0 ? Math.Round(neto * 100.0 / denominatorNeto, 2) : 0.0,
                    g.Max(x => x.FechaIngreso),
                    matTop);
            })
            .Where(r => !excluirCero || r.TotalNetoKg > 0)
            .OrderByDescending(r => r.TotalNetoKg)];
    }

    private static string NormalizeEntityKey(Pesaje p)
    {
        string body = RutChileno.NormalizeBody(p.RutCliente);
        return string.IsNullOrEmpty(body) ? p.NomCliente.Trim().ToUpperInvariant() : body;
    }

    private static string BuildActiveFiltersSummary(EnterpriseReportFilterDto f)
    {
        List<string> parts = [];
        if (f.Planta.HasValue) parts.Add($"Planta: {f.Planta.Value.ToShortTitle()}");
        if (!string.IsNullOrWhiteSpace(f.FolioSearch)) parts.Add($"Folio: {f.FolioSearch.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.PatenteSearch)) parts.Add($"Patente: {f.PatenteSearch.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.RutSearch)) parts.Add($"RUT: {f.RutSearch.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.OperadorSearch)) parts.Add($"Operador: {f.OperadorSearch.Trim()}");
        if (f.ProveedoresRuts is { Count: > 0 }) parts.Add($"Proveedores ({f.ProveedoresRuts.Count})");
        if (f.ClientesRuts is { Count: > 0 }) parts.Add($"Clientes ({f.ClientesRuts.Count})");
        if (f.MaterialesCodigos is { Count: > 0 }) parts.Add($"Materiales ({f.MaterialesCodigos.Count})");
        if (f.GruposMaterial is { Count: > 0 }) parts.Add($"Grupos ({f.GruposMaterial.Count})");
        if (f.PatentesNorm is { Count: > 0 }) parts.Add($"Patentes ({f.PatentesNorm.Count})");
        if (f.TiposPesaje is { Count: > 0 }) parts.Add($"Tipos Pesaje ({f.TiposPesaje.Count})");
        if (f.Anios is { Count: > 0 }) parts.Add($"Años ({f.Anios.Count})");
        if (f.Meses is { Count: > 0 }) parts.Add($"Meses ({f.Meses.Count})");
        if (f.Estados is { Count: > 0 }) parts.Add($"Estados ({f.Estados.Count})");
        else if (f.ExcluirAnulados) parts.Add("Sin Anulados");

        return parts.Count == 0 ? "Todos los registros del período" : string.Join(" · ", parts);
    }
}
