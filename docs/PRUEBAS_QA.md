# Plan y Evidencia de Pruebas y QA · Gestión de Scrap v4.1.0

## 1. Estrategia de Verificación (`GestionScrap.Tests`)

Suite automatizada en **xUnit (`2.9.3`)** sobre **.NET 10** bajo compilación estricta (`TreatWarningsAsErrors=true`, `WarningLevel=9999`, cero supresiones):

| Categoría | Archivo de Pruebas | Alcance Verificado |
| :--- | :--- | :--- |
| **Unit Testing** | [BusinessRulesAndSecurityTests.cs](../tests/GestionScrap.Tests/Unit/BusinessRulesAndSecurityTests.cs) | `RutChileno`, comparativos **YoY** / **MoM**, ficha estadística (min/max/mediana/desv. estándar), segmentación por dimensión con **exclusión de ceros**, árbol padre/hijo con exclusión de ceros, detección de duplicados, hashing PBKDF2-SHA256 y perfiles RBAC. |
| **Integration & Workflow** | [SqliteAndTreeWorkflowIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/SqliteAndTreeWorkflowIntegrationTests.cs) | Autenticación inicial, bloqueo tras `5` intentos, CRUD dinámico de `PerfilUsuario`, árbol CRUD de materiales, trazabilidad `ImportFirebirdPackageAsync` -> `GetImportHistoryAsync` (`Origen -> Destino`), `ExportReportePersonalizado`, `ExportImportHistory`, respaldo en caliente y purga `"ELIMINAR"`. |
| **Migrations, DB & Security Audit** | [AuditMigrationsAndReliabilityTests.cs](../tests/GestionScrap.Tests/Integration/AuditMigrationsAndReliabilityTests.cs) | Migraciones versionadas (`schema_migrations`), eliminación de `TEMP B-TREE` y `FULL SCAN` vía `EXPLAIN QUERY PLAN`, `PRAGMA foreign_keys=ON` por conexión (cascada en árbol), protección del último administrador activo, migración segura sobre copia de base real (`32.178` pesajes), redacción de secretos en `FileAppLogger`, metadatos dinámicos (`AppMetadata`) y ausencia de contraseñas en documentación pública. |
| **Real Databases (Firebird & Access)** | [RealDatabasesIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/RealDatabasesIntegrationTests.cs) | Conexión e importación real desde `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` (caracteres `VIÑA`) y migración real desde `BD_SCRAP.mdb` cuando están disponibles. |
| **UI / UX Testing (STA)** | [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) | Auditoría STA completa de `SplashForm`, `LoginForm`, `MainForm`, los `5` módulos (`InformeResumenControl`, `GrupoMaterialControl`, `AnalisisProveedoresControl`, `ImportadorControl`, `SeguridadPerfilesControl`) y los `6` diálogos modales (incluyendo `ExportacionPersonalizadaDialog`): texto, icono, `AccessibleName`, evento `Click`/`DialogResult`, bounds y restricciones por perfil. |
| **Stress & Performance** | [PerformanceAndStressTests.cs](../tests/GestionScrap.Tests/Stress/PerformanceAndStressTests.cs) | Inserción masiva de **10.000** pesajes en SQLite WAL, 100 % deduplicación y consultas analíticas concurrentes en `< 10 s`. |

## 2. Criterios de Aceptación Verificados

- **Compilación `Debug` y `Release`:** `0 errores`, `0 advertencias`.
- **Suite Automatizada (`dotnet test -c Release`):** **`27/27` pruebas exitosas** (`0` fallidas, `0` omitidas).
- **Empaquetado Inno Setup 7 (`7.1.1`) + Portable ZIP:** Compilación sin advertencias de variantes `SelfContained`, `FrameworkDependent` y paquete `Portable`, instalación silenciosa, arranque verificado, actualización conservando `gestion_scrap.db` íntegra y desinstalación limpia.
