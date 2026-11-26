# Manual Funcional · Gestión de Scrap v4.0

## 1. Inicio de Sesión y Navegación

Al iniciar **Gestión de Scrap**, el sistema muestra la pantalla de arranque (`SplashForm`) y luego el acceso (`LoginForm`):
- Ingrese su **Usuario** y **Contraseña** (cuenta inicial de fábrica: `barriagada` / `Cambias0`).
- Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea automáticamente por **5 minutos**.
- La barra lateral izquierda muestra únicamente los módulos habilitados para el perfil (`PerfilUsuario`) asignado a su cuenta.

### Atajos de Teclado Globales
- `Ctrl+1`: **Informe Resumen**
- `Ctrl+2`: **Grupos de Material**
- `Ctrl+3`: **Análisis de Proveedores**
- `Ctrl+4`: **Importador BD**
- `Ctrl+5`: **Seguridad y Perfiles**
- `F5`: Actualizar datos de la pestaña activa.

## 2. Módulos Operativos

### 2.1 Informe Resumen (`Ctrl+1`)
- Filtra por rango de fechas, **Planta** y **Tipo de Pesaje**.
- Presenta tarjetas KPI (`Total Kilos`, `Viajes`, `Promedio Kg/Viaje`, `Variación % vs Período Anterior`, `Última Jornada`, `Proveedores Activos` y `Productos Activos`).
- Dibuja gráficos GDI+ con doble búfer (tendencia comparativa semanal, top proveedores y distribución por grupo de material) más desglose por planta, tipo de pesaje y últimos folios ingresados.
- Permite exportar el resumen completo a **Excel (`.xlsx`)** y navegar directamente hacia el módulo de grupos, proveedores o importación.

### 2.2 Grupos de Material con Árbol CRUD Jerárquico (`Ctrl+2`)
- **Árbol Editable (`MaterialGroupNode`):** Organiza los productos reales en **Nodos Padre**, **Subgrupos** y **Productos Hijo**.
- **Operaciones CRUD disponibles:**
  - **Nuevo Padre:** Crea un grupo raíz con nombre y color corporativo (`MaterialNodeEditorDialog`).
  - **Nuevo Subgrupo:** Agrega un subgrupo debajo del nodo padre o subgrupo seleccionado.
  - **Asignar Producto Hijo:** Inserta productos reales aún no clasificados (`GetUnassignedProductsAsync`) dentro de una carpeta del árbol.
  - **Editar / Renombrar:** Modifica el nombre o color de cualquier nodo.
  - **Mover (`MoverNodoDialog` o Drag & Drop):** Traslada subgrupos o productos hacia otro nodo padre o subgrupo.
  - **Subir / Bajar:** Reordena hermanos dentro de su mismo nivel.
  - **Quitar / Eliminar:** Elimina el nodo o desvincula un producto, permitiendo elegir conservar o eliminar subnodos hijos.
- **Informe Jerárquico:** Consolida kilos netos, viajes, participación porcentual y promedio por viaje para todo el árbol dentro del rango filtrado y exporta a `.xlsx`.

### 2.3 Análisis de Proveedores (`Ctrl+3`)
- Construye el ranking **Pareto ABC** de proveedores (`Clase A` hasta ~80 % acumulado, `Clase B` hasta ~95 %, `Clase C` restante), comparando kilos del período actual contra el período anterior e informando días desde el último envío.
- Al seleccionar un proveedor en la tabla principal, carga su ficha completa (`ProveedorDetalleResultDto`):
  - **Mix de Materiales** y **Productos Principales**.
  - **Evolución Mensual** en gráfico GDI+.
  - **Flota / Patentes Frecuentes** y **Orígenes Frecuentes**.
  - **Entregas / Folios Completos** en el período seleccionado.
- Exporta un libro Excel multi-hoja (`.xlsx`) con el ranking Pareto ABC y el detalle completo del proveedor seleccionado.

### 2.4 Importador Multi-Origen (`Ctrl+4`)
- **Firebird TCP/IP (`WMDBSPC.GDB` / `PWAREHOUSE.FDB`):**
  - Presets incorporados para `Placilla 2 (\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB)`, `Placilla Histórico (WMDBSPC.GDB)` y `Talcahuano (PWAREHOUSE.FDB)`.
  - **Probar Conexión:** Verifica latencia TCP, cantidad de tablas, total de pesajes remotos y fechas mínima/máxima.
  - **Consultar e Importar:** Lista registros del rango elegido, detecta duplicados automáticamente y actualiza en SQLite pesajes, catálogo de clientes (`DBCLIENTES`) y catálogo de productos (`DBPRODUCTOS`).
- **Migración Access (`.mdb` / `.accdb`):** Importación opcional directa de históricos legacy desde archivo Access.

### 2.5 Seguridad, Perfiles y Auditoría (`Ctrl+5`)
- **Perfiles RBAC (`PerfilUsuario`):** Crear, editar y eliminar perfiles personalizados seleccionando cualquiera de los `9` permisos (`PermisoSistema`), más los `5` perfiles base (`Administrador`, `Supervisor`, `Analista`, `Operador`, `Auditor`).
- **Usuarios:** Alta, edición, activación/desactivación, asignación de perfil, restablecimiento de clave y desbloqueo de cuentas.
- **Auditoría y Mantenimiento:** Visor filtrable de `audit_log` con exportación a `.xlsx`, respaldo SQLite en caliente (`BackupDatabaseAsync`) y limpieza total de tablas confirmada con la palabra `"ELIMINAR"`.
