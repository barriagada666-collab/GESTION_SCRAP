# Historial de Cambios (Changelog) · Gestión de Scrap

## [4.0.0] - Migración Completa a .NET 10 + Inno Setup 7

### Agregado
- **Nueva Arquitectura .NET 10 (Clean Architecture):** Capas `Domain`, `Application`, `Infrastructure`, `Desktop` (WinForms `PerMonitorV2`) y `Tests`.
- **Motor Central SQLite WAL:** Reemplazo definitivo de Microsoft Access multiusuario para eliminar corrupción de índices y bloqueos `.ldb`.
- **Seguridad y Auditoría RBAC:** Autenticación PBKDF2-SHA256, bloqueo tras 5 intentos fallidos, 4 roles (`Administrador`, `Supervisor`, `Operador`, `Auditor`), 13 permisos granulares editables y bitácora `audit_log`.
- **Cifrado DPAPI:** Protección nativa Windows de credenciales Firebird en configuración local.
- **Dashboard Ejecutivo Vectorial:** KPIs comparativos, alertas automáticas de tarifas faltantes/pendientes de congelar y gráficos GDI+ con doble búfer.
- **Exportación Excel Nativa:** Generación directa de archivos `.xlsx` vía `ClosedXML 0.105.1` sin requerir Microsoft Excel COM instalado.
- **Migrador Híbrido Access MDB:** Extracción segura desde `BD_SCRAP.mdb` con puente automático PowerShell x86 (`SysWOW64`) cuando solo existen drivers Office de 32 bits.
- **Suite Automatizada de Pruebas:** 22 pruebas xUnit cubriendo lógica unitaria, integración SQLite/Excel, migración Access real, auditoría UI de controles WinForms y carga masiva de 10.000 filas.
- **Instaladores Inno Setup 7.1.1:** Empaquetado x64 moderno en variantes `SelfContained` y `FrameworkDependent`, gráficos de versión `v4.0` y preservación de datos locales en actualizaciones.

### Corregido (Respecto a VBA / Prototipos Anteriores)
- Habilitada la búsqueda operativa por **Folio** en **Informe por Cliente**.
- Separadas **Pesquera** y **Agrosuper** en grillas independientes dentro de **Grupos Especiales**.
- Corregida la consulta de **Grupo Material Talcahuano** para consultar sus propios registros en lugar de Placilla.
- Corregido `Mantenedor de Precios` para ejecutar actualización real en lugar de inserción duplicada.
- Prevenida la división por cero en cálculos ponderados cuando los kilos acumulados son 0.
- Eliminadas vulnerabilidades de concatenación SQL mediante comandos estrictamente parametrizados.
