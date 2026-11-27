using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Interfaces;

public interface IScrapRepository
{
    string DatabasePath { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Pesaje>> GetPesajesAsync(Planta? planta = null, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> GetClientesAsync(Planta? planta = null, CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> GetProductosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MaterialGroupNode>> GetMaterialNodesAsync(CancellationToken ct = default);
    Task<MaterialGroupNode> SaveMaterialNodeAsync(MaterialGroupNode node, string usuario, CancellationToken ct = default);
    Task MoveMaterialNodeAsync(long nodeId, long? newParentId, int newOrden, string usuario, CancellationToken ct = default);
    Task SwapMaterialNodeOrderAsync(long nodeId, bool moveUp, string usuario, CancellationToken ct = default);
    Task DeleteMaterialNodeAsync(long nodeId, bool deleteSubtree, string usuario, CancellationToken ct = default);

    Task<(int Imported, int Duplicates)> ImportPesajesAsync(IEnumerable<Pesaje> pesajes, Planta planta, string usuario, CancellationToken ct = default);
    Task UpsertClientesAsync(IEnumerable<Cliente> clientes, string usuario, CancellationToken ct = default);
    Task UpsertProductosAsync(IEnumerable<Producto> productos, string usuario, CancellationToken ct = default);
    Task<int> PurgeAllPesajesAsync(string usuario, CancellationToken ct = default);

    Task<IReadOnlyList<ImportacionHistorial>> GetImportHistoryAsync(int limit = 200, CancellationToken ct = default);
    Task AddImportHistoryAsync(ImportacionHistorial entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 500, string? accion = null, string? usuario = null, CancellationToken ct = default);
    Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<PerfilUsuario>> GetPerfilesAsync(CancellationToken ct = default);
    Task<PerfilUsuario?> GetPerfilByIdAsync(long id, CancellationToken ct = default);
    Task<PerfilUsuario> SavePerfilAsync(PerfilUsuario perfil, string actor, CancellationToken ct = default);
    Task DeletePerfilAsync(long id, string actor, CancellationToken ct = default);

    Task<IReadOnlyList<Usuario>> GetUsuariosAsync(CancellationToken ct = default);
    Task<Usuario?> GetUsuarioByNombreAsync(string nombreUsuario, CancellationToken ct = default);
    Task SaveUsuarioAsync(Usuario usuario, string actor, CancellationToken ct = default);
    Task DeleteUsuarioAsync(long id, string actor, CancellationToken ct = default);
    Task<string> BackupDatabaseAsync(string destinationPath, CancellationToken ct = default);
}

public interface IFirebirdSyncService
{
    Task<(bool Success, int PingMs, int TablesCount, long PesajesCount, string DateBounds, string Message)> TestConnectionAsync(
        FirebirdPlantaEndpoint endpoint, CancellationToken ct = default);

    Task<FirebirdExtractPackage> QueryRemotePackageAsync(
        FirebirdPlantaEndpoint endpoint, DateOnly? desde, DateOnly? hasta, bool allowFallbackDemo, CancellationToken ct = default);
}

public interface IExcelExportService
{
    string ExportInformeResumen(
        string filePath,
        string tituloPeriodo,
        IReadOnlyList<(string Planta, int Viajes, long Kilos)> porPlanta,
        IReadOnlyList<(string Grupo, int Viajes, long Kilos, double Porcentaje)> porGrupo,
        IReadOnlyList<(string Rut, string Proveedor, int Viajes, long Kilos, double Porcentaje)> topProveedores,
        IReadOnlyList<Pesaje> detallePesajes);

    string ExportGrupoMaterialJerarquico(
        string filePath,
        string periodoLabel,
        IReadOnlyList<(int Nivel, string Nombre, string Tipo, string CodigoProducto, int Viajes, long Kilos, double Porcentaje)> nodosPlanos,
        long totalKilos);

    string ExportAnalisisProveedores(
        string filePath,
        string periodoLabel,
        IReadOnlyList<(string Rut, string RazonSocial, string Ciudad, int Viajes, long Kilos, double Participacion, double Acumulado, string ClaseAbc, double PromedioKg, string ProductoTop)> ranking,
        IReadOnlyList<(string Grupo, int Viajes, long Kilos, double Porcentaje)>? desgloseSeleccionado,
        IReadOnlyList<Pesaje>? entregasSeleccionado,
        string? proveedorSeleccionadoNombre);

    string ExportAuditLogs(string filePath, IReadOnlyList<AuditLogEntry> logs);

    string ExportImportHistory(string filePath, IReadOnlyList<ImportacionHistorial> history);

    string ExportReportePersonalizado(string filePath, ReportePersonalizadoPayload payload);
}

public interface IMdbMigrationService
{
    Task<(int PesajesImported, int ClientesImported, int ProductosImported, IReadOnlyList<string> Warnings)>
        ImportFromMdbAsync(string mdbPath, string usuario, CancellationToken ct = default);
}

public interface IAuthService
{
    Usuario? CurrentUser { get; }
    Task<(bool Success, string Message, Usuario? User)> LoginAsync(string username, string password, CancellationToken ct = default);
    void Logout();
    bool HasPermission(PermisoSistema permiso);
    void EnsurePermission(PermisoSistema permiso);
    IReadOnlyCollection<PermisoSistema> GetEffectivePermissions(Usuario usuario, PerfilUsuario? perfil = null);
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
}
