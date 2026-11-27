# Guía de Compilación y Construcción · Gestión de Scrap v4.1.0

## 1. Requisitos del Entorno de Desarrollo

- **Sistema Operativo:** Windows 10 / Windows 11 (x64)
- **SDK:** **.NET 10 SDK** (`10.0.100` o superior)
- **Empaquetador:** **Inno Setup 7** (`7.1.1`) instalado en `%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe` o `C:\Program Files (x86)\Inno Setup 7\ISCC.exe`
- **Opcional (solo migración `.mdb` directa sin puente x86):** Microsoft Access Database Engine (`ACE.OLEDB.16.0` / `12.0`).

## 2. Dependencias NuGet Principales

| Proyecto | Paquete | Versión |
| :--- | :--- | :--- |
| `GestionScrap.Infrastructure` | `Microsoft.Data.Sqlite` | `10.0.15` |
| `GestionScrap.Infrastructure` | `SQLitePCLRaw.lib.e_sqlite3` | `2.1.14` |
| `GestionScrap.Infrastructure` | `FirebirdSql.Data.FirebirdClient` | `10.3.4` |
| `GestionScrap.Infrastructure` | `ClosedXML` | `0.105.1` |
| `GestionScrap.Infrastructure` | `System.Data.OleDb` | `10.0.15` |
| `GestionScrap.Desktop` | `Microsoft.Extensions.DependencyInjection` | `10.0.15` |
| `GestionScrap.Tests` | `xunit` / `xunit.runner.visualstudio` / `Microsoft.NET.Test.Sdk` | `2.9.3` / `3.1.4` / `17.14.1` |

*(Los gráficos e iconos corporativos se encuentran en `installer/assets/` y están embebidos en tiempo de compilación dentro de `GestionScrap.Desktop` como `EmbeddedResource`, sin paquetes UI externos.)*

## 3. Reglas de Compilación Estricta (`Directory.Build.props`)

- `LangVersion`: `14`
- `Nullable`: `enable`
- `TreatWarningsAsErrors`: `true`
- `WarningLevel`: `9999`
- `AnalysisLevel`: `latest-recommended`
- `EnforceCodeStyleInBuild`: `true`

## 4. Comandos de Compilación, Pruebas y Empaquetado

```powershell
# Restaurar paquetes
dotnet restore GestionScrap.sln

# Compilar Debug y Release (0 errores / 0 advertencias)
dotnet build GestionScrap.sln -c Debug --no-restore
dotnet build GestionScrap.sln -c Release --no-restore

# Ejecutar suite automatizada de pruebas
dotnet test GestionScrap.sln -c Release --no-build --verbosity normal

# Regenerar iconos y gráficos HiDPI Cambiaso (si se modifica la imagen fuente)
dotnet run --project installer\AssetBuilder -- "installer\assets" "v4.1.0"

# Construcción automatizada completa (Build Release + Publish x64 + Instaladores Inno Setup 7 + Portable ZIP + SHA-256)
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\Build-Release.ps1 -Version 4.1.0
```

Los artefactos resultantes quedan en `Instalador\`:
1. `GestionScrap_Setup_v4.1.0_x64_SelfContained.exe`
2. `GestionScrap_Setup_v4.1.0_x64_FrameworkDependent.exe`
3. `GestionScrap_Portable_v4.1.0_win-x64.zip`
4. `checksums_sha256.txt`
