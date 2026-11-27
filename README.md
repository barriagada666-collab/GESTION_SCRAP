<div align="center">
  <img src="installer/assets/app_logo.png" alt="Cambiaso Gestión de Scrap Logo" width="128" />
  <h1>Cambiaso · Gestión de Scrap, CRM, ERP y Pesajes v5.0.0</h1>
  <p><strong>Plataforma Empresarial .NET 10 x64 para CRM Unificado, Operaciones ERP, Pesajes Industriales (1 y 2 Mediciones), Flota/Patentes, Libro Mayor de Inventario, Informes Multi-Selección e Importación Firebird / Access</strong></p>

  ![Version](https://img.shields.io/badge/Version-v5.0.0-0B3C5D?style=for-the-badge)
  ![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms%20x64-0E608A?style=for-the-badge&logo=dotnet)
  ![SQLite WAL](https://img.shields.io/badge/Database-SQLite%20WAL%20v3%20%2B%20Firebird-0891B2?style=for-the-badge&logo=sqlite)
  ![Inno Setup 7](https://img.shields.io/badge/Installer-Inno%20Setup%207-C9A46E?style=for-the-badge)
  ![Tests](https://img.shields.io/badge/Tests-34%2F34%20Passing-059669?style=for-the-badge)
  ![Warnings](https://img.shields.io/badge/Build-0%20Errors%20%7C%200%20Warnings-10B981?style=for-the-badge)
  ![License](https://img.shields.io/badge/License-MIT-475569?style=for-the-badge)
</div>

---

## Descripción General

**Gestión de Scrap v5.0.0** es la plataforma corporativa integral de **Cambiaso Hnos. S.A.C.** desarrollada en **.NET 10 (`C# 14` + Windows Forms `x64` `PerMonitorV2`)** y empaquetada con **Inno Setup 7 (`7.1.1`)**. Amplía la arquitectura histórica de consolidación de pesajes Firebird/Access incorporando de manera unificada **CRM de Clientes y Proveedores por RUT sin duplicación**, **Operaciones Comerciales ERP**, **Gestión Integral de Pesajes Industriales (1 y 2 mediciones)**, **Flota de Vehículos/Patentes normalizadas**, **Libro Mayor Inmutable de Inventario** y un **Centro de Informes Empresariales con 9 segmentadores multi-selección**.

Conserva el 100 % de los datos productivos previos (`32.178+` pesajes, `546+` entidades, `52+` materiales y `56+` nodos jerárquicos) mediante migración automática e idempotente **SQLite WAL `v3`** con `PRAGMA foreign_keys = ON`, exportación nativa **Excel (`.xlsx`)** vía **ClosedXML** (sin dependencia de Office COM), menús contextuales con clic derecho en todas las grillas operacionales y seguridad granular **RBAC** (`13` permisos).

---

## Módulos Integrados (`Ctrl+1` .. `Ctrl+8`)

1. **Informe Resumen, Segmentación y Comparativos YoY/MoM (`Ctrl+1`):**
   - KPIs ejecutivos, ficha estadística cuantitativa (promedio, mediana, mín/máx, desviación estándar, ritmo diario, mejor mes/año), comparativos **YoY** / **MoM**, exclusión de ceros y exportación personalizada.
2. **Centro de Informes Empresariales (`Ctrl+2`):**
   - **Rango por defecto mensual local:** desde el **día 1 del mes actual** hasta la **fecha actual del equipo** (`Mes Actual` / `Histórico`).
   - **9 Segmentadores Multi-Selección (OR dentro de categoría, AND entre categorías):** `Proveedores`, `Clientes`, `Materiales`, `Grupos`, `Patentes`, `Tipos Pesaje`, `Años`, `Meses` y `Estados`, cada uno con búsqueda interna, `Todos`, `Ninguno`, contador de activos y barra resumen de filtros.
   - **6 Informes 100 % Consistentes + Detalle:** `Por Patente`, `Por Material`, `Por Proveedor`, `Por Cliente`, `Por RUT (Rol CRM)`, `Por Tipo de Pesaje` y `Detalle de Pesajes`, alimentados desde una única fuente filtrada con exportación multi-hoja Excel y menú contextual clic derecho.
3. **Pesajes Industriales y Operaciones ERP (`Ctrl+3`):**
   - **Pesajes 1 y 2 Mediciones:** Regla estricta `Neto = Bruto - Tara`, bloqueo de pesos inválidos o inconsistencias de unidad, flujo 2 pasos que preserva fecha/hora y valor de la 1ª medición sin impactar inventario anticipadamente, control de tolerancias por material (`Pendiente Autorización`), correcciones y anulaciones autorizadas con historial inmutable (`pesajes_historial`) sin borrado físico.
   - **Órdenes ERP:** `OrdenCompra`, `OrdenVenta`, `RecepcionMaterial`, `EntregaDespacho`, `TrasladoInterno` con trazabilidad completa `Orden -> Pesajes -> Inventario -> Documentos`.
   - **Inventario (Libro Mayor Inmutable):** Stock derivado exclusivamente de movimientos confirmados (`Entradas - Salidas + Ajustes == StockActual`), índice único SQLite contra movimientos duplicados por pesaje, reversos automáticos por anulación y ajustes autorizados auditados.
   - **Tipos de Pesaje Configurables:** Administración CRUD de tipos (`Recepción`, `Despacho`, `Entrada`, `Salida`, `Carga`, `Descarga`, `Individual`, `Entrada/Salida`, `Movimiento Interno` y personalizados).
4. **CRM y Maestros (`Ctrl+4`):**
   - **CRM Clientes y Proveedores (RUT Único):** Validación Módulo 11 sin duplicados (`Cliente`, `Proveedor` o `Cliente y Proveedor`), **Ficha 360°** con pesajes, operaciones, vehículos y documentos comerciales, activación/desactivación y protección contra borrado físico con historial.
   - **Catálogo de Materiales:** CRUD completo con código único, grupo, unidad (`KG`/`TON`), tolerancia de diferencia `%`, filtros combinados e historial de stock y pesajes.
   - **Vehículos y Patentes (Flota):** Normalización estricta (`AB-CD-12` == `abcd12`) evitando duplicados, búsqueda exacta/parcial e historial completo de pesajes por vehículo.
5. **Grupos de Material — Árbol CRUD Jerárquico (`Ctrl+5`):** Árbol editable Padre / Subgrupo / Producto Hijo con Drag & Drop, menú contextual y consolidado exportable.
6. **Análisis de Proveedores Pareto ABC (`Ctrl+6`):** Ranking Pareto ABC, variaciones, mix de materiales, flota, menú contextual y exportación personalizada.
7. **Importador BD Firebird / Access (`Ctrl+7`):** Sincronización 1 clic desde `WMDBSPC.GDB` / `PWAREHOUSE.FDB` y `.mdb`/`.accdb` con historial trazable `import_history`.
8. **Seguridad, Perfiles RBAC, Auditoría e Integridad (`Ctrl+8`):** `13` permisos granulares, usuarios `PBKDF2-SHA256`, protección del último administrador, `audit_log`, verificación de integridad SQLite y respaldo en caliente.

---

## Descarga e Instalación (`v5.0.0`)

Descargue el paquete deseado desde **[GitHub Releases — v5.0.0](https://github.com/barriagada666-collab/GESTION_SCRAP/releases/tag/v5.0.0)**:

| Artefacto | Modalidad | Uso Recomendado |
| :--- | :--- | :--- |
| `GestionScrap_Setup_v5.0.0_x64_SelfContained.exe` | Instalador Inno Setup 7 (Autocontenido) | **Recomendado** — incluye runtime `.NET 10 x64`, cero requisitos previos |
| `GestionScrap_Setup_v5.0.0_x64_FrameworkDependent.exe` | Instalador Inno Setup 7 (Liviano) | Estaciones con `.NET 10 Desktop Runtime x64` ya instalado |
| `GestionScrap_Portable_v5.0.0_win-x64.zip` | Paquete Portable Autocontenido | Descomprimir y ejecutar `GestionScrap.exe` sin instalador |
| `checksums_sha256.txt` | Verificación criptográfica | Validar integridad de descarga (`Get-FileHash -Algorithm SHA256`) |

### Instalación Silenciosa y Datos Persistentes

```cmd
:: Instalación desatendida para el usuario actual
GestionScrap_Setup_v5.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

- **Base de datos SQLite WAL (`v3`):** `%LocalAppData%\GestionScrap\gestion_scrap.db` *(conservada y migrada automáticamente sin pérdida al actualizar)*
- **Configuración cifrada (DPAPI):** `%LocalAppData%\GestionScrap\settings.json`
- **Logs diagnósticos diarios:** `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log`
- **Acceso inicial:** Inicie sesión con la cuenta administrativa aprovisionada por TI / Operaciones y **cambie la contraseña en el primer inicio de sesión** mediante **Mi Clave** o **Seguridad y Perfiles (`Ctrl+8`)**.

---

## Compilación, Pruebas y Empaquetado

```powershell
# 1. Restaurar dependencias
dotnet restore GestionScrap.sln

# 2. Compilar en Debug y Release (estricto: TreatWarningsAsErrors=true, 0 errores / 0 advertencias)
dotnet build GestionScrap.sln -c Debug --no-restore --no-incremental --disable-build-servers
dotnet build GestionScrap.sln -c Release --no-restore --no-incremental --disable-build-servers

# 3. Ejecutar la suite automatizada completa (34/34 pruebas xUnit)
dotnet test GestionScrap.sln -c Release --no-build --disable-build-servers --verbosity normal

# 4. Generar instaladores Inno Setup 7 + Portable ZIP + checksums_sha256.txt
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\Build-Release.ps1
```

---

## Documentación Técnica y Evidencias

- [Notas de Versión — Release v5.0.0](docs/releases/README-v5.0.0.md)
- [Modelo de Datos, Diccionario de Campos y Reglas de Pesaje](docs/MODELO_DATOS_Y_DICCIONARIO.md)
- [Historial de Cambios (CHANGELOG)](docs/CHANGELOG.md)
- [Arquitectura del Sistema](docs/ARQUITECTURA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación y Construcción](docs/COMPILACION.md)
- [Guía de Despliegue e Instalación](docs/DESPLIEGUE.md)
- [Seguridad, Perfiles RBAC y Auditoría](docs/SEGURIDAD.md)
- [Plan y Evidencia de Pruebas QA](docs/PRUEBAS_QA.md)

---

## Licencia

Distribuido bajo licencia **[MIT](LICENSE)** — Copyright © 2026 **Cambiaso Hnos. S.A.C.**
