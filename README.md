<div align="center">
  <img src="installer/assets/app_logo.png" alt="Gestión de Scrap Logo" width="110" />
  <h1>Gestión de Scrap v4.0</h1>
  <p><strong>Plataforma de Escritorio .NET 10 para Control de Pesajes, Tarifas, Informes Operativos y Sincronización Firebird</strong></p>

  ![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms%20x64-512BD4?style=for-the-badge&logo=dotnet)
  ![SQLite WAL](https://img.shields.io/badge/Database-SQLite%20WAL%20%2B%20Firebird-003B57?style=for-the-badge&logo=sqlite)
  ![Inno Setup 7](https://img.shields.io/badge/Installer-Inno%20Setup%207-0891B2?style=for-the-badge)
  ![Tests](https://img.shields.io/badge/Tests-22%2F22%20Passing-059669?style=for-the-badge)
  ![Warnings](https://img.shields.io/badge/Build-0%20Errors%20%7C%200%20Warnings-10B981?style=for-the-badge)
</div>

---

## Descripción del Proyecto

**Gestión de Scrap v4.0** es la migración integral hacia **.NET 10 (C# 14 + Windows Forms x64)** y **Inno Setup 7** del sistema operativo de gestión de datos de scrap y pesajes de camiones across plantas **Placilla**, **Santiago** y **Talcahuano**.

Reemplaza los libros heredados Excel/VBA y la base Microsoft Access compartida por una arquitectura limpia de cuatro capas respaldada por **SQLite en modo WAL**, conectividad TCP/IP nativa hacia servidores **Firebird (`.GDB`/`.FDB`)**, exportación Excel moderna mediante **ClosedXML** y control de acceso basado en roles (**RBAC**).

---

## Características Principales

- **Dashboard Ejecutivo (`Ctrl+1`):** KPIs del período vs mes anterior, última jornada, alertas automáticas de folios sin tarifa y 3 gráficos vectoriales GDI+ con doble búfer.
- **10 Módulos Operativos Especializados:**
  1. `Grupo de Material` (consolidado por familia en Placilla, Santiago y Talcahuano; rango o semanal).
  2. `Compras y Traslados` (separación de compras reales y movimientos internos entre plantas).
  3. `Informe por Cliente` (búsqueda dual por **RUT** o **Folio** con selector rápido `F2`).
  4. `Grupos Especiales` (8 segmentos estratégicos independientes: Retail Santiago, Retail Empresas, Mix Santiago, Mix Placilla, Retail Regiones, Agrícola, Pesquera y Agrosuper).
  5. `Informe JCRAMM` (control persistente de último folio cerrado y exportación + cierre atómico).
  6. `Informe de Precios` (precios teóricos pactados, congelamiento e índice histórico de precios reales, promedios ponderados sin división por cero).
  7. `Mantenedor de Precios` (CRUD validado por proveedor/producto con filtros en tiempo real).
  8. `Importar Firebird` (conexión TCP/IP puerto `3050`, prevención de duplicados y ejecución transaccional de procedimientos SAP `PA_INS_PROV_STGO` / `PA_INS_MASIVO_EXP_SAP`).
  9. `Seguridad, Auditoría y Migración MDB` (usuarios, permisos granulares, bitácora de auditoría, migrador de `BD_SCRAP.mdb` y respaldo SQLite en caliente).
- **Seguridad End-to-End:** Contraseñas con **PBKDF2-SHA256** (`100.000` iteraciones), bloqueo por intentos fallidos, secretos de red cifrados con **Windows DPAPI** y validación de permisos tanto en UI como en capa de aplicación.
- **Exportación Excel sin Office COM:** Generación directa `.xlsx` mediante `ClosedXML 0.105.1`.
- **Rendimiento Fluido:** Grillas con `DoubleBuffered = true`, carga diferida de pestañas y SQLite WAL indexado validado bajo carga de 10.000 registros.

---

## Arquitectura de la Solución

```text
GESTION_SCRAP/
├── GestionScrap.sln
├── Directory.Build.props                 # Compilación estricta (.NET 10 / Lang 14 / 0 Warnings)
├── src/
│   ├── GestionScrap.Domain/              # Entidades, Enums, RutChileno, Interfaces
│   ├── GestionScrap.Application/         # BusinessRules, DTOs, Seguridad PBKDF2, Servicios
│   ├── GestionScrap.Infrastructure/      # SQLite WAL, Firebird TCP, ClosedXML, Migrador MDB, DPAPI
│   └── GestionScrap.Desktop/             # WinForms UI, Dashboard GDI+, Diálogos, IconFactory
├── tests/
│   └── GestionScrap.Tests/               # Unit, Integration, MDB Migration, STA UI Audit & Stress (22 tests)
├── installer/
│   ├── GestionScrap.iss                  # Script parametrizado Inno Setup 7 (x64 SelfContained / FDD)
│   ├── InfoSoftware.rtf                  # Nota informativa del asistente de instalación
│   ├── build_installer.cmd               # Pipeline completo Build -> Test -> Publish -> ISCC
│   ├── AssetBuilder/                     # Generador .NET 10 de gráficos e iconos v4.0
│   └── assets/                           # Iconos (.ico/.png) y banners modernos Inno Setup 7
└── docs/                                 # Arquitectura, Manual Funcional, Compilación, Despliegue, Seguridad, QA
```

---

## Visualización e Instalador (`v4.0`)

Los instaladores utilizan **Inno Setup 7 (`WizardStyle=modern`)** con gráficos HiDPI regenerables mediante `installer/AssetBuilder`:

| Recurso | Uso | Ubicación |
| :--- | :--- | :--- |
| Icono y Logo `v4.0` | Ejecutable, menú Inicio y formularios | [installer/assets/app_logo.png](installer/assets/app_logo.png) |
| Banner Lateral (`1x` / `2x`) | Pantalla inicial y finalización Inno Setup 7 | [installer/assets/wizard_banner.bmp](installer/assets/wizard_banner.bmp) |
| Cabecera Compacta (`1x` / `2x`) | Páginas internas del asistente | [installer/assets/wizard_small.bmp](installer/assets/wizard_small.bmp) |

---

## Requisitos

### Para Usuarios Finales (Instalación)
- **Sistema Operativo:** Windows 10 o Windows 11 (64 bits / `x64`).
- **Instalador Autocontenido (`SelfContained`):** No requiere descargas previas ni `.NET` preinstalado.
- **Instalador Compacto (`FrameworkDependent`):** Requiere **.NET 10 Desktop Runtime (x64)**.

### Para Desarrollo / Compilación
- **.NET 10 SDK** (`10.0.100+`).
- **Inno Setup 7** (`7.1.1+`) para compilar los instaladores `.exe`.

---

## Instalación

1. Ejecute el instalador deseado generado en `Instalador/`:
   - `GestionScrap_Setup_v4.0.0_x64_SelfContained.exe` *(recomendado)*
   - `GestionScrap_Setup_v4.0.0_x64_FrameworkDependent.exe`
2. Siga el asistente en español (la base operativa local `%LocalAppData%\GestionScrap\gestion_scrap.db` se conserva automáticamente al actualizar versiones).
3. Para despliegue corporativo desatendido:
   ```cmd
   GestionScrap_Setup_v4.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
   ```

---

## Compilación y Pruebas

```powershell
# 1. Restaurar dependencias
dotnet restore GestionScrap.sln

# 2. Compilar Debug y Release (0 advertencias, 0 errores)
dotnet build GestionScrap.sln -c Debug --no-restore
dotnet build GestionScrap.sln -c Release --no-restore

# 3. Ejecutar pruebas automatizadas (Unit, Integration, MDB, STA UI Audit, 10k Stress)
dotnet test GestionScrap.sln -c Release --no-build --verbosity normal

# 4. Publicar binarios win-x64 y generar instaladores Inno Setup 7
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained false -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64-fdd
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" --quiet-progress -DVariant=selfcontained installer\GestionScrap.iss
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" --quiet-progress -DVariant=framework installer\GestionScrap.iss
```

---

## Configuración Inicial y Seguridad

- **Perfiles Iniciales:** `admin` (`Administrador`), `supervisor` (`Supervisor`), `operador` (`Operador`) y `auditor` (`Auditor`). Rotar las contraseñas iniciales inmediatamente después de instalar mediante **Mi Clave** o **Seguridad y MDB** (consulte [docs/SEGURIDAD.md](docs/SEGURIDAD.md)).
- **Conexiones Firebird:** Configure IP, puerto `3050`, ruta `.GDB`/`.FDB` y credenciales cifradas con DPAPI desde **Config. BD**.
- **Migración desde Access (`BD_SCRAP.mdb`):** Desde **Seguridad y MDB (`Ctrl+0`) -> Migración Access MDB**, importe los datos históricos de pesajes, clientes, familias de productos y tarifas.

---

## Uso y Atajos Principales

| Atajo | Acción |
| :--- | :--- |
| `Ctrl+1` .. `Ctrl+9` | Navegar entre Dashboard, Informes, Precios, Mantenedor e Importación Firebird |
| `Ctrl+0` | Abrir módulo de Seguridad RBAC, Auditoría, Migración MDB y Respaldo SQLite |
| `F5` | Refrescar datos del módulo activo |
| `F2` / `Enter` | Abrir catálogo rápido de Clientes o Productos en campos de búsqueda |

Consulte la guía completa en [docs/MANUAL_FUNCIONAL.md](docs/MANUAL_FUNCIONAL.md).

---

## Documentación Técnica Completa

- [Arquitectura del Sistema](docs/ARQUITECTURA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación](docs/COMPILACION.md)
- [Guía de Despliegue e Instalación](docs/DESPLIEGUE.md)
- [Seguridad, Roles RBAC y Auditoría](docs/SEGURIDAD.md)
- [Plan y Evidencia de Pruebas QA](docs/PRUEBAS_QA.md)
- [Historial de Cambios (Changelog)](docs/CHANGELOG.md)

---

## Versionado e Historial de Cambios

Este proyecto sigue **Versionado Semántico (`SemVer`)** — versión actual **`4.0.0`**.
Revise los detalles de la migración y correcciones aplicadas respecto al sistema Excel/VBA en [docs/CHANGELOG.md](docs/CHANGELOG.md).

---

## Créditos

- Desarrollado y mantenido sobre el ecosistema **.NET 10**, **SQLite**, **FirebirdSql.Data.FirebirdClient**, **ClosedXML** y **Inno Setup 7**.
- Proyecto: [GESTION_SCRAP](https://github.com/barriagada666-collab/GESTION_SCRAP)
