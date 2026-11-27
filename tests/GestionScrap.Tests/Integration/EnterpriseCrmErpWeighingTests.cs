using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GestionScrap.Tests.Integration;

/// <summary>
/// Suite integral CRM + ERP + Pesajes Industriales (1 y 2 mediciones) + Flota + Inventario Ledger +
/// Informes Multi-Selección y Validaciones de Regresión v5.0.0.
/// </summary>
public sealed class EnterpriseCrmErpWeighingTests : IDisposable
{
    private readonly string _tempDir;

    public EnterpriseCrmErpWeighingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_v5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Limpieza temporal best-effort
            }
        }
    }

    private async Task<(SqliteScrapRepository Repo, SecuritySessionService Auth, ScrapApplicationService Scrap, EnterpriseApplicationService Enterprise)>
        CreateLoggedInEnvAsync(string dbName = "enterprise.db")
    {
        string dbPath = Path.Combine(_tempDir, dbName);
        SqliteScrapRepository repo = new(dbPath);
        await repo.InitializeAsync();
        SecuritySessionService auth = new(repo);
        var login = await auth.LoginAsync("barriagada", "Cambias0");
        Assert.True(login.Success, login.Message);

        ClosedXmlExcelExportService excel = new();
        ScrapApplicationService scrap = new(repo, auth, new FirebirdSyncService(), excel, new MdbMigrationService(repo));
        EnterpriseApplicationService ent = new(repo, repo, auth, excel);
        return (repo, auth, scrap, ent);
    }

    [Fact]
    public void WeighingRules_EnforceStrictNetCalculationUnitsAndManualOverrideProtection()
    {
        // Cálculo exacto Neto = Bruto - Tara
        WeighingCalculationResult ok = WeighingRules.ValidateAndCalculateSingleStep(28500, 11200, unidadMedida: "kg", unidadMaterial: "KG");
        Assert.Equal(17300, ok.PesoNeto);
        Assert.Equal("KG", ok.UnidadMedida);
        Assert.False(ok.UsoAjusteManualAutorizado);

        // Pesos negativos rechazados
        Assert.Throws<ArgumentException>(() => WeighingRules.ValidateAndCalculateSingleStep(-100, 0));
        Assert.Throws<ArgumentException>(() => WeighingRules.ValidateAndCalculateSingleStep(1000, -50));

        // Bruto < Tara rechazado
        Assert.Throws<ArgumentException>(() => WeighingRules.ValidateAndCalculateSingleStep(5000, 6000));

        // Unidades inconsistentes rechazadas
        Assert.Throws<InvalidOperationException>(() => WeighingRules.ValidateAndCalculateSingleStep(10000, 2000, unidadMedida: "TON", unidadMaterial: "KG"));
        Assert.Throws<ArgumentException>(() => WeighingRules.NormalizeUnit("LIBRAS"));

        // Bloqueo de sobrescritura manual de peso neto sin autorización + motivo
        Assert.Throws<InvalidOperationException>(() =>
            WeighingRules.ValidateAndCalculateSingleStep(20000, 10000, pesoNetoManual: 9500, ajusteManualAutorizado: false));
        Assert.Throws<InvalidOperationException>(() =>
            WeighingRules.ValidateAndCalculateSingleStep(20000, 10000, pesoNetoManual: 9500, ajusteManualAutorizado: true, motivoAjuste: ""));

        // Ajuste manual autorizado con motivo sí permitido
        WeighingCalculationResult adj = WeighingRules.ValidateAndCalculateSingleStep(
            20000, 10000, pesoNetoManual: 9500, ajusteManualAutorizado: true, motivoAjuste: "Descuento humedad certificada 500 Kg");
        Assert.Equal(9500, adj.PesoNeto);
        Assert.True(adj.UsoAjusteManualAutorizado);

        // 2 mediciones: orden independiente (entrada cargado o vacío)
        WeighingCalculationResult twoStep = WeighingRules.ResolveTwoStepMeasurements(12000, 29400);
        Assert.Equal(29400, twoStep.PesoBruto);
        Assert.Equal(12000, twoStep.PesoTara);
        Assert.Equal(17400, twoStep.PesoNeto);
    }

    [Fact]
    public async Task CrmUnifiedEntity_PreventsDuplicateRuts_ValidatesMod11_AndProtectsHistory()
    {
        var (repo, _, _, enterprise) = await CreateLoggedInEnvAsync("crm.db");
        using (repo)
        {
            // RUT inválido (dígito verificador incorrecto) rechazado
            await Assert.ThrowsAsync<ArgumentException>(() => enterprise.SaveEntidadComercialAsync(new Cliente
            {
                Rut = "76218764-9",
                RazonSocial = "Empresa RUT Malo",
                EsProveedor = true,
                EsCliente = false
            }));

            // Crear como Proveedor (DV real para 76218764 es '5'? Calculemos dinámicamente con RutChileno.ComputeDv)
            string dv = RutChileno.ComputeDv("76218764");
            Cliente created = await enterprise.SaveEntidadComercialAsync(new Cliente
            {
                Rut = $"76.218.764-{dv}",
                RazonSocial = "Reciclajes del Pacífico SpA",
                Giro = "Reciclaje Industrial",
                Ciudad = "Valparaíso",
                EsCliente = false,
                EsProveedor = true
            });
            Assert.Equal(RolEntidadComercial.Proveedor, created.Rol);

            // Actualizar mismo RUT con otro formato para hacerlo Ambos -> NO debe duplicarse
            Cliente updated = await enterprise.SaveEntidadComercialAsync(new Cliente
            {
                Rut = $"76218764-{dv}",
                RazonSocial = "Reciclajes del Pacífico SpA",
                Ciudad = "Valparaíso",
                EsCliente = true,
                EsProveedor = true
            });
            Assert.Equal(RolEntidadComercial.Ambos, updated.Rol);

            IReadOnlyList<Cliente> matches = await enterprise.GetEntidadesComercialesAsync(search: "76218764");
            Assert.Single(matches);
            Assert.Equal(RolEntidadComercial.Ambos, matches[0].Rol);

            // Asociar documento comercial y consultar Ficha 360°
            await enterprise.SaveDocumentoAsync(new DocumentoAsociado
            {
                RutEntidad = updated.RutFormateado,
                TipoDocumento = "Contrato Comercial",
                NumeroDocumento = "CTR-2026-88"
            });

            EntidadComercialFichaDto ficha = await enterprise.GetEntidadFicha360Async(updated.RutFormateado);
            Assert.Single(ficha.Documentos);

            // Crear entidad temporal sin movimientos y eliminarla limpiamente
            string dvTemp = RutChileno.ComputeDv("77112233");
            Cliente temp = await enterprise.SaveEntidadComercialAsync(new Cliente
            {
                Rut = $"77112233-{dvTemp}",
                RazonSocial = "Temporal Sin Historia",
                EsCliente = true,
                EsProveedor = false
            });
            await enterprise.DeleteEntidadSafelyAsync(temp.RutFormateado);
            Assert.Empty(await enterprise.GetEntidadesComercialesAsync(search: "77112233"));
        }
    }

    [Fact]
    public async Task FleetVehicles_NormalizePlates_BlockDuplicates_AndRejectNonexistentVehicleInWeighing()
    {
        var (repo, _, _, enterprise) = await CreateLoggedInEnvAsync("fleet.db");
        using (repo)
        {
            Vehiculo v1 = await enterprise.SaveVehiculoAsync(new Vehiculo
            {
                PatenteOriginal = "ab-cd-12",
                TipoVehiculo = "Camión Tolva",
                CapacidadCargaKg = 25000
            });
            Assert.Equal("ABCD12", v1.PatenteNormalizada);
            Assert.Equal("AB-CD-12", v1.PatenteOriginal);

            // Intento de duplicado con distinta capitalización / sin guiones -> BLOQUEADO
            await Assert.ThrowsAsync<InvalidOperationException>(() => enterprise.SaveVehiculoAsync(new Vehiculo
            {
                PatenteOriginal = "ABCD12",
                TipoVehiculo = "Camión"
            }));

            // Búsqueda parcial encuentra el vehículo
            IReadOnlyList<Vehiculo> found = await enterprise.GetVehiculosAsync(search: "cd12");
            Assert.Single(found);

            // Registrar pesaje con patente inexistente -> RECHAZADO
            await Assert.ThrowsAsync<InvalidOperationException>(() => enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Patente = "ZZ-99-88",
                CodProducto = "5300000009",
                PesoBruto = 25000,
                PesoTara = 10000
            }));
        }
    }

    [Fact]
    public async Task TwoStepWeighing_PreservesFirstTimestamp_PostsInventoryExactlyOnce_AndBlocksDuplicateCompletion()
    {
        var (repo, _, _, enterprise) = await CreateLoggedInEnvAsync("twostep.db");
        using (repo)
        {
            await enterprise.SaveVehiculoAsync(new Vehiculo { PatenteOriginal = "JK-LP-44", TipoVehiculo = "Tracto Camión" });

            // Paso 1: Iniciar 1ª medición -> Pendiente, Neto=0, 0 movimientos de inventario
            Pesaje step1 = await enterprise.IniciarPesajeDosMedicionesAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Placilla,
                TipoPesaje = TipoPesaje.EntradaSalida,
                Patente = "JKLP44",
                CodProducto = "5300000009",
                PrimeraMedicionKg = 28500,
                FolioCodigo = "PL-2026-0001"
            });

            Assert.Equal(EstadoPesaje.PendienteSegundaMedicion, step1.Estado);
            Assert.Equal(0, step1.PesoNeto);
            Assert.Equal(28500, step1.PrimeraMedicionKg);
            Assert.NotNull(step1.PrimeraMedicionUtc);
            Assert.Equal("PL-2026-0001", step1.FolioDisplay);
            Assert.Empty(await enterprise.GetMovimientosInventarioAsync("5300000009"));

            DateTime originalFirstTs = step1.PrimeraMedicionUtc!.Value;

            // Paso 2: Completar 2ª medición -> calcula Bruto/Tara/Neto y genera EXACTAMENTE 1 movimiento de inventario
            Pesaje step2 = await enterprise.CompletarSegundaMedicionAsync(
                step1.Id,
                segundaMedicionKg: 12000,
                fechaSalida: DateOnly.FromDateTime(DateTime.Today),
                horaSalida: new TimeOnly(15, 30));

            Assert.Equal(EstadoPesaje.Confirmado, step2.Estado);
            Assert.Equal(28500, step2.PesoBruto);
            Assert.Equal(12000, step2.PesoTara);
            Assert.Equal(16500, step2.PesoNeto);
            Assert.Equal(28500, step2.PrimeraMedicionKg);
            Assert.Equal(originalFirstTs, step2.PrimeraMedicionUtc);
            Assert.Equal(12000, step2.SegundaMedicionKg);

            IReadOnlyList<InventarioMovimiento> movs = await enterprise.GetMovimientosInventarioAsync("5300000009");
            Assert.Single(movs);
            Assert.Equal(16500, movs[0].CantidadKg);

            // Intento de volver a completar 2ª medición (duplicación) -> BLOQUEADO
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                enterprise.CompletarSegundaMedicionAsync(step1.Id, 11000, DateOnly.FromDateTime(DateTime.Today), new TimeOnly(16, 0)));

            // Idempotencia a nivel repositorio: re-postear el mismo pesaje confirmado no crea movimiento duplicado
            await repo.PostInventarioParaPesajeConfirmadoAsync(step2, TipoMovimientoInventario.Entrada, 16500, "barriagada");
            Assert.Single(await enterprise.GetMovimientosInventarioAsync("5300000009"));
        }
    }

    [Fact]
    public async Task DiscrepancyAuthorization_AuthorizedCorrection_AnnulmentReversal_AndInventoryAdjustment_AreConsistent()
    {
        var (repo, auth, scrap, enterprise) = await CreateLoggedInEnvAsync("erp_inv.db");
        using (repo)
        {
            string dv = RutChileno.ComputeDv("76555444");
            Cliente prov = await enterprise.SaveEntidadComercialAsync(new Cliente
            {
                Rut = $"76555444-{dv}",
                RazonSocial = "Proveedor Industrial Norte",
                EsProveedor = true,
                EsCliente = false
            });
            await enterprise.SaveVehiculoAsync(new Vehiculo { PatenteOriginal = "RT-YU-55", RutEntidadAsociada = prov.RutFormateado, NombreEntidadAsociada = prov.RazonSocial });
            await enterprise.SaveMaterialAsync(new Producto
            {
                Codigo = "MAT-TEST-01",
                Nombre = "SCRAP TEST TOLERANCIA",
                Grupo = "SCRAP PESQUERA",
                UnidadMedida = "KG",
                ToleranciaDiferenciaPct = 5.0
            });

            // Crear Orden de Compra ERP esperando 10.000 Kg
            OperacionComercial op = await enterprise.SaveOperacionAsync(new OperacionComercial
            {
                Codigo = "OC-2026-900",
                TipoOperacion = TipoOperacionComercial.OrdenCompra,
                RutProveedor = prov.RutFormateado,
                NombreProveedor = prov.RazonSocial,
                CodProducto = "MAT-TEST-01",
                CantidadEsperadaKg = 10000,
                Patente = "RT-YU-55",
                DocumentoAsociado = "GD-7788"
            });

            // Registrar pesaje con 12.000 Kg (+20% > 5% tolerancia) -> PendienteAutorizacion, 0 inventario
            Pesaje pending = await enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Placilla,
                TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = "RT-YU-55",
                CodProducto = "MAT-TEST-01",
                RutEntidad = prov.RutFormateado,
                OperacionId = op.Id,
                PesoBruto = 22000,
                PesoTara = 10000,
                GuiaDespacho = "GD-7788"
            });

            Assert.Equal(EstadoPesaje.PendienteAutorizacion, pending.Estado);
            Assert.Empty(await enterprise.GetExistenciasInventarioAsync());

            // Operador sin permiso AutorizarAnularPesajesInventario no puede autorizar
            await scrap.SaveUsuarioAsync(new Usuario
            {
                NombreUsuario = "op_bascula",
                NombreCompleto = "Operador Báscula",
                PerfilId = 4, // Operador
                Activo = true
            }, "Operador2026");
            auth.Logout();
            Assert.True((await auth.LoginAsync("op_bascula", "Operador2026")).Success);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => enterprise.AutorizarPesajePendienteAsync(pending.Id, "Intento sin permiso"));

            // Reingresar como Administrador y autorizar -> stock sube a +12.000 Kg y operación ERP se completa
            auth.Logout();
            Assert.True((await auth.LoginAsync("barriagada", "Cambias0")).Success);
            Pesaje approved = await enterprise.AutorizarPesajePendienteAsync(pending.Id, "Verificado en romana con supervisor");
            Assert.Equal(EstadoPesaje.Confirmado, approved.Estado);

            IReadOnlyList<InventarioExistencia> stock1 = await enterprise.GetExistenciasInventarioAsync();
            Assert.Single(stock1);
            Assert.Equal(12000, stock1[0].StockActualKg);

            OperacionTrazabilidadDto trace1 = await enterprise.GetOperacionTrazabilidadAsync(op.Id);
            Assert.Equal(12000, trace1.TotalPesadoConfirmadoKg);
            Assert.Equal(EstadoOperacion.Completada, trace1.Operacion.Estado);

            // Corrección autorizada: 12.000 -> 11.500 Kg (genera historial + reverso -12.000 + nuevo +11.500)
            Pesaje corrected = await enterprise.ModificarPesajeAutorizadoAsync(new PesajeRegistroRequestDto
            {
                Id = approved.Id,
                Planta = approved.Planta,
                TipoPesaje = approved.TipoPesaje,
                Patente = approved.Patente,
                CodProducto = approved.CodProducto,
                RutEntidad = approved.RutCliente,
                OperacionId = op.Id,
                PesoBruto = 21500,
                PesoTara = 10000
            }, "Ajuste tara verificada por báscula");

            Assert.Equal(11500, corrected.PesoNeto);
            IReadOnlyList<InventarioExistencia> stock2 = await enterprise.GetExistenciasInventarioAsync();
            Assert.Equal(11500, stock2[0].StockActualKg);
            Assert.Equal(stock2[0].EntradasKg - stock2[0].SalidasKg + stock2[0].AjustesKg, stock2[0].StockActualKg);

            // Anular pesaje -> NO elimina físicamente, guarda historial y revierte inventario a 0
            Pesaje annulled = await enterprise.AnularPesajeAsync(corrected.Id, "Error de asignación de guía");
            Assert.Equal(EstadoPesaje.Anulado, annulled.Estado);
            Assert.NotNull(await repo.GetPesajeByIdAsync(corrected.Id));

            IReadOnlyList<PesajeHistorialCambio> hist = await enterprise.GetPesajeHistorialAsync(corrected.Id);
            Assert.True(hist.Count >= 4); // Crear + Autorizar + Corregir + Anular

            IReadOnlyList<InventarioExistencia> stock3 = await enterprise.GetExistenciasInventarioAsync();
            Assert.Equal(0, stock3[0].StockActualKg);

            // Ajuste autorizado de inventario (+500 y -200 => stock 300)
            await enterprise.RegistrarAjusteInventarioAsync("MAT-TEST-01", 500, "ACTA-01", "Inventario físico inicial");
            await enterprise.RegistrarAjusteInventarioAsync("MAT-TEST-01", -200, "ACTA-02", "Merma verificada");

            IReadOnlyList<InventarioExistencia> stock4 = await enterprise.GetExistenciasInventarioAsync();
            Assert.Equal(300, stock4[0].StockActualKg);
            Assert.Equal(300, stock4[0].AjustesKg);

            // Entidad, material y vehículo con historial están protegidos contra borrado físico
            await Assert.ThrowsAsync<InvalidOperationException>(() => enterprise.DeleteEntidadSafelyAsync(prov.RutFormateado));
            await Assert.ThrowsAsync<InvalidOperationException>(() => enterprise.DeleteMaterialSafelyAsync("MAT-TEST-01"));
            Vehiculo veh = (await repo.GetVehiculoByPatenteAsync("RT-YU-55"))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => enterprise.DeleteVehiculoSafelyAsync(veh.Id));
        }
    }

    [Fact]
    public async Task EnterpriseReports_MultiSelectSlicers_Are100PercentConsistentAcrossAll6ReportsAndExcelExport()
    {
        var (repo, _, _, enterprise) = await CreateLoggedInEnvAsync("reports.db");
        using (repo)
        {
            string dvProv = RutChileno.ComputeDv("76111222");
            string dvCli = RutChileno.ComputeDv("76333444");
            string dvAmbos = RutChileno.ComputeDv("76555666");

            Cliente soloProv = await enterprise.SaveEntidadComercialAsync(new Cliente { Rut = $"76111222-{dvProv}", RazonSocial = "Proveedor Alfa", EsProveedor = true, EsCliente = false });
            Cliente soloCli = await enterprise.SaveEntidadComercialAsync(new Cliente { Rut = $"76333444-{dvCli}", RazonSocial = "Cliente Beta", EsProveedor = false, EsCliente = true });
            Cliente ambos = await enterprise.SaveEntidadComercialAsync(new Cliente { Rut = $"76555666-{dvAmbos}", RazonSocial = "Comercial Gamma", EsProveedor = true, EsCliente = true });

            await enterprise.SaveVehiculoAsync(new Vehiculo { PatenteOriginal = "AA-BB-11" });
            await enterprise.SaveVehiculoAsync(new Vehiculo { PatenteOriginal = "CC-DD-22" });

            DateOnly hoy = DateOnly.FromDateTime(DateTime.Today);

            await enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "AA-BB-11",
                CodProducto = "5300000009", RutEntidad = soloProv.RutFormateado, PesoBruto = 20000, PesoTara = 8000, FechaIngreso = hoy, FechaSalida = hoy
            });
            await enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Santiago, TipoPesaje = TipoPesaje.DespachoCarga, Patente = "CC-DD-22",
                CodProducto = "5301000007", RutEntidad = soloCli.RutFormateado, PesoBruto = 18000, PesoTara = 8000, FechaIngreso = hoy, FechaSalida = hoy
            });
            await enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "AA-BB-11",
                CodProducto = "5301000007", RutEntidad = ambos.RutFormateado, PesoBruto = 15000, PesoTara = 5000, FechaIngreso = hoy, FechaSalida = hoy
            });
            Pesaje anulado = await enterprise.RegistrarPesajeCompletoAsync(new PesajeRegistroRequestDto
            {
                Planta = Planta.Placilla, TipoPesaje = TipoPesaje.RecepcionCarga, Patente = "AA-BB-11",
                CodProducto = "5300000009", RutEntidad = soloProv.RutFormateado, PesoBruto = 9000, PesoTara = 4000, FechaIngreso = hoy, FechaSalida = hoy
            });
            await enterprise.AnularPesajeAsync(anulado.Id, "Anulado de prueba");

            // 1. Consulta sin anulados: 12000 + 10000 + 10000 = 32000 Kg
            EnterpriseReportResultDto repAll = await enterprise.GetEnterpriseReportAsync(new EnterpriseReportFilterDto
            {
                Desde = new DateOnly(hoy.Year, hoy.Month, 1),
                Hasta = hoy,
                ExcluirAnulados = true
            });

            Assert.Equal(3, repAll.CantidadPesajes);
            Assert.Equal(32000, repAll.TotalNetoKg);
            Assert.Equal(repAll.TotalNetoKg, repAll.PorPatente.Sum(r => r.TotalNetoKg));
            Assert.Equal(repAll.TotalNetoKg, repAll.PorMaterial.Sum(r => r.TotalNetoKg));
            Assert.Equal(repAll.TotalNetoKg, repAll.PorRut.Sum(r => r.TotalNetoKg));
            Assert.Equal(repAll.TotalNetoKg, repAll.PorTipoPesaje.Sum(r => r.TotalNetoKg));
            Assert.Equal(repAll.TotalNetoKg, repAll.DetallePesajes.Sum(p => (long)p.PesoNeto));

            // Verificación de roles en PorProveedor / PorCliente / PorRut
            Assert.Equal(2, repAll.PorProveedor.Count); // soloProv + ambos
            Assert.Equal(2, repAll.PorCliente.Count);   // soloCli + ambos
            Assert.Contains(repAll.PorRut, r => r.Nombre == "Comercial Gamma" && r.RolDescripcion == "Cliente y Proveedor");

            // 2. Segmentación combinada: (Material 5300000009 OR 5301000007) AND Patente AABB11 => 12000 + 10000 = 22000 Kg
            EnterpriseReportResultDto repSliced = await enterprise.GetEnterpriseReportAsync(new EnterpriseReportFilterDto
            {
                Desde = new DateOnly(hoy.Year, hoy.Month, 1),
                Hasta = hoy,
                ExcluirAnulados = true,
                MaterialesCodigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "5300000009", "5301000007" },
                PatentesNorm = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AABB11" }
            });

            Assert.Equal(2, repSliced.CantidadPesajes);
            Assert.Equal(22000, repSliced.TotalNetoKg);
            Assert.Equal(22000, repSliced.PorPatente.Sum(r => r.TotalNetoKg));
            Assert.Equal(22000, repSliced.PorMaterial.Sum(r => r.TotalNetoKg));

            // 3. Exportación Excel multi-hoja consistente
            string xlsx = Path.Combine(_tempDir, "enterprise_report.xlsx");
            string saved = enterprise.ExportEnterpriseReportExcel(xlsx, repSliced);
            Assert.True(File.Exists(saved));
            Assert.True(new FileInfo(saved).Length > 4000);
        }
    }

    [Fact]
    public async Task LiveDatabaseCopy_MigratesCleanlyToSchemaV3_WithFleetTiposAndAdminPermissions()
    {
        string localLiveDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestionScrap",
            "gestion_scrap.db");

        if (!File.Exists(localLiveDb))
        {
            return;
        }

        string copyDb = Path.Combine(_tempDir, "live_v3_copy.db");
        File.Copy(localLiveDb, copyDb, overwrite: true);

        using SqliteScrapRepository repo = new(copyDb);
        await repo.InitializeAsync();

        var integrity = await repo.VerifyIntegrityAsync();
        Assert.True(integrity.IsHealthy, integrity.Summary);
        Assert.Equal(3, integrity.AppliedSchemaVersion);
        Assert.Equal(0, integrity.ForeignKeyViolations);

        IReadOnlyList<TipoPesajeConfig> tipos = await repo.GetTiposPesajeConfigAsync();
        Assert.True(tipos.Count >= 9);

        IReadOnlyList<Vehiculo> vehs = await repo.GetVehiculosAsync();
        Assert.True(vehs.Count > 0, "La migración v3 debe poblar vehículos desde las patentes históricas válidas.");

        SecuritySessionService auth = new(repo);
        Assert.True((await auth.LoginAsync("barriagada", "Cambias0")).Success);
        foreach (PermisoSistema p in Enum.GetValues<PermisoSistema>())
        {
            Assert.True(auth.HasPermission(p), $"El perfil Administrador migrado debe incluir el permiso {p}.");
        }
    }
}
