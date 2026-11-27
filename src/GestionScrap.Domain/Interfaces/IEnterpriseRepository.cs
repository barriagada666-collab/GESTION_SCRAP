using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Domain.Interfaces;

/// <summary>
/// Contrato de persistencia extendido para CRM (clientes/proveedores unificados), materiales,
/// vehículos/patentes, tipos de pesaje, pesajes industriales (1 y 2 pasos), operaciones ERP,
/// libro mayor de inventario y documentos asociados.
/// </summary>
public interface IEnterpriseRepository
{
    // CRM Entidades (Clientes / Proveedores unificados por RUT)
    Task<IReadOnlyList<Cliente>> GetEntidadesComercialesAsync(RolEntidadComercial? rol = null, bool? soloActivos = null, string? search = null, CancellationToken ct = default);
    Task<Cliente?> GetEntidadByRutAsync(string rut, CancellationToken ct = default);
    Task<Cliente> SaveEntidadComercialAsync(Cliente entidad, string actor, CancellationToken ct = default);
    Task SetEntidadActivoAsync(string rut, bool activo, string actor, CancellationToken ct = default);
    Task DeleteEntidadSafelyAsync(string rut, string actor, CancellationToken ct = default);

    // Documentos Asociados
    Task<IReadOnlyList<DocumentoAsociado>> GetDocumentosAsync(string? rutEntidad = null, long? operacionId = null, long? pesajeId = null, CancellationToken ct = default);
    Task<DocumentoAsociado> SaveDocumentoAsync(DocumentoAsociado doc, string actor, CancellationToken ct = default);

    // Materiales (Productos extendidos)
    Task<IReadOnlyList<Producto>> GetMaterialesAsync(bool? soloActivos = null, CancellationToken ct = default);
    Task<Producto?> GetMaterialByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Producto> SaveMaterialAsync(Producto material, string actor, CancellationToken ct = default);
    Task SetMaterialActivoAsync(string codigo, bool activo, string actor, CancellationToken ct = default);
    Task DeleteMaterialSafelyAsync(string codigo, string actor, CancellationToken ct = default);

    // Vehículos y Patentes
    Task<IReadOnlyList<Vehiculo>> GetVehiculosAsync(bool? soloActivos = null, string? patenteSearch = null, CancellationToken ct = default);
    Task<Vehiculo?> GetVehiculoByPatenteAsync(string patente, CancellationToken ct = default);
    Task<Vehiculo> SaveVehiculoAsync(Vehiculo vehiculo, string actor, CancellationToken ct = default);
    Task SetVehiculoActivoAsync(long id, bool activo, string actor, CancellationToken ct = default);
    Task DeleteVehiculoSafelyAsync(long id, string actor, CancellationToken ct = default);

    // Configuración de Tipos de Pesaje
    Task<IReadOnlyList<TipoPesajeConfig>> GetTiposPesajeConfigAsync(bool? soloActivos = null, CancellationToken ct = default);
    Task<TipoPesajeConfig> SaveTipoPesajeConfigAsync(TipoPesajeConfig config, string actor, CancellationToken ct = default);
    Task SetTipoPesajeConfigActivoAsync(long id, bool activo, string actor, CancellationToken ct = default);

    // Operaciones Comerciales ERP
    Task<IReadOnlyList<OperacionComercial>> GetOperacionesAsync(TipoOperacionComercial? tipo = null, EstadoOperacion? estado = null, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default);
    Task<OperacionComercial?> GetOperacionByIdAsync(long id, CancellationToken ct = default);
    Task<OperacionComercial> SaveOperacionAsync(OperacionComercial op, string actor, CancellationToken ct = default);
    Task UpdateOperacionEstadoAsync(long id, EstadoOperacion nuevoEstado, string motivo, string actor, CancellationToken ct = default);

    // Pesajes Industriales (CRUD, 2 mediciones, correcciones y anulaciones auditadas)
    Task<Pesaje?> GetPesajeByIdAsync(long id, CancellationToken ct = default);
    Task<long> GetNextFolioAsync(Planta planta, CancellationToken ct = default);
    Task<Pesaje> SavePesajeIndustrialAsync(Pesaje pesaje, string actor, string accionAuditoria, string? motivoCambio = null, CancellationToken ct = default);
    Task<IReadOnlyList<PesajeHistorialCambio>> GetPesajeHistorialAsync(long pesajeId, CancellationToken ct = default);

    // Inventario y Movimientos (Ledger Inmutable + Idempotencia por Pesaje)
    Task<IReadOnlyList<InventarioExistencia>> GetExistenciasInventarioAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventarioMovimiento>> GetMovimientosInventarioAsync(string? codProducto = null, long? operacionId = null, int limit = 500, CancellationToken ct = default);
    Task<InventarioMovimiento?> PostInventarioParaPesajeConfirmadoAsync(Pesaje pesaje, TipoMovimientoInventario tipo, long cantidadFirmadaKg, string actor, CancellationToken ct = default);
    Task<InventarioMovimiento?> RevertirInventarioParaPesajeAnuladoAsync(Pesaje pesaje, string motivo, string actor, CancellationToken ct = default);
    Task<InventarioMovimiento> RegistrarAjusteInventarioAsync(string codProducto, long cantidadFirmadaKg, string documentoRef, string motivo, string actor, CancellationToken ct = default);
}
