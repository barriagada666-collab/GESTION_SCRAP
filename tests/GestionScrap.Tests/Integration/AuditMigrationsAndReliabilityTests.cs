using System.Diagnostics;
using System.Text.RegularExpressions;
using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Domain;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Diagnostics;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GestionScrap.Tests.Integration;

/// <summary>
/// Pruebas de migración versionada, integridad referencial SQLite por conexión, protección del último
/// administrador, optimización de planes de consulta, logger estructurado sanitizado y seguridad documental.
/// </summary>
public sealed partial class AuditMigrationsAndReliabilityTests : IDisposable
{
    private readonly string _tempDir;

    public AuditMigrationsAndReliabilityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_Audit_" + Guid.NewGuid().ToString("N"));
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
                // Limpieza best-effort
            }
        }
    }

    [Fact]
    public async Task SchemaMigrations_ApplyIdempotently_AndVerifyIntegrityPasses()
    {
        string dbPath = Path.Combine(_tempDir, "mig_test.db");
        using SqliteScrapRepository repo = new(dbPath);
        await repo.InitializeAsync();
        await repo.InitializeAsync(); // Idempotente

        DatabaseIntegrityReport integrity = await repo.VerifyIntegrityAsync();
        Assert.True(integrity.IsHealthy, integrity.Summary);
        Assert.Equal("ok", integrity.QuickCheckResult, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, integrity.ForeignKeyViolations);
        Assert.True(integrity.AppliedSchemaVersion >= 2);

        // Validar plan de consulta indexado sin TEMP B-TREE ni SCAN completo en audit_log
        await using SqliteConnection conn = new($"Data Source={dbPath};Pooling=false");
        await conn.OpenAsync();

        string pesajesPlan = await ReadExplainPlanAsync(
            conn,
            "EXPLAIN QUERY PLAN SELECT * FROM pesajes WHERE planta = 1 AND fecha_ingreso >= '2026-01-01' AND fecha_ingreso <= '2026-12-31' ORDER BY fecha_ingreso DESC, folio DESC");
        Assert.Contains("idx_pesajes_planta_fecha_folio", pesajesPlan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TEMP B-TREE", pesajesPlan, StringComparison.OrdinalIgnoreCase);

        string auditPlan = await ReadExplainPlanAsync(
            conn,
            "EXPLAIN QUERY PLAN SELECT * FROM audit_log WHERE accion = 'LOGIN_EXITOSO' ORDER BY id DESC LIMIT 500");
        Assert.Contains("idx_audit_accion_id", auditPlan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForeignKeysCascade_EnforcedOnFreshConnectionWithoutPriorInit()
    {
        string dbPath = Path.Combine(_tempDir, "fk_fresh.db");
        long parentId;
        long childId;

        using (SqliteScrapRepository repo1 = new(dbPath))
        {
            await repo1.InitializeAsync();
            MaterialGroupNode parent = await repo1.SaveMaterialNodeAsync(
                new MaterialGroupNode { Nombre = "PADRE FK", TipoNodo = NodoGrupoTipo.CarpetaGrupo }, "TEST");
            MaterialGroupNode child = await repo1.SaveMaterialNodeAsync(
                new MaterialGroupNode { ParentId = parent.Id, Nombre = "HIJO FK", TipoNodo = NodoGrupoTipo.ProductoAsignado, CodigoProducto = "FK001" }, "TEST");
            parentId = parent.Id;
            childId = child.Id;
        }

        // Abrir una instancia completamente nueva para verificar que PRAGMA foreign_keys = ON se aplica por conexión
        using (SqliteScrapRepository repo2 = new(dbPath))
        {
            await repo2.DeleteMaterialNodeAsync(parentId, deleteSubtree: true, "TEST");
            IReadOnlyList<MaterialGroupNode> remaining = await repo2.GetMaterialNodesAsync();
            Assert.DoesNotContain(remaining, n => n.Id == parentId || n.Id == childId);

            DatabaseIntegrityReport check = await repo2.VerifyIntegrityAsync();
            Assert.True(check.IsHealthy);
            Assert.Equal(0, check.ForeignKeyViolations);
        }
    }

    [Fact]
    public async Task Security_ProtectsLastActiveAdministratorFromDeleteDeactivateOrDemote()
    {
        string dbPath = Path.Combine(_tempDir, "last_admin.db");
        using SqliteScrapRepository repo = new(dbPath);
        await repo.InitializeAsync();

        Usuario admin = (await repo.GetUsuarioByNombreAsync("barriagada"))!;

        // 1. Impedir eliminar al último administrador activo
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteUsuarioAsync(admin.Id, "TEST"));

        // 2. Impedir desactivar al último administrador activo
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveUsuarioAsync(new Usuario
        {
            Id = admin.Id,
            NombreUsuario = admin.NombreUsuario,
            NombreCompleto = admin.NombreCompleto,
            PerfilId = admin.PerfilId,
            PasswordHash = admin.PasswordHash,
            PasswordSalt = admin.PasswordSalt,
            Activo = false,
            CreadoEnUtc = admin.CreadoEnUtc
        }, "TEST"));

        // 3. Impedir degradar al último administrador a perfil Operador (ID 4)
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveUsuarioAsync(new Usuario
        {
            Id = admin.Id,
            NombreUsuario = admin.NombreUsuario,
            NombreCompleto = admin.NombreCompleto,
            PerfilId = 4,
            PasswordHash = admin.PasswordHash,
            PasswordSalt = admin.PasswordSalt,
            Activo = true,
            CreadoEnUtc = admin.CreadoEnUtc
        }, "TEST"));
    }

    [Fact]
    public async Task LegacyRealDatabaseCopy_MigratesCleanlyPreserving100PercentOfRecords()
    {
        string localLiveDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestionScrap",
            "gestion_scrap.db");

        if (!File.Exists(localLiveDb))
        {
            return;
        }

        string copyDb = Path.Combine(_tempDir, "migrated_live_copy.db");
        File.Copy(localLiveDb, copyDb, overwrite: true);

        long expectedPesajes;
        long expectedClientes;
        long expectedProductos;
        await using (SqliteConnection preConn = new($"Data Source={copyDb};Pooling=false"))
        {
            await preConn.OpenAsync();
            expectedPesajes = await ScalarLongAsync(preConn, "SELECT COUNT(*) FROM pesajes");
            expectedClientes = await ScalarLongAsync(preConn, "SELECT COUNT(*) FROM clientes");
            expectedProductos = await ScalarLongAsync(preConn, "SELECT COUNT(*) FROM productos");
        }

        using SqliteScrapRepository repo = new(copyDb);
        await repo.InitializeAsync();

        DatabaseIntegrityReport integrity = await repo.VerifyIntegrityAsync();
        Assert.True(integrity.IsHealthy, integrity.Summary);
        Assert.Equal(expectedPesajes, integrity.PesajesCount);
        Assert.Equal(3, integrity.AppliedSchemaVersion);

        Stopwatch sw = Stopwatch.StartNew();
        DatabaseSummaryMetrics metrics = await repo.GetDatabaseSummaryMetricsAsync();
        sw.Stop();

        Assert.Equal(expectedPesajes, metrics.TotalPesajes);
        Assert.Equal(expectedClientes, metrics.TotalClientes);
        Assert.Equal(expectedProductos, metrics.TotalProductos);
        Assert.True(sw.ElapsedMilliseconds < 250, $"GetDatabaseSummaryMetricsAsync tardó {sw.ElapsedMilliseconds} ms (esperado < 250 ms)");
    }

    [Fact]
    public void FileAppLogger_WritesStructuredEntriesAndRedactsSecrets()
    {
        string logDir = Path.Combine(_tempDir, "diag_logs");
        FileAppLogger logger = new(logDir);

        string syntheticPat = string.Concat("ghp", "_", "1234567890abcdefghij1234567890abcd");
        logger.Log(
            DiagnosticLogLevel.Error,
            "FirebirdSync",
            "Error conectando con Password=SuperSecret99; Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.token",
            context: $"Token={syntheticPat} DPAPI:AQAAANCMnd8BFdERjHoAwE==",
            exception: new InvalidOperationException("Fallo TCP con Pwd=MySecretPass"),
            remediation: "Reintentar en puerto 3050",
            verification: "PRUEBA_OK");

        Assert.True(File.Exists(logger.CurrentLogFilePath));
        string content = File.ReadAllText(logger.CurrentLogFilePath);

        Assert.Contains("[ERROR]", content);
        Assert.Contains("[FirebirdSync]", content);
        Assert.Contains(AppMetadata.ShortVersion, content);
        Assert.Contains("Correccion: Reintentar en puerto 3050", content);
        Assert.Contains("Verificacion: PRUEBA_OK", content);

        // Ningún secreto debe persistir en texto plano
        Assert.DoesNotContain("SuperSecret99", content);
        Assert.DoesNotContain("MySecretPass", content);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", content);
        Assert.DoesNotContain(syntheticPat, content);
        Assert.DoesNotContain("AQAAANCMnd8BFdERjHoAwE", content);
        Assert.Contains("[REDACTED]", content);
    }

    [Fact]
    public void AppMetadata_And_PublicRepositoryDocs_ContainValidVersionAndNoExposedSecrets()
    {
        Assert.StartsWith("5.0.", AppMetadata.Version, StringComparison.Ordinal);
        Assert.StartsWith("v5.0.", AppMetadata.ShortVersion, StringComparison.Ordinal);

        string? repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return;
        }

        Regex patRegex = SecretPatRegex();

        string readmePath = Path.Combine(repoRoot, "README.md");
        if (File.Exists(readmePath))
        {
            string readme = File.ReadAllText(readmePath);
            Assert.False(patRegex.IsMatch(readme), "Se detectó un token secreto en README.md.");
            Assert.DoesNotContain("Cambias0", readme, StringComparison.Ordinal);
        }

        string docsDir = Path.Combine(repoRoot, "docs");
        if (Directory.Exists(docsDir))
        {
            foreach (string md in Directory.EnumerateFiles(docsDir, "*.md", SearchOption.AllDirectories))
            {
                string txt = File.ReadAllText(md);
                Assert.False(patRegex.IsMatch(txt), $"Se detectó un token secreto en {md}.");
                Assert.DoesNotContain("Cambias0", txt, StringComparison.Ordinal);
            }
        }

        string wfDir = Path.Combine(repoRoot, ".github", "workflows");
        if (Directory.Exists(wfDir))
        {
            foreach (string yml in Directory.EnumerateFiles(wfDir, "*.yml"))
            {
                string txt = File.ReadAllText(yml);
                Assert.False(patRegex.IsMatch(txt), $"Se detectó un token secreto en {yml}.");
            }
        }
    }

    [Fact]
    public async Task ApplicationService_VerifyIntegrityAndSummaryStats_EnforceRbacAndReturnExactCounts()
    {
        string dbPath = Path.Combine(_tempDir, "app_stats.db");
        using SqliteScrapRepository repo = new(dbPath);
        await repo.InitializeAsync();
        SecuritySessionService auth = new(repo);
        FileAppLogger logger = new(Path.Combine(_tempDir, "app_logs"));
        ScrapApplicationService app = new(
            repo,
            auth,
            new FirebirdSyncService(),
            new ClosedXmlExcelExportService(),
            new MdbMigrationService(repo),
            logger);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => app.VerifyDatabaseIntegrityAsync());

        Assert.True((await auth.LoginAsync("barriagada", "Cambias0")).Success);
        DatabaseIntegrityReport integrity = await app.VerifyDatabaseIntegrityAsync();
        Assert.True(integrity.IsHealthy);

        DatabaseResumenStatsDto stats = await app.GetDatabaseResumenStatsAsync();
        Assert.Equal(0, stats.TotalPesajes);
        Assert.True(stats.TotalProductos >= 45);
        Assert.True(File.Exists(logger.CurrentLogFilePath));
    }

    private static async Task<string> ReadExplainPlanAsync(SqliteConnection conn, string sql)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        List<string> lines = [];
        await using SqliteDataReader rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            lines.Add(rd.GetString(3));
        }

        return string.Join(" | ", lines);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection conn, string sql)
    {
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? cur = new(AppContext.BaseDirectory);
        while (cur is not null)
        {
            if (File.Exists(Path.Combine(cur.FullName, "GestionScrap.sln")))
            {
                return cur.FullName;
            }

            cur = cur.Parent;
        }

        return null;
    }

    [GeneratedRegex(@"\b(?:ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPatRegex();
}
