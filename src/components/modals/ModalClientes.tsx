import React, { useState, useMemo } from 'react';
import { Search, UserCheck, X, Building2 } from 'lucide-react';
import { Cliente, Planta } from '../../types/pesajes';
import { dbService } from '../../services/db';

interface ModalClientesProps {
  isOpen: boolean;
  onClose: () => void;
  plantaDefault?: Planta;
  onSelectCliente: (cliente: Cliente) => void;
}

export const ModalClientes: React.FC<ModalClientesProps> = ({
  isOpen,
  onClose,
  plantaDefault = 'PLACILLA',
  onSelectCliente,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [plantaFiltro, setPlantaFiltro] = useState<Planta>(plantaDefault);

  const clientes = useMemo(() => dbService.getClientes(), []);

  const clientesFiltrados = useMemo(() => {
    return clientes.filter((c) => {
      const matchPlanta = c.planta === plantaFiltro || c.planta === 'TODAS';
      const term = searchTerm.toLowerCase();
      const matchSearch =
        c.rut.toLowerCase().includes(term) ||
        c.nombre.toLowerCase().includes(term) ||
        c.razonSocial.toLowerCase().includes(term);
      return matchPlanta && matchSearch;
    });
  }, [clientes, plantaFiltro, searchTerm]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4">
      <div className="bg-slate-900 border border-slate-700 w-full max-w-2xl rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[85vh]">
        <div className="flex items-center justify-between px-5 py-4 bg-slate-800/80 border-b border-slate-700">
          <div className="flex items-center gap-2 text-cyan-400 font-semibold text-lg">
            <Building2 className="w-5 h-5" />
            <span>Selector de Clientes / Proveedores (Formulario Clientes)</span>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-white p-1 rounded-md hover:bg-slate-700 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 border-b border-slate-800 space-y-3 bg-slate-900/50">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2 bg-slate-800 p-1 rounded-lg border border-slate-700">
              <button
                type="button"
                onClick={() => setPlantaFiltro('PLACILLA')}
                className={`px-3 py-1.5 rounded-md text-xs font-semibold uppercase tracking-wider transition-all ${
                  plantaFiltro === 'PLACILLA'
                    ? 'bg-cyan-500 text-slate-950 shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                Placilla (clientes_placilla)
              </button>
              <button
                type="button"
                onClick={() => setPlantaFiltro('SANTIAGO')}
                className={`px-3 py-1.5 rounded-md text-xs font-semibold uppercase tracking-wider transition-all ${
                  plantaFiltro === 'SANTIAGO'
                    ? 'bg-cyan-500 text-slate-950 shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                Santiago (clientes)
              </button>
              <button
                type="button"
                onClick={() => setPlantaFiltro('TALCAHUANO')}
                className={`px-3 py-1.5 rounded-md text-xs font-semibold uppercase tracking-wider transition-all ${
                  plantaFiltro === 'TALCAHUANO'
                    ? 'bg-cyan-500 text-slate-950 shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                Talcahuano (CLI_PROV)
              </button>
            </div>

            <span className="text-xs text-slate-400">
              Doble click en una fila para seleccionar
            </span>
          </div>

          <div className="relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Buscar por RUT, Nombre o Razón Social..."
              className="w-full pl-9 pr-4 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-cyan-400 focus:ring-1 focus:ring-cyan-400"
              autoFocus
            />
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-800/80 text-slate-400 uppercase tracking-wider sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">N°</th>
                <th className="py-2.5 px-3 w-32 font-mono">RUT</th>
                <th className="py-2.5 px-3">Razón Social / Proveedor</th>
                <th className="py-2.5 px-3 w-28">Ciudad</th>
                <th className="py-2.5 px-3 w-24 text-right">Acción</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {clientesFiltrados.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-500">
                    No se encontraron clientes para el filtro actual.
                  </td>
                </tr>
              ) : (
                clientesFiltrados.map((c, idx) => (
                  <tr
                    key={c.rut}
                    onDoubleClick={() => {
                      onSelectCliente(c);
                      onClose();
                    }}
                    className="hover:bg-slate-800/70 cursor-pointer group transition-colors"
                  >
                    <td className="py-2 px-3 text-center text-slate-500 font-mono">
                      {idx + 1}
                    </td>
                    <td className="py-2 px-3 font-mono font-medium text-cyan-300">
                      {c.rut}{c.digito ? `-${c.digito}` : ''}
                    </td>
                    <td className="py-2 px-3 font-medium text-white group-hover:text-cyan-200">
                      {c.razonSocial}
                    </td>
                    <td className="py-2 px-3 text-slate-400">{c.ciudad || '-'}</td>
                    <td className="py-2 px-3 text-right">
                      <button
                        type="button"
                        onClick={() => {
                          onSelectCliente(c);
                          onClose();
                        }}
                        className="px-2 py-1 bg-slate-800 group-hover:bg-cyan-500 group-hover:text-slate-950 text-slate-300 rounded text-xs font-semibold inline-flex items-center gap-1 transition-colors"
                      >
                        <UserCheck className="w-3.5 h-3.5" />
                        <span>Elegir</span>
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="flex items-center justify-between px-5 py-3 bg-slate-800/80 border-t border-slate-700 text-xs text-slate-400">
          <span>{clientesFiltrados.length} registros cargados</span>
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-slate-700 hover:bg-slate-600 text-white font-medium"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
};
