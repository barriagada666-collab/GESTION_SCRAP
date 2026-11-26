using System.Globalization;
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

public sealed class SqliteExportAndSecurityIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteScrapRepository _repo;
    private readonly SecuritySessionService _auth;
    private readonly ScrapApplicationService _app;

    public SqliteExportAndSecurityIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_IntTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        string dbPath = Path.Combine(_tempDir, "test_scrap.db");

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
                // Ignorar bloqueo temporal en limpieza de archivos temporales
            }
        }
    }

    [Fact]
    public async Task Login_LocksAccountAfterFiveFailedAttempts()
    {
        for (int i = 0; i < 4; i++)
        {
            var fail = await _auth.LoginAsync("operador", "WrongPass99");
            Assert.False(fail.Success);
        }

        var fifth = await _auth.LoginAsync("operador", "WrongPass99");
        Assert.False(fifth.Success);
        Assert.Contains("bloqueada", fifth.Message, StringComparison.OrdinalIgnoreCase);

        var blockedValid = await _auth.LoginAsync("operador", "Oper@2026");
        Assert.False(blockedValid.Success);
        Assert.Contains("bloqueada", blockedValid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rbac_EnforcesPermissionsAtApplicationLayer()
    {
        var opLogin = await _auth.LoginAsync("operador", "Oper@2026");
        Assert.True(opLogin.Success);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _app.ActualizarPrecioRealIndividualAsync(Planta.Placilla, ModoPrecio.Reales, 100, 150m, "RECAM"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _app.PurgeAllPesajesAsync("ELIMINAR"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _app.GetAuditLogsAsync());

        _auth.Logout();

        var audLogin = await _auth.LoginAsync("auditor", "Audit@2026");
        Assert.True(audLogin.Success);
        IReadOnlyList<AuditLogEntry> logs = await _app.GetAuditLogsAsync();
        Assert.NotEmpty(logs);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _app.GuardarPrecioProveedorAsync(Planta.Placilla, "76123456-K", "PROV", "S0400", 120m, false));
    }

    [Fact]
    public async Task EndToEnd_Workflows_Reports_Prices_Jcramm_And_ExcelVerification()
    {
        var login = await _auth.LoginAsync("admin", "Admin@2026");
        Assert.True(login.Success);

        DashboardSummaryDto dash = await _app.GetDashboardSummaryAsync();
        Assert.True(dash.UltimosFolios.Count > 0);
        Assert.True(dash.TotalKilosMesActual > 0);

        DateOnly desde = DateOnly.FromDateTime(DateTime.Today.AddMonths(-6));
        DateOnly hasta = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var mat = await _app.GetGrupoMaterialAsync(desde, hasta, semanal: false);
        Assert.NotEmpty(mat.Placilla);

        var comp = await _app.GetComprasTrasladosAsync(Planta.Placilla, desde, hasta);
        Assert.True(comp.TotalGeneralKilos > 0);

        string folioStr = comp.Traslados.Count > 0 ? comp.Traslados[0].Folio.ToString(CultureInfo.InvariantCulture) : "1001";
        var cliFolio = await _app.GetInformeClienteAsync(Planta.Placilla, buscarPorRut: false, folioStr, desde, hasta);
        Assert.NotNull(cliFolio);

        IReadOnlyList<GrupoEspecialCardDto> especiales = await _app.GetGruposEspecialesAsync(desde, hasta);
        Assert.Equal(8, especiales.Count);

        InformePreciosResultDto teo = await _app.GetInformePreciosAsync(Planta.Placilla, ModoPrecio.Teoricos, desde, hasta);
        Assert.NotEmpty(teo.Detalle);

        int frozen = await _app.CongelarPreciosTeoricosAsync(Planta.Placilla, ModoPrecio.Teoricos, teo.Detalle);
        Assert.True(frozen > 0);

        long sampleFolio = teo.Detalle[0].Folio;
        await _app.ActualizarPrecioRealIndividualAsync(Planta.Placilla, ModoPrecio.Reales, sampleFolio, 345m, teo.Detalle[0].Familia);

        InformePreciosResultDto real = await _app.GetInformePreciosAsync(Planta.Placilla, ModoPrecio.Reales, desde, hasta);
        Assert.Equal(345m, real.Detalle.First(d => d.Folio == sampleFolio).Precio);

        // Exportar todos los formatos Excel y validarlos reabriéndolos con ClosedXML
        string xlsxMat = Path.Combine(_tempDir, "mat.xlsx");
        _app.ExcelExporter.ExportGrupoMaterial(
            xlsxMat, desde, hasta,
            [.. mat.Placilla.Select(x => (x.Grupo, x.Kilos))],
            [.. mat.Santiago.Select(x => (x.Grupo, x.Kilos))],
            [.. mat.Talcahuano.Select(x => (x.Grupo, x.Kilos))]);
        using (XLWorkbook wb = new(xlsxMat))
        {
            Assert.Equal("Grupos Material", wb.Worksheets.First().Name);
        }

        string xlsxPrecios = Path.Combine(_tempDir, "precios.xlsx");
        _app.ExcelExporter.ExportInformePrecios(
            xlsxPrecios, Planta.Placilla, desde, hasta, ModoPrecio.Reales,
            [.. real.Detalle.Select(d => (d.Folio, d.CodigoProd, d.Descripcion, d.Proveedor, d.Kilos, d.Precio, d.Total, d.Familia))],
            [.. real.ResumenFamilias.Select(r => (r.Grupo, r.TotalKilos, r.PrecioPromedioPonderado, r.TotalValorizado))]);
        using (XLWorkbook wb = new(xlsxPrecios))
        {
            Assert.True(wb.Worksheets.Count >= 1);
            Assert.NotEmpty(wb.Worksheets.First().Cell(1, 1).GetString());
        }

        // Respaldo SQLite en caliente
        string backupPath = Path.Combine(_tempDir, "backup.db");
        string savedBackup = await _app.BackupDatabaseAsync(backupPath);
        Assert.True(File.Exists(savedBackup));
        Assert.True(new FileInfo(savedBackup).Length > 0);
    }

    [Fact]
    public async Task PurgeAndChangePassword_WorkAsExpected()
    {
        var login = await _auth.LoginAsync("admin", "Admin@2026");
        Assert.True(login.Success);

        await Assert.ThrowsAsync<ArgumentException>(() => _app.PurgeAllPesajesAsync("BORRAR"));

        int purged = await _app.PurgeAllPesajesAsync("ELIMINAR");
        Assert.True(purged > 0);

        bool changed = await _auth.ChangePasswordAsync("Admin@2026", "AdminNuevo2026");
        Assert.True(changed);

        _auth.Logout();
        var reLogin = await _auth.LoginAsync("admin", "AdminNuevo2026");
        Assert.True(reLogin.Success);
    }
}
