import React, { useState } from 'react';
import {
  Layers,
  ShoppingCart,
  User,
  Store,
  FileText,
  DollarSign,
  PlusCircle,
  Database,
  Cpu,
  CheckCircle2,
  LayoutDashboard,
} from 'lucide-react';
import { Navbar } from './components/Navbar';
import { Dashboard } from './components/Dashboard';
import { Tab0GrupoMaterial } from './components/tabs/Tab0GrupoMaterial';
import { Tab1ComprasTraslados } from './components/tabs/Tab1ComprasTraslados';
import { Tab2InformeCliente } from './components/tabs/Tab2InformeCliente';
import { Tab3GruposEspeciales } from './components/tabs/Tab3GruposEspeciales';
import { Tab4InformeJCRAMM } from './components/tabs/Tab4InformeJCRAMM';
import { Tab5InformePrecios } from './components/tabs/Tab5InformePrecios';
import { Tab6MantenedorPrecios } from './components/tabs/Tab6MantenedorPrecios';
import { Tab7ImportacionFirebird } from './components/tabs/Tab7ImportacionFirebird';
import { ArchitectureViewer } from './components/architecture/ArchitectureViewer';
import { ModalLimpiarTablas } from './components/modals/ModalLimpiarTablas';
import { ModalEjecutarPA } from './components/modals/ModalEjecutarPA';
import { dbService } from './services/db';

