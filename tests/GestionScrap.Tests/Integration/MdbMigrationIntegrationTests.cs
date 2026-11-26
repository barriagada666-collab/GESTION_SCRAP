using GestionScrap.Application.Services;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.Integration;

public sealed class MdbMigrationIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteScrapRepository _repo;
    private readonly SecuritySessionService _auth;
    private readonly ScrapApplicationService _app;

    public MdbMigrationIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_MdbTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        string dbPath = Path.Combine(_tempDir, "mdb_import_test.db");

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
                // Ignorar bloqueo temporal al limpiar archivos temporales
            }
        }
    }

    [Fact]
    public async Task ImportFromAccessMdb_MissingFileThrowsFileNotFound()
    {
        await _auth.LoginAsync("admin", "Admin@2026");
        string bogus = Path.Combine(_tempDir, "NO_EXISTE_BD.mdb");
        await Assert.ThrowsAsync<FileNotFoundException>(() => _app.ImportFromAccessMdbAsync(bogus));
    }

    [Fact]
    public async Task ImportFromAccessMdb_MigratesRealLegacyDatabaseWhenAvailable()
    {
        string legacyMdb = @"D:\archivos qlabel\IMPORTADOR PESAJES\BD_SCRAP.mdb";
        if (!File.Exists(legacyMdb))
        {
            return;
        }

        await _auth.LoginAsync("admin", "Admin@2026");
        var res = await _app.ImportFromAccessMdbAsync(legacyMdb);

        Assert.True(res.PesajesImported > 0, "Debe importar pesajes históricos desde BD_SCRAP.mdb.");
        Assert.True(res.ClientesImported > 0, "Debe importar catálogo de clientes desde BD_SCRAP.mdb.");
        Assert.True(res.ProductosImported > 0, "Debe importar productos y familias desde BD_SCRAP.mdb.");

        var placilla = await _repo.GetPesajesAsync(Planta.Placilla);
        var santiago = await _repo.GetPesajesAsync(Planta.Santiago);
        var talcahuano = await _repo.GetPesajesAsync(Planta.Talcahuano);

        Assert.NotEmpty(placilla);
        Assert.NotEmpty(santiago);
        Assert.NotEmpty(talcahuano);
    }
}
