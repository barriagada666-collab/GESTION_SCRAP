import React, { useState, useMemo } from 'react';
import { Calendar, Search, FileSpreadsheet, Building2, User, Ticket, ExternalLink } from 'lucide-react';
import { Planta, Cliente } from '../../types/pesajes';
import { dbService } from '../../services/db';
import { formatKilos, isDateInRange } from '../../utils/pesajeRules';
import { exportarExcelCliente } from '../../services/excelExport';
import { ModalClientes } from '../modals/ModalClientes';

export const Tab2InformeCliente: React.FC = () => {
  const [planta, setPlanta] = useState<Planta>('PLACILLA');
  const [tipoBusqueda, setTipoBusqueda] = useState<'RUT' | 'FOLIO'>('RUT');
  const [rutCliente, setRutCliente] = useState('76218764');
  const [folioInput, setFolioInput] = useState('10420');
  const [nombreCliente, setNombreCliente] = useState('Sociedad Agricola Galea Ltda.');
  const [fechaInicio, setFechaInicio] = useState('2026-02-01');
  const [fechaTermino, setFechaTermino] = useState('2026-02-28');
  const [isModalClientesOpen, setIsModalClientesOpen] = useState(false);

  const pesajes = useMemo(() => dbService.getPesajes(), []);

  // Filter pesajes by Client/Folio and Dates
  const pesajesCliente = useMemo(() => {
    return pesajes.filter((p) => {
      const matchPlanta = p.planta === planta;
      const matchFecha = isDateInRange(p.fechaIngreso, fechaInicio, fechaTermino);
      const isRecepcion = p.tipoPesaje === 'Recepcion de carga';

      if (!matchPlanta || !matchFecha || !isRecepcion) return false;

      if (tipoBusqueda === 'RUT') {
        const cleanP = p.rutCliente.trim().replace(/[^0-9K]/g, '');
        const cleanInput = rutCliente.trim().replace(/[^0-9K]/g, '');
        return cleanP === cleanInput;
      } else {
        return p.folio === Number(folioInput);
      }
    });
  }, [pesajes, planta, fechaInicio, fechaTermino, tipoBusqueda, rutCliente, folioInput]);

  const totalKilos = useMemo(
    () => pesajesCliente.reduce((acc, p) => acc + p.pesoNeto, 0),
    [pesajesCliente]
  );

  const handleSelectCliente = (c: Cliente) => {
    setRutCliente(c.rut);
    setNombreCliente(c.razonSocial);
  };

  const handleExportarExcel = () => {
    exportarExcelCliente(
      planta,
      rutCliente,
      nombreCliente,
      fechaInicio,
      fechaTermino,
      pesajesCliente.map((p) => ({
        folio: p.folio,
        patente: p.patente,
        producto: p.nomProducto,
        fechaIngreso: p.fechaIngreso,
        fechaSalida: p.fechaSalida,
        pesoBruto: p.pesoBruto,
        pesoTara: p.pesoTara,
        pesoNeto: p.pesoNeto,
      })),
      totalKilos
    );
  };

  return (
    <div className="space-y-6">
      {/* Search Header Form */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Planta:</span>
            <div className="flex items-center gap-1 bg-slate-800 p-1 rounded-lg border border-slate-700">
              <button
                type="button"
                onClick={() => setPlanta('PLACILLA')}
                className={`px-3 py-1 text-xs font-bold uppercase rounded-md transition-all ${
                  planta === 'PLACILLA' ? 'bg-cyan-500 text-slate-950' : 'text-slate-300 hover:text-white'
                }`}
              >
                Placilla
              </button>
              <button
                type="button"
                onClick={() => setPlanta('SANTIAGO')}
                className={`px-3 py-1 text-xs font-bold uppercase rounded-md transition-all ${
                  planta === 'SANTIAGO' ? 'bg-cyan-500 text-slate-950' : 'text-slate-300 hover:text-white'
                }`}
              >
                Santiago
              </button>
              <button
                type="button"
                onClick={() => setPlanta('TALCAHUANO')}
                className={`px-3 py-1 text-xs font-bold uppercase rounded-md transition-all ${
                  planta === 'TALCAHUANO' ? 'bg-cyan-500 text-slate-950' : 'text-slate-300 hover:text-white'
                }`}
              >
                Talcahuano
              </button>
            </div>
          </div>

          <div className="flex items-center gap-4 border-l border-slate-800 pl-4">
            <label className="flex items-center gap-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="radio"
                name="tipoBusquedaCliente"
                checked={tipoBusqueda === 'RUT'}
                onChange={() => setTipoBusqueda('RUT')}
                className="accent-cyan-500 w-4 h-4"
              />
              <span className="font-semibold">Buscar por RUT</span>
            </label>
            <label className="flex items-center gap-2 text-xs text-slate-300 cursor-pointer">
              <input
                type="radio"
                name="tipoBusquedaCliente"
                checked={tipoBusqueda === 'FOLIO'}
                onChange={() => setTipoBusqueda('FOLIO')}
                className="accent-cyan-500 w-4 h-4"
              />
              <span className="font-semibold">Buscar por Folio Pesaje</span>
            </label>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleExportarExcel}
              disabled={pesajesCliente.length === 0}
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 text-white font-medium text-xs rounded-lg flex items-center gap-2 shadow-md shadow-emerald-600/20 transition-all cursor-pointer"
            >
              <FileSpreadsheet className="w-4 h-4" />
              <span>Imprimir Ticket Excel (.xlsx)</span>
            </button>
          </div>
        </div>

        {/* Inputs */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 pt-2 border-t border-slate-800">
          {tipoBusqueda === 'RUT' ? (
            <>
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                  RUT Proveedor / Cliente:
                </label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={rutCliente}
                    onChange={(e) => setRutCliente(e.target.value)}
                    placeholder="76218764"
                    className="flex-1 px-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-cyan-300 focus:outline-none focus:border-cyan-400"
                  />
                  <button
                    type="button"
                    onClick={() => setIsModalClientesOpen(true)}
                    className="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 border border-slate-600 rounded-lg text-slate-200 text-xs flex items-center gap-1 transition-colors"
                    title="Abrir formulario Clientes (Barra Espaciadora en VBA)"
                  >
                    <ExternalLink className="w-3.5 h-3.5" />
                    <span>Buscar</span>
                  </button>
                </div>
              </div>
              <div className="md:col-span-2">
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                  Razón Social Seleccionada:
                </label>
                <input
                  type="text"
                  value={nombreCliente}
                  readOnly
                  className="w-full px-3 py-1.5 bg-slate-950/60 border border-slate-800 rounded-lg text-xs text-white font-medium focus:outline-none"
                />
              </div>
            </>
          ) : (
            <div className="md:col-span-3">
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                Número de Folio a Consultar:
              </label>
              <div className="flex items-center gap-2 max-w-sm">
                <Ticket className="w-4 h-4 text-slate-400" />
                <input
                  type="number"
                  value={folioInput}
                  onChange={(e) => setFolioInput(e.target.value)}
                  placeholder="Ej. 10420"
                  className="w-full px-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-cyan-300 focus:outline-none focus:border-cyan-400"
                />
              </div>
            </div>
          )}

          <div className="flex items-center gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                Desde:
              </label>
              <input
                type="date"
                value={fechaInicio}
                onChange={(e) => setFechaInicio(e.target.value)}
                className="w-full px-2.5 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                Hasta:
              </label>
              <input
                type="date"
                value={fechaTermino}
                onChange={(e) => setFechaTermino(e.target.value)}
                className="w-full px-2.5 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none"
              />
            </div>
          </div>
        </div>
      </div>

      {/* Grid: Pesajes del Cliente (grilla_inf_cte) */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <User className="w-4 h-4 text-cyan-400" />
            <h3 className="font-bold text-sm text-white uppercase tracking-wider">
              TICKETS DE RECEPCIÓN: {tipoBusqueda === 'RUT' ? `${nombreCliente} (${rutCliente})` : `FOLIO #${folioInput}`}
            </h3>
          </div>
          <span className="text-xs text-slate-400 font-mono">
            {pesajesCliente.length} pesajes encontrados
          </span>
        </div>

        <div className="overflow-x-auto max-h-96">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">N°</th>
                <th className="py-2.5 px-3 w-24">Folio</th>
                <th className="py-2.5 px-3 w-24">Patente</th>
                <th className="py-2.5 px-3">Producto Scrap</th>
                <th className="py-2.5 px-3 w-24">Fecha Ent.</th>
                <th className="py-2.5 px-3 w-20">Hora Ent.</th>
                <th className="py-2.5 px-3 text-right w-24">Peso Bruto</th>
                <th className="py-2.5 px-3 text-right w-24">Peso Tara</th>
                <th className="py-2.5 px-3 text-right w-28 font-bold">Peso Neto</th>
                <th className="py-2.5 px-3 w-28">Guía</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {pesajesCliente.length === 0 ? (
                <tr>
                  <td colSpan={10} className="py-8 text-center text-slate-500 italic">
                    No se encontraron tickets de recepción para el criterio seleccionado en {planta}.
                  </td>
                </tr>
              ) : (
                pesajesCliente.map((p, idx) => (
                  <tr key={p.folio} className="hover:bg-slate-800/50">
                    <td className="py-2 px-3 text-center text-slate-500 font-mono">{idx + 1}</td>
                    <td className="py-2 px-3 font-mono font-bold text-cyan-300">#{p.folio}</td>
                    <td className="py-2 px-3 font-mono font-semibold text-white">{p.patente}</td>
                    <td className="py-2 px-3 text-slate-200">{p.nomProducto}</td>
                    <td className="py-2 px-3 text-slate-400 font-mono">{p.fechaIngreso}</td>
                    <td className="py-2 px-3 text-slate-400 font-mono">{p.horaIngreso}</td>
                    <td className="py-2 px-3 text-right font-mono text-slate-400">{formatKilos(p.pesoBruto)}</td>
                    <td className="py-2 px-3 text-right font-mono text-slate-400">{formatKilos(p.pesoTara)}</td>
                    <td className="py-2 px-3 text-right font-mono font-bold text-emerald-400">
                      {formatKilos(p.pesoNeto)}
                    </td>
                    <td className="py-2 px-3 font-mono text-slate-400">{p.guiaDespacho || '-'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Total Kilos (txt_total_kilos_cte) */}
        <div className="p-4 bg-slate-950 border-t border-slate-800 flex items-center justify-between">
          <div className="text-xs text-slate-400">
            Filtro de pesajes por condición: <code className="text-cyan-400">tipo_pesaje = 'Recepcion de carga'</code>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-xs uppercase tracking-wider text-slate-400 font-semibold">
              Total Kilos Recepcionados (txt_total_kilos_cte):
            </span>
            <span className="text-xl font-black font-mono text-cyan-400">
              {formatKilos(totalKilos)}
            </span>
          </div>
        </div>
      </div>

      <ModalClientes
        isOpen={isModalClientesOpen}
        onClose={() => setIsModalClientesOpen(false)}
        plantaDefault={planta}
        onSelectCliente={handleSelectCliente}
      />
    </div>
  );
};
