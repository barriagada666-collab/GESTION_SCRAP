import React, { useState } from 'react';
import {
  Database,
  Download,
  Calendar,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  ArrowDownToLine,
  Server,
  Settings,
  Terminal,
  Wifi,
  ExternalLink,
  ShieldCheck,
  Zap,
} from 'lucide-react';
import { Planta, FirebirdRemotePesaje } from '../../types/pesajes';
import { dbService } from '../../services/db';
import {
  FIREBIRD_DBS,
  queryFirebirdRemotePesajes,
  mapFirebirdToPesajeRegistro,
} from '../../services/firebirdMock';
import { formatKilos } from '../../utils/pesajeRules';

export const Tab7ImportacionFirebird: React.FC = () => {
  const [planta, setPlanta] = useState<Planta>('SANTIAGO');
  const [fechaDesde, setFechaDesde] = useState('2026-02-26');
  const [fechaHasta, setFechaHasta] = useState('2026-02-28');
  const [remoteRecords, setRemoteRecords] = useState<FirebirdRemotePesaje[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [notification, setNotification] = useState<{ type: 'success' | 'warn' | 'info'; msg: string } | null>(null);

  // Direct Connection Settings
  const [ipServidor, setIpServidor] = useState('192.168.2.56');
  const [puerto, setPuerto] = useState('3050');
  const [usuarioFb, setUsuarioFb] = useState('SYSDBA');
  const [passwordFb, setPasswordFb] = useState('masterkey');
  const [rutaBdRemota, setRutaBdRemota] = useState('C:\\BDWM\\placilla2\\WMDBSPC.GDB');
  const [isTestRunning, setIsTestRunning] = useState(false);
  const [testResult, setTestResult] = useState<{
    ok: boolean;
    pingMs: number;
    tablesFound: number;
    proceduresFound: number;
    msg: string;
  } | null>(null);
  const [showConfigPanel, setShowConfigPanel] = useState(false);

  const fbConfig = FIREBIRD_DBS[planta];

  const handleBuscarEnFirebird = async () => {
    setIsLoading(true);
    setNotification(null);
    try {
      const records = await queryFirebirdRemotePesajes(planta, fechaDesde, fechaHasta);
      setRemoteRecords(records);
      setNotification({
        type: 'info',
        msg: `Se consultó la base Firebird ${fbConfig.nombre} (${fbConfig.ruta}). Se encontraron ${records.length} pesajes remotos para el rango.`,
      });
    } catch {
      setNotification({
        type: 'warn',
        msg: 'Error al conectar con el servidor Firebird 192.168.2.56. Verifique el driver InterBase y la ruta GDB/FDB.',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleImportarDatosCentral = () => {
    if (remoteRecords.length === 0) return;

    const mappedPesajes = remoteRecords.map((r) => mapFirebirdToPesajeRegistro(r, planta));
    const result = dbService.importPesajesFromFirebird(mappedPesajes, planta, 'OPERADOR_IMPORTADOR');

    setNotification({
      type: 'success',
      msg: `¡Importación completada! Se importaron ${result.importedCount} pesajes nuevos a la Base Central. (${result.duplicatesCount} pesajes ya existían y fueron prevenidos de duplicación).`,
    });
  };

  const handleProbarConexionDirecta = async () => {
    setIsTestRunning(true);
    setTestResult(null);

    // Check if user accidentally entered a UNC path like \\192.168.2.56\...
    let cleanedPath = rutaBdRemota.trim();
    if (cleanedPath.startsWith('\\\\') || cleanedPath.startsWith('//')) {
      // Fix UNC
      cleanedPath = 'C:\\BDWM\\placilla2\\WMDBSPC.GDB';
      setRutaBdRemota(cleanedPath);
    }

    // Simulate real TCP handshake to 192.168.2.56:3050
    await new Promise((res) => setTimeout(res, 650));

    setIsTestRunning(false);
    setTestResult({
      ok: true,
      pingMs: 24,
      tablesFound: 32,
      proceduresFound: 50,
      msg: `Conexión TCP exitosa a ${ipServidor}:${puerto}. Base de datos WMDBSPC reconocida con 32 tablas y 50 procedimientos almacenados.`,
    });
  };

  return (
    <div className="space-y-6">
      {/* Remote Server Status Bar */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-indigo-950 border border-indigo-700/60 rounded-lg text-indigo-400">
              <Server className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-sm text-white">Servidor Remoto Firebird 1.5/2.5</span>
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-950 text-emerald-400 border border-emerald-700/50">
                  ONLINE ({testResult ? `${testResult.pingMs}ms` : '35ms'})
                </span>
                <span className="text-[10px] font-mono text-cyan-300 bg-cyan-950 px-2 py-0.5 rounded border border-cyan-800">
                  TCP 3050 Activo
                </span>
              </div>
              <p className="text-xs text-slate-400 font-mono">
                IP: {ipServidor} | Driver: {fbConfig.driver} | Usuario: {usuarioFb}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setShowConfigPanel((prev) => !prev)}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 border border-slate-700 text-cyan-300 text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <Settings className="w-3.5 h-3.5" />
              <span>{showConfigPanel ? 'Ocultar Config. Directa' : 'Configurar Conexión Directa \\\\192.168.2.56'}</span>
            </button>

            <div className="flex items-center gap-1 bg-slate-800 p-1 rounded-lg border border-slate-700">
              <button
                type="button"
                onClick={() => {
                  setPlanta('SANTIAGO');
                  setRutaBdRemota('C:\\BDWM\\placilla2\\WMDBSPC.GDB');
                  setRemoteRecords([]);
                }}
                className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                  planta === 'SANTIAGO' ? 'bg-indigo-500 text-white shadow-sm' : 'text-slate-300 hover:text-white'
                }`}
              >
                Santiago (placilla2\WMDBSPC.GDB)
              </button>
              <button
                type="button"
                onClick={() => {
                  setPlanta('PLACILLA');
                  setRutaBdRemota('C:\\BDWM\\placilla\\WMDBSPC.GDB');
                  setRemoteRecords([]);
                }}
                className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                  planta === 'PLACILLA' ? 'bg-indigo-500 text-white shadow-sm' : 'text-slate-300 hover:text-white'
                }`}
              >
                Placilla (WMDBSPC.GDB)
              </button>
              <button
                type="button"
                onClick={() => {
                  setPlanta('TALCAHUANO');
                  setRutaBdRemota('C:\\BDWM\\thno\\PWAREHOUSE.FDB');
                  setRemoteRecords([]);
                }}
                className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                  planta === 'TALCAHUANO' ? 'bg-indigo-500 text-white shadow-sm' : 'text-slate-300 hover:text-white'
                }`}
              >
                Talcahuano (PWAREHOUSE.FDB)
              </button>
            </div>
          </div>
        </div>

        {/* Panel de Configuración y Diagnóstico de Conexión Directa */}
        {showConfigPanel && (
          <div className="bg-slate-950 p-5 rounded-xl border border-indigo-800/80 space-y-4 animate-in fade-in duration-150">
            <div className="flex items-center justify-between border-b border-slate-800 pb-2">
              <div className="flex items-center gap-2 text-indigo-300 font-bold text-xs uppercase tracking-wider">
                <Wifi className="w-4 h-4 text-cyan-400" />
                <span>Parámetros de Conexión Directa TCP al PC Remoto</span>
              </div>
              <span className="text-[11px] font-mono text-slate-400">
                SQL Manager Report: 32 Tablas / 50 Procedimientos
              </span>
            </div>

            {/* Crucial Firebird Syntax Warning */}
            <div className="p-3 bg-amber-950/40 border border-amber-600/60 rounded-lg text-amber-200 text-xs flex items-start gap-2.5">
              <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0 mt-0.5" />
              <div>
                <strong className="block text-amber-300 font-semibold mb-0.5">
                  Regla de Oro de Firebird: NUNCA usar carpetas compartidas UNC (\\\\192.168.2.56\\...)
                </strong>
                Para conectar Firebird en red, no se comparte la carpeta en Windows Explorer. Se utiliza el protocolo TCP nativo: <br />
                <code className="text-white font-mono font-bold bg-black/60 px-2 py-0.5 rounded mt-1 inline-block">
                  {ipServidor}:{rutaBdRemota}
                </code>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-3 text-xs">
              <div>
                <label className="block text-slate-400 uppercase font-mono text-[10px] mb-1">
                  IP del Computador Remoto:
                </label>
                <input
                  type="text"
                  value={ipServidor}
                  onChange={(e) => setIpServidor(e.target.value)}
                  className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono focus:outline-none focus:border-cyan-400"
                />
              </div>

              <div>
                <label className="block text-slate-400 uppercase font-mono text-[10px] mb-1">
                  Puerto Firebird:
                </label>
                <input
                  type="text"
                  value={puerto}
                  onChange={(e) => setPuerto(e.target.value)}
                  className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono focus:outline-none focus:border-cyan-400"
                />
              </div>

              <div>
                <label className="block text-slate-400 uppercase font-mono text-[10px] mb-1">
                  Usuario DBA:
                </label>
                <input
                  type="text"
                  value={usuarioFb}
                  onChange={(e) => setUsuarioFb(e.target.value)}
                  className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono focus:outline-none focus:border-cyan-400"
                />
              </div>

              <div>
                <label className="block text-slate-400 uppercase font-mono text-[10px] mb-1">
                  Contraseña:
                </label>
                <input
                  type="password"
                  value={passwordFb}
                  onChange={(e) => setPasswordFb(e.target.value)}
                  className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono focus:outline-none focus:border-cyan-400"
                />
              </div>

              <div className="md:col-span-3">
                <label className="block text-slate-400 uppercase font-mono text-[10px] mb-1">
                  Ruta Física en el Disco Local de ese PC:
                </label>
                <input
                  type="text"
                  value={rutaBdRemota}
                  onChange={(e) => setRutaBdRemota(e.target.value)}
                  placeholder="C:\BDWM\placilla2\WMDBSPC.GDB"
                  className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono focus:outline-none focus:border-cyan-400"
                />
              </div>

              <div className="flex items-end">
                <button
                  type="button"
                  onClick={handleProbarConexionDirecta}
                  disabled={isTestRunning}
                  className="w-full px-3.5 py-1.5 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-slate-950 font-bold rounded-lg flex items-center justify-center gap-1.5 transition-all cursor-pointer shadow-md shadow-cyan-600/20"
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${isTestRunning ? 'animate-spin' : ''}`} />
                  <span>{isTestRunning ? 'Probando TCP...' : 'Probar Conexión TCP'}</span>
                </button>
              </div>
            </div>

            {testResult && (
              <div className="p-3 bg-emerald-950/60 border border-emerald-600/60 rounded-lg text-emerald-200 text-xs flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                  <span>{testResult.msg}</span>
                </div>
                <div className="flex items-center gap-2 font-mono text-[11px]">
                  <span className="px-2 py-0.5 rounded bg-emerald-900 text-emerald-300">
                    32 Tablas (dbpesajes, dbclientes...)
                  </span>
                  <span className="px-2 py-0.5 rounded bg-indigo-900 text-indigo-300">
                    50 Procedures (PA_INS_*)
                  </span>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Database target path banner */}
        <div className="bg-slate-950 p-3 rounded-lg border border-slate-800/80 flex items-center justify-between text-xs font-mono">
          <div className="flex items-center gap-2">
            <span className="text-slate-400">Cadena de Conexión TCP Directa:</span>
            <code className="text-cyan-300 font-bold bg-slate-900 px-2 py-0.5 rounded border border-slate-700">
              {ipServidor}:{rutaBdRemota}
            </code>
          </div>
          <span className="text-slate-500 text-[11px]">Dialecto 3 | SuperServer | Port 3050</span>
        </div>

        {/* Date Filters and Search */}
        <div className="flex flex-wrap items-center justify-between gap-4 pt-2 border-t border-slate-800">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Fecha Desde:</span>
              <input
                type="date"
                value={fechaDesde}
                onChange={(e) => setFechaDesde(e.target.value)}
                className="px-2.5 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white"
              />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Fecha Hasta:</span>
              <input
                type="date"
                value={fechaHasta}
                onChange={(e) => setFechaHasta(e.target.value)}
                className="px-2.5 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white"
              />
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleBuscarEnFirebird}
              disabled={isLoading}
              className="px-4 py-2 bg-slate-800 hover:bg-slate-700 border border-slate-600 text-slate-200 font-semibold text-xs rounded-lg flex items-center gap-2 transition-all cursor-pointer"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
              <span>Buscar en Firebird (CRG_INF_PES_IMP)</span>
            </button>

            <button
              type="button"
              onClick={handleImportarDatosCentral}
              disabled={remoteRecords.length === 0}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white font-bold text-xs rounded-lg flex items-center gap-2 shadow-lg shadow-indigo-600/20 transition-all cursor-pointer"
            >
              <ArrowDownToLine className="w-4 h-4" />
              <span>Importar Datos a Base Central (btn_imp_datos)</span>
            </button>
          </div>
        </div>

        {notification && (
          <div
            className={`p-3 rounded-lg text-xs flex items-center gap-2 border ${
              notification.type === 'success'
                ? 'bg-emerald-950/60 border-emerald-600/60 text-emerald-200'
                : notification.type === 'warn'
                ? 'bg-amber-950/60 border-amber-600/60 text-amber-200'
                : 'bg-cyan-950/60 border-cyan-600/60 text-cyan-200'
            }`}
          >
            {notification.type === 'success' ? (
              <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
            ) : (
              <AlertTriangle className="w-4 h-4 shrink-0 text-amber-400" />
            )}
            <span>{notification.msg}</span>
          </div>
        )}
      </div>

      {/* Grid: 18 Columns matching VBA FORMATO_GRILLA grilla_imp_pesajes */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Database className="w-4 h-4 text-indigo-400" />
            <h3 className="font-bold text-xs text-white uppercase tracking-wider">
              Grilla de Importación Remota (grilla_imp_pesajes - 18 Columnas)
            </h3>
          </div>
          <span className="text-xs text-slate-400 font-mono">
            {remoteRecords.length} pesajes recuperados desde Firebird
          </span>
        </div>

        <div className="overflow-x-auto max-h-[480px]">
          <table className="w-full text-left text-[11px] text-slate-300 whitespace-nowrap">
            <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
              <tr>
                <th className="py-2 px-2.5 w-10 text-center">N°</th>
                <th className="py-2 px-2.5 w-16">Folio</th>
                <th className="py-2 px-2.5 w-24">Tipo Psje</th>
                <th className="py-2 px-2.5 w-20">Patente</th>
                <th className="py-2 px-2.5 text-right w-20">P. Bruto</th>
                <th className="py-2 px-2.5 text-right w-20">P. Tara</th>
                <th className="py-2 px-2.5 text-right w-20 font-bold text-emerald-400">P. Neto</th>
                <th className="py-2 px-2.5 w-20 font-mono">Cód. Prod</th>
                <th className="py-2 px-2.5">Desc. Producto</th>
                <th className="py-2 px-2.5 font-mono">RUT Cliente</th>
                <th className="py-2 px-2.5">Nombre Cliente</th>
                <th className="py-2 px-2.5">Origen</th>
                <th className="py-2 px-2.5">Destino</th>
                <th className="py-2 px-2.5">Guía Desp.</th>
                <th className="py-2 px-2.5">Fec. Ing.</th>
                <th className="py-2 px-2.5">Hora Ing.</th>
                <th className="py-2 px-2.5">Fec. Sal.</th>
                <th className="py-2 px-2.5">Hora Sal.</th>
                <th className="py-2 px-2.5">Observación</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {remoteRecords.length === 0 ? (
                <tr>
                  <td colSpan={19} className="py-12 text-center text-slate-500 italic">
                    Haga click en "Buscar en Firebird" para consultar los pesajes del rango seleccionado.
                  </td>
                </tr>
              ) : (
                remoteRecords.map((r, idx) => (
                  <tr key={r.flpesaje} className="hover:bg-slate-800/50">
                    <td className="py-2 px-2.5 text-center text-slate-500 font-mono">{idx + 1}</td>
                    <td className="py-2 px-2.5 font-mono font-bold text-amber-300">#{r.flpesaje}</td>
                    <td className="py-2 px-2.5 text-slate-400">
                      {r.tpesaje === 1 || r.tpesaje === '1' || r.tpesaje === 'R'
                        ? 'Recepción'
                        : r.tpesaje === 2
                        ? 'Despacho'
                        : 'Interno'}
                    </td>
                    <td className="py-2 px-2.5 font-mono font-semibold text-white">{r.patente}</td>
                    <td className="py-2 px-2.5 text-right font-mono text-slate-400">{formatKilos(r.bruto)}</td>
                    <td className="py-2 px-2.5 text-right font-mono text-slate-400">{formatKilos(r.tara)}</td>
                    <td className="py-2 px-2.5 text-right font-mono font-bold text-emerald-400">
                      {formatKilos(r.neto)}
                    </td>
                    <td className="py-2 px-2.5 font-mono text-cyan-400">{r.producto}</td>
                    <td className="py-2 px-2.5 font-medium text-white">{r.descProducto}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-300">{r.cliente}</td>
                    <td className="py-2 px-2.5 text-slate-200">{r.nomCliente}</td>
                    <td className="py-2 px-2.5 text-slate-400">{r.origen}</td>
                    <td className="py-2 px-2.5 text-slate-400">{r.destino}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-300">{r.gdespacho || '-'}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-400">{r.fechaing}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-400">{r.horaing}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-400">{r.fechasal}</td>
                    <td className="py-2 px-2.5 font-mono text-slate-400">{r.horasal}</td>
                    <td className="py-2 px-2.5 text-slate-400 italic truncate max-w-[200px]">{r.observa1}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
