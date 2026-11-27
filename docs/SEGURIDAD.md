# Seguridad, Perfiles RBAC y Auditoría · Gestión de Scrap v5.0.0

## 1. Modelo de Seguridad Implementado

### 1.1 Cifrado y Almacenamiento de Credenciales
- **Hashing de Contraseñas:** `PBKDF2` con `HMAC-SHA256`, sal aleatoria de 16 bytes (`RandomNumberGenerator.GetBytes`), derivada a 32 bytes con **100.000 iteraciones**.
- **Comparación Segura:** Verificación en tiempo constante mediante `CryptographicOperations.FixedTimeEquals`.
- **Política de Complejidad:** Longitud mínima de 8 caracteres combinando mayúsculas, minúsculas y dígitos (`PasswordSecurity.ValidateComplexity`).
- **Protección contra Fuerza Bruta:** Bloqueo automático temporal de **5 minutos** tras **5 intentos fallidos consecutivos** registrado en `audit_log` (`LOGIN_FALLIDO`).
- **Secretos Firebird:** Cifrado en `%LocalAppData%\GestionScrap\settings.json` mediante **Windows DPAPI** (`ProtectedData.Protect`, `DataProtectionScope.CurrentUser`).

### 1.2 Defensa en Profundidad (UI + Aplicación + SQLite)
Cada permiso se valida en **tres niveles**:
1. **Nivel Visual (`GestionScrap.Desktop`):** Botones, pestañas laterales (`Ctrl+1..Ctrl+8`) y menús contextuales consultan `_auth.HasPermission(...)`.
2. **Nivel de Negocio (`ScrapApplicationService` y `EnterpriseApplicationService`):** Cada caso de uso exige `_auth.EnsurePermission(...)` lanzando `UnauthorizedAccessException` ante cualquier intento no autorizado.
3. **Nivel de Persistencia (`SqliteScrapRepository`):** Consultas 100 % parametrizadas, protección del último administrador activo, integridad referencial `PRAGMA foreign_keys = ON` e índice único parcial contra movimientos duplicados de inventario.

## 2. Perfiles Iniciales y Permisos (`PermisoSistema` · 13 Permisos)

| # | Permiso (`PermisoSistema`) | Administrador | Supervisor | Analista | Operador | Auditor |
| :-: | :--- | :---: | :---: | :---: | :---: | :---: |
| 1 | `VerInformeResumen` | ✓ | ✓ | ✓ | ✓ | ✓ |
| 2 | `VerGrupoMaterial` | ✓ | ✓ | ✓ | ✓ | ✓ |
| 3 | `AdministrarGrupoMaterial` | ✓ | ✓ | ✓ | — | — |
| 4 | `VerAnalisisProveedores` | ✓ | ✓ | ✓ | ✓ | ✓ |
| 5 | `ImportarDatos` | ✓ | ✓ | — | ✓ | — |
| 6 | `ExportarExcel` | ✓ | ✓ | ✓ | ✓ | ✓ |
| 7 | `AdministrarSeguridadPerfiles` | ✓ | — | — | — | — |
| 8 | `VerAuditoria` | ✓ | ✓ | — | — | ✓ |
| 9 | `MantenimientoBaseDatos` | ✓ | — | — | — | — |
| 10 | `AdministrarCrmEntidades` | ✓ | ✓ | — | — | — |
| 11 | `AdministrarMaterialesVehiculos` | ✓ | ✓ | — | ✓ | — |
| 12 | `GestionarPesajesOperaciones` | ✓ | ✓ | — | ✓ | — |
| 13 | `AutorizarAnularPesajesInventario` | ✓ | ✓ | — | — | — |

Al migrar bases existentes al esquema `v3`, el sistema sincroniza automáticamente los permisos `10..13` en los perfiles sembrados (`Administrador`, `Supervisor`, `Operador`) sin alterar perfiles personalizados.

## 3. Cuenta Inicial de Administrador y Trazabilidad Forense

- **Usuario inicial:** `barriagada` *(Perfil `Administrador`, protegido contra eliminación, desactivación o degradación cuando es el último administrador activo)*; contraseña inicial provista únicamente por canal interno de TI (no publicada en el repositorio).
- **Auditoría doble:** `audit_log` registra eventos globales del sistema (login, seguridad, CRM, materiales, flota, ERP, inventario, importaciones y mantenimiento) y `pesajes_historial` conserva de forma inmutable cada cambio de estado y peso de todo pesaje industrial.
