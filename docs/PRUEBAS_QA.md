# Plan y Evidencia de Pruebas y QA · Gestión de Scrap v4.0

## 1. Estrategia de Verificación (`GestionScrap.Tests`)

Suite automatizada en **xUnit (`2.9.3`)** sobre **.NET 10** bajo compilación estricta (`TreatWarningsAsErrors=true`, `WarningLevel=9999`):

| Categoría | Archivo de Pruebas | Alcance Verificado |
| :--- | :--- | :--- |
| **Unit Testing** | [BusinessRulesAndSecurityTests.cs](../tests/GestionScrap.Tests/Unit/BusinessRulesAndSecurityTests.cs) | `RutChileno`, clasificación por defecto de grupos, detección de duplicados por planta (incluyendo `HoraIngreso` en Talcahuano), mapeo de árbol padre/hijo, hashing PBKDF2-SHA256 y validación de perfiles RBAC. |
| **Integration & Tree CRUD** | [SqliteAndTreeWorkflowIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/SqliteAndTreeWorkflowIntegrationTests.cs) | Inicialización `barriagada`/`Cambias0`, bloqueo tras `5` intentos fallidos, CRUD completo de **Perfiles** dinámicos y verificación `UnauthorizedAccessException`, flujo completo de Árbol de Grupos (Padre/Subgrupo/Producto/Mover/Reordenar/Eliminar), Pareto ABC de proveedores, exportación `.xlsx` reabierta con `ClosedXML`, respaldo SQLite en caliente y purga `"ELIMINAR"`. |
| **Real Databases (Firebird & Access)** | [RealDatabasesIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/RealDatabasesIntegrationTests.cs) | Conexión e importación real desde `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` verificando caracteres especiales (`VIÑA`) cuando el servidor está alcanzable, más manejo de archivo `.mdb` inexistente / importación real cuando existe `BD_SCRAP.mdb`. |
| **UI / UX Testing (STA)** | [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) | Auditoría STA completa de `SplashForm`, `LoginForm`, `MainForm`, los `5` `UserControl` modulares y los `5` diálogos modales: texto no vacío, icono asignado, tamaño/coordenadas válidas, `AccessibleName`, evento `Click`/`DialogResult` y visibilidad/habilitación según perfil RBAC. |
| **Stress & Performance** | [PerformanceAndStressTests.cs](../tests/GestionScrap.Tests/Stress/PerformanceAndStressTests.cs) | Inserción masiva de **10.000** pesajes en SQLite WAL, validación de 100 % de detección de duplicados y ejecución paralela de consultas de Informe Resumen, Árbol de Grupos y Análisis Pareto ABC. |

## 2. Criterios de Aceptación

- **Compilación `Debug` y `Release`:** `0 errores` y `0 advertencias`.
- **Suite Automatizada (`dotnet test -c Release`):** **`19/19` pruebas exitosas** (`0` fallidas, `0` omitidas).
- **Empaquetado Inno Setup 7 (`7.1.1`):** Generación sin errores ni advertencias de `SelfContained` y `FrameworkDependent`, verificando instalación silenciosa, arranque con creación de `gestion_scrap.db`, actualización conservando datos locales y desinstalación limpia.
