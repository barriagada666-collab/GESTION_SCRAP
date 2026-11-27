<div align="center">
  <img src="installer/assets/app_logo.png" alt="Cambiaso Gestión de Scrap Logo" width="128" />
  <h1>Cambiaso · Gestión de Scrap y Pesajes v4.0</h1>
  <p><strong>Plataforma de Escritorio .NET 10 x64 para Control de Pesajes, Analítica YoY/MoM, Segmentación de Datos, Árbol de Materiales e Importación Trazable Firebird</strong></p>

  ![.NET 10](https://img.shields.io/badge/.NET-10.0%20WinForms%20x64-0B3C5D?style=for-the-badge&logo=dotnet)
  ![SQLite WAL](https://img.shields.io/badge/Database-SQLite%20WAL%20%2B%20Firebird-0891B2?style=for-the-badge&logo=sqlite)
  ![Inno Setup 7](https://img.shields.io/badge/Installer-Inno%20Setup%207-C9A46E?style=for-the-badge)
  ![Tests](https://img.shields.io/badge/Tests-20%2F20%20Passing-059669?style=for-the-badge)
  ![Warnings](https://img.shields.io/badge/Build-0%20Errors%20%7C%200%20Warnings-10B981?style=for-the-badge)
</div>

---

## Descripción del Proyecto

**Gestión de Scrap v4.0** es la plataforma corporativa desarrollada en **.NET 10 (`C# 14` + Windows Forms `x64` `PerMonitorV2`)** y **Inno Setup 7** para consolidar, clasificar, segmentar y analizar los pesajes de báscula desde las bases productivas **Firebird (`WMDBSPC.GDB` / `PWAREHOUSE.FDB`)** y respaldos históricos **Access (`.mdb`/`.accdb`)**.

Opera con almacenamiento local **SQLite en modo WAL**, exportación nativa **Excel (`.xlsx`)** mediante **ClosedXML** (resúmenes completos, **solo filas seleccionadas**, **subramas del árbol**, e **informes personalizados**), **cero dependencias externas de UI o recursos remotos** (identidad visual Cambiaso embebida directamente como `EmbeddedResource`) y control de acceso dinámico basado en perfiles (**RBAC**).

---

## Los 5 Módulos Operativos (`Ctrl+1` .. `Ctrl+5`)

1. **Informe Resumen, Segmentación y Comparativos YoY/MoM (`Ctrl+1`):**
   - Filtros avanzados (`Planta`, `Movimiento`, `Grupo Material`, `Segmentar por`, `Proveedor/RUT/Patente`, `Producto/Código`, `Desde/Hasta`) + checkboxes **`Excluir totales en 0`** y **`Sin traslados`**.
   - **KPIs y Ficha Estadística Cuantitativa:** kilos netos, toneladas, viajes, promedio, mediana, mínimo, máximo, desviación estándar, días operativos, ritmo diario, mejor mes/año y variación anual exacta (**YoY**).
   - **Comparativo Año a Año (YoY) y Mes a Mes (MoM):** evolución mensual con variación MoM y contra el mismo mes del año anterior, más consolidado anual y gráfico comparativo GDI+.
   - **Segmentación Dinámica y Exportación Personalizada (`Exportar Selección...`):** agrupa por Planta, Grupo, Tipo, Proveedor, Producto, Origen, Mes o Año con multi-selección en grillas para exportar **solo lo seleccionado** o armar informes a medida.
2. **Grupos de Material con Árbol CRUD Jerárquico (`Ctrl+2`):**
   - Árbol editable (`MaterialGroupNode`) con **Nodos Padre**, **Subgrupos** y **Productos Hijo** (crear, editar, mover por diálogo o Drag & Drop, reordenar, eliminar con elección de cascada).
   - Checkbox **`Excluir totales en 0`** y exportación completa o **solo de la subrama / filas seleccionadas**.
3. **Análisis de Proveedores (`Ctrl+3`):**
   - Clasificación **Pareto ABC**, variación porcentual respecto al período anterior, días de inactividad, mix de materiales, evolución mensual, patentes y orígenes frecuentes.
   - Checkbox **`Excluir totales en 0`**, multi-selección de proveedores/entregas y diálogo **`Exportar Selección...`**.
4. **Importador Simplificado y Control Origen -> Destino (`Ctrl+4`):**
   - Flujo visual **DESDE (Firebird TCP `WMDBSPC.GDB` / `PWAREHOUSE.FDB` o Access `.mdb`) ➔ HACIA (SQLite `gestion_scrap.db`)** con botón directo de 1 clic (`Importar Directo`), vista previa opcional y checkbox `Omitir 0 Kg`.
   - Pestaña **Control e Historial (Desde -> Hacia)** respaldada por la tabla SQLite `import_history` con KPIs y exportación a Excel.
5. **Seguridad, Perfiles y Auditoría (`Ctrl+5`):**
   - CRUD de **Perfiles RBAC (`PerfilUsuario`)** y **Usuarios** (`barriagada` / `Cambias0`), PBKDF2-SHA256 (`100.000` iteraciones), bloqueo tras `5` intentos, DPAPI, `audit_log`, respaldo SQLite en caliente y purga confirmada (`"ELIMINAR"`).

---

## Instalación, Uso y Compilación

1. Instale desde `Instalador/`:
   - `GestionScrap_Setup_v4.0.0_x64_SelfContained.exe` *(recomendado — cero dependencias previas)*
   - `GestionScrap_Setup_v4.0.0_x64_FrameworkDependent.exe` *(requiere .NET 10 Desktop Runtime x64)*
2. Inicie sesión con la cuenta administrativa sembrada (`barriagada` / `Cambias0`) y **cambie la contraseña inicial en producción** desde **Mi Clave** o **Seguridad y Perfiles (`Ctrl+5`)**.

```powershell
dotnet restore GestionScrap.sln
dotnet build GestionScrap.sln -c Debug --no-restore --disable-build-servers
dotnet build GestionScrap.sln -c Release --no-restore --disable-build-servers
dotnet test GestionScrap.sln -c Release --no-build --disable-build-servers --verbosity normal
```

---

## Documentación Técnica Completa

- [Arquitectura del Sistema](docs/ARQUITECTURA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación](docs/COMPILACION.md)
- [Guía de Despliegue e Instalación](docs/DESPLIEGUE.md)
- [Seguridad, Perfiles RBAC y Auditoría](docs/SEGURIDAD.md)
- [Plan y Evidencia de Pruebas QA](docs/PRUEBAS_QA.md)
- [Historial de Cambios (Changelog)](docs/CHANGELOG.md)
