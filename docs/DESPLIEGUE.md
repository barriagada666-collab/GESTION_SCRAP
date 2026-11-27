# Guía de Despliegue e Instalación · Gestión de Scrap v5.0.0

## 1. Modalidades de Distribución (Inno Setup 7 + Portable)

Se generan tres paquetes x64 verificados mediante `checksums_sha256.txt`:

1. **Instalador Autocontenido (`GestionScrap_Setup_v5.0.0_x64_SelfContained.exe`):**
   - Incluye el runtime `.NET 10 Desktop x64` completo, SQLite nativo (`e_sqlite3.dll`) y todos los activos visuales Cambiaso embebidos. **Cero requisitos previos.**
2. **Instalador Dependiente de Framework (`GestionScrap_Setup_v5.0.0_x64_FrameworkDependent.exe`):**
   - Paquete liviano para estaciones que ya cuentan con **Microsoft Windows Desktop Runtime 10.0 (x64)**.
3. **Paquete Portable (`GestionScrap_Portable_v5.0.0_win-x64.zip`):**
   - Distribución autocontenida sin instalador lista para descomprimir y ejecutar `GestionScrap.exe`.

## 2. Rutas de Instalación y Migración Automática a Esquema `v3`

- **Binarios de la aplicación:** `%LocalAppData%\Programs\GestionScrap`
- **Base de datos SQLite WAL (`v3`):** `%LocalAppData%\GestionScrap\gestion_scrap.db`
- **Configuración cifrada (DPAPI):** `%LocalAppData%\GestionScrap\settings.json`
- **Bitácora diagnóstica diaria (sanitizada):** `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log`

> **Actualización sin pérdida desde `v4.x`:** Al ejecutar **v5.0.0** sobre una base existente (`v4.0.0`, `v4.1.0` o `v4.1.1`), la migración idempotente `v3` conserva el 100 % de pesajes, clientes, productos, árbol de materiales, usuarios y auditoría, poblando automáticamente la flota desde patentes históricas válidas, sembrando los 9 tipos de pesaje configurables y activando los nuevos permisos RBAC `10..13`.

## 3. Despliegue Silencioso (Desatendido)

```cmd
:: Instalación silenciosa para usuario actual
GestionScrap_Setup_v5.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER

:: Desinstalación silenciosa (conserva %LocalAppData%\GestionScrap\gestion_scrap.db)
"%LocalAppData%\Programs\GestionScrap\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```
