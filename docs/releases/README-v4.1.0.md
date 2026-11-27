# Gestión de Scrap v4.1.0 — Notas de Versión (Release Notes)

- **Versión publicada:** `v4.1.0` (`AssemblyVersion` / `FileVersion`: `4.1.0.0`)
- **Fecha de liberación:** `26-11-2026`
- **Versión estable anterior (referencia de recuperación):** `v4.0.0` (commit `c7641c55c9cada9ee9f0e4bc48a694ed5e215c4a`)
- **Plataforma objetivo:** Windows 10 / Windows 11 `x64` · `.NET 10` (`C# 14`, WinForms `PerMonitorV2`) · Inno Setup `7.1.1`
- **Repositorio y Releases:** https://github.com/barriagada666-collab/GESTION_SCRAP/releases/tag/v4.1.0

---

## 1. Resumen de Correcciones, Mejoras y Optimizaciones

### Base de Datos SQLite, Integridad y Rendimiento
- **Integridad Referencial Garantizada por Conexión (`F-02`):** `SqliteScrapRepository.OpenConnectionAsync` ahora ejecuta `PRAGMA foreign_keys = ON;` y `PRAGMA busy_timeout = 5000;` en **cada conexión abierta**, asegurando las eliminaciones en cascada (`ON DELETE CASCADE`) en el árbol `material_group_nodes` y previniendo contenciones de escritura.
- **Migraciones Versionadas e Idempotentes (`schema_migrations`):** Esquema versionado automáticamente (`v1` base + `v2` índices compuestos) probado sobre copia de base productiva real (`32.178` pesajes, `180.914.573 kg`), preservando el **100 % de los registros** sin pérdida de datos.
- **Eliminación de `TEMP B-TREE` y `FULL TABLE SCAN` (`F-05`):** Incorporación de 5 índices compuestos (`idx_pesajes_planta_fecha_folio`, `idx_pesajes_fecha_folio`, `idx_pesajes_folio_hora`, `idx_audit_accion_id`, `idx_audit_usuario_id`).
- **Resumen de Base de Datos ~15x Más Rápido:** `GetDatabaseResumenStatsAsync` reemplazado por agregación SQL directa (`GetDatabaseSummaryMetricsAsync`), reduciendo el tiempo medido sobre `32.178` registros de **`185,78 ms` a `~11 ms`** (**`-93,5 %`**) y evitando hidratar toda la tabla en memoria.
- **Importación Más Eficiente:** `ImportPesajesAsync` proyecta únicamente `(folio, hora_ingreso)` para deduplicar y reutiliza un comando `SqliteCommand` preparado dentro de la transacción.
- **Verificación de Integridad y Respaldos Post-Verificados:** Nuevo `VerifyIntegrityAsync` (`PRAGMA quick_check` + `PRAGMA foreign_key_check`) ejecutado en el arranque y expuesto vía RBAC (`VerifyDatabaseIntegrityAsync`); `BackupDatabaseAsync` ahora valida la integridad del archivo de respaldo recién creado antes de confirmarlo.
- **Resolución de Árbol `O(1)` con Protección de Ciclos:** `CollectDescendantCodes` en `ScrapApplicationService` optimizado con diccionario padre->hijos y conjunto de nodos visitados.

### Seguridad, Diagnóstico y Estabilidad UI
- **Protección del Último Administrador Activo:** `SqliteScrapRepository` impide eliminar, desactivar o degradar al último usuario activo perteneciente a un perfil con permiso `AdministrarSeguridadPerfiles`.
- **Logger Diagnóstico Diario Sanitizado (`FileAppLogger`):** Registro estructurado en `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log` con rotación diaria, retención automática de 30 días y redacción automática de contraseñas, tokens Bearer, GitHub PAT y cadenas `DPAPI:`.
- **Captura Global de Excepciones Asíncronas:** Suscripción a `TaskScheduler.UnobservedTaskException` junto con `Application.ThreadException` y `AppDomain.UnhandledException`.
- **Corrección GDI+ y Caché de Fuentes (`AppTheme`):** Corregido el riesgo de `Stream` desechado en `LoadEmbeddedImage` retornando un `Bitmap` independiente y reemplazadas instanciaciones redundantes de `Font` en formularios principales por recursos cacheados.
- **Metadatos Centralizados (`AppMetadata`) y Sanitización Documental:** Versión única leída desde `Directory.Build.props` (`4.1.0`), licencia oficial `MIT` (`LICENSE`) integrada en el instalador, y eliminación completa de credenciales en texto plano en `README.md` y `docs/*.md`.

### Calidad, Pruebas y CI/CD
- **Compilación Estricta:** `0` errores y `0` advertencias en `Debug` y `Release` (`TreatWarningsAsErrors=true`, `WarningLevel=9999`, cero supresiones).
- **Suite Automatizada Ampliada:** **`27/27` pruebas `xUnit` exitosas** (`+7` pruebas de migración, integridad referencial, último administrador, copia de base real de `32.178` filas, redacción de secretos y auditoría documental).
- **Pipelines Automatizados:** Flujo `ci.yml` endurecido (permisos mínimos, auditoría estática, vulnerabilidades NuGet `0/5`, compilación Debug/Release y TRX) y nuevo flujo `release.yml` con QA silencioso de instalador.

---

## 2. Requisitos del Sistema

