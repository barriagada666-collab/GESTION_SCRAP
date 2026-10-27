import React, { useState, useMemo } from 'react';
import { Calendar, DollarSign, FileSpreadsheet, Lock, Unlock, Save, Edit3, AlertTriangle, Layers } from 'lucide-react';
import { Planta } from '../../types/pesajes';
import { dbService } from '../../services/db';
import {
  calculateWeightedAverage,
  findRealPrice,
  findTheoreticalPrice,
  formatCLP,
  formatKilos,
  isDateInRange,
} from '../../utils/pesajeRules';
import { exportarExcelPrecios } from '../../services/excelExport';
import { ModalActPrecios } from '../modals/ModalActPrecios';

interface DetallePrecioRow {
  folio: number;
  codigoProd: string;
  descripcion: string;
  rutCliente: string;
  proveedor: string;
  kilos: number;
  precio: number;
  total: number;
  familia: string;
}

export const Tab5InformePrecios: React.FC = () => {
  const [planta, setPlanta] = useState<'PLACILLA' | 'SANTIAGO'>('PLACILLA');
  const [modoPrecios, setModoPrecios] = useState<'TEORICOS' | 'REALES'>('TEORICOS');
  const [filtroTipo, setFiltroTipo] = useState<'Todos' | 'Resumen'>('Todos');
  const [fechaDesde, setFechaDesde] = useState('2026-02-01');
  const [fechaHasta, setFechaHasta] = useState('2026-02-28');
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Selected row for Act_precios modal
  const [modalEditState, setModalEditState] = useState<{
    isOpen: boolean;
    row: DetallePrecioRow | null;
  }>({
    isOpen: false,
    row: null,
  });

  const [notification, setNotification] = useState<{ type: 'success' | 'warn'; msg: string } | null>(null);

  const pesajes = useMemo(() => dbService.getPesajes(), [refreshTrigger]);
  const preciosProveedor = useMemo(() => dbService.getPreciosProveedor(), [refreshTrigger]);
  const indexPrecios = useMemo(() => dbService.getIndexPrecios(), [refreshTrigger]);

  // Compute detailed priced rows
  const detalleRows: DetallePrecioRow[] = useMemo(() => {
    return pesajes
      .filter((p) => p.planta === planta && isDateInRange(p.fechaIngreso, fechaDesde, fechaHasta))
      .map((p) => {
        let precio = 0;
        if (modoPrecios === 'TEORICOS') {
          precio = findTheoreticalPrice(preciosProveedor, planta, p.rutCliente, p.codProducto);
        } else {
          // Reales: query index_pesaje_placilla / stgo
          precio = findRealPrice(indexPrecios, planta, p.folio);
          // If no real price yet, default to 0 as in VBA
        }

        const total = p.pesoNeto * precio;
        return {
          folio: p.folio,
          codigoProd: p.codProducto,
          descripcion: p.nomProducto,
          rutCliente: p.rutCliente,
          proveedor: p.nomCliente,
          kilos: p.pesoNeto,
          precio,
          total,
          familia: p.grupo,
        };
      });
  }, [pesajes, preciosProveedor, indexPrecios, planta, fechaDesde, fechaHasta, modoPrecios]);

  // Resumen grouped for "SUPER-BASULIP", "RECAM", "ALTA"
  const resumenFamilias = useMemo(() => {
    const familias = ['SUPER-BASULIP', 'RECAM', 'ALTA'];

    return familias.map((fam) => {
      const rows = detalleRows.filter((r) => r.familia.toUpperCase().includes(fam.toUpperCase()));
      const kilos = rows.reduce((acc, r) => acc + r.kilos, 0);
      const totalPesos = rows.reduce((acc, r) => acc + r.total, 0);
      const precioPromedio = calculateWeightedAverage(totalPesos, kilos);

      return {
        grupo: fam === 'SUPER-BASULIP' ? 'SUPER BASULIP' : fam,
        kilos,
        precioPromedio,
        totalPesos,
      };
    });
  }, [detalleRows]);

  // Handle double click on row (UserForm Act_precios)
  const handleRowDoubleClick = (row: DetallePrecioRow) => {
    if (modoPrecios !== 'REALES') {
      setNotification({
        type: 'warn',
        msg: 'Para actualizar el precio debe buscar por opción "Precios Reales" y buscar nuevamente.',
      });
      setTimeout(() => setNotification(null), 5000);
      return;
    }

    setModalEditState({
      isOpen: true,
      row,
    });
  };

  // Button "Guardar Precios Teóricos"
  const handleGuardarPreciosTeoricos = () => {
    if (modoPrecios !== 'TEORICOS') {
      setNotification({
        type: 'warn',
        msg: 'Para Guardar precios teóricos seleccione opción: "Informe con Precios Teóricos".',
      });
      setTimeout(() => setNotification(null), 5000);
      return;
    }

    const itemsToFreeze = detalleRows.map((r) => ({
      folio: r.folio,
      precio: r.precio,
      familia: r.familia,
    }));

    const count = dbService.bulkFreezeTheoreticalPrices(itemsToFreeze, planta);
    setRefreshTrigger((prev) => prev + 1);

    setNotification({
      type: 'success',
      msg: `Se congelaron e ingresaron ${count} precios teóricos a la tabla de precios reales indexados en ${planta}.`,
    });
    setTimeout(() => setNotification(null), 5000);
  };

  const handleExportarExcel = () => {
    exportarExcelPrecios(
      planta,
      fechaDesde,
      fechaHasta,
      modoPrecios,
      detalleRows,
      resumenFamilias
    );
  };

  return (
    <div className="space-y-6">
      {/* Top Controls */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-1.5 bg-slate-800 p-1 rounded-lg border border-slate-700">
              <button
                type="button"
                onClick={() => setPlanta('PLACILLA')}
                className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                  planta === 'PLACILLA'
                    ? 'bg-cyan-500 text-slate-950 shadow-sm'
                    : 'text-slate-300 hover:text-white'
                }`}
              >
                Placilla
              </button>
              <button
                type="button"
                onClick={() => setPlanta('SANTIAGO')}
                className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                  planta === 'SANTIAGO'
                    ? 'bg-cyan-500 text-slate-950 shadow-sm'
                    : 'text-slate-300 hover:text-white'
                }`}
              >
                Santiago
              </button>
            </div>

            <div className="flex items-center gap-3 bg-slate-800/80 p-1 rounded-lg border border-slate-700">
              <button
                type="button"
                onClick={() => setModoPrecios('TEORICOS')}
                className={`px-3 py-1.5 rounded-md text-xs font-bold transition-all flex items-center gap-1.5 ${
                  modoPrecios === 'TEORICOS'
                    ? 'bg-amber-500 text-slate-950 shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <Unlock className="w-3.5 h-3.5" />
                <span>Precios Teóricos (Pactados)</span>
              </button>
              <button
                type="button"
                onClick={() => setModoPrecios('REALES')}
                className={`px-3 py-1.5 rounded-md text-xs font-bold transition-all flex items-center gap-1.5 ${
                  modoPrecios === 'REALES'
                    ? 'bg-emerald-500 text-slate-950 shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <Lock className="w-3.5 h-3.5" />
                <span>Precios Reales (Indexados)</span>
              </button>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                Filtro Vista:
              </span>
              <select
                value={filtroTipo}
                onChange={(e) => setFiltroTipo(e.target.value as 'Todos' | 'Resumen')}
                className="px-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-medium text-white focus:outline-none"
              >
                <option value="Todos">Todos (Detalle + Resumen)</option>
                <option value="Resumen">Resumen Exclusivo</option>
              </select>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleGuardarPreciosTeoricos}
              className="px-3.5 py-2 bg-amber-600 hover:bg-amber-500 text-slate-950 font-bold text-xs rounded-lg flex items-center gap-1.5 shadow-md shadow-amber-600/20 transition-all cursor-pointer"
              title="Copia los precios teóricos calculados a la tabla de precios reales indexados"
            >
              <Save className="w-4 h-4" />
              <span>Guardar Precios Teóricos a Reales</span>
            </button>
            <button
              type="button"
              onClick={handleExportarExcel}
              className="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs rounded-lg flex items-center gap-1.5 shadow-md shadow-emerald-600/20 transition-all cursor-pointer"
            >
              <FileSpreadsheet className="w-4 h-4" />
              <span>Exportar Excel Precios</span>
            </button>
          </div>
        </div>

        {/* Date Filter */}
        <div className="flex items-center gap-4 pt-2 border-t border-slate-800 text-xs">
          <div className="flex items-center gap-2">
            <span className="text-slate-400 font-semibold uppercase">Desde:</span>
            <input
              type="date"
              value={fechaDesde}
              onChange={(e) => setFechaDesde(e.target.value)}
              className="px-2.5 py-1 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono"
            />
          </div>
          <div className="flex items-center gap-2">
            <span className="text-slate-400 font-semibold uppercase">Hasta:</span>
            <input
              type="date"
              value={fechaHasta}
              onChange={(e) => setFechaHasta(e.target.value)}
              className="px-2.5 py-1 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono"
            />
          </div>
          <span className="text-slate-500 text-[11px] ml-auto">
            {modoPrecios === 'REALES'
              ? '💡 Modo Precios Reales: Doble click sobre un pesaje para editar su precio individual con Act_precios'
              : '⚠️ Modo Precios Teóricos: Los precios provienen del maestro de precios por proveedor y producto'}
          </span>
        </div>

        {notification && (
          <div
            className={`p-3 rounded-lg text-xs flex items-center gap-2 border ${
              notification.type === 'success'
                ? 'bg-emerald-950/60 border-emerald-600/60 text-emerald-200'
                : 'bg-amber-950/60 border-amber-600/60 text-amber-200'
            }`}
          >
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{notification.msg}</span>
          </div>
        )}
      </div>

      {/* Summary KPI Matrix by Material Family (grilla_res_mat_precio) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {resumenFamilias.map((rf) => (
          <div
            key={rf.grupo}
            className="bg-slate-900 border border-slate-800 p-4 rounded-xl shadow-lg relative overflow-hidden"
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-300">
                {rf.grupo}
              </span>
              <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-400">
                P. Promedio
              </span>
            </div>
            <div className="flex items-baseline justify-between">
              <div>
                <span className="text-[11px] text-slate-400 block font-mono">Total Kilos</span>
                <span className="text-lg font-black font-mono text-cyan-300">
                  {formatKilos(rf.kilos)}
                </span>
              </div>
              <div className="text-right">
                <span className="text-[11px] text-slate-400 block font-mono">Precio Prom. Ponderado</span>
                <span className="text-lg font-black font-mono text-amber-400">
                  {formatCLP(rf.precioPromedio)}/Kg
                </span>
              </div>
            </div>
            <div className="mt-2 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400 font-mono">
              <span>Valorización:</span>
              <span className="text-emerald-400 font-bold">{formatCLP(rf.totalPesos)}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Main Grid: Pesajes con Precios (grilla_psjes_precios) */}
      {filtroTipo === 'Todos' && (
        <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
          <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <DollarSign className="w-4 h-4 text-amber-400" />
              <h3 className="font-bold text-xs text-white uppercase tracking-wider">
                Detalle Pesajes Valorizados ({planta} - Modo {modoPrecios})
              </h3>
            </div>
            <span className="text-xs text-slate-400 font-mono">
              {detalleRows.length} pesajes valorizados
            </span>
          </div>

          <div className="overflow-x-auto max-h-96">
            <table className="w-full text-left text-xs text-slate-300">
              <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
                <tr>
                  <th className="py-2.5 px-3 w-10 text-center">N°</th>
                  <th className="py-2.5 px-3 w-20">Folio</th>
                  <th className="py-2.5 px-3 w-24">Código</th>
                  <th className="py-2.5 px-3">Descripción Producto</th>
                  <th className="py-2.5 px-3">Proveedor / Razón Social</th>
                  <th className="py-2.5 px-3 text-right w-24">Kilos</th>
                  <th className="py-2.5 px-3 text-right w-24 font-bold text-amber-400">Precio Unit.</th>
                  <th className="py-2.5 px-3 text-right w-28 font-bold text-emerald-400">Total ($)</th>
                  <th className="py-2.5 px-3 w-28">Familia</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {detalleRows.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="py-8 text-center text-slate-500 italic">
                      No hay pesajes registrados para los filtros seleccionados en {planta}.
                    </td>
                  </tr>
                ) : (
                  detalleRows.map((r, idx) => (
                    <tr
                      key={r.folio}
                      onDoubleClick={() => handleRowDoubleClick(r)}
                      className="hover:bg-slate-800/60 cursor-pointer group transition-colors"
                      title="Doble click para editar precio con Act_precios"
                    >
                      <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                      <td className="py-2 px-3 font-mono font-bold text-cyan-300 group-hover:text-cyan-200">
                        #{r.folio}
                      </td>
                      <td className="py-2 px-3 font-mono text-slate-400">{r.codigoProd}</td>
                      <td className="py-2 px-3 font-medium text-white">{r.descripcion}</td>
                      <td className="py-2 px-3 text-slate-300">{r.proveedor}</td>
                      <td className="py-2 px-3 text-right font-mono font-semibold text-white">
                        {formatKilos(r.kilos)}
                      </td>
                      <td className="py-2 px-3 text-right font-mono font-bold text-amber-300">
                        {formatCLP(r.precio)}
                      </td>
                      <td className="py-2 px-3 text-right font-mono font-bold text-emerald-400">
                        {formatCLP(r.total)}
                      </td>
                      <td className="py-2 px-3">
                        <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700">
                          {r.familia}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Modal Act_precios */}
      {modalEditState.row && (
        <ModalActPrecios
          isOpen={modalEditState.isOpen}
          onClose={() => setModalEditState({ isOpen: false, row: null })}
          folio={modalEditState.row.folio}
          codigoProd={modalEditState.row.codigoProd}
          descripcion={modalEditState.row.descripcion}
          proveedor={modalEditState.row.proveedor}
          precioActual={modalEditState.row.precio}
          planta={planta}
          isPreciosReales={modoPrecios === 'REALES'}
          familia={modalEditState.row.familia}
          onPriceUpdated={() => setRefreshTrigger((prev) => prev + 1)}
        />
      )}
    </div>
  );
};
