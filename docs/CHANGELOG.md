# Historial de Cambios (Changelog) · Gestión de Scrap

## [4.1.1] — 2026-11-26 — Reparación Integral UI High-DPI, Botones, Diálogos, Grillas y Gráficos

### Interfaz de Usuario, Botones y Escalado High-DPI (`PerMonitorV2`)
- **Dimensionamiento Automático de Botones en Una Línea (`AppTheme.EnsureButtonFitsText`):** Recálculo dinámico con `TextRenderer.MeasureText(..., SingleLine | NoPrefix)` sensible al `DeviceDpi`, eliminando saltos de línea, textos cortados y botones vacíos (`Config. BD`, `Mi Clave`, `Salir`, barra CRUD de `Grupos de Material` y barras de filtros).
- **Tarjetas de Filtros y Toolbars Autoajustables (`UiHelpers.BuildAutoFilterCard`):** Reemplazadas las alturas fijas de filtros y la barra CRUD de [GrupoMaterialControl.cs](../src/GestionScrap.Desktop/Controls/GrupoMaterialControl.cs) por contenedores `AutoSize` con sincronización de `MaximumSize`, evitando recortes verticales y superposición sobre el `TreeView`.
- **Pies Uniformes y Anclaje Seguro en `LoginForm` y los 6 Diálogos Modales:** Nuevo `AppTheme.CreateDialogFooter` con altura explícita y botones alineados a la derecha; corregido el desbordamiento por `Anchor` en `CambiarPasswordDialog`, `ConfiguracionDbDialog`, `ExportacionPersonalizadaDialog`, `LimpiarTablasDialog`, `MaterialNodeEditorDialog` y `MoverNodoDialog`; corregida visualización del carácter `&` (`UseMnemonic = false`) en `SplashForm` y `MaterialNodeEditorDialog`.
- **Layouts Responsivos en Importador y Seguridad:** Migradas la fila Access `.mdb` de [ImportadorControl.cs](../src/GestionScrap.Desktop/Controls/ImportadorControl.cs) y la barra inferior de respaldo/purga en [SeguridadPerfilesControl.cs](../src/GestionScrap.Desktop/Controls/SeguridadPerfilesControl.cs) a `TableLayoutPanel` responsivo sin coordenadas fijas.
- **Contraste de Cabeceras `DataGridView` y Gráficos GDI+:** Fijados `SelectionBackColor = HeaderDark` y `SelectionForeColor = Color.White` en cabeceras de todas las grillas, calculados anchos mínimos `MinimumWidth` según texto de encabezado y corregidos solapamientos/recortes en gráficos de tendencia, barras y donut en [DoubleBufferedControls.cs](../src/GestionScrap.Desktop/Controls/DoubleBufferedControls.cs).
- **Auditoría Automatizada UI Endurecida:** [WinFormsAuditTests.cs](../tests/GestionScrap.Tests/UI/WinFormsAuditTests.cs) ahora verifica ajuste en 1 línea, cero recortes, altura mínima de footers, cero solapamientos entre hermanos, cero desbordamiento en diálogos y alto contraste de cabeceras (`27/27` pruebas exitosas, `0` errores, `0` advertencias).

---

## [4.1.0] — 2026-11-26 — Auditoría Integral, Migraciones SQLite, Rendimiento, Logger Sanitizado y CI/CD

### Base de Datos SQLite y Optimización de Rendimiento
- **Integridad Referencial por Conexión (`PRAGMA foreign_keys = ON` + `busy_timeout = 5000`):** Aplicado sistemáticamente en `SqliteScrapRepository.OpenConnectionAsync`, corrigiendo eliminaciones en cascada en conexiones nuevas sin inicialización previa.
- **Migraciones Versionadas e Idempotentes (`schema_migrations`):** Esquema `v1` + `v2` con 5 índices compuestos (`idx_pesajes_planta_fecha_folio`, `idx_pesajes_fecha_folio`, `idx_pesajes_folio_hora`, `idx_audit_accion_id`, `idx_audit_usuario_id`) eliminando `TEMP B-TREE` en consultas de pesajes y `FULL TABLE SCAN` en `audit_log`.
- **Agregación SQL Directa en Resumen de BD:** `GetDatabaseResumenStatsAsync` ahora delega en `GetDatabaseSummaryMetricsAsync`, reduciendo la consulta sobre `32.178` registros de `185,78 ms` a `~11 ms` (`-93,5 %`).
- **Importación y Árbol Optimizados:** Deduplicación por proyección ligera `(folio, hora_ingreso)` + `SqliteCommand` preparado en `ImportPesajesAsync`; `GetPerfilByIdAsync` directo por `WHERE id = @id LIMIT 1`; resolución `CollectDescendantCodes` en `O(1)` con protección contra ciclos.
- **Verificación de Integridad y Respaldos Post-Verificados:** Agregados `VerifyIntegrityAsync` (`PRAGMA quick_check` + `PRAGMA foreign_key_check`), `VerifyDatabaseIntegrityAsync` con RBAC y validación `quick_check` sobre copias generadas por `BackupDatabaseAsync`.

