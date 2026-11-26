# Guía de Despliegue e Instalación · Gestión de Scrap v4.0

## 1. Modalidades de Instalación (Inno Setup 7)

Se entregan dos instaladores x64 compilados con **Inno Setup 7 (`7.1.1`)**:

1. **Autocontenido (`GestionScrap_Setup_v4.0.0_x64_SelfContained.exe`)**
   - Incluye el runtime `.NET 10 Desktop x64` y librerías nativas SQLite (`e_sqlite3.dll`).
   - Recomendado para equipos cliente sin conexión a internet o sin runtime previo.
2. **Dependiente de Framework (`GestionScrap_Setup_v4.0.0_x64_FrameworkDependent.exe`)**
   - Paquete compacto para estaciones que ya disponen de **Microsoft Windows Desktop Runtime 10.0 (x64)**.

## 2. Ruta de Instalación y Datos Persistentes

- **Binarios de aplicación:**
  - Por defecto: `%LocalAppData%\Programs\GestionScrap` (sin requerir privilegios administrativos; permite elevación manual para instalación global si el usuario la selecciona).
- **Datos y configuración local del usuario:**
  - Base SQLite WAL: `%LocalAppData%\GestionScrap\gestion_scrap.db`
  - Configuración protegida (DPAPI): `%LocalAppData%\GestionScrap\settings.json`
  - Bitácora de errores fatales (si ocurriera alguno): `%LocalAppData%\GestionScrap\error.log`

> **Actualizaciones:** Reinstalar o ejecutar una versión más reciente sobre el mismo directorio reemplaza los binarios conservando intacta la base SQLite `%LocalAppData%\GestionScrap\gestion_scrap.db`.

## 3. Instalación / Actualización / Desinstalación Desatendida (TI)

```cmd
:: Instalación silenciosa para usuario actual
GestionScrap_Setup_v4.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER

:: Desinstalación silenciosa
"%LocalAppData%\Programs\GestionScrap\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

## 4. Puesta en Marcha y Migración Inicial desde Access

1. Inicie sesión con la cuenta administrativa inicial configurada (consulte [SEGURIDAD.md](SEGURIDAD.md)) y cambie inmediatamente la contraseña desde **Mi Clave** o **Seguridad y MDB**.
2. Si proviene del sistema heredado Access:
   - Ingrese a **Seguridad y MDB** (`Ctrl+0`) -> pestaña **Migración Access MDB y Respaldo**.
   - Seleccione su archivo `BD_SCRAP.mdb` y presione **Iniciar Migración**.
   - El sistema importa pesajes de las 3 plantas, catálogo de clientes, familias de productos, tarifas pactadas e índices reales sin requerir arquitectura de 32 bits en la app principal.
3. Configure las direcciones IP y rutas Firebird (`.GDB`/`.FDB`) en **Config. BD** dentro de la barra superior.
4. Programe copias periódicas utilizando **Respaldar SQLite (.db)**.
