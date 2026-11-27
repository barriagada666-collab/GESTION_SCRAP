# Manual Funcional · Gestión de Scrap v4.1.0

## 1. Inicio de Sesión y Navegación

Al abrir **Gestión de Scrap**, el sistema ejecuta una verificación automática de integridad de SQLite (`PRAGMA quick_check` y `PRAGMA foreign_key_check`), presenta `SplashForm` y luego `LoginForm` (la cuenta administrativa inicial se aprovisiona mediante TI / Operaciones; cambie la clave en el primer inicio de sesión). Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea por **5 minutos**.

### Atajos de Teclado
- `Ctrl+1`: **Informe Resumen**
- `Ctrl+2`: **Grupos de Material**
- `Ctrl+3`: **Análisis de Proveedores**
- `Ctrl+4`: **Importador BD**
- `Ctrl+5`: **Seguridad y Perfiles**
- `F5`: Actualizar datos de la pestaña activa.

## 2. Módulos Operativos

### 2.1 Informe Resumen, Datos, Segmentación y Comparativos (`Ctrl+1`)
- **Filtros y Checkboxes:** Filtre por `Planta`, `Movimiento`, `Grupo Material`, `Proveedor/RUT/Patente`, `Producto/Código` y rango `Desde/Hasta`. Active **`Excluir totales en 0`** para ocultar registros y grupos sin kilos, o **`Sin traslados`** para aislar compras/recepciones reales.
- **KPIs y Ficha Estadística:** Visualice volumen en Kg y Toneladas, viajes, promedio, mediana, mínimo, máximo, desviación estándar, días operativos, ritmo diario, mejor mes/año y variación interanual (**YoY**).
- **Subpestaña `Comparativo Año a Año (YoY) y Mes a Mes (MoM)`:** Muestra el gráfico mensual vs año anterior, el consolidado anual con variación YoY y la evolución mes a mes con variación **MoM** y **YoY**.
- **Subpestaña `Segmentación y Análisis de Datos`:** Elija `Segmentar por` (`Grupo Material`, `Planta`, `Tipo Movimiento`, `Proveedor`, `Producto`, `Origen`, `Mes`, `Año`). Seleccione una o varias filas de segmento (`Ctrl`/`Shift` + clic) para filtrar los movimientos detallados inferiores.
- **Exportación:** Use **`Exportar Resumen`** para el libro estándar, o **`Exportar Selección...`** (`ExportacionPersonalizadaDialog`) para exportar **únicamente las filas seleccionadas** y elegir qué secciones estadísticas incluir.

### 2.2 Grupos de Material con Árbol CRUD Jerárquico (`Ctrl+2`)
- Cree y organice **Grupos Padre**, **Subgrupos** y **Productos Hijo** (editar, mover mediante diálogo o arrastrar/soltar, reordenar y quitar).
- Active **`Excluir totales en 0`** para limpiar nodos sin movimiento en el rango analizado.
- Use **`Exportar Selección...`** para exportar **únicamente la subrama del árbol** o las filas seleccionadas en la grilla.

### 2.3 Análisis de Proveedores (`Ctrl+3`)
- Ranking **Pareto ABC**, variación porcentual entre períodos, días inactivos, ficha detalle con mix de materiales, evolución mensual, patentes y orígenes.
- Incluye **`Excluir totales en 0`**, multi-selección de proveedores/entregas y **`Exportar Selección...`**.

### 2.4 Importador Simple y Control Origen -> Destino (`Ctrl+4`)
- **Pestaña `Importar Firebird (WMDBSPC.GDB)`:** Muestra el flujo directo `DESDE (IP:Puerto · Ruta .GDB/.FDB) ➔ HACIA (SQLite local)`. Permite `Importar Directo` en 1 clic, `Vista Previa`, `Omitir 0 Kg` y exportar la vista previa/selección.
- **Pestaña `Importar Access (.mdb / .accdb)`:** Migra bases Access hacia SQLite local registrando la trazabilidad.
- **Pestaña `Control e Historial (Desde -> Hacia)`:** Consulta la tabla persistente `import_history` con origen exacto, destino exacto, rango, leídos, nuevos, duplicados, kilos y exportación Excel.

### 2.5 Seguridad y Perfiles (`Ctrl+5`)
- Administración CRUD de **Perfiles RBAC (`PerfilUsuario`)** y **Usuarios**, visor filtrable `audit_log`, respaldo SQLite en caliente (`BackupDatabaseAsync`) y purga confirmada (`"ELIMINAR"`).
