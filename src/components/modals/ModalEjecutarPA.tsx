import React, { useState } from 'react';
import { Play, CheckCircle2, AlertCircle, Loader2, X, Terminal, Database } from 'lucide-react';
import { executeFirebirdStoredProcedures } from '../../services/firebirdMock';

interface ModalEjecutarPAProps {
  isOpen: boolean;
  onClose: () => void;
}

export const ModalEjecutarPA: React.FC<ModalEjecutarPAProps> = ({ isOpen, onClose }) => {
  const [isRunning, setIsRunning] = useState(false);
  const [currentStep, setCurrentStep] = useState<string | null>(null);
  const [logs, setLogs] = useState<string[]>([]);
  const [result, setResult] = useState<'success' | 'error' | null>(null);

  if (!isOpen) return null;

  const handleStart = async () => {
    setIsRunning(true);
    setResult(null);
    setLogs([]);
    setCurrentStep('Conectando al servidor...');

    const res = await executeFirebirdStoredProcedures((step) => {
      setCurrentStep(step);
    });

    setLogs(res.logs);
    setResult(res.success ? 'success' : 'error');
    setIsRunning(false);
    setCurrentStep(null);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-xs p-4">
      <div className="bg-slate-900 border border-slate-700 w-full max-w-xl rounded-xl shadow-2xl overflow-hidden flex flex-col">
        <div className="flex items-center justify-between px-5 py-4 bg-slate-800/80 border-b border-slate-700">
          <div className="flex items-center gap-2 text-indigo-400 font-bold text-lg">
            <Database className="w-5 h-5 text-indigo-400" />
            <span>Ejecutar Procedimientos Almacenados Firebird con Commit</span>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-white p-1 rounded-md hover:bg-slate-700 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4 text-sm text-slate-300">
          <div className="bg-slate-950 p-4 rounded-lg border border-slate-800 space-y-2">
            <p className="text-xs font-semibold uppercase text-slate-400">
              Objetivo de la Transacción (Equivalente VBA: <code>EjecutarProcedimientosConCommit</code>)
            </p>
            <p className="text-xs text-slate-300 leading-relaxed">
              Ejecuta dentro de una transacción aislada (<code>BeginTrans ... CommitTrans</code>) los dos procedimientos almacenados de Firebird:
            </p>
            <ul className="text-xs text-indigo-300 list-disc list-inside space-y-1 font-mono">
              <li>Paso 1: PA_INS_PROV_STGO (Inserción / Sincronización Proveedores)</li>
              <li>Paso 2: PA_INS_MASIVO_EXP_SAP (Inserción Masiva Exportación SAP)</li>
            </ul>
          </div>

          {currentStep && (
            <div className="flex items-center gap-2 p-3 bg-indigo-950/40 border border-indigo-700/50 rounded-lg text-indigo-200">
              <Loader2 className="w-4 h-4 animate-spin text-indigo-400 shrink-0" />
              <span className="text-xs font-mono">{currentStep}</span>
            </div>
          )}

          {result === 'success' && (
            <div className="flex items-center gap-2 p-3 bg-emerald-950/40 border border-emerald-600/50 rounded-lg text-emerald-200 text-xs">
              <CheckCircle2 className="w-5 h-5 text-emerald-400 shrink-0" />
              <span>El proceso completo se ejecutó y confirmó (COMMIT) exitosamente en Firebird.</span>
            </div>
          )}

          {result === 'error' && (
            <div className="flex items-center gap-2 p-3 bg-red-950/40 border border-red-600/50 rounded-lg text-red-200 text-xs">
              <AlertCircle className="w-5 h-5 text-red-400 shrink-0" />
              <span>Ocurrió un error. Se ejecutó RollbackTrans: ningún cambio fue guardado.</span>
            </div>
          )}

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <span className="text-xs font-semibold text-slate-400 flex items-center gap-1.5">
                <Terminal className="w-3.5 h-3.5" />
                Terminal de Salida Transaccional
              </span>
              <span className="text-[10px] text-slate-500 font-mono">192.168.2.56 (Port 3050)</span>
            </div>
            <div className="bg-black/80 font-mono text-xs p-3 rounded-lg border border-slate-800 text-emerald-400 h-40 overflow-y-auto space-y-1">
              {logs.length === 0 ? (
                <span className="text-slate-600 italic">Esperando inicio de ejecución...</span>
              ) : (
                logs.map((l, idx) => (
                  <div key={idx} className="leading-snug">
                    {l}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 px-5 py-3.5 bg-slate-800/80 border-t border-slate-700">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-slate-300 hover:text-white hover:bg-slate-700 font-medium text-sm transition-colors"
          >
            Cerrar
          </button>
          <button
            type="button"
            onClick={handleStart}
            disabled={isRunning}
            className="px-5 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white font-semibold text-sm flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition-all cursor-pointer"
          >
            {isRunning ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Ejecutando...</span>
              </>
            ) : (
              <>
                <Play className="w-4 h-4" />
                <span>Iniciar Ejecución Transaccional</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
