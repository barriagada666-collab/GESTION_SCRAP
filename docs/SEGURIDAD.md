# Seguridad, Autenticación y Control de Acceso (RBAC) · Gestión de Scrap v4.0

## 1. Modelo de Seguridad Implementado

### 1.1 Cifrado y Almacenamiento de Credenciales
- **Hashing de Contraseñas:** `PBKDF2` con `HMAC-SHA256`, sal criptográfica aleatoria de 16 bytes (`RandomNumberGenerator.GetBytes`), derivada a 32 bytes con **100.000 iteraciones**.
- **Comparación Segura:** Verificación en tiempo constante mediante `CryptographicOperations.FixedTimeEquals`.
- **Política de Complejidad:** Longitud mínima de 8 caracteres combinando mayúsculas, minúsculas y dígitos numéricos.
- **Protección contra Fuerza Bruta:** Bloqueo automático temporal de **5 minutos** tras **5 intentos fallidos consecutivos**, con registro en la bitácora de auditoría (`LOGIN_FALLIDO`).
- **Secretos de Red Firebird:** Almacenamiento cifrado en `%LocalAppData%\GestionScrap\settings.json` usando **Windows DPAPI** (`ProtectedData.Protect`, `DataProtectionScope.CurrentUser`).

### 1.2 Defensa en Profundidad (UI + Aplicación + SQLite)
Cada permiso es verificado en **dos niveles**:
1. **Nivel Visual (`GestionScrap.Desktop`):** Los botones de barra superior, pestañas de navegación y acciones internas se habilitan o deshabilitan dinámicamente según `_auth.HasPermission(...)`.
2. **Nivel de Negocio (`ScrapApplicationService`):** Cada método de lectura, escritura, exportación Excel o administración invoca `_auth.EnsurePermission(...)`, lanzando `UnauthorizedAccessException` si se intenta eludir la interfaz gráfica.
3. **Consultas SQL Parametrizadas y Lista Blanca:** Cero concatenación SQL dinámica en SQLite y Firebird; el migrador `.mdb` valida tablas contra una lista blanca explícita (`AllowedTables`).

## 2. Matriz de Roles y Permisos (`RolePermissionMatrix`)

| Permiso (`PermisoSistema`) | Administrador | Supervisor | Operador | Auditor |
| :--- | :---: | :---: | :---: | :---: |
| `VerDashboard` | ✓ | ✓ | ✓ | ✓ |
| `ConsultarInformes` | ✓ | ✓ | ✓ | ✓ |
| `ExportarExcel` | ✓ | ✓ | ✓ | ✓ |
| `EditarPreciosReales` | ✓ | ✓ | — | — |
| `CongelarPreciosTeoricos` | ✓ | ✓ | — | — |
| `AdministrarMantenedorPrecios` | ✓ | ✓ | — | — |
| `CerrarFolioJcramm` | ✓ | ✓ | — | — |
| `ImportarFirebird` | ✓ | ✓ | ✓ | — |
| `EjecutarProcedimientosFirebird` | ✓ | ✓ | — | — |
| `PurgarTablasPesaje` | ✓ | — | — | — |
| `MigrarDesdeMdb` | ✓ | — | — | — |
| `AdministrarSeguridad` | ✓ | — | — | — |
| `VerAuditoria` | ✓ | ✓ | — | ✓ |

Además, desde **Seguridad y MDB -> Usuarios y Permisos RBAC**, el administrador puede sobrescribir permisos granulares por cuenta individual (`PermisosPersonalizados`).

## 3. Cuentas Iniciales Sembradas (Entorno Nuevo)

En bases SQLite recién creadas se inicializan cuatro perfiles base (`admin`, `supervisor`, `operador`, `auditor`) con contraseñas temporales de demostración (`Admin@2026`, `Super@2026`, `Oper@2026`, `Audit@2026`).

> **Importante:** En cualquier entorno productivo, cambie inmediatamente las contraseñas iniciales desde el botón **Mi Clave** o el módulo **Seguridad y MDB**.

## 4. Auditoría Forense (`audit_log`)

Todas las acciones sensibles se registran con marca temporal UTC, usuario actor, entidad, ID de registro y detalle operativo:
- `LOGIN_EXITOSO`, `LOGIN_FALLIDO`, `CAMBIO_CLAVE`
- `CREACION_PRECIO_PROV`, `ACTUALIZACION_PRECIO_PROV`, `ELIMINACION_PRECIO_PROV`
- `ACTUALIZACION_PRECIO_REAL`, `CONGELAMIENTO_PRECIOS`
- `EXPORTACION_EXCEL`, `IMPORTACION_PESAJES`, `EJECUCION_PA`, `MIGRACION_MDB`
- `PURGA_TABLAS`, `RESTABLECER_DEMO`, `CREACION_USUARIO`, `ACTUALIZACION_USUARIO`, `ELIMINACION_USUARIO`
