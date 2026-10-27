import React, { useState, useMemo } from 'react';
import { Calendar, Search, FileSpreadsheet, Layers } from 'lucide-react';
import { dbService } from '../../services/db';
import { formatKilos, isDateInRange } from '../../utils/pesajeRules';
import { exportarExcelGruposMaterial } from '../../services/excelExport';

export const Tab0GrupoMaterial: React.FC = () => {
  const [fechaDesde, setFechaDesde] = useState('2026-02-01');
  const [fechaHasta, setFechaHasta] = useState('2026-02-28');
  const [modoSemanal, setModoSemanal] = useState(true);

  // Loaded pesajes
  const pesajes = useMemo(() => dbService.getPesajes(), []);

  // Filter pesajes by date
  const pesajesFiltrados = useMemo(() => {
    return pesajes.filter((p) => isDateInRange(p.fechaIngreso, fechaDesde, fechaHasta));
  }, [pesajes, fechaDesde, fechaHasta]);

  // Aggregate by Planta & Grupo
  const gruposPlacilla = useMemo(() => {
    const map = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'PLACILLA')
      .forEach((p) => {
        map.set(p.grupo, (map.get(p.grupo) || 0) + p.pesoNeto);
      });
    return Array.from(map.entries()).map(([grupo, kilos]) => ({ grupo, kilos }));
  }, [pesajesFiltrados]);

  const gruposSantiago = useMemo(() => {
    const map = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'SANTIAGO')
      .forEach((p) => {
        map.set(p.grupo, (map.get(p.grupo) || 0) + p.pesoNeto);
      });
    return Array.from(map.entries()).map(([grupo, kilos]) => ({ grupo, kilos }));
  }, [pesajesFiltrados]);

  const gruposTalcahuano = useMemo(() => {
    const map = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'TALCAHUANO')
      .forEach((p) => {
        map.set(p.grupo, (map.get(p.grupo) || 0) + p.pesoNeto);
      });
    return Array.from(map.entries()).map(([grupo, kilos]) => ({ grupo, kilos }));
  }, [pesajesFiltrados]);

  const totalPlacilla = useMemo(() => gruposPlacilla.reduce((acc, g) => acc + g.kilos, 0), [gruposPlacilla]);
  const totalSantiago = useMemo(() => gruposSantiago.reduce((acc, g) => acc + g.kilos, 0), [gruposSantiago]);
  const totalTalcahuano = useMemo(() => gruposTalcahuano.reduce((acc, g) => acc + g.kilos, 0), [gruposTalcahuano]);
  const granTotal = totalPlacilla + totalSantiago + totalTalcahuano;

  const handleExportarExcel = () => {
    exportarExcelGruposMaterial(
      fechaDesde,
      fechaHasta,
      gruposPlacilla,
      gruposSantiago,
      gruposTalcahuano
    );
  };

  return (
    <div className="space-y-6">
      {/* Top Filter Bar */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Desde:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaDesde}
                onChange={(e) => setFechaDesde(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Hasta:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaHasta}
                onChange={(e) => setFechaHasta(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>

          <div className="flex items-center gap-4 border-l border-slate-800 pl-4">
            <label className="flex items-center gap-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="radio"
                name="tipoCalculo"
                checked={modoSemanal}
                onChange={() => setModoSemanal(true)}
                className="accent-cyan-500 w-4 h-4"
              />
              <span className="font-medium">Consolidado Semanal</span>
            </label>
            <label className="flex items-center gap-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="radio"
                name="tipoCalculo"
                checked={!modoSemanal}
                onChange={() => setModoSemanal(false)}
                className="accent-cyan-500 w-4 h-4"
              />
              <span className="font-medium">Normal Diario</span>
            </label>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={handleExportarExcel}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs rounded-lg flex items-center gap-2 transition-all shadow-md shadow-emerald-600/20 cursor-pointer"
          >
            <FileSpreadsheet className="w-4 h-4" />
            <span>Exportar Excel (.xlsx)</span>
          </button>
        </div>
      </div>

      {/* 3 Columns Grids for Placilla, Santiago, Talcahuano */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Placilla */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col">
          <div className="bg-cyan-950/60 border-b border-cyan-800/60 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-cyan-400" />
              <h3 className="font-semibold text-sm text-cyan-200">Planta Placilla</h3>
            </div>
            <span className="text-xs font-mono bg-cyan-900/60 text-cyan-300 px-2 py-0.5 rounded border border-cyan-700/50">
              VISTA_PSJES_GRUPO_PLACILLA
            </span>
          </div>

          <div className="p-3 flex-1 overflow-y-auto max-h-96">
            <table className="w-full text-xs text-left">
              <thead className="bg-slate-800/70 text-slate-400 uppercase font-mono">
                <tr>
                  <th className="py-2 px-3 w-10 text-center">N°</th>
                  <th className="py-2 px-3">Grupo de Material</th>
                  <th className="py-2 px-3 text-right">Total Kilos</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-300">
                {gruposPlacilla.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="py-6 text-center text-slate-500 italic">
                      Sin pesajes en el periodo
                    </td>
                  </tr>
                ) : (
                  gruposPlacilla.map((g, idx) => (
                    <tr key={g.grupo} className="hover:bg-slate-800/40">
                      <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                      <td className="py-2 px-3 font-semibold text-white">{g.grupo}</td>
                      <td className="py-2 px-3 text-right font-mono font-medium text-cyan-300">
                        {formatKilos(g.kilos)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-semibold">
            <span className="text-slate-400 uppercase">Total Placilla (txt_total_plc):</span>
            <span className="font-mono text-sm text-cyan-400">{formatKilos(totalPlacilla)}</span>
          </div>
        </div>

        {/* Santiago */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col">
          <div className="bg-blue-950/60 border-b border-blue-800/60 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-blue-400" />
              <h3 className="font-semibold text-sm text-blue-200">Planta Santiago</h3>
            </div>
            <span className="text-xs font-mono bg-blue-900/60 text-blue-300 px-2 py-0.5 rounded border border-blue-700/50">
              PSJES_POR_MATERIAL_STGO
            </span>
          </div>

          <div className="p-3 flex-1 overflow-y-auto max-h-96">
            <table className="w-full text-xs text-left">
              <thead className="bg-slate-800/70 text-slate-400 uppercase font-mono">
                <tr>
                  <th className="py-2 px-3 w-10 text-center">N°</th>
                  <th className="py-2 px-3">Grupo de Material</th>
                  <th className="py-2 px-3 text-right">Total Kilos</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-300">
                {gruposSantiago.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="py-6 text-center text-slate-500 italic">
                      Sin pesajes en el periodo
                    </td>
                  </tr>
                ) : (
                  gruposSantiago.map((g, idx) => (
                    <tr key={g.grupo} className="hover:bg-slate-800/40">
                      <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                      <td className="py-2 px-3 font-semibold text-white">{g.grupo}</td>
                      <td className="py-2 px-3 text-right font-mono font-medium text-blue-300">
                        {formatKilos(g.kilos)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-semibold">
            <span className="text-slate-400 uppercase">Total Santiago (txt_total_stgo):</span>
            <span className="font-mono text-sm text-blue-400">{formatKilos(totalSantiago)}</span>
          </div>
        </div>

        {/* Talcahuano */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl flex flex-col">
          <div className="bg-emerald-950/60 border-b border-emerald-800/60 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-emerald-400" />
              <h3 className="font-semibold text-sm text-emerald-200">Planta Talcahuano</h3>
            </div>
            <span className="text-xs font-mono bg-emerald-900/60 text-emerald-300 px-2 py-0.5 rounded border border-emerald-700/50">
              VISTA_PSJES_GRUPO_THNO_2
            </span>
          </div>

          <div className="p-3 flex-1 overflow-y-auto max-h-96">
            <table className="w-full text-xs text-left">
              <thead className="bg-slate-800/70 text-slate-400 uppercase font-mono">
                <tr>
                  <th className="py-2 px-3 w-10 text-center">N°</th>
                  <th className="py-2 px-3">Grupo de Material</th>
                  <th className="py-2 px-3 text-right">Total Kilos</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-300">
                {gruposTalcahuano.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="py-6 text-center text-slate-500 italic">
                      Sin pesajes en el periodo
                    </td>
                  </tr>
                ) : (
                  gruposTalcahuano.map((g, idx) => (
                    <tr key={g.grupo} className="hover:bg-slate-800/40">
                      <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                      <td className="py-2 px-3 font-semibold text-white">{g.grupo}</td>
                      <td className="py-2 px-3 text-right font-mono font-medium text-emerald-300">
                        {formatKilos(g.kilos)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-semibold">
            <span className="text-slate-400 uppercase">Total Talcahuano (txt_total_thno):</span>
            <span className="font-mono text-sm text-emerald-400">{formatKilos(totalTalcahuano)}</span>
          </div>
        </div>
      </div>

      {/* Global Summary Footnote */}
      <div className="bg-linear-to-r from-slate-900 via-slate-850 to-slate-900 border border-slate-800 p-4 rounded-xl flex items-center justify-between">
        <div>
          <span className="text-xs uppercase tracking-wider text-slate-400 font-semibold">
            Consolidado Nacional P. Cambiaso
          </span>
          <p className="text-sm text-slate-200">
            Suma de pesajes netos recepcionados en todas las básculas operativas
          </p>
        </div>
        <div className="text-right">
          <span className="text-xs uppercase tracking-wider text-slate-400 font-semibold block">
            Gran Total General Kilos
          </span>
          <span className="text-2xl font-black font-mono text-amber-400">
            {formatKilos(granTotal)}
          </span>
        </div>
      </div>
    </div>
  );
};
