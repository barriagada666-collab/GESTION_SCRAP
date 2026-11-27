# Seguridad, Perfiles RBAC y Auditoría · Gestión de Scrap v4.1.0

## 1. Modelo de Seguridad Implementado

### 1.1 Cifrado y Almacenamiento de Credenciales
- **Hashing de Contraseñas:** `PBKDF2` con `HMAC-SHA256`, sal aleatoria de 16 bytes (`RandomNumberGenerator.GetBytes`), derivada a 32 bytes con **100.000 iteraciones**.
- **Comparación Segura:** Verificación en tiempo constante mediante `CryptographicOperations.FixedTimeEquals`.
- **Política de Complejidad:** Longitud mínima de 8 caracteres combinando mayúsculas, minúsculas y dígitos (`PasswordSecurity.ValidateComplexity`).
- **Protección contra Fuerza Bruta:** Bloqueo automático temporal de **5 minutos** tras **5 intentos fallidos consecutivos** registrado en `audit_log` (`LOGIN_FALLIDO`).
- **Secretos Firebird:** Cifrado en `%LocalAppData%\GestionScrap\settings.json` mediante **Windows DPAPI** (`ProtectedData.Protect`, `DataProtectionScope.CurrentUser`).

### 1.2 Defensa en Profundidad (UI + Aplicación + SQLite)
Cada permiso se valida en **dos niveles**:
1. **Nivel Visual (`GestionScrap.Desktop`):** Botones de barra superior, pestañas del menú lateral y acciones internas consultan `_auth.HasPermission(...)`.
2. **Nivel de Negocio (`ScrapApplicationService`):** Cada caso de uso exige `_auth.EnsurePermission(...)` lanzando `UnauthorizedAccessException` ante cualquier acceso no autorizado.
3. **Consultas Parametrizadas y Lista Blanca:** Consultas 100 % parametrizadas en SQLite/Firebird y validación de tablas en lista blanca para migración Access.

## 2. Perfiles Iniciales y Permisos (`PermisoSistema`)

| Permiso (`PermisoSistema`) | Administrador | Supervisor | Analista | Operador | Auditor |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `VerInformeResumen` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `VerGrupoMaterial` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `AdministrarGrupoMaterial` | ✓ | ✓ | ✓ | — | — |
| `VerAnalisisProveedores` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `ImportarDatos` | ✓ | ✓ | — | ✓ | — |
| `ExportarExcel` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `AdministrarSeguridadPerfiles` | ✓ | — | — | — | — |
| `VerAuditoria` | ✓ | ✓ | — | — | ✓ |
| `MantenimientoBaseDatos` | ✓ | — | — | — | — |

Además de los `5` perfiles iniciales (`DefaultRoleProfiles`), desde **Seguridad y Perfiles (`Ctrl+5`)** el administrador puede crear nuevos perfiles (`PerfilUsuario`) o editar los existentes con combinaciones personalizadas de permisos.

## 3. Cuenta Inicial de Administrador

Al crear una base SQLite nueva, el sistema siembra los `5` perfiles base y aprovisiona la cuenta inicial de administrador:
- **Usuario:** `barriagada` *(Perfil `Administrador`, protegido contra eliminación/desactivación/degradación cuando es el último administrador activo)*
- **Contraseña inicial:** entregada exclusivamente por canal administrativo interno (no publicada en el repositorio).

> **Nota de Seguridad en Producción:** Cambie la contraseña inicial en el primer inicio de sesión desde **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.

## 4. Auditoría Forense (`audit_log`)

Se registran con marca temporal UTC, actor, entidad, identificador y detalle las acciones de login, fallo de autenticación, cambio de clave, creación/edición/eliminación de perfiles y usuarios, cambios en el árbol de grupos de material, exportaciones Excel, importaciones Firebird/Access, respaldos SQLite y purgas de base de datos.
