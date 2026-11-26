# Arquitectura Técnica · Gestión de Scrap v4.0 (.NET 10)

## 1. Visión General

**Gestión de Scrap v4.0** implementa una arquitectura limpia de cuatro capas sobre **.NET 10 (`net10.0` / `net10.0-windows`)**, consolidando en cinco módulos enfocados el monitoreo operativo, clasificación jerárquica de materiales, analítica Pareto de proveedores, importación Firebird (`WMDBSPC.GDB`) / Access y seguridad dinámica basada en perfiles (**RBAC**).

```text
+-------------------------------------------------------------------------+
|                    GestionScrap.Desktop (WinForms UI)                   |
|  SplashForm · LoginForm · MainForm · 5 Módulos · 5 Diálogos · GDI+      |
|  AppTheme (Paleta Cambiaso) · IconFactory · Assets Embebidos (0 Ext.)   |
+------------------------------------+------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                  GestionScrap.Application (Casos de Uso)                |
|  ScrapApplicationService · BusinessRules · SecuritySessionService       |
|  DefaultRoleProfiles · PasswordSecurity (PBKDF2-SHA256) · ReportDtos    |
+------------------------------------+------------------------------------+
                                     |
                 +-------------------+-------------------+
                 |                                       |
                 v                                       v
+----------------------------------+   +----------------------------------+
|   GestionScrap.Domain (Núcleo)   |   |  GestionScrap.Infrastructure     |
|  Entidades · Enums · RutChileno  |   |  SqliteScrapRepository (WAL)     |
|  Contratos IScrapRepository /    |   |  FirebirdSyncService (TCP 3050)  |
|  IFirebirdSyncService / Export   |   |  ClosedXmlExcelExportService     |
+----------------------------------+   |  MdbMigrationService (x64/x86)   |
                                       |  AppSettingsStore (DPAPI)        |
+----------------------------------+   +----------------------------------+
```

## 2. Capas y Responsabilidades

### 2.1 `GestionScrap.Domain` (`net10.0`, cero dependencias externas)
- **Entidades:**
  - `Pesaje`: Registro de báscula con `Folio`, `Planta`, `TipoPesaje`, `Patente`, pesos (`PesoBruto`, `PesoTara`, `PesoNeto`), `CodProducto`, `NomProducto`, `GrupoMaterial`, `RutCliente`, `NomCliente`, trazabilidad de fechas/horas, `Origen`, `Destino`, `Chofer` y `EsTraslado`.
  - `Cliente` y `Producto`: Catálogos operativos sincronizados desde Firebird / Access o inicializados con los `48` productos reales de `WMDBSPC.GDB`.
  - `MaterialGroupNode`: Nodo jerárquico editable (`Id`, `ParentId`, `Nombre`, `TipoNodo`, `CodigoProducto`, `ColorHex`, `Orden`).
  - `PerfilUsuario`, `Usuario`, `AuditLogEntry`, `FirebirdPlantaEndpoint`, `AppSettings`: Modelos de perfiles RBAC, usuarios, auditoría y configuración.
- **Enumeraciones:** `Planta`, `TipoPesaje`, `PermisoSistema` (`9` permisos granulares), `NodoGrupoTipo` (`CarpetaGrupo`, `ProductoAsignado`).
- **Objeto de Valor (`RutChileno`):** Normalización de RUT chileno (`NormalizeBody`, `Clean`, `Matches`).
- **Contratos:** `IScrapRepository`, `IFirebirdSyncService`, `IExcelExportService`, `IMdbMigrationService`, `IAuthService`.

### 2.2 `GestionScrap.Application` (`net10.0`)
- **`BusinessRules`:**
  - Reglas por defecto de agrupación en `DefaultParentGroups` (`8` ramas iniciales) y clasificación `ClassifyProductoDefaultGroup`.
  - Resolución recursiva de árbol (`BuildProductToRootGroupMap`) y consolidación jerárquica por nodo/rama.
  - Detección de duplicados (`IsDuplicatePesaje`: `Planta + Folio` en Placilla/Santiago; `Planta + Folio + HoraIngreso` en Talcahuano).
  - Cálculo Pareto ABC y variación temporal de proveedores.
- **`SecuritySessionService` y `DefaultRoleProfiles`:**
  - `5` perfiles iniciales (`Administrador`, `Supervisor`, `Analista`, `Operador`, `Auditor`) editables dinámicamente y extensibles con nuevos perfiles personalizados.
  - Hashing `PBKDF2-SHA256` (`100.000` iteraciones, comparación en tiempo constante) y bloqueo temporal tras `5` intentos fallidos.
- **`ScrapApplicationService`:** Puerta de acceso única con validación previa `_auth.EnsurePermission(...)` en cada operación de lectura, edición, exportación o administración.

### 2.3 `GestionScrap.Infrastructure` (`net10.0`)
- **`SqliteScrapRepository`:** Persistencia embebida vía `Microsoft.Data.Sqlite` `10.0.15` + `SQLitePCLRaw.lib.e_sqlite3` `2.1.14` con `PRAGMA journal_mode=WAL;`, consultas parametrizadas, inicialización automática del administrador `barriagada` / `Cambias0` y respaldo en caliente (`BackupDatabaseAsync`).
- **`FirebirdSyncService`:** Conector TCP/IP (`FirebirdSql.Data.FirebirdClient` `10.3.4`) configurado con `Charset=NONE` y `CodePagesEncodingProvider` para interoperar limpiamente con `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB`, `placilla\WMDBSPC.GDB` y `thno\PWAREHOUSE.FDB` preservando caracteres latinos (`VIÑA`).
- **`ClosedXmlExcelExportService`:** Exportación `.xlsx` sin dependencia de Microsoft Excel COM mediante `ClosedXML` `0.105.1`.
- **`MdbMigrationService`:** Lectura directa vía `System.Data.OleDb` (64 bits) o puente seguro x86 PowerShell (`SysWOW64`) cuando solo existe Access Database Engine de 32 bits.
- **`AppSettingsStore`:** Persistencia de configuración local en `%LocalAppData%\GestionScrap\settings.json` con protección **Windows DPAPI** para contraseñas Firebird.

### 2.4 `GestionScrap.Desktop` (`net10.0-windows`, WinForms `PerMonitorV2`)
- **Identidad Visual Cambiaso Sin Dependencias Externas:** Paleta corporativa (`#0B253A`, `#0B3C5D`, `#0891B2`, `#0E608A`, `#C9A46E`) y recursos gráficos (`app_icon.ico`, `app_logo.png`, `cambiaso_emblem.png`, `cambiaso_wordmark.png`) embebidos como `EmbeddedResource` en el ensamblado principal junto a iconos vectoriales GDI+ (`IconFactory`).
- **Componentes de UI:** `SplashForm`, `LoginForm`, `MainForm`, `5` controles modulares (`InformeResumenControl`, `GrupoMaterialControl`, `AnalisisProveedoresControl`, `ImportadorControl`, `SeguridadPerfilesControl`) y `5` diálogos (`MaterialNodeEditorDialog`, `MoverNodoDialog`, `ConfiguracionDbDialog`, `CambiarPasswordDialog`, `LimpiarTablasDialog`).
