import React from 'react';
import { Database, Server, Trash2, Play, RefreshCw, Cpu, Layers } from 'lucide-react';
import { Planta } from '../types/pesajes';

interface NavbarProps {
  onOpenLimpiarTablas: () => void;
  onOpenEjecutarPA: () => void;
  onResetDemoData: () => void;
  isArchitectureView: boolean;
  onToggleArchitectureView: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  onOpenLimpiarTablas,
  onOpenEjecutarPA,
  onResetDemoData,
  isArchitectureView,
  onToggleArchitectureView,
}) => {
  return (
    <header className="bg-slate-900 border-b border-slate-800 sticky top-0 z-40 shadow-xl">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo and System Title */}
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-linear-to-br from-cyan-500 to-blue-700 flex items-center justify-center shadow-lg shadow-cyan-500/20 text-slate-950 font-black text-xl tracking-tighter">
              PC
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="font-bold text-white text-base tracking-wide flex items-center gap-2">
                  <span>P. CAMBIASO</span>
                  <span className="text-slate-500 font-normal">|</span>
                  <span className="text-cyan-400">Sistema de Pesajes e Informes</span>
                </h1>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-cyan-950 text-cyan-300 border border-cyan-800">
                  v3.0 .NET Core Modernizado
                </span>
              </div>
              <p className="text-[11px] text-slate-400">
                Migración de "Informes P.Cambiaso 2.5.xlsm" (Placilla, Santiago y Talcahuano)
              </p>
            </div>
          </div>

          {/* Quick Actions & View Switcher */}
          <div className="flex items-center gap-3">
            {/* View Switcher: Operational App vs Architecture Blueprint */}
            <button
              type="button"
              onClick={onToggleArchitectureView}
              className={`px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 border transition-all ${
                isArchitectureView
                  ? 'bg-amber-500 text-slate-950 border-amber-400 shadow-md shadow-amber-500/20'
                  : 'bg-slate-800 hover:bg-slate-700 text-amber-300 border-amber-700/60'
              }`}
            >
              <Cpu className="w-4 h-4" />
              <span>{isArchitectureView ? 'Volver a Formularios' : 'Dossier Arquitectónico'}</span>
            </button>

            {/* Run Stored Procedures Firebird */}
            <button
              type="button"
              onClick={onOpenEjecutarPA}
              className="px-3 py-1.5 bg-indigo-950 hover:bg-indigo-900 border border-indigo-700/80 text-indigo-200 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
              title="Ejecutar PA_INS_PROV_STGO y PA_INS_MASIVO_EXP_SAP con Commit"
            >
              <Play className="w-3.5 h-3.5 text-indigo-400" />
              <span>Ejecutar PA Firebird</span>
            </button>

            {/* Clear Pesaje Tables */}
            <button
              type="button"
              onClick={onOpenLimpiarTablas}
              className="px-3 py-1.5 bg-red-950/70 hover:bg-red-900/80 border border-red-700/70 text-red-200 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
              title="Limpiar Tablas de Pesaje con confirmación crítica"
            >
              <Trash2 className="w-3.5 h-3.5 text-red-400" />
              <span>Limpiar Tablas</span>
            </button>

            {/* Reset Demo Data */}
            <button
              type="button"
              onClick={onResetDemoData}
              className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white rounded-lg transition-colors"
              title="Restablecer datos demostrativos de pesajes"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Server status bar */}
        <div className="py-1.5 border-t border-slate-800/80 flex items-center justify-between text-[11px] font-mono text-slate-400">
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span className="text-slate-300">BD Central:</span>
              <span className="text-emerald-400 font-semibold">PostgreSQL (Conectada)</span>
            </span>

            <span className="text-slate-600">|</span>

            <span className="flex items-center gap-1.5">
              <Server className="w-3 h-3 text-cyan-400" />
              <span>Firebird Placilla:</span>
              <span className="text-cyan-300">WMDBSPC.GDB (192.168.2.56)</span>
            </span>

            <span className="text-slate-600">|</span>

            <span className="flex items-center gap-1.5">
              <Server className="w-3 h-3 text-blue-400" />
              <span>Firebird Santiago:</span>
              <span className="text-blue-300">placilla2\WMDBSPC.GDB</span>
            </span>

            <span className="text-slate-600">|</span>

            <span className="flex items-center gap-1.5">
              <Server className="w-3 h-3 text-emerald-400" />
              <span>Firebird Talcahuano:</span>
              <span className="text-emerald-300">PWAREHOUSE.FDB</span>
            </span>
          </div>

          <div className="text-slate-400">
            {new Date().toLocaleDateString('es-CL', {
              weekday: 'short',
              year: 'numeric',
              month: 'short',
              day: 'numeric',
            })}
          </div>
        </div>
      </div>
    </header>
  );
};
