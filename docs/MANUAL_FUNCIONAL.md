# Manual Funcional · Gestión de Scrap v4.0

## 1. Inicio de Sesión y Navegación

Al abrir **Gestión de Scrap**, se muestra el formulario de acceso (`LoginForm`):
- Ingrese su **Usuario** y **Contraseña**.
- Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea temporalmente por **5 minutos**.
- Una vez autenticado, el panel lateral izquierdo muestra únicamente los módulos habilitados para su perfil (`Administrador`, `Supervisor`, `Operador` o `Auditor`).

### Atajos de Teclado Globales
- `Ctrl+1` a `Ctrl+9`, `Ctrl+0`: Cambio rápido entre los 10 módulos principales.
- `F5`: Recargar/actualizar la pestaña activa.
- `F2` / `Espacio` (en campo criterio de Informe por Cliente) o `Enter` (en Mantenedor de Precios): Abre el buscador de clientes o productos.

## 2. Descripción de Módulos

### 2.1 Dashboard Ejecutivo (`Ctrl+1`)
- Muestra 4 tarjetas KPI (`Kilos Período Actual`, `Período Anterior`, `Última Jornada` y `Alertas de Precios`).
- Renderiza 3 gráficos vectoriales: tendencia semanal comparativa, top proveedores del período y distribución por grupo de material.
- Permite saltar directamente a **Informe de Precios**, **Mantenedor de Precios** o **Importar Firebird**.

### 2.2 Grupo de Material (`Ctrl+2`)
- Consolida kilos netos por familia (`SUPER-BASULIP`, `RECAM`, `ALTA`, `MIX SUPERMERCADO`, `STRETCH`, `MAQUILADO`, `BASURA`, `GENERAL`) en **Placilla**, **Santiago** y **Talcahuano**.
- Soporta consulta por **Rango de Fechas** completo o modo **Semanal (7 días)**.
- Exporta un libro Excel consolidado (`.xlsx`) con totales por planta y total nacional.

### 2.3 Compras y Traslados (`Ctrl+3`)
- Separa estrictamente recepciones de compra de los traslados internos entre bodegas/plantas por **Planta** seleccionada.
- Exporta un libro Excel con pestañas separadas de Compras y Traslados.

### 2.4 Informe por Cliente o Folio (`Ctrl+4`)
- Permite buscar por **RUT** (con diálogo `ClienteSelectorDialog`) o directamente por **Número de Folio** en Placilla, Santiago o Talcahuano.
- Exporta ticket detallado del cliente/folio a `.xlsx`.

### 2.5 Grupos Especiales (`Ctrl+5`)
- Calcula de forma simultánea los 8 segmentos estratégicos independientes:
  1. Retail Santiago
  2. Retail Empresas
  3. Mix Supermercado Santiago
  4. Mix Supermercado Placilla
  5. Retail Regiones (Martino / Johnson)
  6. Sector Agrícola
  7. Sector Pesquero
  8. Grupo Agrosuper Placilla

### 2.6 Informe JCRAMM (`Ctrl+6`)
- Lista pesajes elegibles en **Placilla** posteriores al último folio cerrado persistente (`UltimoFolioJcramm`), excluyendo códigos administrativos/cartón no elegibles.
- Al presionar **Exportar y Cerrar Folio** (requiere permiso `CerrarFolioJcramm`), genera el Excel y avanza el folio de corte de forma persistente.

### 2.7 Informe de Precios (`Ctrl+7`)
- Calcula valorización en **Placilla** o **Santiago** bajo dos modos:
  - **Precios Teóricos (Pactados):** Cruza RUT + código contra `MantenedorPrecios`.
  - **Precios Reales (Indexados):** Lee precios fijados por folio en `IndexPrecioPesaje`.
- Calcula el resumen ponderado para familias **SUPER BASULIP**, **RECAM** y **ALTA**.
- Acciones:
  - **Congelar Teóricos:** Fija tarifas teóricas en el índice histórico real.
  - **Editar Precio Folio** (o doble clic en fila, solo en modo Reales): Ajusta un folio específico con auditoría.

### 2.8 Mantenedor de Precios (`Ctrl+8`)
- Permite **Insertar Nuevo**, **Actualizar**, **Eliminar** y filtrar en vivo tarifas pactadas (`RUT + Producto`) en Placilla o Santiago con selectores de catálogo de clientes y productos.

### 2.9 Importar Firebird (`Ctrl+9`)
- Prueba conectividad TCP (`3050`), consulta pesajes remotos en `.GDB` / `.FDB` por rango de fechas e importa a SQLite omitiendo duplicados.
- Incluye diálogo transaccional **Ejecutar PA SAP** (`PA_INS_PROV_STGO` y `PA_INS_MASIVO_EXP_SAP`).

### 2.10 Seguridad, Auditoría y Migración MDB (`Ctrl+0`)
- **Usuarios y Permisos RBAC:** Crear, editar, activar/desactivar, desbloquear cuentas y personalizar los 13 permisos granulares.
- **Registro de Auditoría:** Filtra por acción/usuario los eventos de seguridad, cambios de precios, exportaciones y purgas, con exportación Excel.
- **Migración Access MDB y Respaldo SQLite:** Importa bases heredadas `BD_SCRAP.mdb`, respalda la base SQLite en caliente y ejecuta recarga demo o purga segura confirmada (`ELIMINAR`).