### Seguridad, Diagnóstico y Estabilidad UI
- **Protección del Último Administrador Activo:** `EnsureNotRemovingLastActiveAdminAsync` bloquea eliminación, desactivación o degradación del último administrador activo.
- **Logger Estructurado Diario con Redacción de Secretos (`FileAppLogger`):** Rotación diaria en `%LocalAppData%\GestionScrap\logs\gestionscrap_YYYY-MM-DD.log`, retención de 30 días y redacción automática de contraseñas, tokens Bearer, PAT y cadenas `DPAPI:`.
- **Captura Global Asíncrona y Corrección GDI+:** Agregado manejador `TaskScheduler.UnobservedTaskException`; corregido `AppTheme.LoadEmbeddedImage` para devolver `Bitmap` independiente de `MemoryStream`; caché de fuentes en `AppTheme` y metadatos centralizados en `AppMetadata`.
- **Sanitización Documental y Licencia MIT:** Eliminadas credenciales por defecto en texto plano de `README.md` y toda la carpeta `docs/`; incorporada licencia `LICENSE` (MIT) al repositorio e instalador Inno Setup 7.

### Calidad, Pruebas, Empaquetado y DevOps
- **Compilación y Pruebas:** `0` errores y `0` advertencias en `Debug` y `Release`, `0` vulnerabilidades NuGet y **`27/27` pruebas `xUnit` exitosas** (incluyendo migración sobre copia real de `32.178` registros).
- **Empaquetado y Pipelines:** Script reproducible `scripts/Build-Release.ps1`, instaladores Inno Setup `7.1.1` (`SelfContained` + `FrameworkDependent`), paquete `Portable ZIP`, `checksums_sha256.txt`, `ci.yml` endurecido y nuevo workflow `release.yml`.

---

## [4.0.0] - Arquitectura .NET 10, Branding Cambiaso, Analítica YoY/MoM y Trazabilidad de Importaciones

### Analítica Avanzada, Exclusión de Ceros y Exportación Personalizada
- **Exclusión de Totales en Cero (`Excluir totales en 0`):** Agregado filtro interactivo y de negocio transversal en `InformeResumenControl`, `GrupoMaterialControl` y `AnalisisProveedoresControl`.
- **Comparativos Año a Año (YoY) y Mes a Mes (MoM) + Fichas Estadísticas:** Agregados cálculos de toneladas, mediana, mínimo, máximo, desviación estándar, ritmo diario, variación interanual exacta (`YoY`) y evolución mensual (`MoM` y `YoY`) en [BusinessRules.cs](../src/GestionScrap.Application/Services/BusinessRules.cs).
- **Segmentación Dinámica y Multi-Selección:** Segmentación por Planta, Grupo, Tipo, Proveedor, Producto, Origen, Mes o Año con filtrado interactivo y selección múltiple en grillas.
- **Exportación por Selección y Personalizada ([ExportacionPersonalizadaDialog.cs](../src/GestionScrap.Desktop/Forms/Dialogs/ExportacionPersonalizadaDialog.cs)):** Permite exportar solo las filas seleccionadas, solo la subrama del árbol elegida o libros multi-hoja configurables por sección vía `ExportReportePersonalizado`.
- **Importador Simplificado + Control Origen -> Destino:** Botón de sincronización directa en 1 clic (`ImportDirectFromFirebirdAsync`) y tabla persistente `import_history` mostrando exactamente **Desde Dónde -> Hacia Dónde**, fechas, leídos, nuevos, duplicados y kilos importados con exportación a Excel.

### Módulos Base, Branding Cambiaso y Firebird (`WMDBSPC.GDB`)
- Consolidado el sistema exclusivamente en los `5` módulos operativos (`Informe Resumen`, `Grupos de Material CRUD Jerárquico`, `Análisis de Proveedores Pareto ABC`, `Importador BD` y `Seguridad y Perfiles RBAC`).
- Identidad visual Cambiaso (`#0B3C5D`, `#0B253A`, `#0891B2`, `#C9A46E`) embebida sin dependencias externas (`EmbeddedResource`), inicialización de cuenta administrativa sembrada, ajuste `Charset=NONE` validado contra `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB`, suite de `20` pruebas `xUnit` e instaladores `Inno Setup 7.1.1`.
