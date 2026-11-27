# Plan y Evidencia de Pruebas y QA · Gestión de Scrap v5.0.0

## 1. Estrategia de Verificación (`GestionScrap.Tests`)

Suite automatizada en **xUnit (`2.9.3`)** sobre **.NET 10** bajo compilación estricta (`TreatWarningsAsErrors=true`, `WarningLevel=9999`, cero supresiones):

| Categoría | Archivo de Pruebas | Alcance Verificado |
| :--- | :--- | :--- |
| **Unit Testing (Negocio y Seguridad)** | [BusinessRulesAndSecurityTests.cs](../tests/GestionScrap.Tests/Unit/BusinessRulesAndSecurityTests.cs) | `RutChileno`, comparativos YoY/MoM, ficha estadística, segmentación con exclusión de ceros, árbol padre/hijo, duplicados por planta, PBKDF2-SHA256 y perfiles RBAC (`13` permisos). |
| **CRM, ERP, Pesajes 1/2 Pasos, Inventario e Informes Multi-Selección** | [EnterpriseCrmErpWeighingTests.cs](../tests/GestionScrap.Tests/Integration/EnterpriseCrmErpWeighingTests.cs) | Reglas estrictas `WeighingRules` (pesos negativos, Bruto < Tara, unidades inconsistentes, bloqueo de sobrescritura manual sin autorización), CRM RUT Módulo 11 sin duplicados + Ficha 360°, flota/patentes normalizadas (`AB-CD-12` == `abcd12`) y rechazo de vehículos inexistentes, flujo 2 mediciones (preservación de 1ª marca UTC, sin impacto prematuro, idempotencia de inventario, bloqueo de cierre duplicado), discrepancias sobre tolerancia (`PendienteAutorizacion`), corrección autorizada, anulación con reverso sin borrado físico, ajustes de inventario, consistencia 100 % entre los 6 informes multi-selección + Excel y migración real `v3`. |
| **Integration & Workflow Base** | [SqliteAndTreeWorkflowIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/SqliteAndTreeWorkflowIntegrationTests.cs) | Login/lockout, CRUD perfiles RBAC, árbol CRUD de materiales, trazabilidad `import_history`, exportación personalizada, respaldo en caliente y purga `"ELIMINAR"`. |
| **Migrations, DB & Security Audit** | [AuditMigrationsAndReliabilityTests.cs](../tests/GestionScrap.Tests/Integration/AuditMigrationsAndReliabilityTests.cs) | Migraciones `v1..v3`, `EXPLAIN QUERY PLAN` sin `TEMP B-TREE` ni `FULL SCAN`, `PRAGMA foreign_keys = ON` por conexión, protección del último administrador, copia de base real (`32.178` pesajes) en esquema `v3`, redacción de secretos en `FileAppLogger`, metadatos `5.0.0` y ausencia de credenciales en docs. |
| **Real Databases (Firebird & Access)** | [RealDatabasesIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/RealDatabasesIntegrationTests.cs) | Conexión real a `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` y migración Access `.mdb` cuando están disponibles. |
| **UI / UX Testing (STA)** | [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) | Auditoría STA estricta de `SplashForm`, `LoginForm`, `MainForm` (`1380x840` y `1180x720`), los **8 módulos** y los **15 diálogos modales**: texto en 1 línea, icono, `AccessibleName`, evento `Click`, cero recortes en padres/ancestros, cero solapes, cero desbordamiento en diálogos, contraste de cabeceras `DataGridView` y restricciones RBAC dinámicas. |
| **Stress & Performance** | [PerformanceAndStressTests.cs](../tests/GestionScrap.Tests/Stress/PerformanceAndStressTests.cs) | Inserción masiva de **10.000** pesajes en SQLite WAL, 100 % deduplicación y consultas concurrentes en `< 10 s`. |

## 2. Criterios de Aceptación Verificados (`v5.0.0`)

- **Compilación `Debug` y `Release`:** `0 errores`, `0 advertencias`, `0 supresiones`.
- **Suite Automatizada (`dotnet test -c Release`):** **`34/34` pruebas exitosas** (`0` fallidas, `0` omitidas).
- **Verificación Visual UI High-DPI:** Capturas inspeccionadas de todos los módulos y diálogos nuevos validando ausencia de recortes, alineación limpia y coherencia visual Cambiaso.
- **Empaquetado Inno Setup 7 (`7.1.1`) + Portable ZIP:** Instaladores `SelfContained`, `FrameworkDependent`, `Portable ZIP` y `checksums_sha256.txt` compilados y validados mediante instalación silenciosa, `FileVersion == 5.0.0.0` y desinstalación limpia.
