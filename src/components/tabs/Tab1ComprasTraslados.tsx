import React, { useState, useMemo } from 'react';
import { Calendar, FileSpreadsheet, ArrowRightLeft, ShoppingCart, Truck } from 'lucide-react';
import { Planta } from '../../types/pesajes';
import { dbService } from '../../services/db';
import { formatKilos, isDateInRange } from '../../utils/pesajeRules';
import { exportarExcelComprasTraslados } from '../../services/excelExport';

export const Tab1ComprasTraslados: React.FC = () => {
  const [planta, setPlanta] = useState<Planta>('PLACILLA');
  const [fechaInicio, setFechaInicio] = useState('2026-02-01');
  const [fechaTermino, setFechaTermino] = useState('2026-02-28');

  const pesajes = useMemo(() => dbService.getPesajes(), []);

  // Filtered by date and plant
  const pesajesPlanta = useMemo(() => {
    return pesajes.filter(
      (p) => p.planta === planta && isDateInRange(p.fechaIngreso, fechaInicio, fechaTermino)
    );
  }, [pesajes, planta, fechaInicio, fechaTermino]);

  // Split into Compras (aggregated by producto and client) and Traslados
  const comprasDetalle = useMemo(() => {
    const map = new Map<string, { codigo: string; descripcion: string; razonSocial: string; kilos: number }>();

    pesajesPlanta
      .filter((p) => !p.esTraslado && p.tipoPesaje === 'Recepcion de carga')
      .forEach((p) => {
        const key = `${p.codProducto}_${p.rutCliente}`;
        const prev = map.get(key);
        if (prev) {
          prev.kilos += p.pesoNeto;
        } else {
          map.set(key, {
            codigo: p.codProducto,
            descripcion: p.nomProducto,
            razonSocial: p.nomCliente,
            kilos: p.pesoNeto,
          });
        }
      });

    return Array.from(map.values());
  }, [pesajesPlanta]);

  const trasladosDetalle = useMemo(() => {
    return pesajesPlanta
      .filter((p) => p.esTraslado || p.tipoPesaje === 'Movimiento Interno' || p.tipoPesaje === 'Despacho de Carga')
      .map((p) => ({
        folio: p.folio,
        patente: p.patente,
        producto: p.nomProducto,
        fechaEntrada: p.fechaIngreso,
        fechaSalida: p.fechaSalida,
        pesoEnt: p.pesoBruto,
        pesoSal: p.pesoTara,
        neto: p.pesoNeto,
      }));
  }, [pesajesPlanta]);

  const totalComprasKilos = useMemo(
    () => comprasDetalle.reduce((acc, c) => acc + c.kilos, 0),
    [comprasDetalle]
  );

  const totalTrasladosKilos = useMemo(
    () => trasladosDetalle.reduce((acc, t) => acc + t.neto, 0),
    [trasladosDetalle]
  );

  const totalGeneralKilos = totalComprasKilos + totalTrasladosKilos;

  // Labels matching VBA code
  const labelCompras =
    planta === 'PLACILLA'
      ? 'DETALLE COMPRAS PLACILLA'
      : planta === 'SANTIAGO'
      ? 'DETALLE COMPRAS SANTIAGO'
      : 'DETALLES COMPRAS TALCAHUANO';

  const labelTraslados =
    planta === 'PLACILLA'
      ? 'TRASLADOS BODEGA SANTIAGO (RECEPCIÓN)'
      : planta === 'SANTIAGO'
      ? 'TRASLADOS SCRAP SANTIAGO (HACIA PLACILLA)'
      : 'TRASLADOS A SANTIAGO';

  const handleExportarExcel = () => {
    exportarExcelComprasTraslados(
      planta,
      fechaInicio,
      fechaTermino,
      comprasDetalle,
      trasladosDetalle,
      totalComprasKilos,
      totalTrasladosKilos
    );
  };

  return (
    <div className="space-y-6">
      {/* Filter Control Header */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-5">
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
            <button
              type="button"
              onClick={() => setPlanta('TALCAHUANO')}
              className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                planta === 'TALCAHUANO'
                  ? 'bg-cyan-500 text-slate-950 shadow-sm'
                  : 'text-slate-300 hover:text-white'
              }`}
            >
              Talcahuano
            </button>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Inicio:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaInicio}
                onChange={(e) => setFechaInicio(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Término:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaTermino}
                onChange={(e) => setFechaTermino(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={handleExportarExcel}
          className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs rounded-lg flex items-center gap-2 shadow-md shadow-emerald-600/20 transition-all cursor-pointer"
        >
          <FileSpreadsheet className="w-4 h-4" />
          <span>Exportar Informe {planta} (.xlsx)</span>
        </button>
      </div>

      {/* Resumen KPI Boxes (grilla_inf_res) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-slate-900 border border-slate-800 p-4 rounded-xl flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-cyan-950 rounded-lg border border-cyan-800/60 text-cyan-400">
              <ShoppingCart className="w-5 h-5" />
            </div>
            <div>
              <span className="text-[11px] font-semibold uppercase text-slate-400 block font-mono">
                COMPRAS {planta} (grilla_inf_res: Fila 1)
              </span>
              <span className="text-lg font-black font-mono text-white">
                {formatKilos(totalComprasKilos)}
              </span>
            </div>
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 p-4 rounded-xl flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-amber-950 rounded-lg border border-amber-800/60 text-amber-400">
              <Truck className="w-5 h-5" />
            </div>
            <div>
              <span className="text-[11px] font-semibold uppercase text-slate-400 block font-mono">
                TRASLADOS {planta} (grilla_inf_res: Fila 2)
              </span>
              <span className="text-lg font-black font-mono text-amber-300">
                {formatKilos(totalTrasladosKilos)}
              </span>
            </div>
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 p-4 rounded-xl flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-emerald-950 rounded-lg border border-emerald-800/60 text-emerald-400">
              <ArrowRightLeft className="w-5 h-5" />
            </div>
            <div>
              <span className="text-[11px] font-semibold uppercase text-slate-400 block font-mono">
                TOTAL GENERAL (grilla_inf_res: Fila 3)
              </span>
              <span className="text-lg font-black font-mono text-emerald-300">
                {formatKilos(totalGeneralKilos)}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Grid 1: Compras (grilla_inf_compras) */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <ShoppingCart className="w-4 h-4 text-cyan-400" />
            <h3 className="font-bold text-sm text-white uppercase tracking-wider">{labelCompras}</h3>
          </div>
          <span className="text-xs text-slate-400 font-mono">
            {comprasDetalle.length} registros consolidados
          </span>
        </div>

        <div className="overflow-x-auto max-h-80">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">N°</th>
                <th className="py-2.5 px-3 w-28">Código</th>
                <th className="py-2.5 px-3">Descripción Producto</th>
                <th className="py-2.5 px-3">Razón Social / Proveedor</th>
                <th className="py-2.5 px-3 text-right w-36">Total Kilos</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {comprasDetalle.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-500 italic">
                    No se registran compras para {planta} en el rango seleccionado.
                  </td>
                </tr>
              ) : (
                comprasDetalle.map((c, idx) => (
                  <tr key={idx} className="hover:bg-slate-800/50">
                    <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                    <td className="py-2 px-3 font-mono font-bold text-cyan-400">{c.codigo}</td>
                    <td className="py-2 px-3 font-medium text-white">{c.descripcion}</td>
                    <td className="py-2 px-3 text-slate-300">{c.razonSocial}</td>
                    <td className="py-2 px-3 text-right font-mono font-semibold text-white">
                      {formatKilos(c.kilos)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Grid 2: Traslados (grilla_inf_tras) */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Truck className="w-4 h-4 text-amber-400" />
            <h3 className="font-bold text-sm text-white uppercase tracking-wider">{labelTraslados}</h3>
          </div>
          <span className="text-xs text-slate-400 font-mono">
            {trasladosDetalle.length} viajes registrados
          </span>
        </div>

        <div className="overflow-x-auto max-h-80">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">N°</th>
                <th className="py-2.5 px-3 w-20 font-mono">Folio</th>
                <th className="py-2.5 px-3 w-24">Patente</th>
                <th className="py-2.5 px-3">Producto Trasladado</th>
                <th className="py-2.5 px-3 w-28">Fecha Ent.</th>
                <th className="py-2.5 px-3 w-28">Fecha Sal.</th>
                <th className="py-2.5 px-3 text-right w-28">Peso Bruto</th>
                <th className="py-2.5 px-3 text-right w-28">Peso Tara</th>
                <th className="py-2.5 px-3 text-right w-32 font-bold">Total Kilos</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {trasladosDetalle.length === 0 ? (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-slate-500 italic">
                    No se registran traslados para {planta} en el rango seleccionado.
                  </td>
                </tr>
              ) : (
                trasladosDetalle.map((t, idx) => (
                  <tr key={t.folio} className="hover:bg-slate-800/50">
                    <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                    <td className="py-2 px-3 font-mono font-bold text-amber-400">#{t.folio}</td>
                    <td className="py-2 px-3 font-mono font-semibold text-white">{t.patente}</td>
                    <td className="py-2 px-3 text-slate-200">{t.producto}</td>
                    <td className="py-2 px-3 text-slate-400 font-mono">{t.fechaEntrada}</td>
                    <td className="py-2 px-3 text-slate-400 font-mono">{t.fechaSalida}</td>
                    <td className="py-2 px-3 text-right font-mono text-slate-400">{formatKilos(t.pesoEnt)}</td>
                    <td className="py-2 px-3 text-right font-mono text-slate-400">{formatKilos(t.pesoSal)}</td>
                    <td className="py-2 px-3 text-right font-mono font-bold text-amber-300">
                      {formatKilos(t.neto)}
                    </td>
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
