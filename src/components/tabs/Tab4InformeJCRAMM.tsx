import React, { useState, useMemo } from 'react';
import { FileSpreadsheet, RefreshCw, BookmarkCheck, Database, Filter } from 'lucide-react';
import { dbService } from '../../services/db';
import { formatKilos } from '../../utils/pesajeRules';
import { exportarExcelJCRAMM } from '../../services/excelExport';

export const Tab4InformeJCRAMM: React.FC = () => {
  const ultimoFolioGuardado = dbService.getUltimoFolioJCRAMM();
  const [lastFolioInput, setLastFolioInput] = useState<number>(ultimoFolioGuardado.folio);
  const [notification, setNotification] = useState<string | null>(null);

  const pesajes = useMemo(() => dbService.getPesajes(), []);

  // Filter pesajes with folio > lastFolio
  const detallePesajes = useMemo(() => {
    return pesajes
      .filter((p) => p.folio > lastFolioInput)
      .sort((a, b) => a.folio - b.folio)
      .map((p) => ({
        fecha: p.fechaIngreso,
        folio: p.folio,
        codProd: p.codProducto,
        descripcion: p.nomProducto,
        patente: p.patente,
        proveedor: p.nomCliente,
        ciudad: p.origen,
        kilos: p.pesoNeto,
      }));
  }, [pesajes, lastFolioInput]);

  // Resumen grouped by product description
  const resumenPorProducto = useMemo(() => {
    const map = new Map<string, number>();
    detallePesajes.forEach((d) => {
      map.set(d.descripcion, (map.get(d.descripcion) || 0) + d.kilos);
    });
    return Array.from(map.entries()).map(([producto, kilos]) => ({ producto, kilos }));
  }, [detallePesajes]);

  const totalKilosDetalle = useMemo(
    () => detallePesajes.reduce((acc, d) => acc + d.kilos, 0),
    [detallePesajes]
  );

  const totalKilosResumen = useMemo(
    () => resumenPorProducto.reduce((acc, r) => acc + r.kilos, 0),
    [resumenPorProducto]
  );

  const handleExportarEImprimirJCRAMM = () => {
    if (detallePesajes.length === 0) return;

    // Export to Excel
    exportarExcelJCRAMM(
      detallePesajes,
      resumenPorProducto,
      totalKilosDetalle,
      totalKilosResumen
    );

    // Save last processed folio matching VBA:
    // del_last_folio; ing_last_folio .TextMatrix(.Rows - 1, 2), .TextMatrix(.Rows - 1, 1)
    const ultimoFolioProcesado = detallePesajes[detallePesajes.length - 1].folio;
    const fechaProcesada = detallePesajes[detallePesajes.length - 1].fecha;

    dbService.setUltimoFolioJCRAMM(ultimoFolioProcesado, fechaProcesada, 'OPERADOR_JCRAMM');
    setLastFolioInput(ultimoFolioProcesado);

    setNotification(
      `Informe exportado y último folio guardado exitosamente: #${ultimoFolioProcesado} (${fechaProcesada})`
    );
    setTimeout(() => setNotification(null), 5000);
  };

  return (
    <div className="space-y-6">
      {/* Header and Controls */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                Último Folio Registrado (tabla ultimo_folio):
              </span>
              <div className="flex items-center gap-2">
                <input
                  type="number"
                  value={lastFolioInput}
                  onChange={(e) => setLastFolioInput(Number(e.target.value))}
                  className="w-32 px-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono font-bold text-amber-400 focus:outline-none focus:border-amber-400"
                />
                <button
                  type="button"
                  onClick={() => setLastFolioInput(dbService.getUltimoFolioJCRAMM().folio)}
                  className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs"
                  title="Restablecer último folio guardado en BD"
                >
                  <RefreshCw className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            <span className="text-xs text-slate-500 font-mono">
              Filtro SQL: <code>pes_num &gt; {lastFolioInput}</code>
            </span>
          </div>

          <button
            type="button"
            onClick={handleExportarEImprimirJCRAMM}
            disabled={detallePesajes.length === 0}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 text-white font-semibold text-xs rounded-lg flex items-center gap-2 shadow-md shadow-emerald-600/20 transition-all cursor-pointer"
          >
            <FileSpreadsheet className="w-4 h-4" />
            <span>Imprimir Informe JCRAMM y Guardar Folio (.xlsx)</span>
          </button>
        </div>

        {notification && (
          <div className="p-3 bg-emerald-950/60 border border-emerald-600/60 rounded-lg text-emerald-200 text-xs flex items-center gap-2">
            <BookmarkCheck className="w-4 h-4 text-emerald-400 shrink-0" />
            <span>{notification}</span>
          </div>
        )}
      </div>

      {/* 2 Grids: Resumen por Producto & Detalle de Pesajes */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Resumen por Material (grilla_res_psjes) */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col">
          <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
            <h3 className="font-bold text-xs text-white uppercase tracking-wider">
              Resumen por Material (grilla_res_psjes)
            </h3>
            <span className="text-xs text-slate-400 font-mono">vista_tmp_res_jcramm</span>
          </div>

          <div className="p-3 flex-1 overflow-y-auto max-h-96">
            <table className="w-full text-xs text-left">
              <thead className="bg-slate-850 text-slate-400 uppercase font-mono">
                <tr>
                  <th className="py-2 px-2.5">Producto</th>
                  <th className="py-2 px-2.5 text-right">Kilos</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-300">
                {resumenPorProducto.length === 0 ? (
                  <tr>
                    <td colSpan={2} className="py-8 text-center text-slate-500 italic">
                      No hay pesajes posteriores al folio #{lastFolioInput}.
                    </td>
                  </tr>
                ) : (
                  resumenPorProducto.map((r, idx) => (
                    <tr key={idx} className="hover:bg-slate-800/40">
                      <td className="py-2 px-2.5 font-medium text-white">{r.producto}</td>
                      <td className="py-2 px-2.5 text-right font-mono font-semibold text-cyan-300">
                        {formatKilos(r.kilos)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-bold">
            <span className="text-slate-400 uppercase">Total Kilos Resumen:</span>
            <span className="font-mono text-cyan-400">{formatKilos(totalKilosResumen)}</span>
          </div>
        </div>

        {/* Right: Detalle Pesajes (grilla_det_psjes) */}
        <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col">
          <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
            <h3 className="font-bold text-xs text-white uppercase tracking-wider">
              Detalle de Pesajes (grilla_det_psjes - pes_num &gt; {lastFolioInput})
            </h3>
            <span className="text-xs text-slate-400 font-mono">
              {detallePesajes.length} registros pendientes
            </span>
          </div>

          <div className="overflow-x-auto max-h-96 flex-1">
            <table className="w-full text-left text-xs text-slate-300">
              <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
                <tr>
                  <th className="py-2 px-2.5 w-10 text-center">N°</th>
                  <th className="py-2 px-2.5 w-24">Fecha</th>
                  <th className="py-2 px-2.5 w-20">Folio</th>
                  <th className="py-2 px-2.5">Producto</th>
                  <th className="py-2 px-2.5 w-20">Patente</th>
                  <th className="py-2 px-2.5">Proveedor</th>
                  <th className="py-2 px-2.5 w-24">Ciudad</th>
                  <th className="py-2 px-2.5 text-right w-24 font-bold">Kilos</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {detallePesajes.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="py-8 text-center text-slate-500 italic">
                      Todos los pesajes hasta el folio #{lastFolioInput} ya fueron procesados.
                    </td>
                  </tr>
                ) : (
                  detallePesajes.map((d, idx) => (
                    <tr key={d.folio} className="hover:bg-slate-800/40">
                      <td className="py-2 px-2.5 text-center text-slate-500 font-mono">{idx + 1}</td>
                      <td className="py-2 px-2.5 text-slate-400 font-mono">{d.fecha}</td>
                      <td className="py-2 px-2.5 font-mono font-bold text-amber-400">#{d.folio}</td>
                      <td className="py-2 px-2.5 font-medium text-white">{d.descripcion}</td>
                      <td className="py-2 px-2.5 font-mono text-slate-300">{d.patente}</td>
                      <td className="py-2 px-2.5 text-slate-300">{d.proveedor}</td>
                      <td className="py-2 px-2.5 text-slate-400">{d.ciudad}</td>
                      <td className="py-2 px-2.5 text-right font-mono font-bold text-emerald-400">
                        {formatKilos(d.kilos)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-bold">
            <span className="text-slate-400 uppercase">Total Kilos Detalle (txt_total_kilos_det):</span>
            <span className="font-mono text-emerald-400">{formatKilos(totalKilosDetalle)}</span>
          </div>
        </div>
      </div>
    </div>
  );
};
