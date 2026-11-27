# Guía de Compilación y Construcción · Gestión de Scrap v5.0.0

## 1. Requisitos del Entorno de Desarrollo

- **Sistema Operativo:** Windows 10 / Windows 11 (x64)
- **SDK:** **.NET 10 SDK** (`10.0.100` o superior)
- **Empaquetador:** **Inno Setup 7** (`7.1.1`) instalado en `%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe` o `C:\Program Files (x86)\Inno Setup 7\ISCC.exe`
- **Opcional (migración `.mdb` directa 64 bits):** Microsoft Access Database Engine (`ACE.OLEDB.16.0` / `12.0`).

## 2. Reglas de Compilación Estricta (`Directory.Build.props`)

- `Version`: `5.0.0` (`AssemblyVersion` / `FileVersion`: `5.0.0.0`)
- `LangVersion`: `14`, `Nullable`: `enable`, `TreatWarningsAsErrors`: `true`, `WarningLevel`: `9999`, `AnalysisLevel`: `latest-recommended`, `EnforceCodeStyleInBuild`: `true`.
- **Cero supresiones:** prohibidos `NoWarn`, `#pragma warning disable`, `SuppressMessage` y `async void` en `src/`.

## 3. Comandos de Compilación, Pruebas y Empaquetado

```powershell
# Restaurar paquetes
dotnet restore GestionScrap.sln

# Compilar Debug y Release (0 errores / 0 advertencias)
dotnet build GestionScrap.sln -c Debug --no-restore --no-incremental --disable-build-servers
dotnet build GestionScrap.sln -c Release --no-restore --no-incremental --disable-build-servers

# Ejecutar suite automatizada completa (34/34 pruebas xUnit)
dotnet test GestionScrap.sln -c Release --no-build --disable-build-servers --verbosity normal

# Regenerar iconos y gráficos HiDPI Cambiaso (v5.0.0)
dotnet run --project installer\AssetBuilder -- "installer\assets" "v5.0.0"

# Construcción automatizada completa (Debug + Release + Tests + Publish x64 + Inno Setup 7 + Portable ZIP + SHA-256)
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\Build-Release.ps1
```

Los artefactos resultantes quedan en `Instalador\`:
1. `GestionScrap_Setup_v5.0.0_x64_SelfContained.exe`
2. `GestionScrap_Setup_v5.0.0_x64_FrameworkDependent.exe`
3. `GestionScrap_Portable_v5.0.0_win-x64.zip`
4. `checksums_sha256.txt`
