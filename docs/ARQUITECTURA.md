# Arquitectura Técnica · Gestión de Scrap v4.0 (.NET 10)

## 1. Visión General

**Gestión de Scrap v4.0** implementa una arquitectura limpia de cuatro capas desacopladas sobre **.NET 10 (`net10.0` / `net10.0-windows`)**, migrando la lógica histórica desde Excel/VBA + Access (`BD_SCRAP.mdb`) hacia una aplicación de escritorio Windows Forms de alto rendimiento con almacenamiento central **SQLite (modo WAL)** y conectividad directa TCP/IP hacia **Firebird**.

```
+-------------------------------------------------------------------------+
|                    GestionScrap.Desktop (WinForms UI)                   |
|  LoginForm · MainForm · 10 UserControls · 7 Diálogos · Theme/GDI+       |
+------------------------------------+------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                  GestionScrap.Application (Casos de Uso)                |
|  ScrapApplicationService · BusinessRules · SecuritySessionService       |
|  ReportDtos · RolePermissionMatrix · PasswordSecurity (PBKDF2-SHA256)   |
+------------------------------------+------------------------------------+
                                     |
                 +-------------------+-------------------+
                 |                                       |
                 v                                       v
+----------------------------------+   +----------------------------------+
|   GestionScrap.Domain (Núcleo)   |   |  GestionScrap.Infrastructure     |
|  Entidades · Enums · RutChileno  |   |  SqliteScrapRepository (WAL)     |
|  Interfaces de Repositorio/Serv. |   |  FirebirdSyncService (TCP 3050)  |
+----------------------------------+   |  ClosedXmlExcelExportService     |
                                       |  MdbMigrationService (x64/x86)   |
                                       |  AppSettingsStore (DPAPI)        |
+----------------------------------+   +----------------------------------+
```

## 2. Capas y Responsabilidades

### 2.1 `GestionScrap.Domain` (`net10.0`, sin dependencias externas)
- **Entidades:**
  - `Pesaje`: Registro de báscula con `Folio`, `Planta`, `TipoPesaje`, `Patente`, pesos (`PesoBruto`, `PesoTara`, `PesoNeto`), producto, cliente (`RutCliente`, `NomCliente`), trazabilidad de fechas/horas, `GrupoMaterial`, `EsTraslado` y `CiudadCliente`.
  - `Cliente`, `Producto`, `PrecioProveedor`, `IndexPrecioPesaje`, `UltimoFolioJcramm`: Catálogos operativos, tarifas pactadas, índice histórico congelado y puntero de cierre JCRAMM.
  - `Usuario`, `AuditLogEntry`, `FirebirdPlantaEndpoint`, `FirebirdRemotePesaje`, `AppSettings`: Modelos de seguridad, bitácora y configuración de red.
- **Enumeraciones:** `Planta` (`Placilla`, `Santiago`, `Talcahuano`), `TipoPesaje`, `RolUsuario` (`Administrador`, `Supervisor`, `Operador`, `Auditor`), `PermisoSistema` (13 permisos granulares), `ModoPrecio`, `FiltroInformePrecio`.
- **Objeto de Valor (`RutChileno`):** Normaliza puntos, guiones, ceros iniciales y variantes históricas de dígitos concatenados (`NormalizeBody`, `Clean`, `Matches`).
- **Contratos:** `IScrapRepository`, `IFirebirdSyncService`, `IExcelExportService`, `IMdbMigrationService`, `IAuthService`.

### 2.2 `GestionScrap.Application` (`net10.0`)
- **`BusinessRules`:** Encapsula toda la lógica de negocio verificable unitariamente:
  - Prevención de división por cero en promedio ponderado (`CalculateWeightedAverage`).
  - Detección de duplicados por planta (`Talcahuano` considera `Folio + HoraIngreso`; `Placilla` y `Santiago` consideran `Planta + Folio`).
  - Exclusión de traslados internos por planta (ej. RUT `15071375`, `13856570`, `96781200`).
  - Filtros de elegibilidad JCRAMM (`IsJcrammEligible`).
  - Reglas de clasificación de los 8 segmentos estratégicos (`Retail Santiago`, `Retail Empresas`, `Mix Supermercado Santiago`, `Mix Supermercado Placilla`, `Retail Regiones`, `Sector Agrícola`, `Sector Pesquero`, `Grupo Agrosuper Placilla`).
