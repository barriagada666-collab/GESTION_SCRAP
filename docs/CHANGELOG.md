# Historial de Cambios (Changelog) · Gestión de Scrap

## [4.0.0] - Arquitectura .NET 10, Branding Cambiaso, Analítica YoY/MoM y Trazabilidad de Importaciones

### Analítica Avanzada, Exclusión de Ceros y Exportación Personalizada
- **Exclusión de Totales en Cero (`Excluir totales en 0`):** Agregado filtro interactivo y de negocio transversal en `InformeResumenControl`, `GrupoMaterialControl` y `AnalisisProveedoresControl`.
- **Comparativos Año a Año (YoY) y Mes a Mes (MoM) + Fichas Estadísticas:** Agregados cálculos de toneladas, mediana, mínimo, máximo, desviación estándar, ritmo diario, variación interanual exacta (`YoY`) y evolución mensual (`MoM` y `YoY`) en [BusinessRules.cs](../src/GestionScrap.Application/Services/BusinessRules.cs).
- **Segmentación Dinámica y Multi-Selección:** Segmentación por Planta, Grupo, Tipo, Proveedor, Producto, Origen, Mes o Año con filtrado interactivo y selección múltiple en grillas.
- **Exportación por Selección y Personalizada ([ExportacionPersonalizadaDialog.cs](../src/GestionScrap.Desktop/Forms/Dialogs/ExportacionPersonalizadaDialog.cs)):** Permite exportar solo las filas seleccionadas, solo la subrama del árbol elegida o libros multi-hoja configurables por sección vía `ExportReportePersonalizado`.
- **Importador Simplificado + Control Origen -> Destino:** Botón de sincronización directa en 1 clic (`ImportDirectFromFirebirdAsync`) y tabla persistente `import_history` mostrando exactamente **Desde Dónde -> Hacia Dónde**, fechas, leídos, nuevos, duplicados y kilos importados con exportación a Excel.

### Módulos Base, Branding Cambiaso y Firebird (`WMDBSPC.GDB`)
- Consolidado el sistema exclusivamente en los `5` módulos operativos (`Informe Resumen`, `Grupos de Material CRUD Jerárquico`, `Análisis de Proveedores Pareto ABC`, `Importador BD` y `Seguridad y Perfiles RBAC`).
- Identidad visual Cambiaso (`#0B3C5D`, `#0B253A`, `#0891B2`, `#C9A46E`) embebida sin dependencias externas (`EmbeddedResource`), cuenta inicial `barriagada` / `Cambias0`, ajuste `Charset=NONE` validado contra `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB`, suite de `20` pruebas `xUnit` e instaladores `Inno Setup 7.1.1`.
