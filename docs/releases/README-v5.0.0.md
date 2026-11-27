# Gestión de Scrap v5.0.0 — CRM, ERP, Pesajes Industriales (1 y 2 Mediciones), Flota, Inventario e Informes Multi-Selección

**Fecha de publicación:** 26-11-2026  
**Plataforma:** Windows 10 / Windows 11 x64 · `.NET 10` (`C# 14` + WinForms `PerMonitorV2`) · SQLite WAL `v3` · Inno Setup `7.1.1`  
**Calidad verificada:** `0` errores · `0` advertencias · `0` supresiones · **`34/34` pruebas `xUnit` exitosas** · QA silencioso de instaladores y verificación visual High-DPI aprobados

---

## Novedades Principales de `v5.0.0`

### 1. CRM Unificado de Clientes y Proveedores (`Ctrl+4`)
- **Entidad Única por RUT (Cero Duplicados):** Validación estricta de dígito verificador Módulo 11 (`RutChileno.ValidateAndFormat`) con roles `Cliente`, `Proveedor` o `Cliente y Proveedor` (`RolEntidadComercial`).
- **Ficha 360° Integrada:** Consolida en tiempo real los pesajes confirmados, órdenes ERP, flota asociada y documentos comerciales (`entidades_documentos`), con activación/desactivación y protección contra eliminación física cuando existe historial.

### 2. Gestión Integral de Materiales y Flota de Vehículos/Patentes (`Ctrl+4`)
- **Catálogo Maestro de Materiales (`productos`):** CRUD completo con código único, grupo, unidad de medida (`KG`/`TON`), porcentaje de tolerancia de diferencia (`ToleranciaDiferenciaPct`), filtros combinados (texto, grupo, unidad, estado y proveedor) e historial de stock y pesajes.
- **Flota y Patentes Normalizadas (`vehiculos`):** Normalización estricta (`PatenteChilena`, impidiendo duplicados `AB-CD-12` vs `abcd12`), backfill automático desde patentes históricas válidas, búsqueda exacta/parcial e historial completo de pesajes.

### 3. Pesajes Industriales (1 y 2 Mediciones), Operaciones ERP e Inventario (`Ctrl+3`)
- **Reglas Estrictas de Cálculo (`WeighingRules`):** `Neto = Bruto - Tara`, rechazo de pesos negativos o invertidos, consistencia de unidades con el material maestro y bloqueo de sobrescritura manual de peso neto sin ajuste autorizado + motivo.
- **Flujo de 2 Mediciones (Entrada y Salida):** Registra 1ª medición preservando fecha/hora y valor en estado `PendienteSegundaMedicion` (`Neto = 0`, cero movimiento de inventario); al completar la 2ª medición calcula `Bruto = max`, `Tara = min`, `Neto = Bruto - Tara`, bloquea cierres duplicados y contabiliza un único asiento de inventario.
- **Tolerancias, Autorización, Correcciones y Anulaciones (`pesajes_historial`):** Diferencias superiores a la tolerancia quedan en `PendienteAutorizacion` sin mover stock hasta autorización (`AutorizarAnularPesajesInventario`). Los pesajes confirmados nunca se eliminan físicamente: toda corrección o anulación conserva historial inmutable y revierte automáticamente el inventario.
- **Órdenes ERP (`operaciones_comerciales`):** `OrdenCompra`, `OrdenVenta`, `RecepcionMaterial`, `EntregaDespacho` y `TrasladoInterno` con trazabilidad punta a punta (`Orden -> Pesajes -> Movimientos Inventario -> Documentos`).
- **Libro Mayor Inmutable de Inventario (`inventario_movimientos`):** Stock derivado exclusivamente de movimientos confirmados (`Entradas - Salidas + Ajustes == StockActual`), índice único parcial SQLite `idx_inv_mov_pesaje_activo` impidiendo duplicados por pesaje y ajustes autorizados auditados.
- **Tipos de Pesaje Configurables (`tipos_pesaje_config`):** 9 tipos sembrados (`Recepción`, `Despacho`, `Movimiento Interno`, `Entrada`, `Salida`, `Carga`, `Descarga`, `Individual`, `Entrada y Salida`) administrables vía CRUD.

### 4. Centro de Informes Empresariales y 9 Segmentadores Multi-Selección (`Ctrl+2`)
- **Período por Defecto Mensual Local:** Todos los informes inicializan automáticamente desde el día 1 del mes actual local hasta hoy.
- **9 Segmentadores Multi-Selección:** `Proveedores`, `Clientes`, `Materiales`, `Grupos`, `Patentes`, `Tipos Pesaje`, `Años`, `Meses` y `Estados` (OR dentro de categoría, AND entre categorías), con búsqueda interna, `Todos`, `Ninguno`, contador visual y barra resumen de filtros activos.
- **6 Informes 100 % Consistentes + Detalle:** `Por Patente`, `Por Material`, `Por Proveedor`, `Por Cliente`, `Por RUT (Rol CRM)`, `Por Tipo de Pesaje` y `Detalle de Pesajes`, con exportación multi-hoja a Excel y menú contextual con clic derecho en todas las grillas operacionales.

### 5. Migración Automática SQLite WAL `v3` y Seguridad RBAC (`13` Permisos)
- Migración idempotente `v3` validada sobre copia de base productiva (`32.178` pesajes, `546` clientes, `52` productos) con `PRAGMA quick_check = ok`, `0` violaciones FK, `100 %` de registros preservados y sincronización automática de los permisos `10..13` en perfiles sembrados.

---

## Artefactos Oficiales y Verificación SHA-256

| Archivo | Tamaño | SHA-256 |
| :--- | ---: | :--- |
| `GestionScrap_Setup_v5.0.0_x64_SelfContained.exe` | 40.183.609 bytes | `24D3EE3CF8BD04E0E64FB02B7B5D837D9791EBA61F68BCC2B380468A0068188D` |
| `GestionScrap_Setup_v5.0.0_x64_FrameworkDependent.exe` | 5.787.755 bytes | `1768B1E8820652C3327B3DDDAA4C9E21C6D25B20A06F8271DFF1F4EC4F4253FC` |
| `GestionScrap_Portable_v5.0.0_win-x64.zip` | 56.890.942 bytes | `96068FEFBF0451AEE9883A10CDC9CAE9F49BC6E8DC80A4EE4B8F21CC70CE4489` |
| `checksums_sha256.txt` | — | Verificación de los 3 artefactos binarios |
| `README-v5.0.0.md` | — | Notas de versión oficiales |

### Verificación en PowerShell

```powershell
Get-FileHash .\GestionScrap_Setup_v5.0.0_x64_SelfContained.exe -Algorithm SHA256
```

### Instalación Silenciosa

```cmd
GestionScrap_Setup_v5.0.0_x64_SelfContained.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

> **Continuidad de datos:** La actualización desde cualquier versión `v4.x` conserva intacta `%LocalAppData%\GestionScrap\gestion_scrap.db` y ejecuta automáticamente la migración idempotente `v3`.
