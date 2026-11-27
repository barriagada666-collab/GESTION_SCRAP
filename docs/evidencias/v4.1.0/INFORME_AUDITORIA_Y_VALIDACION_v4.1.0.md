# Informe de Auditoría Integral, Optimización y Validación — Gestión de Scrap v4.1.0

- **Proyecto:** Gestión de Scrap y Pesajes (`GESTION_SCRAP`) — Cambiaso Hnos. S.A.C.
- **Fecha de ejecución:** `26-11-2026`
- **Versión estable origen:** `v4.0.0` (`main@c7641c55c9cada9ee9f0e4bc48a694ed5e215c4a`)
- **Versión candidata validada:** `v4.1.0` (`4.1.0.0`)
- **Rama de trabajo:** `chore/audit-optimize-v4.1.0` -> integrada en `main` mediante merge `--no-ff`
- **Entorno verificado:** Windows x64 · `.NET SDK 10.0.404` · `Git 2.47.1` · `Inno Setup 7.1.1` · `Python 3 + sqlite3`

---

## 1. Resumen Ejecutivo y Matriz de Aprobación

| Etapa | Estado | Evidencia Verificada |
| :--- | :---: | :--- |
| **1. Auditoría y Diagnóstico Integral** | **APROBADA** | Inventario de 5 proyectos, 0 vulnerabilidades NuGet, auditoría SQLite real (`32.178` pesajes) y matriz de hallazgos. |
| **2. Respaldo y Protección `v4.0.0`** | **APROBADA** | Bundle Git completo + copia verificada de `gestion_scrap.db` (`SHA-256: 45E28FE4...`) fuera del repositorio. |
| **3. Versionado y Estrategia Git** | **APROBADA** | Rama independiente `chore/audit-optimize-v4.1.0` desde `main@c7641c5`; commits descriptivos; merge `--no-ff` + tag `v4.1.0`. |
| **4. Captura de Errores y Logging** | **APROBADA** | `FileAppLogger` diario con redacción automática de secretos + `TaskScheduler.UnobservedTaskException`. |
| **5. Correcciones de Código y Arquitectura** | **APROBADA** | FK SQLite por conexión, último administrador protegido, GDI+ detached `Bitmap`, `AppMetadata`, `0` supresiones. |
| **6. Base de Datos y Migraciones** | **APROBADA** | `schema_migrations` (`v1`/`v2`), 5 índices compuestos, `quick_check=ok`, `foreign_key_check=[]`, 100 % registros preservados. |
| **7. Optimización de Rendimiento** | **APROBADA** | `GetDatabaseResumenStatsAsync`: `185,78 ms -> ~11 ms` (`-93,5 %`), `0` `TEMP B-TREE`, `0` `SCAN audit_log`. |
| **8. Seguridad y Buenas Prácticas** | **APROBADA** | `0` contraseñas en texto plano en `README.md`/`docs/`, `0` tokens en repositorio, `0` vulnerabilidades NuGet, `LICENSE` MIT. |
| **9. Compilación `Debug` y `Release`** | **APROBADA** | `Debug`: `0` errores / `0` advertencias (`20,99 s`); `Release`: `0` errores / `0` advertencias (`19,48 s`). |
| **10. Pruebas Automatizadas (`xUnit`)** | **APROBADA** | **`27/27` exitosas** (`0` fallidas, `0` omitidas) en `5,85 s` (`TRX` + log completo). |
| **11. Empaquetado y QA de Instaladores** | **APROBADA** | `SelfContained` (`38,20 MB`), `FrameworkDependent` (`5,40 MB`) y `Portable ZIP` (`54,07 MB`) instalados, actualizados, arrancados y desinstalados limpiamente. |
| **12. CI/CD GitHub Actions** | **APROBADA** | `ci.yml` endurecido (`checkout@v6`, `setup-dotnet@v5`, permisos mínimos, auditoría recursiva) + nuevo `release.yml`. |
| **13. Documentación y Release Notes** | **APROBADA** | `README.md`, `docs/releases/README-v4.1.0.md`, `docs/CHANGELOG.md` y manuales técnicos actualizados y sanitizados. |

---

## 2. Hallazgos Detectados y Resoluciones Aplicadas

