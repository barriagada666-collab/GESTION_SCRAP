# Historial de Cambios (Changelog) · Gestión de Scrap

## [4.0.0] - Arquitectura .NET 10, Branding Cambiaso e Importación Directa Firebird

### Refactorización de Alcance (5 Módulos Activos)
- Eliminados los módulos y diálogos fuera de alcance (`Precios`, `Mantenedor de Precios`, `JCRAMM`, `Grupos Especiales`, `Compras y Traslados`, `Informe por Cliente` y ejecución directa PA Firebird) para enfocar el sistema exclusivamente en los `5` flujos centrales:
  1. `Informe Resumen` (`Ctrl+1`).
  2. `Grupos de Material` (`Ctrl+2`) con árbol CRUD jerárquico completo (Nodos Padre / Subgrupos / Productos Hijo, mover con diálogo o Drag & Drop, reordenar y eliminar).
  3. `Análisis de Proveedores` (`Ctrl+3`) con ranking Pareto ABC, variación entre períodos, mix de materiales, evolución mensual, flota/patentes y orígenes.
  4. `Importador BD` (`Ctrl+4`) conectado a Firebird `WMDBSPC.GDB` y migración Access `.mdb`/`.accdb`.
  5. `Seguridad y Perfiles` (`Ctrl+5`) con CRUD dinámico de `PerfilUsuario`, CRUD de usuarios, bitácora y respaldo/purga SQLite.

### Identidad Visual, Base de Datos y Seguridad
- **Branding Corporativo Cambiaso con Cero Dependencias:** Paleta azul petróleo (`#0B3C5D`, `#0B253A`), cian (`#0891B2`) y dorado (`#C9A46E`), más emblema/monograma e icono embebidos en `GestionScrap.Desktop.dll` (`EmbeddedResource`) y pantalla de arranque (`SplashForm`).
- **Auditoría e Interoperabilidad Firebird (`WMDBSPC.GDB`):** Corregido el ajuste `Charset=NONE` en `FirebirdSyncService` tras auditar `\\192.168.2.56\bdwm\placilla2\WMDBSPC.GDB` (`DEFAULT CHARACTER SET NONE`, `2.730` pesajes activos, `48` productos, `263` clientes) y `\\192.168.2.56\bdwm\placilla\WMDBSPC.GDB` (`18.031` pesajes activos).
- **Seguridad y Cuenta Inicial:** Sembrado de `5` perfiles iniciales (`Administrador`, `Supervisor`, `Analista`, `Operador`, `Auditor`) y cuenta administrativa `barriagada` / `Cambias0` con PBKDF2-SHA256, bloqueo tras `5` intentos y DPAPI.
- **Pruebas y Empaquetado:** Suite automatizada `xUnit` de `19` pruebas (`Unit`, `Integration`, `RealDatabases`, `UI STA Audit`, `10k Stress`) y compilación dual Inno Setup 7 (`SelfContained` y `FrameworkDependent`).