| Modalidad | Requisitos |
| :--- | :--- |
| **Instalador Autocontenido (`.exe`)** | Windows 10 / Windows 11 `x64`. **No requiere instalar .NET previamente.** |
| **Instalador Framework-Dependent (`.exe`)** | Windows 10 / Windows 11 `x64` + **Microsoft Windows Desktop Runtime 10.0 (x64)**. |
| **Paquete Portable (`.zip`)** | Windows 10 / Windows 11 `x64`. Descomprimir y ejecutar `GestionScrap.exe`. |

---

## 3. Instrucciones de Instalación y Actualización

### Instalación Nueva
1. Descargue **`GestionScrap_Setup_v4.1.0_x64_SelfContained.exe`** (opción recomendada).
2. Verifique su integridad comparando el hash SHA-256 con la sección 4:
   ```powershell
   Get-FileHash .\GestionScrap_Setup_v4.1.0_x64_SelfContained.exe -Algorithm SHA256
   ```
3. Ejecute el instalador interactivamente o en modo desatendido:
   ```cmd
   GestionScrap_Setup_v4.1.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
   ```

### Actualización desde `v4.0.0`
1. Cierre **Gestión de Scrap** si se encuentra en ejecución.
2. *(Opcional recomendado)* Genere un respaldo de `%LocalAppData%\GestionScrap\gestion_scrap.db` desde **Seguridad y Perfiles (`Ctrl+5`) -> Respaldar SQLite (.db)**.
3. Ejecute el instalador `v4.1.0` sobre la instalación existente. El instalador reemplaza únicamente los binarios en `%LocalAppData%\Programs\GestionScrap` y **conserva intacta** la base de datos `%LocalAppData%\GestionScrap\gestion_scrap.db` y su configuración `settings.json`.
4. Al iniciar `v4.1.0`, se aplicarán automáticamente las migraciones `v1` y `v2` (`schema_migrations`) y los 5 índices compuestos sin pérdida de datos.

---

## 4. Archivos Incluidos y Verificación SHA-256

| Archivo | Tamaño (Bytes) | Tamaño (MB) | SHA-256 |
| :--- | ---: | ---: | :--- |
| `GestionScrap_Setup_v4.1.0_x64_SelfContained.exe` | `40.052.620` | `38,20 MB` | `3FA75272C684A4E20804C86BF74DA68848B5631C2004394418ED96AE48A37633` |
| `GestionScrap_Setup_v4.1.0_x64_FrameworkDependent.exe` | `5.659.965` | `5,40 MB` | `5FEBBE1574F4E1B7044DE1B0340C3DFB95BC5B6FA960D149B16848B1D62F7A99` |
| `GestionScrap_Portable_v4.1.0_win-x64.zip` | `56.694.741` | `54,07 MB` | `EF079C736E1659D55694D66167D64DD0D1B961F943E253506756F7AC2D6FFBC9` |
| `checksums_sha256.txt` | — | — | Listado oficial de sumas de verificación SHA-256 |
| `README-v4.1.0.md` | — | — | Este documento de notas de versión y recuperación |

---

## 5. Notas de Compatibilidad y Limitaciones Conocidas

- **Compatibilidad de Esquema SQLite:** Totalmente retrocompatible con bases `v4.0.0`. Las migraciones únicamente crean la tabla `schema_migrations` e índices no destructivos (`CREATE INDEX IF NOT EXISTS`).
- **Firma Digital Authenticode:** Los instaladores aún no cuentan con certificado corporativo EV Code Signing, por lo que Windows SmartScreen puede mostrar advertencia en equipos nuevos; verifique siempre el `SHA-256` oficial publicado en `checksums_sha256.txt`.
- **Importación Access `.mdb`/`.accdb`:** Requiere Access Database Engine (`ACE.OLEDB`) instalado en 64 o 32 bits en el equipo local cuando se ejecutan migraciones heredadas desde archivos Access.
- **Conectividad Firebird:** El acceso directo a servidores Firebird (`puerto 3050`) requiere alcance de red corporativa/VPN hacia el servidor configurado (por defecto `192.168.2.56`).

---

## 6. Procedimiento de Recuperación / Rollback a `v4.0.0`

Si fuera necesario revertir a la versión estable anterior **`v4.0.0`**:

1. **Restaurar código fuente (Git):**
   ```powershell
   git checkout c7641c55c9cada9ee9f0e4bc48a694ed5e215c4a
   ```
   *(También se conserva el respaldo offline completo en bundle Git: `GESTION_SCRAP_main_c7641c55c9cada9ee9f0e4bc48a694ed5e215c4a.bundle`.)*
2. **Restaurar binarios instalados:**
   - Desinstale `v4.1.0` desde **Aplicaciones instaladas** o ejecutando `"%LocalAppData%\Programs\GestionScrap\unins000.exe" /VERYSILENT` y reinstale el ejecutable anterior de `v4.0.0`.
3. **Restaurar base de datos (si se desea volver exactamente al snapshot previo):**
   - Cierre la aplicación, respalde el archivo actual y copie el respaldo `gestion_scrap_backup.db` (`SHA-256: 45E28FE49FEA719D63FA9164BBF3A7863CD29CC0CDC1862D57B4D6E60CEC2089`) hacia `%LocalAppData%\GestionScrap\gestion_scrap.db` (eliminando si existen `-wal` y `-shm` antiguos).
