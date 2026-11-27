# Guía de Despliegue e Instalación · Gestión de Scrap v4.1.0

## 1. Modalidades de Distribución (Inno Setup 7 + Portable)

Se generan tres paquetes x64 verificados mediante `checksums_sha256.txt`:

1. **Instalador Autocontenido (`GestionScrap_Setup_v4.1.0_x64_SelfContained.exe`):**
   - Incluye el runtime `.NET 10 Desktop x64` completo, SQLite nativo (`e_sqlite3.dll`) y todos los activos visuales Cambiaso embebidos.
   - **Cero dependencias previas requeridas en el equipo cliente.**
2. **Instalador Dependiente de Framework (`GestionScrap_Setup_v4.1.0_x64_FrameworkDependent.exe`):**
   - Paquete liviano para estaciones que ya cuentan con **Microsoft Windows Desktop Runtime 10.0 (x64)**.
3. **Paquete Portable (`GestionScrap_Portable_v4.1.0_win-x64.zip`):**
   - Distribución sin instalador (autocontenida x64), lista para descomprimir y ejecutar `GestionScrap.exe`.

## 2. Rutas de Instalación y Datos Persistentes

- **Binarios de la aplicación:** `%LocalAppData%\Programs\GestionScrap` (instalación por usuario sin requerir privilegios administrativos, con soporte de elevación si se selecciona instalación global).
- **Base de datos SQLite WAL:** `%LocalAppData%\GestionScrap\gestion_scrap.db`
- **Configuración de endpoints cifrada (DPAPI):** `%LocalAppData%\GestionScrap\settings.json`
- **Bitácora diagnóstica diaria (con redacción de secretos):** `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log`
- **Registro de excepciones fatales (si ocurriera alguna):** `%LocalAppData%\GestionScrap\error.log`

> **Actualizaciones:** Instalar una versión actualizada sobre el mismo directorio reemplaza únicamente los binarios de aplicación, preservando íntegra la base operativa `%LocalAppData%\GestionScrap\gestion_scrap.db` y aplicando automáticamente las migraciones de esquema idempotentes (`schema_migrations`) sin pérdida de datos.

## 3. Despliegue Desatendido (Silencioso)

```cmd
:: Instalación silenciosa para usuario actual
GestionScrap_Setup_v4.1.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER

:: Desinstalación silenciosa (conserva %LocalAppData%\GestionScrap\gestion_scrap.db)
"%LocalAppData%\Programs\GestionScrap\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

## 4. Puesta en Marcha e Importación desde Firebird

1. Inicie sesión con la cuenta administrativa sembrada inicialmente (credencial inicial administrada internamente por TI / Operaciones) y **cambie obligatoriamente la contraseña en producción** mediante **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.
2. Verifique en **Config. BD** o **Importador BD (`Ctrl+4`)** el servidor configurado (`192.168.2.56`, puerto `3050`, ruta `C:\bdwm\placilla2\WMDBSPC.GDB`).
3. Ejecute **Probar Conexión**, consulte el rango de fechas deseado e importe pesajes, clientes y productos hacia SQLite WAL (o migre históricos `.mdb`/`.accdb`).
4. Cree periódicamente respaldos SQLite en caliente y verificados desde **Seguridad y Perfiles -> Respaldar SQLite (.db)**.
