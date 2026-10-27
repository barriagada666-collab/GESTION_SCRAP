import React, { useState } from 'react';
import { DollarSign, Save, X, AlertTriangle } from 'lucide-react';
import { Planta } from '../../types/pesajes';
import { dbService } from '../../services/db';
import { validateCanUpdatePrice, formatCLP } from '../../utils/pesajeRules';

interface ModalActPreciosProps {
  isOpen: boolean;
  onClose: () => void;
  folio: number;
  codigoProd: string;
  descripcion: string;
  proveedor: string;
  precioActual: number;
  planta: 'PLACILLA' | 'SANTIAGO';
  isPreciosReales: boolean;
  familia: string;
  onPriceUpdated: (newPrice: number) => void;
}

export const ModalActPrecios: React.FC<ModalActPreciosProps> = ({
  isOpen,
  onClose,
  folio,
  codigoProd,
  descripcion,
  proveedor,
  precioActual,
  planta,
  isPreciosReales,
  familia,
  onPriceUpdated,
}) => {
  const [nuevoPrecio, setNuevoPrecio] = useState<number>(precioActual);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleGuardar = () => {
    const val = validateCanUpdatePrice(isPreciosReales);
    if (!val.allowed) {
      setError(val.message);
      return;
    }

    if (nuevoPrecio <= 0 || isNaN(nuevoPrecio)) {
      setError('Por favor ingrese un precio numérico válido mayor a 0.');
      return;
    }

    dbService.updateOrSetIndexPrecio(folio, planta, nuevoPrecio, familia);
    onPriceUpdated(nuevoPrecio);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4">
      <div className="bg-slate-900 border border-slate-700 w-full max-w-lg rounded-xl shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between px-5 py-4 bg-slate-800/80 border-b border-slate-700">
          <div className="flex items-center gap-2 text-amber-400 font-semibold text-lg">
            <DollarSign className="w-5 h-5" />
            <span>Actualizar Precio Individual (Act_precios)</span>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-white p-1 rounded-md hover:bg-slate-700 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4 text-sm">
          {!isPreciosReales && (
            <div className="p-3 bg-amber-950/60 border border-amber-600/60 rounded-lg flex items-start gap-2.5 text-amber-200">
              <AlertTriangle className="w-5 h-5 text-amber-400 shrink-0 mt-0.5" />
              <div>
                <strong className="block text-amber-300 font-medium">Validación Activa de Reglas VBA</strong>
                Para actualizar el precio individual de un pesaje, el informe debe estar configurado en{' '}
                <span className="underline font-semibold">Precios Reales</span>. En modo teórico no está permitido.
              </div>
            </div>
          )}

          {error && (
            <div className="p-3 bg-red-950/60 border border-red-600/60 rounded-lg text-red-200 flex items-center gap-2">
              <AlertTriangle className="w-5 h-5 text-red-400 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <div className="grid grid-cols-2 gap-3 bg-slate-800/50 p-4 rounded-lg border border-slate-700/60">
            <div>
              <span className="text-xs text-slate-400 uppercase font-mono">Folio Pesaje</span>
              <p className="font-bold text-white text-base">#{folio}</p>
            </div>
            <div>
              <span className="text-xs text-slate-400 uppercase font-mono">Planta Operativa</span>
              <p className="font-semibold text-cyan-400">{planta}</p>
            </div>
            <div className="col-span-2">
              <span className="text-xs text-slate-400 uppercase font-mono">Producto</span>
              <p className="text-slate-200 font-medium">{codigoProd} - {descripcion}</p>
            </div>
            <div className="col-span-2">
              <span className="text-xs text-slate-400 uppercase font-mono">Proveedor / Razón Social</span>
              <p className="text-slate-300">{proveedor}</p>
            </div>
            <div>
              <span className="text-xs text-slate-400 uppercase font-mono">Familia</span>
              <p className="text-emerald-400 font-medium">{familia}</p>
            </div>
            <div>
              <span className="text-xs text-slate-400 uppercase font-mono">Precio Actual Registrado</span>
              <p className="text-slate-300 font-mono">{formatCLP(precioActual)}</p>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
              Nuevo Precio Unitario ($/Kg)
            </label>
            <div className="relative">
              <span className="absolute left-3 top-2.5 text-slate-400 font-bold">$</span>
              <input
                type="number"
                min="0"
                step="1"
                value={nuevoPrecio}
                onChange={(e) => {
                  setError(null);
                  setNuevoPrecio(Number(e.target.value));
                }}
                className="w-full pl-8 pr-4 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono text-base focus:outline-none focus:border-amber-400 focus:ring-1 focus:ring-amber-400"
                placeholder="Ej. 350"
              />
            </div>
            <p className="text-xs text-slate-400 mt-1">
              Este cambio actualizará la tabla indexada <code>index_pesaje_{planta.toLowerCase()}</code> para este folio.
            </p>
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 px-5 py-3.5 bg-slate-800/80 border-t border-slate-700">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-slate-300 hover:text-white hover:bg-slate-700/60 transition-colors font-medium text-sm"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleGuardar}
            disabled={!isPreciosReales}
            className="px-5 py-2 rounded-lg bg-amber-500 hover:bg-amber-400 disabled:opacity-40 disabled:cursor-not-allowed text-slate-950 font-semibold text-sm flex items-center gap-2 shadow-lg shadow-amber-500/20 transition-all cursor-pointer"
          >
            <Save className="w-4 h-4" />
            <span>Guardar Precio</span>
          </button>
        </div>
      </div>
    </div>
  );
};
