using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Interfaces;

public interface IScrapRepository
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Pesaje>> GetPesajesAsync(Planta? planta = null, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> GetClientesAsync(Planta? planta = null, CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PrecioProveedor>> GetPreciosProveedorAsync(Planta? planta = null, CancellationToken ct = default);
    Task<IReadOnlyList<IndexPrecioPesaje>> GetIndexPreciosAsync(Planta? planta = null, CancellationToken ct = default);
    Task<UltimoFolioJcramm> GetUltimoFolioJcrammAsync(CancellationToken ct = default);
    Task SetUltimoFolioJcrammAsync(long folio, DateOnly fecha, string usuario, CancellationToken ct = default);

    Task UpsertPrecioProveedorAsync(PrecioProveedor precio, bool allowUpdate, string usuario, CancellationToken ct = default);
    Task DeletePrecioProveedorAsync(Planta planta, string rut, string codigoProducto, string usuario, CancellationToken ct = default);

    Task UpsertIndexPrecioAsync(IndexPrecioPesaje indexPrecio, string usuario, CancellationToken ct = default);
    Task<int> BulkFreezeIndexPreciosAsync(IEnumerable<IndexPrecioPesaje> items, string usuario, CancellationToken ct = default);

    Task<(int Imported, int Duplicates)> ImportPesajesAsync(IEnumerable<Pesaje> pesajes, Planta planta, string usuario, CancellationToken ct = default);
    Task UpsertClientesAsync(IEnumerable<Cliente> clientes, string usuario, CancellationToken ct = default);
    Task UpsertProductosAsync(IEnumerable<Producto> productos, string usuario, CancellationToken ct = default);
    Task<int> PurgeAllPesajesAsync(string usuario, CancellationToken ct = default);
    Task ResetDemoDataAsync(string usuario, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 500, string? accion = null, string? usuario = null, CancellationToken ct = default);
    Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default);
    Task<Usuario?> GetUsuarioByNombreAsync(string nombreUsuario, CancellationToken ct = default);
    Task SaveUsuarioAsync(Usuario usuario, string actor, CancellationToken ct = default);
    Task DeleteUsuarioAsync(long id, string actor, CancellationToken ct = default);
    Task<string> BackupDatabaseAsync(string destinationPath, CancellationToken ct = default);
}

public interface IFirebirdSyncService
{
    Task<(bool Success, int PingMs, int TablesCount, int ProceduresCount, string Message)> TestConnectionAsync(
        FirebirdPlantaEndpoint endpoint, CancellationToken ct = default);

    Task<(IReadOnlyList<FirebirdRemotePesaje> Records, bool UsedSimulatedFallback, string StatusMessage)> QueryRemotePesajesAsync(
        FirebirdPlantaEndpoint endpoint, DateOnly desde, DateOnly hasta, bool allowFallbackDemo, CancellationToken ct = default);

    Task<(bool Success, IReadOnlyList<string> Logs, string? ErrorMessage)> ExecuteStoredProceduresAsync(
        FirebirdPlantaEndpoint endpoint, bool allowSimulatedDemo, IProgress<string>? progress = null, CancellationToken ct = default);
}

public interface IExcelExportService
{
    string ExportGrupoMaterial(string filePath, DateOnly desde, DateOnly hasta,
        IReadOnlyList<(string Grupo, long Kilos)> placilla,
        IReadOnlyList<(string Grupo, long Kilos)> santiago,
        IReadOnlyList<(string Grupo, long Kilos)> talcahuano);

    string ExportComprasTraslados(string filePath, Planta planta, DateOnly desde, DateOnly hasta,
        IReadOnlyList<(string Codigo, string Descripcion, string RazonSocial, long Kilos)> compras,
        IReadOnlyList<(long Folio, string Patente, string Producto, DateOnly FechaEnt, DateOnly FechaSal, int Bruto, int Tara, int Neto)> traslados,
        long totalCompras, long totalTraslados);

    string ExportInformeCliente(string filePath, Planta planta, string rut, string razonSocial, DateOnly desde, DateOnly hasta,
        IReadOnlyList<(long Folio, string Patente, string Producto, DateOnly FechaIng, DateOnly FechaSal, int Bruto, int Tara, int Neto)> pesajes,
        long totalKilos);

    string ExportInformeJcramm(string filePath,
        IReadOnlyList<(DateOnly Fecha, long Folio, string CodProd, string Descripcion, string Patente, string Proveedor, string Ciudad, int Kilos)> detalle,
        IReadOnlyList<(string Producto, long Kilos)> resumen,
        long totalDetalle, long totalResumen);

    string ExportInformePrecios(string filePath, Planta planta, DateOnly desde, DateOnly hasta, ModoPrecio modo,
        IReadOnlyList<(long Folio, string CodigoProd, string Descripcion, string Proveedor, int Kilos, decimal Precio, decimal Total, string Familia)> detalle,
        IReadOnlyList<(string Grupo, long Kilos, decimal PrecioPromedio, decimal TotalPesos)> resumen);

    string ExportAuditLogs(string filePath, IReadOnlyList<AuditLogEntry> logs);
}

public interface IMdbMigrationService
{
    Task<(int PesajesImported, int ClientesImported, int ProductosImported, int PreciosImported, int IndexPreciosImported, IReadOnlyList<string> Warnings)>
        ImportFromMdbAsync(string mdbPath, string usuario, CancellationToken ct = default);
}

public interface IAuthService
{
    Usuario? CurrentUser { get; }
    Task<(bool Success, string Message, Usuario? User)> LoginAsync(string username, string password, CancellationToken ct = default);
    void Logout();
    bool HasPermission(PermisoSistema permiso);
    void EnsurePermission(PermisoSistema permiso);
    IReadOnlyCollection<PermisoSistema> GetEffectivePermissions(Usuario usuario);
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
}
