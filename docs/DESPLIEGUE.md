# Guía de Despliegue e Instalación · Gestión de Scrap v4.0

## 1. Modalidades de Instalación (Inno Setup 7)

Se generan dos instaladores modernos x64 con **Inno Setup 7 (`7.1.1`)**:

1. **Autocontenido (`GestionScrap_Setup_v4.0.0_x64_SelfContained.exe`):**
   - Incluye el runtime `.NET 10 Desktop x64` completo, SQLite nativo (`e_sqlite3.dll`) y todos los activos visuales Cambiaso embebidos.
   - **Cero dependencias previas requeridas en el equipo cliente.**
2. **Dependiente de Framework (`GestionScrap_Setup_v4.0.0_x64_FrameworkDependent.exe`):**
   - Paquete liviano para estaciones que ya cuentan con **Microsoft Windows Desktop Runtime 10.0 (x64)**.

## 2. Rutas de Instalación y Datos Persistentes

- **Binarios de la aplicación:** `%LocalAppData%\Programs\GestionScrap` (instalación por usuario sin requerir privilegios administrativos, con soporte de elevación si se selecciona instalación global).
- **Base de datos SQLite WAL:** `%LocalAppData%\GestionScrap\gestion_scrap.db`
- **Configuración de endpoints cifrada (DPAPI):** `%LocalAppData%\GestionScrap\settings.json`
- **Bitácora de errores fatales (si ocurriera alguno):** `%LocalAppData%\GestionScrap\error.log`

> **Actualizaciones:** Instalar una versión actualizada sobre el mismo directorio reemplaza únicamente los binarios de aplicación, preservando íntegra la base operativa `%LocalAppData%\GestionScrap\gestion_scrap.db`.

## 3. Despliegue Desatendido (Silencioso)

```cmd
:: Instalación silenciosa para usuario actual
GestionScrap_Setup_v4.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER

:: Desinstalación silenciosa
"%LocalAppData%\Programs\GestionScrap\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

## 4. Puesta en Marcha e Importación desde Firebird

1. Inicie sesión con la cuenta inicial sembrada (`barriagada` / `Cambias0`) y **cambie la contraseña en producción** mediante **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.
2. Verifique en **Config. BD** o **Importador BD (`Ctrl+4`)** el servidor preconfigurado (`192.168.2.56`, puerto `3050`, ruta `C:\bdwm\placilla2\WMDBSPC.GDB`).
3. Ejecute **Probar Conexión**, consulte el rango de fechas deseado e importe pesajes, clientes y productos hacia SQLite WAL (o migre históricos `.mdb`/`.accdb`).
4. Cree periódicamente respaldos SQLite en caliente desde **Seguridad y Perfiles -> Respaldar SQLite (.db)**.