| ID | Gravedad | Módulo / Archivo | Hallazgo | Corrección Aplicada |
| :---: | :---: | :--- | :--- | :--- |
| **F-01** | **ALTA** | `README.md`, `docs/*.md` | Credencial administrativa inicial expuesta en texto plano en documentación pública. | Eliminada de `README.md` y los 6 archivos de `docs/`; agregada prueba automatizada anti-regresión. |
| **F-02** | **ALTA** | `SqliteScrapRepository.cs` | `PRAGMA foreign_keys = ON` no se ejecutaba al abrir cada conexión SQLite, desactivando cascadas FK en instancias nuevas. | `OpenConnectionAsync` ahora ejecuta `PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;` en cada conexión + test `ForeignKeysCascade_EnforcedOnFreshConnectionWithoutPriorInit`. |
| **F-03** | **MEDIA** | `SqliteScrapRepository.cs` | Posibilidad de eliminar, desactivar o degradar al último administrador activo del sistema. | Implementada guarda `EnsureNotRemovingLastActiveAdminAsync` + test automatizado. |
| **F-04** | **MEDIA** | `AppTheme.cs` | `LoadEmbeddedImage` retornaba `Image.FromStream(copy)` mientras `copy` se desechaba por `using`, riesgo GDI+ en operaciones posteriores. | Retorno de `new Bitmap(loaded)` independiente del stream + caché de fuentes `Font`. |
| **F-05** | **MEDIA** | `SqliteScrapRepository.cs`, `ScrapApplicationService.cs` | `EXPLAIN QUERY PLAN` mostraba `TEMP B-TREE` en `pesajes` y `SCAN audit_log`; `GetDatabaseResumenStatsAsync` cargaba toda la tabla en RAM; `ImportPesajesAsync` leía 26 columnas por fila. | Migración `v2` con 5 índices compuestos, `GetDatabaseSummaryMetricsAsync` SQL agregado (`-93,5 %` latencia), proyección `(folio, hora_ingreso)` + `SqliteCommand` preparado, `GetPerfilByIdAsync` directo y `CollectDescendantCodes` en `O(1)`. |
| **F-06** | **MEDIA** | `Program.cs`, `FileAppLogger.cs` | Sin rotación diaria de logs ni captura de `TaskScheduler.UnobservedTaskException` ni redacción automática de secretos. | Implementado `FileAppLogger` (rotación diaria, 30 días, regex de redacción), `UnobservedTaskException` y verificación de integridad SQLite en arranque. |
| **F-07** | **BAJA** | `MainForm`, `SplashForm`, `LoginForm`, `GestionScrap.iss`, `LICENSE` | Versiones `"v4.0"` codificadas en texto fijo, ausencia de `LICENSE` y `<RuntimeIdentifiers>` faltante en Desktop. | Creado `AppMetadata`, agregada `LICENSE` MIT, añadido `RuntimeIdentifiers=win-x64`, parametrizado `GestionScrap.iss` y `scripts/Build-Release.ps1`. |
| **F-08** | **BAJA** | `.github/workflows/` | Solo existía CI básico sin puerta de vulnerabilidades, sin auditoría estática ni pipeline de Release. | Endurecido `ci.yml` y creado `release.yml` con QA silencioso de instalador y publicación segura. |

---

## 3. Evidencia Comparativa de Base de Datos (Antes vs. Después)

Evaluado sobre copia aislada de la base productiva (`32.178` pesajes · `180.914.573 kg` · `546` clientes · `52` productos · `9.322.496 bytes`), dejando el archivo productivo local **100 % intacto** (`SHA-256: 45E28FE49FEA719D63FA9164BBF3A7863CD29CC0CDC1862D57B4D6E60CEC2089`):

| Indicador | Antes (`v4.0.0`) | Después (`v4.1.0`) | Mejora |
| :--- | :--- | :--- | :--- |
| `EXPLAIN` consulta `pesajes` (planta + rango + orden) | `USING INDEX idx_pesajes_planta_fecha` + **`USE TEMP B-TREE FOR LAST TERM OF ORDER BY`** | **`SEARCH pesajes USING INDEX idx_pesajes_planta_fecha_folio`** | `TEMP B-TREE` eliminado |
| `EXPLAIN` consulta `audit_log` por acción | **`SCAN audit_log`** (Full Table Scan) | **`SEARCH audit_log USING INDEX idx_audit_accion_id`** | Index Seek directo |
| Resumen de base de datos (`32.178` filas) | `185,78 ms` (carga completa de `32.178` entidades en RAM) | **`10,09 – 11,99 ms`** (agregación SQL directa) | **`~15x` más rápido (`-93,5 %`)** |
| `PRAGMA foreign_keys` por conexión | Desactivado fuera de `InitializeAsync` | **Activo (`ON`) en cada conexión** | Integridad FK garantizada |
| `PRAGMA quick_check` / `foreign_key_check` | `ok` / `0` violaciones | `ok` / `0` violaciones (`schema_migrations=2`) | 100 % datos preservados |

---

## 4. Verificación de Artefactos de Distribución y QA Silencioso

| Artefacto | Tamaño | SHA-256 | Resultado QA |
| :--- | ---: | :--- | :--- |
| `GestionScrap_Setup_v4.1.0_x64_SelfContained.exe` | `40.052.620 bytes` (`38,20 MB`) | `3FA75272C684A4E20804C86BF74DA68848B5631C2004394418ED96AE48A37633` | **APROBADA** (Install `292` archivos, `FileVersion=4.1.0.0`, Upgrade in-place, Startup Log OK, Uninstall limpio, `0` `.pdb`/`.xml`) |
| `GestionScrap_Setup_v4.1.0_x64_FrameworkDependent.exe` | `5.659.965 bytes` (`5,40 MB`) | `5FEBBE1574F4E1B7044DE1B0340C3DFB95BC5B6FA960D149B16848B1D62F7A99` | **APROBADA** (Install `26` archivos, `FileVersion=4.1.0.0`, Uninstall limpio, `0` `.pdb`/`.xml`) |
| `GestionScrap_Portable_v4.1.0_win-x64.zip` | `56.694.741 bytes` (`54,07 MB`) | `EF079C736E1659D55694D66167D64DD0D1B961F943E253506756F7AC2D6FFBC9` | **APROBADA** (Extracción `290` archivos, `FileVersion=4.1.0.0`, `coreclr.dll` + `e_sqlite3.dll`, `0` `.pdb`/`.xml`) |

---

## 5. Control de Calidad y Seguridad Final

- **Compilación `Debug`:** `0` errores, `0` advertencias (`20,99 s`).
- **Compilación `Release`:** `0` errores, `0` advertencias (`19,48 s`).
- **Supresiones prohibidas (`NoWarn`, `#pragma warning disable`, `SuppressMessage`):** **`0`**.
- **Métodos `async void` en `src/`:** **`0`**.
- **Vulnerabilidades NuGet (`--vulnerable --include-transitive`):** **`0`** en los 5 proyectos.
- **Secretos o credenciales en código/documentación/workflows:** **`0`**.
- **Base de datos productiva local durante todo el proceso:** **`SHA-256` idéntico antes y después (`45E28FE49FEA719D63FA9164BBF3A7863CD29CC0CDC1862D57B4D6E60CEC2089`)**.