export default function App() {
  const [activeTab, setActiveTab] = useState<number | 'dashboard'>('dashboard');
  const [isArchitectureView, setIsArchitectureView] = useState<boolean>(false);
  const [isModalLimpiarOpen, setIsModalLimpiarOpen] = useState<boolean>(false);
  const [isModalPAOpen, setIsModalPAOpen] = useState<boolean>(false);
  const [globalAlert, setGlobalAlert] = useState<string | null>(null);
  const [appKey, setAppKey] = useState<number>(0);

  const tabsConfig: { id: number | 'dashboard'; label: string; icon: React.ReactNode; sublabel: string }[] = [
    { id: 'dashboard', label: 'Dashboard General', icon: <LayoutDashboard className="w-4 h-4" />, sublabel: 'KPIs & Gráficos' },
    { id: 0, label: '0. Grupo Material', icon: <Layers className="w-4 h-4" />, sublabel: 'Placilla / Stgo / Thno' },
    { id: 1, label: '1. Compras + Traslados', icon: <ShoppingCart className="w-4 h-4" />, sublabel: 'Informes y Totales' },
    { id: 2, label: '2. Informe por Cliente', icon: <User className="w-4 h-4" />, sublabel: 'RUT o Folio' },
    { id: 3, label: '3. Grupos Especiales', icon: <Store className="w-4 h-4" />, sublabel: 'Retail / Agrícola / Pesquera' },
    { id: 4, label: '4. Informe JCRAMM', icon: <FileText className="w-4 h-4" />, sublabel: 'Detalle & Resumen' },
    { id: 5, label: '5. Informe de Precios', icon: <DollarSign className="w-4 h-4" />, sublabel: 'Teóricos y Reales' },
    { id: 6, label: '6. Mantenedor Precios', icon: <PlusCircle className="w-4 h-4" />, sublabel: 'Proveedor / Material' },
    { id: 7, label: '7. Importación Firebird', icon: <Database className="w-4 h-4" />, sublabel: 'Sincronización Remota' },
  ];

  const handleResetDemoData = () => {
    dbService.resetToFactoryDefaults();
    setAppKey((k) => k + 1);
    setGlobalAlert('Datos demostrativos de fábrica restablecidos correctamente.');
    setTimeout(() => setGlobalAlert(null), 4000);
  };

  const handleTablesCleared = (count: number) => {
    setAppKey((k) => k + 1);
    setGlobalAlert(`Se vaciaron con éxito las tablas de pesajes (${count} registros purgados).`);
    setTimeout(() => setGlobalAlert(null), 5000);
  };

  return (
    <div key={appKey} className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans">
      {/* Top Header Navbar */}
      <Navbar
        onOpenLimpiarTablas={() => setIsModalLimpiarOpen(true)}
        onOpenEjecutarPA={() => setIsModalPAOpen(true)}
        onResetDemoData={handleResetDemoData}
        isArchitectureView={isArchitectureView}
        onToggleArchitectureView={() => setIsArchitectureView((prev) => !prev)}
      />

      {/* Global Toast Notification */}
      {globalAlert && (
        <div className="bg-emerald-950/90 border-b border-emerald-700/80 px-4 py-2.5 flex items-center justify-center gap-2 text-emerald-200 text-xs font-semibold animate-in slide-in-from-top duration-200">
          <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
          <span>{globalAlert}</span>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {isArchitectureView ? (
          <div>
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h2 className="text-xl font-bold text-white flex items-center gap-2">
                  <Cpu className="w-6 h-6 text-amber-400" />
                  <span>Dossier Técnico del Arquitecto: Migración .NET 8 C# & SQL Relacional</span>
                </h2>
                <p className="text-xs text-slate-400">
                  Especificación de diseño, arquitectura limpia, modelo relacional, código fuente y plan de transición.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsArchitectureView(false)}
                className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 transition-colors"
              >
                Volver a la Aplicación Operativa
              </button>
            </div>
            <ArchitectureViewer />
          </div>
        ) : (
          <div>
            {/* MultiPage Tab Bar matching VBA UserForm Principal + Dashboard */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-1.5 shadow-lg mb-6">
              <nav className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-9 gap-1.5" aria-label="Pestañas de Informes">
                {tabsConfig.map((t) => {
                  const isActive = activeTab === t.id;
                  return (
                    <button
                      key={String(t.id)}
                      type="button"
                      onClick={() => setActiveTab(t.id)}
                      className={`px-3 py-2 rounded-lg text-left transition-all flex flex-col justify-between ${
                        isActive
                          ? 'bg-cyan-500 text-slate-950 shadow-md font-bold'
                          : 'text-slate-400 hover:text-white hover:bg-slate-850'
                      }`}
                    >
                      <div className="flex items-center gap-1.5 text-xs">
                        <span className={isActive ? 'text-slate-950' : 'text-cyan-400'}>{t.icon}</span>
                        <span className="truncate">{t.label}</span>
                      </div>
                      <span
                        className={`text-[10px] truncate mt-1 ${
                          isActive ? 'text-slate-800 font-medium' : 'text-slate-500'
                        }`}
                      >
                        {t.sublabel}
                      </span>
                    </button>
                  );
                })}
              </nav>
            </div>

            {/* Active Tab Screen */}
            <div className="animate-in fade-in-50 duration-150">
              {activeTab === 'dashboard' && <Dashboard onNavigateToTab={(idx) => setActiveTab(idx)} />}
              {activeTab === 0 && <Tab0GrupoMaterial />}
              {activeTab === 1 && <Tab1ComprasTraslados />}
              {activeTab === 2 && <Tab2InformeCliente />}
              {activeTab === 3 && <Tab3GruposEspeciales />}
              {activeTab === 4 && <Tab4InformeJCRAMM />}
              {activeTab === 5 && <Tab5InformePrecios />}
              {activeTab === 6 && <Tab6MantenedorPrecios />}
              {activeTab === 7 && <Tab7ImportacionFirebird />}
            </div>
          </div>
        )}
      </main>

      {/* Global Footer */}
      <footer className="bg-slate-900 border-t border-slate-800 py-4 text-center text-xs text-slate-500">
        <p>
          P. Cambiaso Hermanos S.A.C. &bull; Sistema de Pesajes e Informes de Scrap &bull; Arquitectura Migrada a C# .NET 8 / React
        </p>
      </footer>

      {/* Modals */}
      <ModalLimpiarTablas
        isOpen={isModalLimpiarOpen}
        onClose={() => setIsModalLimpiarOpen(false)}
        onTablesCleared={handleTablesCleared}
      />

      <ModalEjecutarPA
        isOpen={isModalPAOpen}
        onClose={() => setIsModalPAOpen(false)}
      />
    </div>
  );
}
