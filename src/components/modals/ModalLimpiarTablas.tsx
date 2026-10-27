import React, { useState } from 'react';
import { Trash2, AlertTriangle, X, ShieldAlert } from 'lucide-react';
import { dbService } from '../../services/db';

interface ModalLimpiarTablasProps {
  isOpen: boolean;
  onClose: () => void;
  onTablesCleared: (count: number) => void;
}

export const ModalLimpiarTablas: React.FC<ModalLimpiarTablasProps> = ({
  isOpen,
  onClose,
  onTablesCleared,
}) => {
  const [confirmInput, setConfirmInput] = useState('');
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleEjecutarLimpieza = () => {
    if (confirmInput.toUpperCase() !== 'ELIMINAR') {
      setError('Escriba exactamente "ELIMINAR" para confirmar la operación.');
      return;
    }

    const res = dbService.purgeAllPesajeTables('SUPER_ADMIN');
    onTablesCleared(res.clearedCount);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-xs p-4">
      <div className="bg-slate-900 border-2 border-red-600/80 w-full max-w-lg rounded-xl shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between px-5 py-4 bg-red-950/80 border-b border-red-800/80">
          <div className="flex items-center gap-2 text-red-400 font-bold text-lg">
            <ShieldAlert className="w-6 h-6 text-red-500" />
            <span>Confirmar Borrado Crítico de Tablas de Pesaje</span>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar modal"
            className="text-red-300 hover:text-white p-1 rounded-md hover:bg-red-900/60 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4 text-sm text-slate-300">
          <div className="p-3.5 bg-red-950/40 border border-red-700/60 rounded-lg flex items-start gap-3 text-red-200">
            <AlertTriangle className="w-6 h-6 text-red-400 shrink-0 mt-0.5" />
            <div className="space-y-1">
              <strong className="block text-red-300 font-semibold">
                ¿Estás seguro de que deseas borrar TODOS los registros de las tablas de pesaje?
              </strong>
              <p className="text-xs text-red-200/90 leading-relaxed">
                Esta acción es irreversible y purgará en cascada las tablas:
                <br />
                <code className="text-white font-mono bg-black/40 px-1 py-0.5 rounded">PESAJE_PLACILLA</code>,{' '}
                <code className="text-white font-mono bg-black/40 px-1 py-0.5 rounded">PESAJE_PLACILLA_2</code>,{' '}
                <code className="text-white font-mono bg-black/40 px-1 py-0.5 rounded">PESAJE_SANTIAGO</code> y{' '}
                <code className="text-white font-mono bg-black/40 px-1 py-0.5 rounded">PESAJE_TALCAHUANO</code>.
              </p>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">
              Para validar esta operación destructiva, escriba <span className="text-red-400 font-mono">ELIMINAR</span>:
            </label>
            <input
              type="text"
              value={confirmInput}
              onChange={(e) => {
                setError(null);
                setConfirmInput(e.target.value);
              }}
              placeholder="ELIMINAR"
              className="w-full px-3.5 py-2 bg-slate-950 border border-red-800/80 rounded-lg text-white font-mono text-center tracking-widest focus:outline-none focus:border-red-500 focus:ring-1 focus:ring-red-500"
            />
            {error && <p className="text-xs text-red-400 mt-1 font-medium">{error}</p>}
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 px-5 py-3.5 bg-slate-800/80 border-t border-slate-700">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-slate-300 hover:text-white hover:bg-slate-700 font-medium text-sm transition-colors"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleEjecutarLimpieza}
            className="px-5 py-2 rounded-lg bg-red-600 hover:bg-red-500 text-white font-bold text-sm flex items-center gap-2 shadow-lg shadow-red-600/30 transition-all cursor-pointer"
          >
            <Trash2 className="w-4 h-4" />
            <span>Confirmar y Purgar Tablas</span>
          </button>
        </div>
      </div>
    </div>
  );
};
