using System.Diagnostics;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.Stress;

public sealed class PerformanceAndStressTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteScrapRepository _repo;
    private readonly SecuritySessionService _auth;
    private readonly ScrapApplicationService _app;

    public PerformanceAndStressTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_Stress_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        string dbPath = Path.Combine(_tempDir, "stress_10k.db");

        _repo = new SqliteScrapRepository(dbPath);
        _repo.InitializeAsync().GetAwaiter().GetResult();
        _auth = new SecuritySessionService(_repo);
        _auth.LoginAsync("admin", "Admin@2026").GetAwaiter().GetResult();
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
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task BulkImport10kRows_AndConcurrentQueries_MeetLatencyTargets()
    {
        DateOnly baseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-60));
        string[] familias = ["SUPER-BASULIP", "RECAM", "ALTA", "MIX SUPERMERCADO", "STRETCH"];
        string[] cods = ["S0400", "S0200", "S0300", "5300000009", "S0500"];

        List<Pesaje> placillaBatch = new(4000);
        List<Pesaje> stgoBatch = new(4000);
        List<Pesaje> thnoBatch = new(2000);

        for (int i = 0; i < 4000; i++)
        {
            int fIdx = i % familias.Length;
            placillaBatch.Add(new Pesaje
            {
                Folio = 900_000 + i,
                Planta = Planta.Placilla,
                TipoPesaje = i % 10 == 0 ? TipoPesaje.MovimientoInterno : TipoPesaje.RecepcionCarga,
                Patente = $"AB{(i % 90) + 10}CD",
                PesoBruto = 18_500 + (i % 500),
                PesoTara = 8_000,
                PesoNeto = 10_500 + (i % 500),
                CodProducto = cods[fIdx],
                NomProducto = familias[fIdx],
                RutCliente = $"76{(i % 20) + 100000}-1",
                NomCliente = $"PROVEEDOR STRESS {i % 20}",
                FechaIngreso = baseDate.AddDays(i % 60),
                HoraIngreso = new TimeOnly(8 + (i % 10), i % 60),
                FechaSalida = baseDate.AddDays(i % 60),
                HoraSalida = new TimeOnly(8 + (i % 10), (i + 15) % 60),
                GrupoMaterial = familias[fIdx],
                EsTraslado = i % 10 == 0
            });

            stgoBatch.Add(new Pesaje
            {
                Folio = 910_000 + i,
                Planta = Planta.Santiago,
                TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = $"XY{(i % 90) + 10}ZW",
                PesoBruto = 16_000,
                PesoTara = 7_500,
                PesoNeto = 8_500,
                CodProducto = cods[fIdx],
                NomProducto = familias[fIdx],
                RutCliente = $"77{(i % 15) + 100000}-K",
                NomCliente = $"RETAIL CLIENTE {i % 15}",
                FechaIngreso = baseDate.AddDays(i % 60),
                HoraIngreso = new TimeOnly(9 + (i % 8), i % 60),
                FechaSalida = baseDate.AddDays(i % 60),
                HoraSalida = new TimeOnly(9 + (i % 8), (i + 10) % 60),
                GrupoMaterial = familias[fIdx],
                EsTraslado = false
            });
        }

        for (int i = 0; i < 2000; i++)
        {
            thnoBatch.Add(new Pesaje
            {
                Folio = 920_000 + i,
                Planta = Planta.Talcahuano,
                TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = $"TH{(i % 90) + 10}NO",
                PesoBruto = 19_000,
                PesoTara = 8_200,
                PesoNeto = 10_800,
                CodProducto = "S0400",
                NomProducto = "SUPER-BASULIP",
                RutCliente = "76555444-3",
                NomCliente = "PESQUERA SUR",
                FechaIngreso = baseDate.AddDays(i % 60),
                HoraIngreso = new TimeOnly(7 + (i % 12), i % 60),
                FechaSalida = baseDate.AddDays(i % 60),
                HoraSalida = new TimeOnly(7 + (i % 12), (i + 20) % 60),
                GrupoMaterial = "SUPER-BASULIP"
            });
        }

        Stopwatch swImport = Stopwatch.StartNew();
        var r1 = await _repo.ImportPesajesAsync(placillaBatch, Planta.Placilla, "STRESS");
        var r2 = await _repo.ImportPesajesAsync(stgoBatch, Planta.Santiago, "STRESS");
        var r3 = await _repo.ImportPesajesAsync(thnoBatch, Planta.Talcahuano, "STRESS");
        swImport.Stop();

        Assert.Equal(4000, r1.Imported);
        Assert.Equal(4000, r2.Imported);
        Assert.Equal(2000, r3.Imported);
        Assert.True(swImport.ElapsedMilliseconds < 15_000, $"Importación 10k tardó demasiado: {swImport.ElapsedMilliseconds} ms");

        // Reimportación idempotente debe detectar 100% duplicados
        var dupCheck = await _repo.ImportPesajesAsync(placillaBatch, Planta.Placilla, "STRESS");
        Assert.Equal(0, dupCheck.Imported);
        Assert.Equal(4000, dupCheck.Duplicates);

        // Consultas concurrentes sobre >10k filas bajo modo WAL
        DateOnly desde = baseDate;
        DateOnly hasta = baseDate.AddDays(65);

        Stopwatch swQueries = Stopwatch.StartNew();
        await Task.WhenAll(
            _app.GetDashboardSummaryAsync(),
            _app.GetGrupoMaterialAsync(desde, hasta, semanal: false),
            _app.GetComprasTrasladosAsync(Planta.Placilla, desde, hasta),
            _app.GetGruposEspecialesAsync(desde, hasta),
            _app.GetInformePreciosAsync(Planta.Placilla, ModoPrecio.Teoricos, desde, hasta),
            _app.GetInformeJcrammAsync());
        swQueries.Stop();

        Assert.True(swQueries.ElapsedMilliseconds < 10_000, $"Consultas concurrentes tardaron {swQueries.ElapsedMilliseconds} ms");
    }
}