- **`SecuritySessionService`:** Autenticación PBKDF2-SHA256 (`100.000` iteraciones), bloqueo automático tras `5` intentos fallidos por `5` minutos y resolución de permisos efectivos.
- **`ScrapApplicationService`:** Caso de uso único de coordinación con validación previa `EnsurePermission(...)` antes de cualquier consulta, modificación, exportación o purga.

### 2.3 `GestionScrap.Infrastructure` (`net10.0`)
- **`SqliteScrapRepository`:** Motor embebido vía `Microsoft.Data.Sqlite` `10.0.15` + `SQLitePCLRaw.lib.e_sqlite3` `2.1.14`, activando `PRAGMA journal_mode=WAL;`, consultas 100% parametrizadas, índices compuestos y respaldo en caliente (`SqliteConnection.BackupDatabase`).
- **`FirebirdSyncService`:** Conectividad directa con `FirebirdSql.Data.FirebirdClient` `10.3.4` (verificación previa de puerto TCP 3050, normalización de rutas UNC, ejecución transaccional `BeginTransaction`/`Commit`/`Rollback` para `PA_INS_PROV_STGO` y `PA_INS_MASIVO_EXP_SAP`).
- **`ClosedXmlExcelExportService`:** Exportación nativa `.xlsx` mediante `ClosedXML` `0.105.1` sin dependencia de Microsoft Office COM.
- **`MdbMigrationService`:** Lector híbrido de archivos `.mdb`/`.accdb`: intenta `System.Data.OleDb` en 64 bits y, cuando solo existe el controlador `ACE/Jet` de 32 bits, delega la lectura segura con lista blanca de tablas hacia `SysWOW64\WindowsPowerShell\v1.0\powershell.exe`.
- **`AppSettingsStore`:** Persistencia de configuración local en `%LocalAppData%\GestionScrap\settings.json` cifrando contraseñas Firebird mediante **Windows DPAPI** (`ProtectedData.Protect`).

### 2.4 `GestionScrap.Desktop` (`net10.0-windows`, WinForms PerMonitorV2)
- **`AppTheme` & `IconFactory`:** Sistema visual centralizado con renderizado vectorial GDI+ para iconos y paletas homogéneas.
- **`DoubleBufferedControls`:** `DoubleBufferedDataGridView` y `DashboardChartControl` (área comparativa semanal, barras horizontales Top Proveedores, gráfico anillo por material).
- **Vistas modulares (`ScrapTabControlBase`):** Carga diferida con cambio de pestañas mediante visibilidad y `SuspendLayout`, además de 7 diálogos modales específicos.

## 3. Correcciones Respecto al Sistema Legacy (Excel/VBA + Access)

| Incidencia Legacy | Solución Implementada en v4.0 |
| :--- | :--- |
| Búsqueda por Folio comentada en Tab 2 | Búsqueda dual operativa por **RUT** o **Folio** en `InformeClienteControl`. |
| Colisión en grilla de Pesquera y Agrosuper (Tab 3) | 8 grillas y tarjetas independientes en `GruposEspecialesControl`. |
| Grupo Material Talcahuano consultando vista de Placilla | Filtrado estricto por `Planta.Talcahuano` en `GetGrupoMaterialAsync`. |
| `upd_precio_prov` ejecutando INSERT en lugar de UPDATE | `UpsertPrecioProveedorAsync` con modo explícito insertar vs actualizar. |
| División por cero en promedios con 0 kilos | Protección explícita en `BusinessRules.CalculateWeightedAverage`. |
| Consultas SQL concatenadas vulnerables | Consultas SQLite/Firebird parametrizadas y lista blanca en migrador MDB. |
| Corrupción de `BD_SCRAP.mdb` multiusuario | SQLite WAL con backups en caliente. |
