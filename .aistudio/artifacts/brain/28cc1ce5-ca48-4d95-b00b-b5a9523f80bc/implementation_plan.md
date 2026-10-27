# Plan de Despliegue y Uso en Windows 11: Sistema de Pesajes e Informes P. Cambiaso

Guía de instalación paso a paso para ejecutar la **Aplicación Web moderna** en tu PC con **Windows 11** y configurar el motor **Firebird localmente** para conectar directamente los archivos `.GDB` y `.FDB` de Placilla, Santiago y Talcahuano.

---

### User Review & Critical Decisions

> [!IMPORTANT]
> **Decisiones Confirmadas por el Usuario:**
> 1. **Modalidad de Ejecución:** Aplicación Web moderna lista para usar en navegador (Chrome / Edge en Windows 11) con capacidad de instalación como PWA (icono directo en escritorio y barra de tareas).
> 2. **Conexión a Bases de Datos Firebird:** Instalación del servidor Firebird de forma local en tu PC Windows 11 (`localhost:3050`), mapeando las rutas locales de los archivos `WMDBSPC.GDB` y `PWAREHOUSE.FDB`.

---

## 1. Paso a Paso para Usar la Aplicación en Windows 11

### Opción A: Uso Inmediato en el Navegador (Sin Instalar Nada Adicional)
1. **Abrir en Edge o Chrome:** Accede a la URL de tu aplicación desde tu navegador en Windows 11.
2. **Instalar como Aplicación Nativa de Escritorio (PWA):**
   - En Microsoft Edge: Haz clic en el menú `...` (arriba a la derecha) ➔ **Aplicaciones** ➔ **Instalar este sitio como una aplicación**.
   - En Google Chrome: Haz clic en el menú `⋮` ➔ **Guardar y compartir** ➔ **Instalar aplicación**.
   - Se creará un acceso directo en tu Escritorio y en el Menú Inicio de Windows 11 con ventana independiente (sin barra de direcciones web), luciendo idéntica a una aplicación de escritorio nativa de Windows.

---

### Opción B: Ejecución Local en tu PC con Node.js

Para tener el código fuente corriendo en tu máquina Windows 11 de forma 100% autónoma:

