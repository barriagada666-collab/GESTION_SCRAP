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
        _auth.LoginAsync("barriagada", "Cambias0").GetAwaiter().GetResult();
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
                // Best-effort
            }
        }
    }

    [Fact]
    public async Task BulkImport10kRows_AndConcurrentCoreQueries_MeetLatencyTargets()
    {
        DateOnly baseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-90));
        string[] cods = ["5300000009", "5301000007", "5301001002", "5302000004", "5300000006"];

        List<Pesaje> batchStgo = new(5000);
        List<Pesaje> batchPl = new(5000);

        for (int i = 0; i < 5000; i++)
        {
            string cod = cods[i % cods.Length];
            batchStgo.Add(new Pesaje
            {
                Folio = 900_000 + i,
                Planta = Planta.Santiago,
                TipoPesaje = i % 8 == 0 ? TipoPesaje.MovimientoInterno : TipoPesaje.RecepcionCarga,
                Patente = $"AB{(i % 90) + 10}CD",
                PesoBruto = 18_500 + (i % 500),
                PesoTara = 8_000,
                PesoNeto = 10_500 + (i % 500),
                CodProducto = cod,
                NomProducto = cod,
                RutCliente = $"76{(i % 25) + 100000}-1",
                NomCliente = $"PROVEEDOR STRESS {i % 25}",
                Origen = "BOD. VICKY",
                FechaIngreso = baseDate.AddDays(i % 90),
                HoraIngreso = new TimeOnly(8 + (i % 10), i % 60),
                FechaSalida = baseDate.AddDays(i % 90),
                HoraSalida = new TimeOnly(8 + (i % 10), (i + 15) % 60),
                GrupoMaterial = "SCRAP SUPERMERCADO & MIX"
            });

            batchPl.Add(new Pesaje
            {
                Folio = 950_000 + i,
                Planta = Planta.Placilla,
                TipoPesaje = TipoPesaje.RecepcionCarga,
                Patente = $"XY{(i % 90) + 10}ZW",
                PesoBruto = 17_000,
                PesoTara = 7_500,
                PesoNeto = 9_500,
                CodProducto = cod,
                NomProducto = cod,
                RutCliente = $"77{(i % 20) + 100000}-K",
                NomCliente = $"PROVEEDOR PLACILLA {i % 20}",
                Origen = "PLACILLA",
                FechaIngreso = baseDate.AddDays(i % 90),
                HoraIngreso = new TimeOnly(9 + (i % 8), i % 60),
                FechaSalida = baseDate.AddDays(i % 90),
                HoraSalida = new TimeOnly(9 + (i % 8), (i + 10) % 60),
                GrupoMaterial = "SCRAP PESQUERA"
            });
        }

        Stopwatch swImport = Stopwatch.StartNew();
        var r1 = await _repo.ImportPesajesAsync(batchStgo, Planta.Santiago, "STRESS");
        var r2 = await _repo.ImportPesajesAsync(batchPl, Planta.Placilla, "STRESS");
        swImport.Stop();

        Assert.Equal(5000, r1.Imported);
        Assert.Equal(5000, r2.Imported);
        Assert.True(swImport.ElapsedMilliseconds < 15_000, $"Importación 10k tardó {swImport.ElapsedMilliseconds} ms");

        var dup = await _repo.ImportPesajesAsync(batchStgo, Planta.Santiago, "STRESS");
        Assert.Equal(0, dup.Imported);
        Assert.Equal(5000, dup.Duplicates);

        DateOnly desde = baseDate;
        DateOnly hasta = baseDate.AddDays(95);

        Stopwatch swQueries = Stopwatch.StartNew();
        await Task.WhenAll(
            _app.GetInformeResumenAsync(null, null, desde, hasta),
            _app.GetGrupoMaterialReportAsync(null, desde, hasta),
            _app.GetAnalisisProveedoresAsync(null, desde, hasta));
        swQueries.Stop();

        Assert.True(swQueries.ElapsedMilliseconds < 10_000, $"Consultas concurrentes tardaron {swQueries.ElapsedMilliseconds} ms");
    }
}
