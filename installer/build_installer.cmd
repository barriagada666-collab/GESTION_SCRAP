@echo off
setlocal enabledelayedexpansion

set ROOT=%~dp0..
pushd "%ROOT%"

echo [1/6] Generando graficos de version v4.0 (.NET 10)...
dotnet run --project installer\AssetBuilder -- "installer\assets" "v4.0"
if errorlevel 1 exit /b %errorlevel%

echo [2/6] Compilando solucion Debug y Release...
dotnet build GestionScrap.sln -c Debug
if errorlevel 1 exit /b %errorlevel%
dotnet build GestionScrap.sln -c Release --no-restore
if errorlevel 1 exit /b %errorlevel%

echo [3/6] Ejecutando pruebas automatizadas (Release)...
dotnet test GestionScrap.sln -c Release --no-build
if errorlevel 1 exit /b %errorlevel%

echo [4/6] Publicando binarios win-x64 (Self-Contained y Framework-Dependent)...
if exist "publish" rmdir /s /q "publish"
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64
if errorlevel 1 exit /b %errorlevel%
dotnet publish src\GestionScrap.Desktop\GestionScrap.Desktop.csproj -c Release -r win-x64 --self-contained false -p:DebugType=none -p:DebugSymbols=false -o publish\win-x64-fdd
if errorlevel 1 exit /b %errorlevel%

echo [5/6] Buscando compilador Inno Setup 7 (ISCC.exe)...
set ISCC=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe
if not exist "!ISCC!" set ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe
if not exist "!ISCC!" set ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe
if not exist "!ISCC!" (
    echo ERROR: No se encontro Inno Setup 7 ISCC.exe
    exit /b 1
)

echo [6/6] Generando instaladores con Inno Setup 7...
"!ISCC!" --quiet-progress -DVariant=selfcontained installer\GestionScrap.iss
if errorlevel 1 exit /b %errorlevel%
"!ISCC!" --quiet-progress -DVariant=framework installer\GestionScrap.iss
if errorlevel 1 exit /b %errorlevel%

echo.
echo Instaladores generados correctamente en "%ROOT%\Instalador":
dir /b Instalador\*.exe

popd
endlocal
