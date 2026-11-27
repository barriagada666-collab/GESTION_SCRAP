# Gestión de Scrap v4.1.1 — Notas de Versión (Release Notes)

- **Versión publicada:** `v4.1.1` (`AssemblyVersion` / `FileVersion`: `4.1.1.0`)
- **Fecha de liberación:** `26-11-2026`
- **Versión estable anterior:** `v4.1.0` (commit `63308874d49ef01c4ea63d838dfc4f35dced26ab`)
- **Plataforma objetivo:** Windows 10 / Windows 11 `x64` · `.NET 10` (`C# 14`, WinForms `PerMonitorV2`) · Inno Setup `7.1.1`
- **Repositorio y Releases:** https://github.com/barriagada666-collab/GESTION_SCRAP/releases/tag/v4.1.1

---

## 1. Resumen de Correcciones y Mejoras (`v4.1.1`)

### Reparación Integral de Botones, Formularios y Espaciados High-DPI
- **Botones Sin Recortes ni Saltos de Línea (`AppTheme.EnsureButtonFitsText`):** Todos los botones del sistema calculan automáticamente su ancho mínimo en una sola línea según `TextRenderer.MeasureText(..., SingleLine | NoPrefix)` y el `DeviceDpi` activo, eliminando recortes en `Config. BD`, `Mi Clave`, `Salir`, barras de filtros y barra CRUD de `Grupos de Material`.
- **Contenedores Autoajustables (`UiHelpers.BuildAutoFilterCard`):** Los paneles de filtros en los 5 módulos y la barra CRUD del árbol en `GrupoMaterialControl` se ajustan dinámicamente en altura al escalar en High DPI (125 % / 150 %), evitando solapamientos sobre el árbol y las grillas.
- **Diálogos Modales y `LoginForm` Reparados:** Incorporado `AppTheme.CreateDialogFooter` (botones siempre visibles y alineados a la derecha) y corregido el tamaño de cuerpo antes del anclaje en `LoginForm`, `CambiarPasswordDialog`, `ConfiguracionDbDialog`, `ExportacionPersonalizadaDialog`, `LimpiarTablasDialog`, `MaterialNodeEditorDialog` y `MoverNodoDialog`.
- **Layouts Responsivos en `ImportadorControl` y `SeguridadPerfilesControl`:** Eliminadas coordenadas fijas en la pestaña Access `.mdb` y corregida la barra de mantenimiento SQLite para que rutas largas nunca desplacen fuera de vista a `Respaldar SQLite (.db)` ni `Purgar Pesajes`.
- **Encabezados `DataGridView` Legibles y Anchos Mínimos:** Fijado contraste `SelectionBackColor = HeaderDark` / `SelectionForeColor = White` en todas las grillas y agregado `MinimumWidth` automático por encabezado para evitar columnas truncadas (`F...`, `T...`, `Ac...`, `Viaj...`).
- **Gráficos GDI+ Pulidos (`DoubleBufferedControls`):** Leyenda separada debajo del título en tendencia comparativa, límite exacto de filas en gráficos de barras y elipsis controlada en leyendas Donut.
- **Visualización de `&` en Etiquetas:** Activado `UseMnemonic = false` en `SplashForm` (`DIVISIÓN SCRAP & BÁSCULAS`) y `MaterialNodeEditorDialog`.
- **Pruebas Automatizadas UI Endurecidas (`27/27`):** `WinFormsAuditTests` ahora valida geométricamente texto en 1 línea, cero recortes, cero solapamientos, altura de footers, no-desbordamiento de campos en diálogos y contraste de cabeceras.

---

## 2. Requisitos del Sistema

| Modalidad | Requisitos |
| :--- | :--- |
| **Instalador Autocontenido (`.exe`)** | Windows 10 / Windows 11 `x64`. **Sin dependencias previas.** |
| **Instalador Framework-Dependent (`.exe`)** | Windows 10 / Windows 11 `x64` + **Microsoft Windows Desktop Runtime 10.0 (x64)**. |
| **Paquete Portable (`.zip`)** | Windows 10 / Windows 11 `x64`. Descomprimir y ejecutar `GestionScrap.exe`. |

---

## 3. Instalación y Actualización desde `v4.1.0` / `v4.0.0`

1. Cierre **Gestión de Scrap** si se encuentra abierto.
2. Descargue **`GestionScrap_Setup_v4.1.1_x64_SelfContained.exe`** (recomendado) y verifique su hash SHA-256:
   ```powershell
   Get-FileHash .\GestionScrap_Setup_v4.1.1_x64_SelfContained.exe -Algorithm SHA256
   ```
3. Ejecute el instalador (o despliegue silencioso):
   ```cmd
   GestionScrap_Setup_v4.1.1_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
   ```
   *(La actualización reemplaza únicamente los binarios en `%LocalAppData%\Programs\GestionScrap`, conservando intacta su base de datos `%LocalAppData%\GestionScrap\gestion_scrap.db` y su configuración `settings.json`.)*

---

## 4. Archivos Incluidos y Verificación SHA-256

| Archivo | Tamaño (Bytes) | Tamaño (MB) | SHA-256 |
| :--- | ---: | ---: | :--- |
| `GestionScrap_Setup_v4.1.1_x64_SelfContained.exe` | `40.046.814` | `38,19 MB` | `9FB06E98B83ABC41DCEF57247C55E3AFB3DB1C3FE7DB6B705D25BED06A795678` |
| `GestionScrap_Setup_v4.1.1_x64_FrameworkDependent.exe` | `5.662.190` | `5,40 MB` | `77D503FA045ADDBD501E595BC9CC2CAF9909EC73E6BEF51637EA611D17B7AB3D` |
| `GestionScrap_Portable_v4.1.1_win-x64.zip` | `56.696.993` | `54,07 MB` | `8B8B0CAAA99094394035AEF499454B6246DF2EE7781EF993B4342170366AD9C2` |
| `checksums_sha256.txt` | — | — | Listado oficial de sumas de verificación SHA-256 |
| `README-v4.1.1.md` | — | — | Este documento de notas de versión |

---

## 5. Compatibilidad y Recuperación / Rollback

- **Compatibilidad SQLite:** 100 % compatible sin cambios de esquema respecto a `v4.1.0`.
- **Rollback a `v4.1.0`:** Desinstalar `v4.1.1` y ejecutar `git checkout v4.1.0` o reinstalar el ejecutable de la Release [v4.1.0](https://github.com/barriagada666-collab/GESTION_SCRAP/releases/tag/v4.1.0).
