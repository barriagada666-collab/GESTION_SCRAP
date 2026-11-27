param(
    [switch]$SkipInstaller,
    [switch]$SkipAssetRebuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Assert-LastExitCode([string]$stepName) {
    if ($LASTEXITCODE -ne 0) {
        throw "Falló el paso '$stepName' con código de salida $LASTEXITCODE."
    }
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$appVersion = $props.Project.PropertyGroup.Version.Trim()
Write-Host "=== Gestión de Scrap v$appVersion — Pipeline Oficial de Compilación y Empaquetado ==="

if (-not $SkipAssetRebuild) {
    Write-Host "=== 1. Regenerando activos HiDPI del instalador (v$appVersion) ==="
    dotnet run --project installer\AssetBuilder -- "installer\assets" "v$appVersion"
    Assert-LastExitCode "AssetBuilder"
}

Write-Host "=== 2. Restaurando dependencias (AnyCPU + win-x64) ==="
dotnet restore GestionScrap.sln
Assert-LastExitCode "dotnet restore GestionScrap.sln"
dotnet restore src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -r win-x64
Assert-LastExitCode "dotnet restore Desktop win-x64"

Write-Host "=== 3. Compilando Debug y Release (0 errores, 0 advertencias) ==="
dotnet build GestionScrap.sln -c Debug --no-restore --no-incremental --disable-build-servers
Assert-LastExitCode "dotnet build Debug"
dotnet build GestionScrap.sln -c Release --no-restore --no-incremental --disable-build-servers
Assert-LastExitCode "dotnet build Release"

Write-Host "=== 4. Ejecutando pruebas automatizadas (Release) ==="
dotnet test GestionScrap.sln -c Release --no-build --disable-build-servers --verbosity normal
Assert-LastExitCode "dotnet test Release"

Write-Host "=== 5. Publicando binarios win-x64 (SelfContained + FrameworkDependent) ==="
$pubRoot = Join-Path $root "publish"
if (Test-Path $pubRoot) { Remove-Item $pubRoot -Recurse -Force }
$pubSc = Join-Path $pubRoot "win-x64"
$pubFdd = Join-Path $pubRoot "win-x64-fdd"

dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -p:DebugType=none -p:DebugSymbols=false -o $pubSc
Assert-LastExitCode "dotnet publish SelfContained"
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained false --no-restore -p:DebugType=none -p:DebugSymbols=false -o $pubFdd
Assert-LastExitCode "dotnet publish FrameworkDependent"

if (-not (Test-Path (Join-Path $pubSc "GestionScrap.exe"))) { throw "Falta GestionScrap.exe en $pubSc" }
if (-not (Test-Path (Join-Path $pubFdd "GestionScrap.exe"))) { throw "Falta GestionScrap.exe en $pubFdd" }

# Limpiar símbolos y XML docs de los directorios publicados antes de empaquetar
Get-ChildItem -Path $pubSc,$pubFdd -Recurse -File -Include *.pdb,*.xml | Remove-Item -Force -ErrorAction SilentlyContinue

$outDir = Join-Path $root "Instalador"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Empaquetar versión Portable ZIP (win-x64 SelfContained)
$portableName = "GestionScrap_Portable_v${appVersion}_win-x64.zip"
$portablePath = Join-Path $outDir $portableName
if (Test-Path $portablePath) { Remove-Item $portablePath -Force }
Write-Host "=== 6. Generando paquete portable $portableName ==="
Compress-Archive -Path (Join-Path $pubSc "*") -DestinationPath $portablePath -CompressionLevel Optimal

$artifacts = New-Object System.Collections.Generic.List[string]
$artifacts.Add($portablePath)

if (-not $SkipInstaller) {
    $isccCmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    $cmdPath = if ($isccCmd) { $isccCmd.Source } else { $null }
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        $cmdPath
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw "No se encontró Inno Setup 7 (ISCC.exe)."
    }

    Write-Host "=== 7. Compilando instaladores Inno Setup ($iscc) ==="
    & $iscc /Qp -DVariant=selfcontained (Join-Path $root "installer\GestionScrap.iss")
    Assert-LastExitCode "ISCC selfcontained"
    & $iscc /Qp -DVariant=framework (Join-Path $root "installer\GestionScrap.iss")
    Assert-LastExitCode "ISCC framework"

    $scSetup = Join-Path $outDir "GestionScrap_Setup_v${appVersion}_x64_SelfContained.exe"
    $fddSetup = Join-Path $outDir "GestionScrap_Setup_v${appVersion}_x64_FrameworkDependent.exe"
    if (-not (Test-Path $scSetup)) { throw "No se generó $scSetup" }
    if (-not (Test-Path $fddSetup)) { throw "No se generó $fddSetup" }

    $artifacts.Insert(0, $fddSetup)
    $artifacts.Insert(0, $scSetup)
}

Write-Host "=== 8. Generando checksums_sha256.txt ==="
$checksumLines = foreach ($file in $artifacts) {
    $h = (Get-FileHash $file -Algorithm SHA256).Hash
    $n = Split-Path $file -Leaf
    $sz = (Get-Item $file).Length
    Write-Host "  $n ($sz bytes) -> $h"
    "$h  $n"
}
$checksumPath = Join-Path $outDir "checksums_sha256.txt"
$checksumLines | Set-Content -Path $checksumPath -Encoding UTF8
Write-Host "Checksums guardados en: $checksumPath"
