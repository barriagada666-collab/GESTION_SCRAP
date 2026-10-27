import React, { useState, useMemo } from 'react';
import { Package, Search, Check, X } from 'lucide-react';
import { Producto } from '../../types/pesajes';
import { dbService } from '../../services/db';

interface ModalProductosProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectProducto: (producto: Producto) => void;
}

export const ModalProductos: React.FC<ModalProductosProps> = ({
  isOpen,
  onClose,
  onSelectProducto,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const productos = useMemo(() => dbService.getProductos(), []);

  const productosFiltrados = useMemo(() => {
    const term = searchTerm.toLowerCase();
    return productos.filter(
      (p) =>
        p.codigo.toLowerCase().includes(term) ||
        p.descripcion.toLowerCase().includes(term) ||
        p.grupo.toLowerCase().includes(term)
    );
  }, [productos, searchTerm]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4">
      <div className="bg-slate-900 border border-slate-700 w-full max-w-xl rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[80vh]">
        <div className="flex items-center justify-between px-5 py-4 bg-slate-800/80 border-b border-slate-700">
          <div className="flex items-center gap-2 text-emerald-400 font-semibold text-lg">
            <Package className="w-5 h-5" />
            <span>Catálogo de Materiales / Productos (Formulario Productos)</span>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-white p-1 rounded-md hover:bg-slate-700 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 border-b border-slate-800 space-y-2 bg-slate-900/50">
          <div className="relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Buscar por código (PRD-01) o descripción..."
              className="w-full pl-9 pr-4 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-emerald-400 focus:ring-1 focus:ring-emerald-400"
              autoFocus
            />
          </div>
          <p className="text-xs text-slate-400">
            Haga doble click sobre un material para transferir el código al formulario.
          </p>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-800/80 text-slate-400 uppercase tracking-wider sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">N°</th>
                <th className="py-2.5 px-3 w-28 font-mono">Código</th>
                <th className="py-2.5 px-3">Descripción Producto</th>
                <th className="py-2.5 px-3 w-28">Familia</th>
                <th className="py-2.5 px-3 w-20 text-right">Elegir</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {productosFiltrados.map((p, idx) => (
                <tr
                  key={p.codigo}
                  onDoubleClick={() => {
                    onSelectProducto(p);
                    onClose();
                  }}
                  className="hover:bg-slate-800/70 cursor-pointer group transition-colors"
                >
                  <td className="py-2 px-3 text-center text-slate-500 font-mono">
                    {idx + 1}
                  </td>
                  <td className="py-2 px-3 font-mono font-bold text-emerald-400">
                    {p.codigo}
                  </td>
                  <td className="py-2 px-3 font-medium text-white group-hover:text-emerald-200">
                    {p.descripcion}
                  </td>
                  <td className="py-2 px-3">
                    <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-slate-800 text-slate-300 border border-slate-700">
                      {p.grupo}
                    </span>
                  </td>
                  <td className="py-2 px-3 text-right">
                    <button
                      type="button"
                      onClick={() => {
                        onSelectProducto(p);
                        onClose();
                      }}
                      className="px-2 py-1 bg-slate-800 group-hover:bg-emerald-500 group-hover:text-slate-950 text-slate-300 rounded text-xs font-semibold inline-flex items-center gap-1 transition-colors"
                    >
                      <Check className="w-3.5 h-3.5" />
                      <span>OK</span>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex items-center justify-between px-5 py-3 bg-slate-800/80 border-t border-slate-700 text-xs text-slate-400">
          <span>{productosFiltrados.length} productos registrados</span>
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