1. **Instalar Node.js:**
   - Descarga e instala **Node.js 20 LTS** o superior desde [nodejs.org](https://nodejs.org/).
2. **Descargar / Clonar el Proyecto:**
   - Coloca la carpeta del proyecto en una ruta como `C:\Sistemas\InformesPCambiaso`.
3. **Abrir Terminal (PowerShell o Terminal de Windows 11):**
   ```powershell
   cd C:\Sistemas\InformesPCambiaso
   npm install
   npm run dev
   ```
4. **Abrir en el Navegador:**
   - Ingresa a `http://localhost:3000`.

---

## 2. Instalación y Configuración de Firebird en Windows 11

Para leer y sincronizar los archivos de base de datos `.GDB` y `.FDB` directamente en tu PC:

```
┌────────────────────────────────────────────────────────────────────────┐
│                          PC WINDOWS 11 LOCAL                           │
│                                                                        │
│   ┌───────────────────────────┐         ┌──────────────────────────┐   │
│   │   Navegador Web / PWA     │ ──────> │  API Local / Middleware  │   │
│   │  (Informes P. Cambiaso)   │  HTTP   │  (Node.js o .NET 8)      │   │
│   └───────────────────────────┘         └────────────┬─────────────┘   │
│                                                      │ TCP 3050        │
│                                                      ▼                 │
│                                         ┌──────────────────────────┐   │
│                                         │ Firebird SuperServer     │   │
│                                         │ (Servicio Windows)       │   │
│                                         └────────────┬─────────────┘   │
│                                                      │                 │
│                 ┌────────────────────────────────────┴──────────────┐  │
│                 ▼                                                   ▼  │
│      ┌─────────────────────┐                            ┌───────────┴──────────┐
│      │ C:\BDWM\placilla\   │                            │ C:\BDWM\thno\        │
│      │ WMDBSPC.GDB         │                            │ PWAREHOUSE.FDB       │
│      └─────────────────────┘                            └──────────────────────┘
└────────────────────────────────────────────────────────────────────────┘
```

### Paso 1: Descargar e Instalar Firebird
1. Descarga el instalador de **Firebird 2.5.9 (64-bit o 32-bit según tu archivo)** o **Firebird 3.0** desde [firebirdsql.org](https://firebirdsql.org/en/server-packages/).
2. Durante el asistente de instalación:
   - Selecciona **"Run as a Service"** (Ejecutar como Servicio de Windows).
   - Tipo de arquitectura: **"SuperServer"**.
   - Marca la casilla **"Install Guardian component"** (opcional) y **"Copy Firebird client library to <system> directory (fbclient.dll)"**.
   - Contraseña de SYSDBA por defecto: `masterkey`.

### Paso 2: Crear la Estructura de Carpetas para las Bases de Datos
En tu disco `C:\` crea las carpetas idénticas al sistema original para evitar conflictos de rutas:
```powershell
New-Item -ItemType Directory -Path "C:\BDWM\placilla" -Force
New-Item -ItemType Directory -Path "C:\BDWM\placilla2" -Force
New-Item -ItemType Directory -Path "C:\BDWM\thno" -Force
```
Copia tus archivos de base de datos reales:
- `C:\BDWM\placilla\WMDBSPC.GDB` (Placilla 1)
- `C:\BDWM\placilla2\WMDBSPC.GDB` (Santiago)
- `C:\BDWM\thno\PWAREHOUSE.FDB` (Talcahuano)

### Paso 3: Configurar el Driver ODBC / ADO.NET (Opcional para .NET)
- Si deseas conectar aplicaciones de escritorio o el backend en C#, instala **Firebird ODBC Driver** o el paquete NuGet `FirebirdSql.Data.FirebirdClient`.
- La cadena de conexión en tu PC local será:
  ```ini
  User=SYSDBA;Password=masterkey;Database=127.0.0.1:C:\BDWM\placilla2\WMDBSPC.GDB;Dialect=3;
  ```

---

## 3. Guía de Operación Diaria de las 8 Pestañas

Una vez abierta la aplicación en Windows 11:

1. **Pestaña 0 (Grupo Material):**
   - Selecciona el rango de fechas (ej. mes en curso).
   - Conmuta entre **Consolidado Semanal** o **Normal Diario**.
   - Presiona **Exportar Excel (.xlsx)** para descargar el informe consolidado.
2. **Pestaña 1 (Compras + Traslados):**
   - Elige la planta (**Placilla**, **Santiago** o **Talcahuano**).
   - Revisa las 3 tarjetas de totales: Compras, Traslados y Total General.
   - Haz clic en **Exportar Informe** para generar el Excel de 2 hojas idéntico a las plantillas legacy.
3. **Pestaña 2 (Informe por Cliente):**
   - Elige **Buscar por RUT** o **Buscar por Folio**.
   - Haz clic en **Buscar** para abrir el modal de selección de clientes (antiguo formulario `Clientes`).
   - Descarga el ticket consolidado de pesajes en Excel.
4. **Pestaña 3 (Grupos Especiales):**
   - Monitorea simultáneamente Retail Stgo, Retail Empresas, Mix, Regiones, Agrícola, Pesquera y Agrosuper.
5. **Pestaña 4 (Informe JCRAMM):**
   - El sistema carga automáticamente el `ultimo_folio`.
   - Consulta los pesajes nuevos y haz clic en **Imprimir Informe JCRAMM y Guardar Folio** para congelar el nuevo número procesado.
6. **Pestaña 5 (Informe de Precios):**
   - Conmuta entre **Precios Teóricos** y **Precios Reales**.
   - En modo **Precios Reales**, haz **doble clic** en cualquier fila para abrir el modal `Act_precios` y cambiar el precio del pesaje.
   - En modo **Precios Teóricos**, haz clic en **Guardar Precios Teóricos a Reales** para congelarlos en bloque.
7. **Pestaña 6 (Mantenedor Precios):**
   - Ingresa o busca precios pactados. Usa los botones de catálogo de productos y clientes para autocompletar.
8. **Pestaña 7 (Importación Firebird):**
   - Conmuta entre Placilla, Santiago o Talcahuano.
   - Haz clic en **Buscar en Firebird** para leer las 18 columnas.
   - Haz clic en **Importar Datos a Base Central**; el sistema filtrará los duplicados automáticamente.
9. **Barra Superior (Acciones Globales):**
   - **Ejecutar PA Firebird:** Ejecuta `PA_INS_PROV_STGO` y `PA_INS_MASIVO_EXP_SAP` con transacción Commit/Rollback.
   - **Limpiar Tablas:** Purga las 4 tablas de pesajes solicitando la palabra de seguridad `ELIMINAR`.
