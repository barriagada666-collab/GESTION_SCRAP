# Arquitectura Técnica · Gestión de Scrap v5.0.0 (.NET 10)

## 1. Visión General

**Gestión de Scrap v5.0.0** implementa una arquitectura limpia de cuatro capas sobre **.NET 10 (`net10.0` / `net10.0-windows`)**, consolidando en **8 módulos integrados** la gestión unificada **CRM (Clientes/Proveedores)**, **Operaciones ERP**, **Pesajes Industriales (1 y 2 mediciones)**, **Flota de Vehículos/Patentes**, **Libro Mayor Inmutable de Inventario**, **Centro de Informes con 9 segmentadores multi-selección**, **Árbol de Grupos de Material**, **Análisis Pareto ABC**, **Importador Firebird/Access** y **Seguridad RBAC (`13` permisos)**.

```text
+-----------------------------------------------------------------------------+
|                      GestionScrap.Desktop (WinForms UI)                     |
|  SplashForm · LoginForm · MainForm (8 Módulos Ctrl+1..8) · 15 Diálogos      |
|  MultiSelectSlicerButton · ContextMenuStrip · AppTheme High-DPI · GDI+      |
+--------------------------------------+--------------------------------------+
                                       |
                                       v
+-----------------------------------------------------------------------------+
|                    GestionScrap.Application (Casos de Uso)                  |
|  EnterpriseApplicationService · WeighingRules · ScrapApplicationService     |
|  BusinessRules · SecuritySessionService · DefaultRoleProfiles · DTOs        |
+--------------------------------------+--------------------------------------+
                                       |
                   +-------------------+-------------------+
                   |                                       |
                   v                                       v
+------------------------------------+   +------------------------------------+
|     GestionScrap.Domain (Núcleo)   |   |    GestionScrap.Infrastructure     |
|  Entidades CRM/ERP/Pesajes/Flota/  |   |  SqliteScrapRepository (WAL v3)    |
|  Inventario · RutChileno (Mod-11)  |   |  FirebirdSyncService (TCP 3050)    |
|  PatenteChilena · IScrapRepository |   |  ClosedXmlExcelExportService       |
|  IEnterpriseRepository · Enums     |   |  MdbMigrationService (x64/x86)     |
+------------------------------------+   |  AppSettingsStore (DPAPI) · Logger |
                                         +------------------------------------+
```

## 2. Capas y Responsabilidades

### 2.1 `GestionScrap.Domain` (`net10.0`, cero dependencias externas)
- **Entidades:** `Pesaje` (extendido con `FolioCodigo`, `UnidadMedida`, `Estado`, `OperacionId`, `VehiculoId`, `PrimeraMedicionKg/Utc`, `SegundaMedicionKg/Utc`, `PesoEsperadoKg`, `AjusteManualAutorizado`), `Cliente` (CRM unificado `EsCliente`/`EsProveedor`/`RutNormalizado`), `Producto` (catálogo extendido con `ToleranciaDiferenciaPct` y `Activo`), `Vehiculo`, `TipoPesajeConfig`, `OperacionComercial`, `InventarioMovimiento`, `InventarioExistencia`, `PesajeHistorialCambio`, `DocumentoAsociado`, `MaterialGroupNode`, `PerfilUsuario`, `Usuario`, `AuditLogEntry`, `ImportacionHistorial`.
- **Objetos de Valor:** `RutChileno` (`Clean`, `NormalizeBody`, `Matches`, `ComputeDv`, `ValidateAndFormat`) y `PatenteChilena` (`Normalize`, `IsValidFormat`, `FormatDisplay`, `MatchesExact`, `MatchesPartial`).
- **Contratos:** `IScrapRepository`, `IEnterpriseRepository`, `IFirebirdSyncService`, `IExcelExportService`, `IMdbMigrationService`, `IAuthService`, `IAppLogger`.

### 2.2 `GestionScrap.Application` (`net10.0`)
- **`WeighingRules`:** Reglas puras deterministas de cálculo `Neto = Bruto - Tara`, validación de consistencia de unidades (`KG`/`TON`), bloqueo de sobrescritura manual sin autorización, resolución de 2 mediciones (`Bruto = max`, `Tara = min`), evaluación de tolerancias y determinación del signo de impacto en inventario.
- **`EnterpriseApplicationService`:** Casos de uso CRM 360°, materiales, flota, tipos de pesaje, pesajes de 1 y 2 pasos, autorizaciones/correcciones/anulaciones, órdenes ERP, libro mayor de inventario y motor unificado de informes con 9 segmentadores multi-selección.
- **`ScrapApplicationService` y `BusinessRules`:** Analítica ejecutiva YoY/MoM, árbol jerárquico de materiales, Pareto ABC de proveedores, importación Firebird/Access y administración RBAC.

### 2.3 `GestionScrap.Infrastructure` (`net10.0`)
- **`SqliteScrapRepository` (partial + `.Enterprise.cs`):** Persistencia embebida SQLite WAL con `PRAGMA foreign_keys = ON` y `busy_timeout = 5000` por conexión, migraciones idempotentes `v1 -> v2 -> v3`, índice único parcial `idx_inv_mov_pesaje_activo` garantizando idempotencia de inventario por pesaje, respaldo post-verificado y verificación `PRAGMA quick_check` + `foreign_key_check`.
- **`FirebirdSyncService`, `MdbMigrationService`, `ClosedXmlExcelExportService`, `AppSettingsStore` (DPAPI), `FileAppLogger` (redacción automática de secretos).**

### 2.4 `GestionScrap.Desktop` (`net10.0-windows`, WinForms `PerMonitorV2`)
- **8 Módulos Modulares (`Ctrl+1..Ctrl+8`):** `InformeResumenControl`, `CentroInformesEnterpriseControl`, `OperacionesPesajesErpControl`, `CrmMaestrosControl`, `GrupoMaterialControl`, `AnalisisProveedoresControl`, `ImportadorControl` y `SeguridadPerfilesControl`.
- **15 Diálogos Modales High-DPI:** Todos construidos con `TableLayoutPanel` y `AppTheme.CreateDialogFooter` sin desbordamientos, acompañados de `MultiSelectSlicerButton` y menús contextuales `ContextMenuStrip` en todas las grillas operacionales.
