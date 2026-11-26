# Plan y Evidencia de Pruebas y QA · Gestión de Scrap v4.0

## 1. Estrategia de Verificación (`GestionScrap.Tests`)

La solución incluye una suite automatizada en **xUnit (`2.9.3`)** ejecutada sobre `.NET 10` con compilación estricta (`TreatWarningsAsErrors=true`).

| Categoría | Archivo de Pruebas | Alcance Verificado |
| :--- | :--- | :--- |
| **Unit Testing** | [BusinessRulesAndSecurityTests.cs](../tests/GestionScrap.Tests/Unit/BusinessRulesAndSecurityTests.cs) | `RutChileno`, promedio ponderado sin división por cero, modo precios reales vs teóricos, detección de duplicados por planta (incluyendo `HoraIngreso` en Talcahuano), aislamiento Agrosuper vs Pesquera, exclusiones JCRAMM, hashing PBKDF2 y matriz RBAC. |
| **Integration & Functional** | [SqliteExportAndSecurityIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/SqliteExportAndSecurityIntegrationTests.cs) | Bloqueo tras 5 intentos fallidos, rechazo RBAC (`UnauthorizedAccessException`) en Operador y Auditor, flujo completo Dashboard/Reportes/Precios/JCRAMM, validación de libros `.xlsx` reabiertos con `ClosedXML`, respaldo SQLite en caliente, cambio de clave y purga confirmada con palabra `"ELIMINAR"`. |
| **MDB Migration Testing** | [MdbMigrationIntegrationTests.cs](../tests/GestionScrap.Tests/Integration/MdbMigrationIntegrationTests.cs) | Manejo de archivo inexistente (`FileNotFoundException`) e importación real desde `BD_SCRAP.mdb` (puente x86 SysWOW64 / OleDb) verificando registros importados en Placilla, Santiago y Talcahuano. |
| **UI / UX Testing (STA)** | [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) | Inspección recursiva de todos los botones en `LoginForm`, `MainForm`, los 10 `UserControl` de pestañas y los 7 diálogos modales: texto no vacío, icono renderizado no nulo, `AccessibleName`, tamaño y coordenadas válidas, evento `Click`/`DialogResult` enlazado y deshabilitación visual por rol (`Operador` y `Auditor`). |
| **Stress & Performance** | [PerformanceAndStressTests.cs](../tests/GestionScrap.Tests/Stress/PerformanceAndStressTests.cs) | Inserción masiva de **10.000** pesajes en SQLite WAL, validación de 100 % detección de duplicados al reimportar y ejecución concurrente de 6 consultas pesadas en paralelo (`< 10 s`). |

## 2. Resultados Verificados

- **Compilación `Debug`:** `0 advertencias`, `0 errores`.
- **Compilación `Release`:** `0 advertencias`, `0 errores`.
- **Ejecución `dotnet test -c Release`:** **22/22 pruebas exitosas** (`Correcto: 22`, `Incorrecto: 0`, `Omitido: 0`).
- **Compilación Inno Setup 7 (`7.1.1`):** Ambos instaladores (`SelfContained` y `FrameworkDependent`) generados con código de salida `0` y sin advertencias.
- **Ciclo Instalador Validado:**
  1. Instalación silenciosa limpia (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER`) -> código `0`.
  2. Arranque verificado de `GestionScrap.exe` con creación de `gestion_scrap.db`.
  3. Actualización silenciosa sobre directorio existente preservando `gestion_scrap.db` -> código `0`.
  4. Desinstalación silenciosa (`unins000.exe`) con limpieza íntegra de binarios -> código `0`.
