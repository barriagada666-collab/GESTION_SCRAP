using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Configuration;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.Integration;

public sealed class RealDatabasesIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteScrapRepository _repo;
    private readonly SecuritySessionService _auth;
    private readonly ScrapApplicationService _app;

    public RealDatabasesIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_RealDb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        string dbPath = Path.Combine(_tempDir, "real_import_test.db");

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
                // Ignorar limpieza temporal
            }
        }
    }

    [Fact]
    public async Task FirebirdWmdbspcPlacilla2_ImportsRealDataCleanlyWhenServerReachable()
    {
        await _auth.LoginAsync("barriagada", "Cambias0");
        FirebirdPlantaEndpoint ep = AppSettingsStore.CreateDefaultEndpoints().First(e => e.Planta == Planta.Santiago);

        var connTest = await _app.TestFirebirdConnectionAsync(ep);
        if (!connTest.Success)
        {
            // En entornos sin acceso a intranet 192.168.2.56 (ej. CI remoto), no fallar por red externa
            return;
        }

        Assert.True(connTest.PesajesCount > 2000);

        FirebirdExtractPackage pkg = await _app.QueryFirebirdPackageAsync(ep, null, null, allowFallbackDemo: false);
        Assert.False(pkg.UsedSimulatedFallback);
        Assert.True(pkg.Pesajes.Count > 2000);
        Assert.True(pkg.Clientes.Count > 200);
        Assert.True(pkg.Productos.Count >= 45);
        Assert.Contains(pkg.Pesajes, p => p.Observacion.Contains("VIÑA", StringComparison.OrdinalIgnoreCase));

        var imp = await _app.ImportFirebirdPackageAsync(Planta.Santiago, pkg);
        Assert.Equal(pkg.Pesajes.Count, imp.ImportedPesajes);

        // Segunda pasada es 100% idempotente
        var imp2 = await _app.ImportFirebirdPackageAsync(Planta.Santiago, pkg);
        Assert.Equal(0, imp2.ImportedPesajes);
        Assert.Equal(pkg.Pesajes.Count, imp2.Duplicates);

        // Verificar los 3 reportes reales sobre datos importados de WMDBSPC.GDB
        InformeResumenDto res = await _app.GetInformeResumenAsync(Planta.Santiago, null, new DateOnly(2024, 1, 1), DateOnly.FromDateTime(DateTime.Today));
        Assert.True(res.TotalKilos > 20_000_000);

        AnalisisProveedoresResultDto prov = await _app.GetAnalisisProveedoresAsync(Planta.Santiago, new DateOnly(2024, 1, 1), DateOnly.FromDateTime(DateTime.Today));
        Assert.True(prov.TotalProveedoresActivos > 20);
        Assert.Equal("A", prov.Ranking[0].ClaseAbc);
    }

    [Fact]
    public async Task AccessMdbMigration_MissingFileThrowsAndRealMdbImportsWhenAvailable()
    {
        await _auth.LoginAsync("barriagada", "Cambias0");
        await Assert.ThrowsAsync<FileNotFoundException>(() => _app.ImportFromAccessMdbAsync(Path.Combine(_tempDir, "inexistente.mdb")));

        string legacyMdb = @"D:\archivos qlabel\IMPORTADOR PESAJES\BD_SCRAP.mdb";
        if (!File.Exists(legacyMdb))
        {
            return;
        }

        var res = await _app.ImportFromAccessMdbAsync(legacyMdb);
        Assert.True(res.PesajesImported > 0);
        Assert.True(res.ClientesImported > 0);
        Assert.True(res.ProductosImported > 0);
    }
}
