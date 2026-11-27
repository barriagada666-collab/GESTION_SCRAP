<div align="center">
  <img src="installer/assets/app_logo.png" alt="Cambiaso Gestión de Scrap Logo" width="128" />
  <h1>Cambiaso · Gestión de Scrap y Pesajes v4.1.0</h1>
  <p><strong>Plataforma Corporativa de Escritorio .NET 10 x64 para Control de Pesajes, Analítica YoY/MoM, Segmentación de Datos, Árbol de Materiales e Importación Trazable Firebird / Access</strong></p>

  ![Version](https://img.shields.io/badge/Version-v4.1.0-0B3C5D?style=for-the-badge)
  ![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms%20x64-0E608A?style=for-the-badge&logo=dotnet)
  ![SQLite WAL](https://img.shields.io/badge/Database-SQLite%20WAL%20%2B%20Firebird-0891B2?style=for-the-badge&logo=sqlite)
  ![Inno Setup 7](https://img.shields.io/badge/Installer-Inno%20Setup%207-C9A46E?style=for-the-badge)
  ![Tests](https://img.shields.io/badge/Tests-27%2F27%20Passing-059669?style=for-the-badge)
  ![Warnings](https://img.shields.io/badge/Build-0%20Errors%20%7C%200%20Warnings-10B981?style=for-the-badge)
  ![License](https://img.shields.io/badge/License-MIT-475569?style=for-the-badge)
</div>

---

## Descripción General

**Gestión de Scrap v4.1.0** es la aplicación corporativa de **Cambiaso Hnos. S.A.C.** desarrollada en **.NET 10 (`C# 14` + Windows Forms `x64` `PerMonitorV2`)** y empaquetada con **Inno Setup 7 (`7.1.1`)** para consolidar, clasificar, segmentar y analizar pesajes de báscula provenientes de servidores productivos **Firebird (`WMDBSPC.GDB` / `PWAREHOUSE.FDB`)** y bases históricas **Microsoft Access (`.mdb`/`.accdb`)**.

Opera con almacenamiento embebido **SQLite en modo WAL** con migraciones versionadas (`schema_migrations`), integridad referencial estricta (`PRAGMA foreign_keys = ON` en cada conexión), exportación nativa **Excel (`.xlsx`)** mediante **ClosedXML** (sin dependencia de Office COM), bitácora diagnóstica diaria con redacción automática de secretos (`FileAppLogger`), activos visuales Cambiaso 100 % embebidos (`EmbeddedResource`) y seguridad dinámica basada en perfiles (**RBAC**).

---

## Funcionalidades Principales (`Ctrl+1` .. `Ctrl+5`)

1. **Informe Resumen, Segmentación y Comparativos YoY/MoM (`Ctrl+1`):**
   - Filtros combinables (`Planta`, `Movimiento`, `Grupo Material`, `Segmentar por`, `Proveedor/RUT/Patente`, `Producto/Código`, rango `Desde/Hasta`) + checkboxes **`Excluir totales en 0`** y **`Sin traslados`**.
   - **KPIs y Ficha Estadística Cuantitativa:** kilos netos, toneladas, viajes, promedio, mediana, mínimo, máximo, desviación estándar, días operativos, ritmo diario, mejor mes/año y variación interanual exacta (**YoY**).
   - **Comparativo Año a Año (YoY) y Mes a Mes (MoM):** evolución mensual con variación MoM/YoY, consolidado anual y gráfico comparativo vectorial GDI+.
   - **Segmentación Dinámica y Exportación Personalizada (`Exportar Selección...`):** agrupa por Planta, Grupo, Tipo, Proveedor, Producto, Origen, Mes o Año con multi-selección en grillas para exportar únicamente lo seleccionado o informes multi-hoja configurables.
2. **Grupos de Material con Árbol CRUD Jerárquico (`Ctrl+2`):**
   - Árbol editable (`MaterialGroupNode`) con **Nodos Padre**, **Subgrupos** y **Productos Hijo** (crear, editar, mover por diálogo o Drag & Drop, reordenar y eliminar en cascada garantizada por SQLite FK).
   - Resolución de descendientes en `O(1)` con protección contra ciclos, exclusión de totales en cero y exportación completa o por subrama.
3. **Análisis de Proveedores (`Ctrl+3`):**
   - Clasificación **Pareto ABC**, variación porcentual entre períodos, días de inactividad, mix de materiales, evolución mensual, patentes y orígenes frecuentes.
4. **Importador Simplificado y Control Origen -> Destino (`Ctrl+4`):**
   - Flujo directo **DESDE (Firebird TCP `WMDBSPC.GDB` / `PWAREHOUSE.FDB` o Access `.mdb`/`.accdb`) ➔ HACIA (SQLite `gestion_scrap.db`)** con botón `Importar Directo`, deduplicación por proyección ligera + comando SQL preparado, vista previa opcional y filtro `Omitir 0 Kg`.
   - Pestaña **Control e Historial (Desde -> Hacia)** sobre `import_history` con métricas SQL agregadas (`~11 ms` en `32.178` registros) y exportación Excel.
5. **Seguridad, Perfiles RBAC, Auditoría e Integridad (`Ctrl+5`):**
   - CRUD de **Perfiles RBAC (`PerfilUsuario`)** y **Usuarios** con protección contra eliminación, desactivación o degradación del último administrador activo.
   - Contraseñas con **PBKDF2-SHA256 (`100.000` iteraciones)**, bloqueo tras `5` intentos fallidos, secretos de red cifrados con **Windows DPAPI**, bitácora `audit_log` indexada, verificación automática de integridad SQLite (`PRAGMA quick_check` + `foreign_key_check`), respaldo en caliente post-verificado y purga confirmada (`"ELIMINAR"`).

---

## Tecnologías y Arquitectura

- **Framework y Lenguaje:** `.NET 10.0` (`net10.0` / `net10.0-windows`), `C# 14`, Windows Forms `x64` (`PerMonitorV2`).
- **Arquitectura Limpia en 4 Capas:**
  - `GestionScrap.Domain`: Entidades, enums, `RutChileno`, `AppMetadata`, contratos (`IScrapRepository`, `IAppLogger`, `IFirebirdSyncService`, `IExcelExportService`, `IMdbMigrationService`, `IAuthService`).
  - `GestionScrap.Application`: `ScrapApplicationService`, `BusinessRules`, `SecuritySessionService`, `DefaultRoleProfiles`, `PasswordSecurity`, DTOs.
  - `GestionScrap.Infrastructure`: `SqliteScrapRepository` (`Microsoft.Data.Sqlite 10.0.15` + `SQLitePCLRaw 2.1.14`), `FirebirdSyncService` (`FirebirdSql.Data.FirebirdClient 10.3.4`), `ClosedXmlExcelExportService` (`ClosedXML 0.105.1`), `MdbMigrationService`, `AppSettingsStore` (DPAPI), `FileAppLogger`.
  - `GestionScrap.Desktop`: WinForms UI, `AppTheme`, `IconFactory`, 5 módulos operativos y 6 diálogos modales.
- **CI/CD y Distribución:** GitHub Actions (`ci.yml` + `release.yml`), `scripts/Build-Release.ps1`, Inno Setup `7.1.1`, paquete Portable ZIP y verificación SHA-256.

---

## Requisitos del Sistema

- **Ejecución (Autocontenido / Portable):** Windows 10 o Windows 11 de 64 bits (`x64`). **Sin dependencias previas.**
- **Ejecución (Framework-Dependent):** Windows 10/11 `x64` + **Microsoft Windows Desktop Runtime 10.0 (x64)**.
- **Desarrollo y Compilación:** `.NET 10 SDK` (`10.0.100` o superior) e **Inno Setup 7** (`7.1.1`) para generar instaladores.
- **Opcional (importación `.mdb` directa 64 bits):** Microsoft Access Database Engine (`ACE.OLEDB.16.0` / `12.0`); si solo existe motor de 32 bits, el sistema utiliza automáticamente el puente x86 seguro.

---

## Descarga, Instalación y Configuración

Descargue el paquete deseado desde **[GitHub Releases — v4.1.0](https://github.com/barriagada666-collab/GESTION_SCRAP/releases/tag/v4.1.0)**:

| Artefacto | Modalidad | Uso Recomendado |
| :--- | :--- | :--- |
| `GestionScrap_Setup_v4.1.0_x64_SelfContained.exe` | Instalador Inno Setup 7 (Autocontenido) | **Recomendado** — incluye runtime `.NET 10 x64`, cero requisitos previos |
| `GestionScrap_Setup_v4.1.0_x64_FrameworkDependent.exe` | Instalador Inno Setup 7 (Liviano) | Estaciones con `.NET 10 Desktop Runtime x64` ya instalado |
| `GestionScrap_Portable_v4.1.0_win-x64.zip` | Paquete Portable Autocontenido | Descomprimir y ejecutar `GestionScrap.exe` sin instalador |
| `checksums_sha256.txt` | Verificación criptográfica | Validar integridad de descarga (`Get-FileHash -Algorithm SHA256`) |

### Instalación Silenciosa y Datos Persistentes

```cmd
:: Instalación desatendida para el usuario actual
GestionScrap_Setup_v4.1.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

- **Base de datos SQLite WAL:** `%LocalAppData%\GestionScrap\gestion_scrap.db` *(conservada intacta al actualizar o desinstalar)*
- **Configuración cifrada (DPAPI):** `%LocalAppData%\GestionScrap\settings.json`
- **Logs diagnósticos diarios:** `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log`
- **Acceso inicial:** Inicie sesión con la cuenta administrativa aprovisionada por TI / Operaciones y **cambie la contraseña en el primer inicio de sesión** mediante **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.

---

## Ejecución, Compilación (`Debug` / `Release`) y Pruebas

```powershell
# 1. Restaurar dependencias
dotnet restore GestionScrap.sln

# 2. Compilar en Debug y Release (estricto: TreatWarningsAsErrors=true, 0 errores / 0 advertencias)
dotnet build GestionScrap.sln -c Debug --no-restore --no-incremental --disable-build-servers
dotnet build GestionScrap.sln -c Release --no-restore --no-incremental --disable-build-servers

# 3. Ejecutar la suite automatizada completa (27/27 pruebas xUnit)
dotnet test GestionScrap.sln -c Release --no-build --disable-build-servers --verbosity normal

# 4. Ejecutar en desarrollo
dotnet run --project src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release

# 5. Generar instaladores Inno Setup 7 + Portable ZIP + checksums_sha256.txt
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\Build-Release.ps1
```

---

## Estructura del Proyecto

```text
GESTION_SCRAP/
├── .github/workflows/          # Pipelines CI (ci.yml) y Release (release.yml)
├── docs/                       # Documentación técnica, releases y evidencias
│   ├── releases/README-v4.1.0.md
│   └── evidencias/v4.1.0/      # Informe de auditoría, benchmarks y checksums
├── installer/                  # Script Inno Setup 7, RTF corporativo y AssetBuilder HiDPI
├── scripts/                    # Pipeline reproducible Build-Release.ps1
├── src/
│   ├── GestionScrap.Domain/
│   ├── GestionScrap.Application/
│   ├── GestionScrap.Infrastructure/
│   └── GestionScrap.Desktop/
├── tests/GestionScrap.Tests/   # Pruebas unitarias, integración, migración, UI STA y estrés
├── Directory.Build.props       # Versión centralizada (4.1.0) y análisis estático estricto
├── GestionScrap.sln
└── LICENSE                     # Licencia MIT
```

---

## Documentación Técnica y Evidencias

- [Notas de Versión — Release v4.1.0](docs/releases/README-v4.1.0.md)
- [Informe de Auditoría, Optimización y Validación v4.1.0](docs/evidencias/v4.1.0/INFORME_AUDITORIA_Y_VALIDACION_v4.1.0.md)
- [Historial de Cambios (CHANGELOG)](docs/CHANGELOG.md)
- [Arquitectura del Sistema](docs/ARQUITECTURA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación y Construcción](docs/COMPILACION.md)
- [Guía de Despliegue e Instalación](docs/DESPLIEGUE.md)
- [Seguridad, Perfiles RBAC y Auditoría](docs/SEGURIDAD.md)
- [Plan y Evidencia de Pruebas QA](docs/PRUEBAS_QA.md)

---

## Licencia

Distribuido bajo licencia **[MIT](LICENSE)** — Copyright © 2026 **Cambiaso Hnos. S.A.C.**
