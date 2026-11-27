# Plan y Evidencia de Pruebas y QA · Gestión de Scrap v4.0

## 1. Estrategia de Verificación (`GestionScrap.Tests`)

Suite automatizada en **xUnit (`2.9.3`)** sobre **.NET 10** bajo compilación estricta (`TreatWarningsAsErrors=true`, `WarningLevel=9999`):

| Categoría | Archivo de Pruebas | Alcance Verificado |
| :--- | :--- | :--- |
| **Unit Testing** | [BusinessRulesAndSecurityTests.cs](../tests/GestionScrap.Tests/Unit/BusinessRulesAndSecurityTests.cs) | `RutChileno`, comparativos **YoY** / **MoM**, ficha estadística (min/max/mediana/desv. estándar), segmentación por dimensión con **exclusión de ceros**, árbol padre/hijo con exclusión de ceros, detección de duplicados, hashing PBKDF2-SHA256 y perfiles RBAC. |
| **Integration & Workflow** | [SqliteAndTreeWorkflowIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/SqliteAndTreeWorkflowIntegrationTests.cs) | Autenticación `barriagada`/`Cambias0`, bloqueo tras `5` intentos, CRUD dinámico de `PerfilUsuario`, árbol CRUD de materiales, trazabilidad `ImportFirebirdPackageAsync` -> `GetImportHistoryAsync` (`Origen -> Destino`), `ExportReportePersonalizado`, `ExportImportHistory`, respaldo en caliente y purga `"ELIMINAR"`. |
| **Real Databases (Firebird & Access)** | [RealDatabasesIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/RealDatabasesIntegrationTests.cs) | Conexión e importación real desde `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` (caracteres `VIÑA`) y migración real desde `BD_SCRAP.mdb` cuando están disponibles. |
| **UI / UX Testing (STA)** | [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) | Auditoría STA completa de `SplashForm`, `LoginForm`, `MainForm`, los `5` módulos (`InformeResumenControl`, `GrupoMaterialControl`, `AnalisisProveedoresControl`, `ImportadorControl`, `SeguridadPerfilesControl`) y los `6` diálogos modales (incluyendo `ExportacionPersonalizadaDialog`): texto, icono, `AccessibleName`, evento `Click`/`DialogResult`, bounds y restricciones por perfil. |
| **Stress & Performance** | [PerformanceAndStressTests.cs](../tests/GestionScrap.Tests/Stress/PerformanceAndStressTests.cs) | Inserción masiva de **10.000** pesajes en SQLite WAL, 100 % deduplicación y consultas analíticas concurrentes en `< 10 s`. |

## 2. Criterios de Aceptación Verificados

- **Compilación `Debug` y `Release`:** `0 errores`, `0 advertencias`.
- **Suite Automatizada (`dotnet test -c Release`):** **`20/20` pruebas exitosas** (`0` fallidas, `0` omitidas).
- **Empaquetado Inno Setup 7 (`7.1.1`):** Compilación sin advertencias de variantes `SelfContained` y `FrameworkDependent`, instalación silenciosa, arranque verificado, actualización conservando `gestion_scrap.db` y desinstalación limpia.
