<div align="center">
  <img src="installer/assets/app_logo.png" alt="Cambiaso Gestión de Scrap Logo" width="128" />
  <h1>Cambiaso · Gestión de Scrap y Pesajes v4.0</h1>
  <p><strong>Plataforma de Escritorio .NET 10 x64 para Control de Pesajes, Clasificación Jerárquica de Materiales, Análisis de Proveedores e Importación Firebird</strong></p>

  ![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms%20x64-0B3C5D?style=for-the-badge&logo=dotnet)
  ![SQLite WAL](https://img.shields.io/badge/Database-SQLite%20WAL%20%2B%20Firebird-0891B2?style=for-the-badge&logo=sqlite)
  ![Inno Setup 7](https://img.shields.io/badge/Installer-Inno%20Setup%207-C9A46E?style=for-the-badge)
  ![Tests](https://img.shields.io/badge/Tests-19%2F19%20Passing-059669?style=for-the-badge)
  ![Warnings](https://img.shields.io/badge/Build-0%20Errors%20%7C%200%20Warnings-10B981?style=for-the-badge)
</div>

---

## Descripción del Proyecto

**Gestión de Scrap v4.0** es la plataforma corporativa desarrollada en **.NET 10 (`C# 14` + Windows Forms `x64` `PerMonitorV2`)** y **Inno Setup 7** para consolidar, clasificar y analizar los pesajes de báscula desde las bases productivas **Firebird (`WMDBSPC.GDB` / `PWAREHOUSE.FDB`)** y respaldos históricos **Access (`.mdb`/`.accdb`)**.

Opera con almacenamiento local **SQLite en modo WAL**, exportación nativa **Excel (`.xlsx`)** mediante **ClosedXML**, **cero dependencias externas de UI o recursos remotos** (identidad visual corporativa Cambiaso azul petróleo `#0B3C5D`, cian técnico `#0891B2` y dorado `#C9A46E` embebida directamente como `EmbeddedResource` en el binario) y control de acceso dinámico basado en perfiles (**RBAC**).

---

## Los 5 Módulos Operativos (`Ctrl+1` .. `Ctrl+5`)

1. **Informe Resumen (`Ctrl+1`):**
   - KPIs ejecutivos de kilos netos, viajes, promedio por viaje, comparación vs período anterior, última jornada, proveedores y productos activos.
   - Gráficos vectoriales GDI+ con doble búfer (tendencia comparativa, top proveedores y distribución por grupo) más resumen por planta, tipo de pesaje y últimos folios.
   - Exportación directa a `.xlsx`.
2. **Grupos de Material con Árbol CRUD Jerárquico (`Ctrl+2`):**
   - Gestión integral de árbol (`MaterialGroupNode`) con **Nodos Padre**, **Subgrupos** y **Productos Hijo**.
   - Acciones completas de creación, edición, asignación de productos no clasificados, movimiento entre padres (mediante diálogo y arrastrar/soltar), reordenamiento arriba/abajo y eliminación segura (mantener subnodos o eliminar subárbol).
   - Agregación jerárquica consolidada por rama y exportación a `.xlsx`.
3. **Análisis de Proveedores (`Ctrl+3`):**
   - Clasificación **Pareto ABC** (`A` / `B` / `C`), participación individual y acumulada, variación porcentual respecto al período anterior y días de inactividad.
   - Ficha detallada del proveedor seleccionado: mix de materiales, productos principales, evolución mensual, patentes frecuentes, orígenes frecuentes y tabla completa de entregas con exportación multi-hoja `.xlsx`.
4. **Importador Multi-Origen (`Ctrl+4`):**
   - Conector directo TCP/IP (`3050`) hacia Firebird preconfigurado para `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` (incluye presets para `placilla\WMDBSPC.GDB` y `thno\PWAREHOUSE.FDB`) usando `Charset=NONE` compatible con bases heredadas.
   - Prueba de conectividad con conteo real de tablas, pesajes y rango de fechas, extracción deduplicada de pesajes/clientes/productos y migración opcional desde `.mdb`/`.accdb`.
5. **Seguridad, Perfiles y Auditoría (`Ctrl+5`):**
   - Administración CRUD de **Perfiles RBAC (`PerfilUsuario`)** y **Usuarios** con hash **PBKDF2-SHA256** (`100.000` iteraciones), bloqueo por `5` intentos fallidos (`5` min) y cifrado **DPAPI** de credenciales Firebird.
   - Bitácora de auditoría (`audit_log`), respaldo SQLite en caliente y purga confirmada mediante palabra de seguridad (`"ELIMINAR"`).

---

## Arquitectura de la Solución

```text
GESTION_SCRAP/
├── GestionScrap.sln
├── Directory.Build.props                 # .NET 10 / C# 14 / 0 Errors & 0 Warnings
├── src/
│   ├── GestionScrap.Domain/              # Entidades (Pesaje, Cliente, Producto, MaterialGroupNode, PerfilUsuario, Usuario), Enums, RutChileno
│   ├── GestionScrap.Application/         # BusinessRules, DTOs, SecuritySessionService, ScrapApplicationService
│   ├── GestionScrap.Infrastructure/      # SqliteScrapRepository (WAL), FirebirdSyncService, ClosedXmlExcelExportService, MdbMigrationService, AppSettingsStore
│   └── GestionScrap.Desktop/             # WinForms UI (SplashForm, LoginForm, MainForm, 5 Módulos, 5 Diálogos, AppTheme, IconFactory, Assets embebidos)
├── tests/
│   └── GestionScrap.Tests/               # Unit, Integration, Real DB (WMDBSPC.GDB), STA UI Audit & 10k Stress (19 tests)
├── installer/
│   ├── GestionScrap.iss                  # Script parametrizado Inno Setup 7 (SelfContained / FrameworkDependent x64)
│   ├── InfoSoftware.rtf                  # Nota informativa del instalador
│   ├── build_installer.cmd               # Pipeline Build -> Test -> Publish -> ISCC
│   ├── AssetBuilder/                     # Generador .NET 10 de gráficos Cambiaso HiDPI (.ico/.png/.bmp)
│   └── assets/                           # Emblema, monograma, icono y banners modernos
└── docs/                                 # Documentación técnica y funcional completa
```

---

## Auditoría Real de Datos (`WMDBSPC.GDB`)

Durante la auditoría directa sobre el servidor `192.168.2.56:3050` se verificó:
- **`\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` (`DEFAULT CHARACTER SET NONE`):** `33` tablas de usuario, `49` procedimientos almacenados, **`2.730` pesajes activos** (`2.732` totales, `ANULADO='0'`, del `12-11-2024` al `26-11-2026`, folios `1..2737`, **`25.443.901 Kg`** netos), `48` productos en `DBPRODUCTOS` y `263` clientes/proveedores en `DBCLIENTES`.
- **`\\192.168.2.56\bdwm\placilla\WMDBSPC.GDB`:** **`18.031` pesajes activos** (`01-04-2011` al `12-11-2024`, `118.944.543 Kg` netos).
- **`\\192.168.2.56\bdwm\thno\PWAREHOUSE.FDB`:** `PESAJES` (`1.040`), `PESAJES_ITEMS` (`11.724`), `CLI_PROV` (`32`), `PRODUCTOS` (`26`).

---

## Instalación y Uso Inicial

1. Ejecute el instalador deseado desde `Instalador/`:
   - `GestionScrap_Setup_v4.0.0_x64_SelfContained.exe` *(recomendado — cero dependencias previas)*
   - `GestionScrap_Setup_v4.0.0_x64_FrameworkDependent.exe` *(requiere .NET 10 Desktop Runtime x64)*
2. Inicie sesión con la cuenta inicial de administrador sembrada (`barriagada` / `Cambias0`) y **cambie la contraseña inicial en producción** desde **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.
3. En **Importador BD (`Ctrl+4`)**, pruebe e importe directamente desde `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB`.

Para instalación silenciosa en despliegue TI:
```cmd
GestionScrap_Setup_v4.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

---

## Compilación y Pruebas

```powershell
# 1. Restaurar dependencias
dotnet restore GestionScrap.sln

# 2. Compilar Debug y Release (0 errores / 0 advertencias)
dotnet build GestionScrap.sln -c Debug --no-restore
dotnet build GestionScrap.sln -c Release --no-restore

# 3. Ejecutar suite automatizada de 19 pruebas
dotnet test GestionScrap.sln -c Release --no-build --verbosity normal

# 4. Publicar e empaquetar instaladores Inno Setup 7
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained false -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64-fdd
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" --quiet-progress -DVariant=selfcontained installer\GestionScrap.iss
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" --quiet-progress -DVariant=framework installer\GestionScrap.iss
```

---

## Documentación Técnica Completa

- [Arquitectura del Sistema](docs/ARQUITECTURA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación](docs/COMPILACION.md)
- [Guía de Despliegue e Instalación](docs/DESPLIEGUE.md)
- [Seguridad, Perfiles RBAC y Auditoría](docs/SEGURIDAD.md)
- [Plan y Evidencia de Pruebas QA](docs/PRUEBAS_QA.md)
- [Historial de Cambios (Changelog)](docs/CHANGELOG.md)
