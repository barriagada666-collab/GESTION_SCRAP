import React, { useState } from 'react';
import {
  FileText,
  Database,
  Code2,
  ShieldCheck,
  CheckSquare,
  Copy,
  Check,
  Cpu,
  Layers,
  ArrowRight,
  Server,
  Zap
} from 'lucide-react';

export const ArchitectureViewer: React.FC = () => {
  const [activeSubTab, setActiveSubTab] = useState<'win11' | 'gaps' | 'stack' | 'ddl' | 'code' | 'migration' | 'checklist'>('win11');
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2500);
  };

  return (
    <div className="space-y-6">
      {/* Sub navigation */}
      <div className="flex flex-wrap items-center gap-2 border-b border-slate-800 pb-3">
        <button
          type="button"
          onClick={() => setActiveSubTab('win11')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'win11' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <Server className="w-4 h-4" />
          <span>Guía Windows 11 & Firebird</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('gaps')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'gaps' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <ShieldCheck className="w-4 h-4" />
          <span>1. Análisis de Gaps y Riesgos</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('stack')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'stack' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <Cpu className="w-4 h-4" />
          <span>2. Stack Tecnológico (.NET 8)</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('ddl')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'ddl' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <Database className="w-4 h-4" />
          <span>3. Esquema DDL (SQL Server / PostgreSQL)</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('code')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'code' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <Code2 className="w-4 h-4" />
          <span>4. Código C# Clean Architecture</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('migration')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'migration' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <Layers className="w-4 h-4" />
          <span>5. Plan de Migración y Despliegue</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveSubTab('checklist')}
          className={`px-3.5 py-2 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
            activeSubTab === 'checklist' ? 'bg-cyan-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white bg-slate-900 border border-slate-800'
          }`}
        >
          <CheckSquare className="w-4 h-4" />
          <span>6. Checklist de Aceptación</span>
        </button>
      </div>

      {/* 0. Windows 11 & Firebird Guide */}
      {activeSubTab === 'win11' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-800 pb-4">
              <div>
                <h3 className="text-lg font-bold text-white flex items-center gap-2">
                  <Server className="w-5 h-5 text-cyan-400" />
                  <span>Paso a Paso: Despliegue en Windows 11 con Servidor Firebird Local</span>
                </h3>
                <p className="text-xs text-slate-400 mt-1">
                  Guía completa para utilizar la aplicación web en tu PC y configurar el motor Firebird para los archivos .GDB y .FDB.
                </p>
              </div>
            </div>

            {/* Step 1: Web App usage */}
            <div className="bg-slate-950 p-5 rounded-xl border border-slate-800 space-y-3">
              <span className="px-2.5 py-1 rounded-md text-xs font-bold font-mono bg-cyan-950 text-cyan-300 border border-cyan-800 uppercase">
                Paso 1: Uso Inmediato en Windows 11 (PWA o Navegador)
              </span>
              <p className="text-xs text-slate-300 leading-relaxed">
                Puedes abrir la aplicación directamente en <strong>Microsoft Edge</strong> o <strong>Google Chrome</strong> en Windows 11 e instalarla como aplicación nativa de escritorio:
              </p>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-1">
                <div className="p-3 bg-slate-900/90 border border-slate-800 rounded-lg space-y-1.5">
                  <span className="text-xs font-bold text-white block">En Microsoft Edge:</span>
                  <p className="text-xs text-slate-400">
                    1. Abre la aplicación en Edge.<br />
                    2. Haz clic en el menú <code>...</code> (arriba a la derecha).<br />
                    3. Selecciona <strong>Aplicaciones</strong> ➔ <strong>Instalar este sitio como una aplicación</strong>.<br />
                    4. Se agregará un acceso directo en tu Escritorio y Menú Inicio.
                  </p>
                </div>
                <div className="p-3 bg-slate-900/90 border border-slate-800 rounded-lg space-y-1.5">
                  <span className="text-xs font-bold text-white block">En Google Chrome:</span>
                  <p className="text-xs text-slate-400">
                    1. Abre la aplicación en Chrome.<br />
                    2. Haz clic en el menú <code>⋮</code> (tres puntos).<br />
                    3. Selecciona <strong>Guardar y compartir</strong> ➔ <strong>Instalar aplicación</strong>.<br />
                    4. La app se abrirá en una ventana limpia sin marcos de navegador.
                  </p>
                </div>
              </div>
            </div>

            {/* Step 2: Firebird Local Setup */}
            <div className="bg-slate-950 p-5 rounded-xl border border-slate-800 space-y-3">
              <span className="px-2.5 py-1 rounded-md text-xs font-bold font-mono bg-indigo-950 text-indigo-300 border border-indigo-800 uppercase">
                Paso 2: Instalación del Servidor Firebird en Windows 11
              </span>
              <p className="text-xs text-slate-300 leading-relaxed">
                Para leer y consultar los archivos <code>WMDBSPC.GDB</code> y <code>PWAREHOUSE.FDB</code> en tu propio equipo:
              </p>

              <ol className="list-decimal list-inside space-y-2 text-xs text-slate-300">
                <li>
                  Descarga el instalador de <strong>Firebird 2.5.9</strong> (o versión correspondiente al archivo GDB) desde <a href="https://firebirdsql.org/en/server-packages/" target="_blank" rel="noreferrer" className="text-cyan-400 underline">firebirdsql.org</a>.
                </li>
                <li>
                  Durante el asistente de instalación en Windows 11:
                  <ul className="list-disc list-inside ml-4 mt-1 text-slate-400 space-y-1">
                    <li>Selecciona: <strong>Run as a Service</strong> (Ejecutar como servicio de Windows).</li>
                    <li>Arquitectura: <strong>SuperServer</strong>.</li>
                    <li>Marca: <strong>Copy Firebird client library to &lt;system&gt; directory (fbclient.dll)</strong>.</li>
                    <li>Contraseña estándar del usuario SYSDBA: <code>masterkey</code>.</li>
                  </ul>
                </li>
              </ol>
            </div>

            {/* Step 3: Directories and Connection Strings */}
            <div className="bg-slate-950 p-5 rounded-xl border border-slate-800 space-y-3">
              <span className="px-2.5 py-1 rounded-md text-xs font-bold font-mono bg-emerald-950 text-emerald-300 border border-emerald-800 uppercase">
                Paso 3: Carpetas y Rutas de los Archivos .GDB / .FDB
              </span>
              <p className="text-xs text-slate-300 leading-relaxed">
                Crea en tu disco <code>C:\</code> las carpetas equivalentes al entorno de Cambiaso en Windows PowerShell:
              </p>

              <pre className="font-mono text-xs text-emerald-300 bg-slate-900 p-3 rounded-lg border border-slate-800 overflow-x-auto">
{`# Ejecuta en PowerShell como Administrador en Windows 11:
New-Item -ItemType Directory -Path "C:\\BDWM\\placilla" -Force
New-Item -ItemType Directory -Path "C:\\BDWM\\placilla2" -Force
New-Item -ItemType Directory -Path "C:\\BDWM\\thno" -Force

# Copia tus archivos en:
# C:\\BDWM\\placilla\\WMDBSPC.GDB
# C:\\BDWM\\placilla2\\WMDBSPC.GDB
# C:\\BDWM\\thno\\PWAREHOUSE.FDB`}
              </pre>

              <div className="p-3 bg-slate-900 border border-slate-800 rounded-lg">
                <span className="text-xs font-bold text-amber-400 block mb-1">Cadena de Conexión Local en Windows 11:</span>
                <code className="text-xs font-mono text-slate-200">
                  User=SYSDBA;Password=masterkey;Database=127.0.0.1:C:\BDWM\placilla2\WMDBSPC.GDB;Dialect=3;
                </code>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 1. Gaps and Risks */}
      {activeSubTab === 'gaps' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <ShieldCheck className="w-5 h-5 text-amber-400" />
              <span>Análisis de Gaps, Deuda Técnica y Riesgos Críticos del Sistema VBA Legacy</span>
            </h3>
            <p className="text-sm text-slate-300 leading-relaxed">
              El sistema actual <strong>“Informes P.Cambiaso 2.5.xlsm”</strong> representa una solución táctica de alto valor para el negocio pero con graves vulnerabilidades de escalabilidad, seguridad y concurrencia debido a su infraestructura monolítica en Excel VBA + Access + Firebird.
            </p>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
              <div className="p-4 bg-slate-950 border border-red-900/60 rounded-xl space-y-2">
                <div className="flex items-center gap-2 text-red-400 font-bold text-sm">
                  <span>1. Inyección SQL y Falta de Sanitización</span>
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">
                  <strong>Estado Actual:</strong> Las consultas concatenan strings directamente: <br/>
                  <code className="text-[11px] bg-red-950/80 px-1 py-0.5 rounded text-red-300">
                    "WHERE rut_cliente='" & rut & "' and tipo_pesaje='...'"
                  </code><br/>
                  <strong>Riesgo:</strong> Posibilidad de corrupción accidental de base de datos o inyecciones maliciosas. <br/>
                  <strong>Solución en .NET / App Moderna:</strong> Parámetros fuertemente tipados con Entity Framework Core o Dapper y validaciones con FluentValidation.
                </p>
              </div>

              <div className="p-4 bg-slate-950 border border-amber-900/60 rounded-xl space-y-2">
                <div className="flex items-center gap-2 text-amber-400 font-bold text-sm">
                  <span>2. Corrupción de Base de Datos Access (Jet Engine)</span>
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">
                  <strong>Estado Actual:</strong> Los archivos Access (.mdb/.accdb) no soportan concurrencia real multi-usuario, tienen límite de 2GB y sufren bloqueos si un usuario cierra Excel abruptamente. <br/>
                  <strong>Riesgo:</strong> Pérdida de pesajes y necesidad frecuente de compactar y reparar. <br/>
                  <strong>Solución:</strong> Servidor relacional PostgreSQL o SQL Server con ACID y transacciones aisladas.
                </p>
              </div>

              <div className="p-4 bg-slate-950 border border-amber-900/60 rounded-xl space-y-2">
                <div className="flex items-center gap-2 text-amber-400 font-bold text-sm">
                  <span>3. Dependencia de Conexión Firebird por IP Fija</span>
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">
                  <strong>Estado Actual:</strong> Hardcoding de credenciales y rutas de red locales: <br/>
                  <code className="text-[11px] bg-amber-950/80 px-1 py-0.5 rounded text-amber-300">
                    192.168.2.56:\BDWM\placilla\WMDBSPC.GDB (SYSDBA / masterkey)
                  </code><br/>
                  <strong>Riesgo:</strong> Vulnerabilidad de credenciales universales de Firebird expuestas en texto plano en la macro VBA. <br/>
                  <strong>Solución:</strong> FirebirdClient pool en capa Infrastructure con connection strings encriptadas en appsettings / Key Vault.
                </p>
              </div>

              <div className="p-4 bg-slate-950 border border-cyan-900/60 rounded-xl space-y-2">
                <div className="flex items-center gap-2 text-cyan-400 font-bold text-sm">
                  <span>4. Ausencia de Trazabilidad y Auditoría de Precios</span>
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">
                  <strong>Estado Actual:</strong> Cuando se modifica un precio con <code>Act_precios</code> o se limpian tablas, no queda registro de quién ni cuándo se alteró el valor financiero. <br/>
                  <strong>Solución:</strong> Tabla de auditoría inmutable con usuario, fecha, valor anterior y nuevo.
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 2. Tech Stack */}
      {activeSubTab === 'stack' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Cpu className="w-5 h-5 text-cyan-400" />
              <span>Stack Tecnológico Justificado: C# .NET 8 + Blazor / React SPA</span>
            </h3>
            <p className="text-sm text-slate-300 leading-relaxed">
              La arquitectura recomendada sigue los principios de <strong>Clean Architecture (Arquitectura Limpia)</strong> y <strong>Domain-Driven Design (DDD)</strong> para desacoplar las reglas de negocio de los pesajes y precios de los mecanismos de almacenamiento.
            </p>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 pt-2">
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                <span className="text-xs font-mono text-cyan-400 font-bold uppercase block mb-1">
                  1. Capa Dominio (Domain)
                </span>
                <p className="text-xs text-slate-300">
                  Entidades puras (Pesaje, PrecioProveedor, Cliente, Producto), Value Objects (RUT, Kilos, CLP), interfaces de repositorios y reglas de negocio sin dependencias externas.
                </p>
              </div>
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                <span className="text-xs font-mono text-blue-400 font-bold uppercase block mb-1">
                  2. Capa Aplicación (Application)
                </span>
                <p className="text-xs text-slate-300">
                  Casos de uso CQRS (MediatR), DTOs, validaciones con FluentValidation, servicios de generación de informes y orquestación de transacciones.
                </p>
              </div>
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                <span className="text-xs font-mono text-indigo-400 font-bold uppercase block mb-1">
                  3. Capa Infraestructura (Infrastructure)
                </span>
                <p className="text-xs text-slate-300">
                  EF Core / PostgreSQL / SQL Server, Conector FirebirdSql.Data.FirebirdClient con Polly para reintentos y pool de conexiones, ClosedXML / EPPlus para Excel.
                </p>
              </div>
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800">
                <span className="text-xs font-mono text-emerald-400 font-bold uppercase block mb-1">
                  4. Capa Presentación (Presentation)
                </span>
                <p className="text-xs text-slate-300">
                  ASP.NET Core Web API + SPA React / TypeScript interactiva (o Blazor Server) con Tailwind CSS, compatible con cualquier navegador empresarial.
                </p>
              </div>
            </div>

            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 mt-4">
              <h4 className="text-xs font-bold text-slate-300 uppercase tracking-wider mb-2">
                Estructura de Carpetas de la Solución .NET 8 (Clean Architecture)
              </h4>
              <pre className="font-mono text-xs text-cyan-300 bg-slate-900/80 p-3 rounded-lg overflow-x-auto leading-relaxed">
{`InformesPCambiaso.sln
├── src/
│   ├── InformesPCambiaso.Domain/
│   │   ├── Entities/ (Pesaje, Cliente, Producto, PrecioProveedor, IndexPrecioPesaje)
│   │   ├── Enums/ (Planta, TipoPesaje, GrupoMaterial)
│   │   └── Interfaces/ (IPesajesRepository, IFirebirdSyncService, IExcelExportService)
│   │
│   ├── InformesPCambiaso.Application/
│   │   ├── Common/ (Behaviors, Exceptions, Mappings)
│   │   ├── Pesajes/Queries/ (GetInformeGrupoMaterial, GetComprasTraslados, GetInformeCliente)
│   │   ├── Precios/Commands/ (ActualizarPrecioRealCommand, IngresarPrecioProveedorCommand)
│   │   ├── Firebird/Commands/ (ImportarPesajesFirebirdCommand, EjecutarProcedimientosCommand)
│   │   └── Validators/ (ActualizarPrecioValidator, IngresarPrecioValidator)
│   │
│   ├── InformesPCambiaso.Infrastructure/
│   │   ├── Persistence/ (ApplicationDbContext, Configurations, Migrations)
│   │   ├── Firebird/ (FirebirdConnectionFactory, FirebirdSyncService)
│   │   ├── Excel/ (ClosedXmlExportService, Templates/)
│   │   └── Logging/ (SerilogConfiguration)
│   │
│   ├── InformesPCambiaso.WebApi/
│   │   ├── Controllers/ (PesajesController, PreciosController, FirebirdController)
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── InformesPCambiaso.ClientApp/ (React 19 + TypeScript + Tailwind CSS UI actual)`}
              </pre>
            </div>
          </div>
        </div>
      )}

      {/* 3. DDL Schema */}
      {activeSubTab === 'ddl' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-lg font-bold text-white flex items-center gap-2">
                  <Database className="w-5 h-5 text-emerald-400" />
                  <span>Modelo de Datos Unificado (DDL PostgreSQL / SQL Server)</span>
                </h3>
                <p className="text-xs text-slate-400 mt-1">
                  Reemplaza las múltiples tablas dispersas de Access por un esquema relacional normalizado con claves foráneas, índices de alto rendimiento y tablas de auditoría.
                </p>
              </div>
              <button
                type="button"
                onClick={() => copyToClipboard(SQL_DDL_SCRIPT, 'ddl_script')}
                className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-colors"
              >
                {copiedId === 'ddl_script' ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
                <span>{copiedId === 'ddl_script' ? '¡Copiado!' : 'Copiar DDL SQL'}</span>
              </button>
            </div>

            <pre className="font-mono text-xs text-emerald-300 bg-slate-950 p-4 rounded-xl overflow-x-auto max-h-96 leading-relaxed border border-slate-800">
              {SQL_DDL_SCRIPT}
            </pre>
          </div>
        </div>
      )}

      {/* 4. C# Code */}
      {activeSubTab === 'code' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-lg font-bold text-white flex items-center gap-2">
                  <Code2 className="w-5 h-5 text-cyan-400" />
                  <span>Implementación C# .NET 8 (Servicios Críticos y Conectores)</span>
                </h3>
                <p className="text-xs text-slate-400 mt-1">
                  Código de producción que reemplaza los módulos VBA Conexion, SQL, INFORMES y cargar_grillas.
                </p>
              </div>
              <button
                type="button"
                onClick={() => copyToClipboard(CSHARP_CODE_SNIPPET, 'csharp_code')}
                className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-colors"
              >
                {copiedId === 'csharp_code' ? <Check className="w-4 h-4 text-cyan-400" /> : <Copy className="w-4 h-4" />}
                <span>{copiedId === 'csharp_code' ? '¡Copiado!' : 'Copiar Código C#'}</span>
              </button>
            </div>

            <pre className="font-mono text-xs text-cyan-200 bg-slate-950 p-4 rounded-xl overflow-x-auto max-h-96 leading-relaxed border border-slate-800">
              {CSHARP_CODE_SNIPPET}
            </pre>
          </div>
        </div>
      )}

      {/* 5. Migration Plan */}
      {activeSubTab === 'migration' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Layers className="w-5 h-5 text-amber-400" />
              <span>Estrategia de Migración de Datos y Plan de Despliegue</span>
            </h3>

            <div className="space-y-4 text-xs text-slate-300">
              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-2">
                <h4 className="font-bold text-sm text-amber-300 flex items-center gap-2">
                  <span>Fase 1: Extracción y Limpieza de Access Histórico</span>
                </h4>
                <p className="leading-relaxed">
                  Exportación de las tablas Access <code>PESAJE_PLACILLA</code>, <code>PESAJE_PLACILLA_2</code>, <code>PESAJE_SANTIAGO</code>, <code>PESAJE_TALCAHUANO</code> y <code>precios_proveedor_*</code> hacia archivos CSV delimitados por punto y coma con encoding UTF-8.
                </p>
              </div>

              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-2">
                <h4 className="font-bold text-sm text-cyan-300 flex items-center gap-2">
                  <span>Fase 2: Script ETL en Python / .NET para Carga Inicial</span>
                </h4>
                <pre className="font-mono text-[11px] text-slate-300 bg-slate-900 p-3 rounded border border-slate-800">
{`# Script Python ETL de migración Access -> PostgreSQL / SQL Server
import pyodbc, psycopg2
conn_access = pyodbc.connect(r'DRIVER={Microsoft Access Driver (*.mdb, *.accdb)};DBQ=C:\\BD\\conexionBD_Scrap.mdb;')
conn_pg = psycopg2.connect("dbname=cambiaso_pesajes user=postgres password=secret host=localhost")
# Transferencia en bloques de 5.000 registros con validación de tipos y nulos`}
                </pre>
              </div>

              <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-2">
                <h4 className="font-bold text-sm text-emerald-300 flex items-center gap-2">
                  <span>Fase 3: Operación en Paralelo (Dual Running) por 2 Semanas</span>
                </h4>
                <p className="leading-relaxed">
                  Se mantiene el Excel VBA en modo lectura mientras los operadores registran y emiten informes en la nueva aplicación web. Se comparan diariamente las sumas de kilos totales por planta para asegurar 100% de paridad aritmética.
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 6. Acceptance Checklist */}
      {activeSubTab === 'checklist' && (
        <div className="space-y-6">
          <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl space-y-4">
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <CheckSquare className="w-5 h-5 text-emerald-400" />
              <span>Checklist de Pruebas de Aceptación (UAT)</span>
            </h3>

            <div className="space-y-3">
              {[
                { cat: 'Pestaña 0', desc: 'Informe Grupo Material Placilla, Santiago, Talcahuano: coincidencia de totales de kilos y opción semanal.' },
                { cat: 'Pestaña 1', desc: 'Informe Compras + Traslados: resumen general (Fila 1 + Fila 2 = Fila 3) y exportación Excel con 2 hojas.' },
                { cat: 'Pestaña 2', desc: 'Informe por Cliente: filtro por RUT o Folio, carga de datos de cliente y exportación de Ticket consolidado.' },
                { cat: 'Pestaña 3', desc: 'Informes Especiales: cálculo simultáneo de Retail Stgo, Retail Empresas, Mix, Regiones, Agrícola, Pesquera, Agrosuper.' },
                { cat: 'Pestaña 4', desc: 'Informe JCRAMM: filtro pes_num > last_folio, resumen de materiales, exportación y actualización de último folio.' },
                { cat: 'Pestaña 5', desc: 'Informe de Precios: cálculo de precios teóricos vs reales, promedio ponderado BASULIP/RECAM/ALTA, bloqueo de edición individual si no está en Precios Reales.' },
                { cat: 'Pestaña 6', desc: 'Mantenedor de Precios: validación de duplicados antes de insertar, selectores modales Clientes y Productos.' },
                { cat: 'Pestaña 7', desc: 'Importación Firebird: conexión remota a 192.168.2.56, detección de duplicados por folio y hora, carga a BD Central.' },
                { cat: 'Seguridad', desc: 'Ejecución transaccional de PA_INS_PROV_STGO y PA_INS_MASIVO_EXP_SAP con Commit/Rollback garantizado.' },
                { cat: 'Integridad', desc: 'Limpieza de tablas con confirmación explícita irreversible y registro en log de auditoría.' },
              ].map((item, idx) => (
                <div key={idx} className="p-3 bg-slate-950 border border-slate-800 rounded-lg flex items-start gap-3">
                  <div className="p-1 bg-emerald-950 text-emerald-400 rounded mt-0.5">
                    <Check className="w-4 h-4" />
                  </div>
                  <div>
                    <span className="text-xs font-bold text-cyan-400 font-mono uppercase mr-2">
                      [{item.cat}]
                    </span>
                    <span className="text-xs text-slate-300">{item.desc}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

const SQL_DDL_SCRIPT = `-- =========================================================================
-- ESQUEMA RELACIONAL CENTRALIZADO: SISTEMA PESAJE E INFORMES CAMBIASO
-- Motor: PostgreSQL 15+ / SQL Server 2022
-- =========================================================================

-- 1. Tabla de Catálogo de Productos y Familias
CREATE TABLE productos (
    codigo VARCHAR(20) PRIMARY KEY,
    descripcion VARCHAR(150) NOT NULL,
    grupo VARCHAR(50) NOT NULL, -- 'SUPER-BASULIP', 'RECAM', 'ALTA', 'MIX', 'SCRAP'
    unidad_medida VARCHAR(10) DEFAULT 'KG',
    activo BOOLEAN DEFAULT TRUE,
    creado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. Tabla Maestra de Clientes / Proveedores
CREATE TABLE clientes_proveedores (
    rut VARCHAR(15) PRIMARY KEY,
    digito_verificador CHAR(1),
    razon_social VARCHAR(200) NOT NULL,
    nombre_fantasia VARCHAR(200),
    ciudad VARCHAR(100),
    planta_asociada VARCHAR(20) DEFAULT 'TODAS', -- 'PLACILLA', 'SANTIAGO', 'TALCAHUANO', 'TODAS'
    creado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 3. Tabla Unificada de Pesajes (Reemplaza PESAJE_PLACILLA, PESAJE_SANTIAGO, PESAJE_TALCAHUANO)
CREATE TABLE pesajes (
    id BIGSERIAL PRIMARY KEY,
    folio BIGINT NOT NULL,
    planta VARCHAR(20) NOT NULL, -- 'PLACILLA', 'SANTIAGO', 'TALCAHUANO'
    tipo_pesaje VARCHAR(50) NOT NULL, -- 'Recepcion de carga', 'Despacho de Carga', 'Movimiento Interno'
    patente VARCHAR(15) NOT NULL,
    peso_bruto INT NOT NULL,
    peso_tara INT NOT NULL,
    peso_neto INT GENERATED ALWAYS AS (peso_bruto - peso_tara) STORED,
    cod_producto VARCHAR(20) REFERENCES productos(codigo),
    rut_cliente VARCHAR(15) REFERENCES clientes_proveedores(rut),
    origen VARCHAR(120),
    destino VARCHAR(120),
    guia_despacho VARCHAR(50),
    fecha_ingreso DATE NOT NULL,
    hora_ingreso TIME NOT NULL,
    fecha_salida DATE,
    hora_salida TIME,
    observacion TEXT,
    es_traslado BOOLEAN DEFAULT FALSE,
    creado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_pesaje_folio_planta_hora UNIQUE (folio, planta, hora_ingreso)
);

CREATE INDEX idx_pesajes_fecha_planta ON pesajes(fecha_ingreso, planta);
CREATE INDEX idx_pesajes_rut ON pesajes(rut_cliente);
CREATE INDEX idx_pesajes_folio ON pesajes(folio);

-- 4. Precios Pactados por Proveedor y Material (precios_proveedor_*)
CREATE TABLE precios_proveedor (
    id BIGSERIAL PRIMARY KEY,
    planta VARCHAR(20) NOT NULL,
    rut_proveedor VARCHAR(15) REFERENCES clientes_proveedores(rut),
    cod_producto VARCHAR(20) REFERENCES productos(codigo),
    precio_unitario NUMERIC(12, 2) NOT NULL,
    fecha_actualizacion DATE NOT NULL DEFAULT CURRENT_DATE,
    actualizado_por VARCHAR(50) DEFAULT 'SISTEMA',
    CONSTRAINT uq_precio_prov_planta UNIQUE (planta, rut_proveedor, cod_producto)
);

-- 5. Precios Reales Indexados por Pesaje (index_pesaje_*)
CREATE TABLE index_pesaje_precios (
    id BIGSERIAL PRIMARY KEY,
    folio BIGINT NOT NULL,
    planta VARCHAR(20) NOT NULL,
    precio_unitario NUMERIC(12, 2) NOT NULL,
    familia VARCHAR(50),
    fecha_asignacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_index_pesaje UNIQUE (folio, planta)
);

-- 6. Control de Último Folio JCRAMM
CREATE TABLE control_ultimo_folio_jcramm (
    id INT PRIMARY KEY DEFAULT 1,
    ultimo_folio BIGINT NOT NULL,
    fecha_procesamiento DATE NOT NULL,
    actualizado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_control_id CHECK (id = 1)
);

-- 7. Tabla de Auditoría Inmutable
CREATE TABLE auditoria_transacciones (
    id BIGSERIAL PRIMARY KEY,
    fecha_hora TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    usuario VARCHAR(50) NOT NULL,
    accion VARCHAR(50) NOT NULL,
    entidad VARCHAR(50) NOT NULL,
    id_registro VARCHAR(50),
    detalle_cambio TEXT NOT NULL
);`;

const CSHARP_CODE_SNIPPET = `// =========================================================================
// CAPA INFRAESTRUCTURA: Conector Firebird con Transacciones Aisladas
// InformesPCambiaso.Infrastructure/Firebird/FirebirdSyncService.cs
// =========================================================================
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging;

public class FirebirdSyncService : IFirebirdSyncService
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdSyncService> _logger;

    public FirebirdSyncService(IConfiguration config, ILogger<FirebirdSyncService> logger)
    {
        _connectionString = config.GetConnectionString("FirebirdSantiago")!;
        _logger = logger;
    }

    public async Task<bool> EjecutarProcedimientosConCommitAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando conexión ADO.NET con Firebird...");
        using var connection = new FbConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // Paso 1: PA_INS_PROV_STGO
            _logger.LogInformation("Ejecutando Stored Procedure PA_INS_PROV_STGO...");
            using var cmd1 = new FbCommand("PA_INS_PROV_STGO", connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            await cmd1.ExecuteNonQueryAsync(ct);

            // Paso 2: PA_INS_MASIVO_EXP_SAP
            _logger.LogInformation("Ejecutando Stored Procedure PA_INS_MASIVO_EXP_SAP...");
            using var cmd2 = new FbCommand("PA_INS_MASIVO_EXP_SAP", connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            await cmd2.ExecuteNonQueryAsync(ct);

            // Confirmación transaccional
            transaction.Commit();
            _logger.LogInformation("Transacción Firebird confirmada exitosamente (COMMIT).");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falla en ejecución de procedimientos Firebird. Aplicando ROLLBACK...");
            transaction.Rollback();
            throw;
        }
    }
}`;
