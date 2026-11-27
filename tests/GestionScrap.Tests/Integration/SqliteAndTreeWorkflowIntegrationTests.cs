using ClosedXML.Excel;
using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.Integration;

public sealed class SqliteAndTreeWorkflowIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteScrapRepository _repo;
    private readonly SecuritySessionService _auth;
    private readonly ScrapApplicationService _app;

    public SqliteAndTreeWorkflowIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_CoreInt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        string dbPath = Path.Combine(_tempDir, "core_test.db");

        _repo = new SqliteScrapRepository(dbPath);
        _repo.InitializeAsync().GetAwaiter().GetResult();
        _auth = new SecuritySessionService(_repo);
        _app = new ScrapApplicationService(
            _repo,
            _auth,
            new FirebirdSyncService(),
            new ClosedXmlExcelExportService(),
            new MdbMigrationService(_repo));
    }

    public void Dispose()
    {
        _repo.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Limpieza best-effort
            }
        }
    }

    [Fact]
    public async Task AdminAccountBarriagada_AuthenticatesAndManagesDynamicProfilesAndLockout()
    {
        var login = await _auth.LoginAsync("barriagada", "Cambias0");
        Assert.True(login.Success);
        Assert.Equal("Administrador", login.User?.NombrePerfil);

        // Crear perfil dinámico personalizado solo para Análisis de Proveedores y Resumen
        PerfilUsuario customProfile = await _app.SavePerfilAsync(new PerfilUsuario
        {
            Nombre = "Analista Proveedores",
            Descripcion = "Solo resumen y proveedores",
            Permisos = [PermisoSistema.VerInformeResumen, PermisoSistema.VerAnalisisProveedores]
        });
        Assert.True(customProfile.Id > 0);

        // Crear usuario asignado a ese perfil
        await _app.SaveUsuarioAsync(new Usuario
        {
            NombreUsuario = "jdoe",
            NombreCompleto = "Juan Pérez",
            PerfilId = customProfile.Id,
            NombrePerfil = customProfile.Nombre,
            Activo = true
        }, "Temporal2026");

        _auth.Logout();

        // Verificar bloqueo tras 5 intentos fallidos sobre jdoe
        for (int i = 0; i < 5; i++)
        {
            var fail = await _auth.LoginAsync("jdoe", "MalaClave9");
            Assert.False(fail.Success);
        }

        var locked = await _auth.LoginAsync("jdoe", "Temporal2026");
        Assert.False(locked.Success);
        Assert.Contains("bloqueada", locked.Message, StringComparison.OrdinalIgnoreCase);

        // Desbloquear con barriagada y comprobar permisos del perfil dinámico
        Assert.True((await _auth.LoginAsync("barriagada", "Cambias0")).Success);
        Usuario jdoe = (await _app.GetUsuariosAsync()).First(u => u.NombreUsuario == "jdoe");
        await _app.SaveUsuarioAsync(new Usuario
        {
            Id = jdoe.Id,
            NombreUsuario = jdoe.NombreUsuario,
            NombreCompleto = jdoe.NombreCompleto,
            PerfilId = jdoe.PerfilId,
            NombrePerfil = jdoe.NombrePerfil,
            PasswordHash = jdoe.PasswordHash,
            PasswordSalt = jdoe.PasswordSalt,
            Activo = true,
            IntentosFallidos = 0,
            BloqueadoHastaUtc = null
        }, null);
        _auth.Logout();

        var jLogin = await _auth.LoginAsync("jdoe", "Temporal2026");
        Assert.True(jLogin.Success);
        Assert.True(_auth.HasPermission(PermisoSistema.VerAnalisisProveedores));
        Assert.False(_auth.HasPermission(PermisoSistema.AdministrarGrupoMaterial));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _app.SaveMaterialNodeAsync(new MaterialGroupNode { Nombre = "Prohibido" }));
    }

    [Fact]
    public async Task MaterialGroupTree_FullCrudMoveReorderAndDelete_WorksEndToEnd()
    {
        Assert.True((await _auth.LoginAsync("barriagada", "Cambias0")).Success);

        MaterialGroupNode grupoA = await _app.SaveMaterialNodeAsync(new MaterialGroupNode
        {
            Nombre = "GRUPO TEST ALPHA",
            TipoNodo = NodoGrupoTipo.CarpetaGrupo,
            ColorHex = "#0891B2"
        });
        MaterialGroupNode grupoB = await _app.SaveMaterialNodeAsync(new MaterialGroupNode
        {
            Nombre = "GRUPO TEST BETA",
            TipoNodo = NodoGrupoTipo.CarpetaGrupo,
            ColorHex = "#C9A46E"
        });

        MaterialGroupNode subA = await _app.SaveMaterialNodeAsync(new MaterialGroupNode
        {
            ParentId = grupoA.Id,
            Nombre = "SUBGRUPO H1",
            TipoNodo = NodoGrupoTipo.CarpetaGrupo
        });

        MaterialGroupNode prodChild = await _app.SaveMaterialNodeAsync(new MaterialGroupNode
        {
            ParentId = subA.Id,
            Nombre = "5300000009 · TEST PRODUCTO",
            TipoNodo = NodoGrupoTipo.ProductoAsignado,
            CodigoProducto = "5300000009"
        });

        // Prohibido mover padre dentro de su propio hijo
        await Assert.ThrowsAsync<InvalidOperationException>(() => _app.MoveMaterialNodeAsync(grupoA.Id, subA.Id));

        // Mover subA completo hacia grupoB
        await _app.MoveMaterialNodeAsync(subA.Id, grupoB.Id);
        IReadOnlyList<MaterialGroupNode> nodesAfterMove = await _app.GetMaterialGroupTreeAsync();
        Assert.Equal(grupoB.Id, nodesAfterMove.First(n => n.Id == subA.Id).ParentId);
        Assert.Equal(subA.Id, nodesAfterMove.First(n => n.Id == prodChild.Id).ParentId);

        // Editar nombre nodo
        await _app.SaveMaterialNodeAsync(new MaterialGroupNode
        {
            Id = subA.Id,
            ParentId = grupoB.Id,
            Nombre = "SUBGRUPO MOVIDO",
            TipoNodo = NodoGrupoTipo.CarpetaGrupo,
            Orden = subA.Orden
        });

        // Reordenar
        await _app.ReorderMaterialNodeAsync(grupoB.Id, moveUp: true);

        // Quitar subA manteniendo su hijo (moviendo hijo a grupoB)
        await _app.DeleteMaterialNodeAsync(subA.Id, deleteSubtree: false);
        IReadOnlyList<MaterialGroupNode> afterPartialDel = await _app.GetMaterialGroupTreeAsync();
        Assert.DoesNotContain(afterPartialDel, n => n.Id == subA.Id);
        Assert.Equal(grupoB.Id, afterPartialDel.First(n => n.Id == prodChild.Id).ParentId);

        // Quitar grupoB en cascada
        await _app.DeleteMaterialNodeAsync(grupoB.Id, deleteSubtree: true);
        IReadOnlyList<MaterialGroupNode> afterCascade = await _app.GetMaterialGroupTreeAsync();
        Assert.DoesNotContain(afterCascade, n => n.Id == prodChild.Id);
    }

    [Fact]
    public async Task Reports_ExcelExports_Backup_And_Purge_WorkCorrectly()
    {
        Assert.True((await _auth.LoginAsync("barriagada", "Cambias0")).Success);

        Pesaje[] seed =
        [
            new()
            {
                Folio = 101, Planta = Planta.Santiago, TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = "ABCD12", PesoBruto = 20_000, PesoTara = 8_000, PesoNeto = 12_000,
                CodProducto = "5300000009", NomProducto = "SCRAP MIX SUPERMERCADO",
                RutCliente = "89201400", NomCliente = "ENV. IMPRESOS CORDILLERA SPA",
                Origen = "BOD. VICKY", Destino = "PLACILLA",
                FechaIngreso = new DateOnly(2026, 11, 20), HoraIngreso = new TimeOnly(10, 0),
                FechaSalida = new DateOnly(2026, 11, 20), HoraSalida = new TimeOnly(10, 30)
            },
            new()
            {
                Folio = 102, Planta = Planta.Santiago, TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = "XYZW98", PesoBruto = 15_000, PesoTara = 7_000, PesoNeto = 8_000,
                CodProducto = "5302000004", NomProducto = "SCRAP STRETCH TRANSPARENTE",
                RutCliente = "77774598", NomCliente = "PROCESADORA GIAIER SPA",
                Origen = "BOD. VICKY", Destino = "PLACILLA",
                FechaIngreso = new DateOnly(2026, 11, 25), HoraIngreso = new TimeOnly(14, 0),
                FechaSalida = new DateOnly(2026, 11, 25), HoraSalida = new TimeOnly(14, 25)
            }
        ];

        await _repo.ImportPesajesAsync(seed, Planta.Santiago, "barriagada");

        DateOnly desde = new(2026, 11, 1);
        DateOnly hasta = new(2026, 11, 30);

        InformeResumenDto resumen = await _app.GetInformeResumenAsync(null, null, desde, hasta);
        Assert.Equal(20_000, resumen.TotalKilos);
        Assert.Equal(2, resumen.TotalViajes);

        GrupoMaterialReportResultDto treeRep = await _app.GetGrupoMaterialReportAsync(Planta.Santiago, desde, hasta);
        Assert.Equal(20_000, treeRep.TotalKilos);

        AnalisisProveedoresResultDto provRep = await _app.GetAnalisisProveedoresAsync(Planta.Santiago, desde, hasta);
        Assert.Equal(2, provRep.TotalProveedoresActivos);
        ProveedorDetalleResultDto det = await _app.GetProveedorDetalleAsync(provRep.Ranking[0], Planta.Santiago, desde, hasta);
        Assert.Single(det.Entregas);

        // Validar generación real .xlsx con ClosedXML
        string xlsxRes = Path.Combine(_tempDir, "resumen.xlsx");
        _app.ExcelExporter.ExportInformeResumen(
            xlsxRes, resumen.PeriodoActualLabel,
            [.. resumen.PorPlanta.Select(x => (x.NombrePlanta, x.Viajes, x.Kilos))],
            [.. resumen.PorGrupo.Select(x => (x.Grupo, x.Viajes, x.Kilos, x.Porcentaje))],
            [.. resumen.TopProveedores.Select(x => (string.Empty, x.Label, 0, x.Value, 50.0))],
            resumen.UltimosFolios);
        using (XLWorkbook wb = new(xlsxRes))
        {
            Assert.Equal(3, wb.Worksheets.Count);
        }

        string xlsxGrp = Path.Combine(_tempDir, "grupos.xlsx");
        _app.ExcelExporter.ExportGrupoMaterialJerarquico(
            xlsxGrp, treeRep.PeriodoLabel,
            [.. treeRep.Filas.Select(f => (f.Nivel, f.Nombre, "Nodo", f.CodigoProducto, f.Viajes, f.TotalKilos, f.Porcentaje))],
            treeRep.TotalKilos);
        using (XLWorkbook wb = new(xlsxGrp))
        {
            Assert.Single(wb.Worksheets);
        }

        string xlsxProv = Path.Combine(_tempDir, "proveedores.xlsx");
        _app.ExcelExporter.ExportAnalisisProveedores(
            xlsxProv, provRep.PeriodoLabel,
            [.. provRep.Ranking.Select(r => (r.Rut, r.Nombre, r.Ciudad, r.Viajes, r.TotalKilos, r.PorcentajeParticipacion, r.PorcentajeAcumulado, r.ClaseAbc, r.PromedioKgViaje, r.ProductoPrincipal))],
            [.. det.MixMateriales.Select(m => (m.Grupo, m.Viajes, m.Kilos, m.Porcentaje))],
            det.Entregas, det.Resumen.Nombre);
        using (XLWorkbook wb = new(xlsxProv))
        {
            Assert.Equal(2, wb.Worksheets.Count);
        }

        // Respaldo y purga confirmada
        string bk = await _app.BackupDatabaseAsync(Path.Combine(_tempDir, "backup.db"));
        Assert.True(File.Exists(bk));

        // Verificar historial Origen -> Destino y Exportación Personalizada + Historial
        FirebirdPlantaEndpoint ep = new()
        {
            Planta = Planta.Santiago,
            ServidorIp = "192.168.2.56",
            Puerto = 3050,
            RutaArchivo = @"C:\bdwm\placilla2\WMDBSPC.GDB"
        };
        FirebirdExtractPackage pkg = new(
            [
                new FirebirdRemotePesaje
                {
                    Folio = 103, TipoPesajeRaw = "1", Patente = "ZZ1122",
                    PesoBruto = 16_000, PesoTara = 6_000, PesoNeto = 10_000,
                    CodProducto = "5300000009", NomProducto = "MIX SUPERMERCADO",
                    RutCliente = "89201400", NomCliente = "ENV. IMPRESOS CORDILLERA SPA",
                    FechaIngreso = new DateOnly(2026, 11, 26), HoraIngreso = new TimeOnly(9, 0),
                    FechaSalida = new DateOnly(2026, 11, 26), HoraSalida = new TimeOnly(9, 20)
                }
            ],
            [],
            [],
            false,
            "OK");

        var impRes = await _app.ImportFirebirdPackageAsync(Planta.Santiago, pkg, ep, "Todo el historial", true);
        Assert.Equal(1, impRes.ImportedPesajes);

        IReadOnlyList<ImportacionHistorial> history = await _app.GetImportHistoryAsync();
        Assert.Single(history);
        Assert.Contains("WMDBSPC.GDB", history[0].OrigenDetalle, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("core_test.db", history[0].DestinoDetalle, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10_000, history[0].KilosImportados);

        string xlsxPers = Path.Combine(_tempDir, "personalizado.xlsx");
        _app.ExcelExporter.ExportReportePersonalizado(
            xlsxPers,
            new ReportePersonalizadoPayload(
                "Análisis Personalizado Test",
                resumen.PeriodoActualLabel,
                [("Volumen", $"{resumen.TotalKilos:N0} Kg", "OK")],
                [new ExportTableSection("Resumen", "Segmentación", ["Grupo", "Kilos"], [["MIX", 12_000L]])]));
        using (XLWorkbook wb = new(xlsxPers))
        {
            Assert.Equal(2, wb.Worksheets.Count);
        }

        string xlsxHist = Path.Combine(_tempDir, "historial.xlsx");
        _app.ExcelExporter.ExportImportHistory(xlsxHist, history);
        using (XLWorkbook wb = new(xlsxHist))
        {
            Assert.Single(wb.Worksheets);
        }

        await Assert.ThrowsAsync<ArgumentException>(() => _app.PurgeAllPesajesAsync("NO"));
        int purged = await _app.PurgeAllPesajesAsync("ELIMINAR");
        Assert.Equal(3, purged);
    }
}
