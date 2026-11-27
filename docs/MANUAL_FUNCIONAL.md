# Manual Funcional · Gestión de Scrap v5.0.0

## 1. Inicio de Sesión y Navegación (`Ctrl+1` .. `Ctrl+8`)

Al abrir **Gestión de Scrap v5.0.0**, el sistema ejecuta automáticamente las migraciones versionadas SQLite WAL (`v3`), verifica integridad (`PRAGMA quick_check` y `PRAGMA foreign_key_check`), presenta `SplashForm` y solicita autenticación en `LoginForm`. Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea por **5 minutos**.

### Atajos de Teclado
- `Ctrl+1`: **Informe Resumen** (KPIs ejecutivos, comparativos YoY/MoM, segmentación dinámica)
- `Ctrl+2`: **Informes Pesajes** (Centro de Informes Empresariales con 9 segmentadores multi-selección)
- `Ctrl+3`: **Pesajes y ERP** (Pesajes 1/2 mediciones, Órdenes ERP, Libro Mayor de Inventario, Tipos de Pesaje)
- `Ctrl+4`: **CRM y Maestros** (Clientes/Proveedores unificados por RUT + Ficha 360°, Materiales y Vehículos/Patentes)
- `Ctrl+5`: **Grupos de Material** (Árbol CRUD jerárquico Padre / Subgrupo / Producto Hijo)
- `Ctrl+6`: **Análisis Proveedores** (Ranking Pareto ABC, mix de materiales, flota y entregas)
- `Ctrl+7`: **Importador BD** (Firebird TCP `WMDBSPC.GDB` / `PWAREHOUSE.FDB` y Access `.mdb`/`.accdb`)
- `Ctrl+8`: **Seguridad y Perfiles** (Usuarios, Perfiles RBAC con `13` permisos, Auditoría y Respaldo SQLite)
- `F5`: Recargar datos de la pestaña activa.
- **Clic derecho (`ContextMenuStrip`)**: Disponible en todas las grillas operacionales y el árbol de materiales para crear, editar, ver historial, activar/desactivar, anular, copiar valor o exportar selección a Excel.

---

## 2. Módulos Empresariales v5.0.0

### 2.1 Centro de Informes Empresariales (`Ctrl+2`)
- **Rango por defecto mensual local:** Inicializa automáticamente desde el **1er día del mes actual** hasta la **fecha actual del equipo** (`Mes Actual` / `Histórico`).
- **Búsqueda combinada:** Planta, Folio (exacto o parcial conservando formato original), Patente, RUT, Operador/Báscula, `Ocultar anulados` y `Excluir 0 Kg`.
- **9 Segmentadores Multi-Selección:** `Proveedores`, `Clientes`, `Materiales`, `Grupos`, `Patentes`, `Tipos Pesaje`, `Años`, `Meses` y `Estados`. Cada botón abre un panel con búsqueda interna, checkboxes, `Todos`, `Ninguno`, contador de activos y aplicación **OR** dentro de la categoría + **AND** entre categorías distintas.
- **6 Informes 100 % Consistentes + Detalle:** `Por Patente`, `Por Material`, `Por Proveedor`, `Por Cliente`, `Por RUT (CRM)` (mostrando el rol comercial exacto), `Por Tipo de Pesaje` y `Detalle de Pesajes`. Todas las cifras coinciden exactamente con los KPIs y la exportación Excel multi-hoja.

### 2.2 Pesajes Industriales, Operaciones ERP e Inventario (`Ctrl+3`)
- **Pesajes Industriales (1 y 2 Mediciones):**
  - **Nuevo Pesaje (1 paso):** Selecciona planta, tipo, vehículo activo, material activo, entidad CRM y operación ERP opcional. Calcula automáticamente `Neto = Bruto - Tara` y valida unidad (`KG`/`TON`) y tolerancia de peso esperado.
  - **Flujo 2 Mediciones (`1ª Medición` -> `Completar 2ª`):** Registra la primera medición preservando su fecha/hora y valor en estado `Pendiente 2ª Medición` (`Neto = 0`, sin mover inventario). Al registrar la segunda medición, calcula `Bruto = max`, `Tara = min`, `Neto = Bruto - Tara` y contabiliza un único movimiento de inventario.
  - **Autorización, Corrección y Anulación:** Exigen permiso `AutorizarAnularPesajesInventario` y motivo obligatorio. Ningún pesaje confirmado se borra físicamente: queda registrado en `pesajes_historial` y las anulaciones generan reverso automático en inventario.
- **Órdenes y Operaciones ERP:** Administra `OrdenCompra`, `OrdenVenta`, `RecepcionMaterial`, `EntregaDespacho` y `TrasladoInterno`, visualizando en tiempo real su trazabilidad completa (`Pesajes vinculados`, `Movimientos de inventario` y `Documentos asociados`).
- **Inventario y Movimientos (Libro Mayor):** Muestra existencias calculadas exclusivamente desde movimientos confirmados (`Entradas - Salidas + Ajustes == Stock Actual`) y permite emitir **Ajustes Autorizados** auditados (`+` / `-`).
- **Tipos de Pesaje Configurables:** CRUD y activación/desactivación de tipos de pesaje con definición de 1 o 2 mediciones e impacto en inventario (`+`, `-` o neutro).

### 2.3 CRM y Maestros (`Ctrl+4`)
- **CRM Clientes y Proveedores (RUT Único):** Un único registro por RUT validado con Módulo 11 (`Cliente`, `Proveedor` o `Cliente y Proveedor`), contacto, dirección, giro, condiciones comerciales, documentos asociados y **Ficha 360°** inferior. Entidades con historial están protegidas contra eliminación física (se desactivan para conservar trazabilidad).
- **Catálogo de Materiales:** Filtros combinados (texto, grupo, unidad, estado y RUT proveedor), configuración de tolerancia `%` por material e historial de stock y pesajes.
- **Vehículos y Patentes (Flota):** Normalización automática evitando duplicados (`AB-CD-12` == `abcd12`), búsqueda exacta/parcial e historial completo de pesajes por patente.

### 2.4 Módulos Operativos Existentes (`Ctrl+1`, `Ctrl+5`, `Ctrl+6`, `Ctrl+7`, `Ctrl+8`)
- **Informe Resumen (`Ctrl+1`)**, **Grupos de Material (`Ctrl+5`)**, **Análisis Proveedores (`Ctrl+6`)**, **Importador BD (`Ctrl+7`)** y **Seguridad y Perfiles (`Ctrl+8`)** mantienen el 100 % de sus capacidades históricas, incorporando rango mensual local por defecto y menús contextuales con clic derecho.
