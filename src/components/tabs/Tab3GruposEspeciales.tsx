import React, { useState, useMemo } from 'react';
import { Calendar, Search, Layers, Store, Sprout, Fish, Building, Globe } from 'lucide-react';
import { dbService } from '../../services/db';
import { formatKilos, isDateInRange } from '../../utils/pesajeRules';

interface GrupoEspecialCard {
  id: string;
  titulo: string;
  vistaSql: string;
  icon: React.ReactNode;
  colorClass: string;
  datos: { cliente: string; kilos: number }[];
}

export const Tab3GruposEspeciales: React.FC = () => {
  const [fechaDesde, setFechaDesde] = useState('2026-02-01');
  const [fechaHasta, setFechaHasta] = useState('2026-02-28');

  const pesajes = useMemo(() => dbService.getPesajes(), []);

  const pesajesFiltrados = useMemo(() => {
    return pesajes.filter((p) => isDateInRange(p.fechaIngreso, fechaDesde, fechaHasta));
  }, [pesajes, fechaDesde, fechaHasta]);

  // Calculations for each special group based on the legacy SQL views
  const gruposEspeciales: GrupoEspecialCard[] = useMemo(() => {
    // 1. Retail Santiago (VISTA_RETAIL_STGO: Cencosud, Walmart, SMU en Santiago)
    const retStgoMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'SANTIAGO' && (p.nomCliente.includes('Retail') || p.nomCliente.includes('Cencosud') || p.nomCliente.includes('Walmart') || p.nomCliente.includes('SMU')))
      .forEach((p) => retStgoMap.set(p.nomCliente, (retStgoMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 2. Retail Empresas (VISTA_RETAIL_EMPRESAS: Empresas Carozzi, etc.)
    const retEmpMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.nomCliente.includes('Carozzi') || p.nomCliente.includes('Empresas') || p.nomCliente.includes('Walmart'))
      .forEach((p) => retEmpMap.set(p.nomCliente, (retEmpMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 3. Mix Supermercado Stgo (VISTA_MIX_SUPERMERCADO_STGO)
    const mixStgoMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'SANTIAGO' && (p.grupo === 'MIX' || p.nomProducto.includes('MIX') || p.nomProducto.includes('STRETCH')))
      .forEach((p) => mixStgoMap.set(p.nomCliente, (mixStgoMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 4. Mix Supermercado Placilla (VISTA_MIX_SUPERMERCADO_PLACILLA)
    const mixPlMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.planta === 'PLACILLA' && (p.grupo === 'MIX' || p.nomProducto.includes('MIX') || p.nomProducto.includes('STRETCH')))
      .forEach((p) => mixPlMap.set(p.nomCliente, (mixPlMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 5. Retail Regiones Stgo (VISTA_RETAIL_REGIONES_STGO + CONSULTA_REG_MARTINO_SOS + VISTA_JAC_JOHNSON_PLAC)
    const retRegMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.nomCliente.includes('Martino') || p.nomCliente.includes('Johnson') || p.origen.includes('Concepción'))
      .forEach((p) => retRegMap.set(p.nomCliente, (retRegMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 6. Agrícola (CONSULTA_GRUPO_AGRICOLA)
    const agrMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.nomCliente.includes('Agricola') || p.nomCliente.includes('Agrícola') || p.nomProducto.includes('AGRÍCOLAS'))
      .forEach((p) => agrMap.set(p.nomCliente, (agrMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 7. Pesquera (CONSULTA_GRUPO_PESQUERA)
    const pesqMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.nomCliente.includes('Pesquera') || p.nomProducto.includes('PESQUERA'))
      .forEach((p) => pesqMap.set(p.nomCliente, (pesqMap.get(p.nomCliente) || 0) + p.pesoNeto));

    // 8. Agrosuper (VISTA_PSJES_AGROSUPER_PL)
    const agroMap = new Map<string, number>();
    pesajesFiltrados
      .filter((p) => p.nomCliente.includes('Agrosuper'))
      .forEach((p) => agroMap.set(p.nomCliente, (agroMap.get(p.nomCliente) || 0) + p.pesoNeto));

    return [
      {
        id: 'ret_stgo',
        titulo: 'Retail Santiago',
        vistaSql: 'VISTA_RETAIL_STGO',
        icon: <Store className="w-4 h-4 text-cyan-400" />,
        colorClass: 'border-cyan-800/60 bg-cyan-950/20',
        datos: Array.from(retStgoMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'ret_emp',
        titulo: 'Retail Empresas',
        vistaSql: 'VISTA_RETAIL_EMPRESAS',
        icon: <Building className="w-4 h-4 text-blue-400" />,
        colorClass: 'border-blue-800/60 bg-blue-950/20',
        datos: Array.from(retEmpMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'mix_stgo',
        titulo: 'Mix Supermercado Santiago',
        vistaSql: 'VISTA_MIX_SUPERMERCADO_STGO',
        icon: <Layers className="w-4 h-4 text-indigo-400" />,
        colorClass: 'border-indigo-800/60 bg-indigo-950/20',
        datos: Array.from(mixStgoMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'mix_pl',
        titulo: 'Mix Supermercado Placilla',
        vistaSql: 'VISTA_MIX_SUPERMERCADO_PLACILLA',
        icon: <Layers className="w-4 h-4 text-teal-400" />,
        colorClass: 'border-teal-800/60 bg-teal-950/20',
        datos: Array.from(mixPlMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'ret_reg',
        titulo: 'Retail Regiones (Martino / Johnson)',
        vistaSql: 'VISTA_RETAIL_REGIONES_STGO',
        icon: <Globe className="w-4 h-4 text-amber-400" />,
        colorClass: 'border-amber-800/60 bg-amber-950/20',
        datos: Array.from(retRegMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'agr',
        titulo: 'Sector Agrícola',
        vistaSql: 'CONSULTA_GRUPO_AGRICOLA',
        icon: <Sprout className="w-4 h-4 text-emerald-400" />,
        colorClass: 'border-emerald-800/60 bg-emerald-950/20',
        datos: Array.from(agrMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'pesq',
        titulo: 'Sector Pesquero',
        vistaSql: 'CONSULTA_GRUPO_PESQUERA',
        icon: <Fish className="w-4 h-4 text-sky-400" />,
        colorClass: 'border-sky-800/60 bg-sky-950/20',
        datos: Array.from(pesqMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
      {
        id: 'agrosuper',
        titulo: 'Grupo Agrosuper Placilla',
        vistaSql: 'VISTA_PSJES_AGROSUPER_PL',
        icon: <Building className="w-4 h-4 text-rose-400" />,
        colorClass: 'border-rose-800/60 bg-rose-950/20',
        datos: Array.from(agroMap.entries()).map(([cliente, kilos]) => ({ cliente, kilos })),
      },
    ];
  }, [pesajesFiltrados]);

  return (
    <div className="space-y-6">
      {/* Top Filter */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Desde:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaDesde}
                onChange={(e) => setFechaDesde(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Hasta:</span>
            <div className="relative">
              <Calendar className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5 pointer-events-none" />
              <input
                type="date"
                value={fechaHasta}
                onChange={(e) => setFechaHasta(e.target.value)}
                className="pl-8 pr-3 py-1.5 bg-slate-950 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>
        </div>

        <span className="text-xs text-slate-400 font-mono">
          8 Módulos de Informes Especiales Sincronizados
        </span>
      </div>

      {/* 8 Special Groups Responsive Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5">
        {gruposEspeciales.map((grp) => {
          const totalKilos = grp.datos.reduce((acc, d) => acc + d.kilos, 0);

          return (
            <div
              key={grp.id}
              className={`border rounded-xl overflow-hidden shadow-lg flex flex-col ${grp.colorClass} bg-slate-900`}
            >
              <div className="p-3.5 border-b border-slate-800 flex items-center justify-between bg-slate-850/80">
                <div className="flex items-center gap-2">
                  {grp.icon}
                  <h4 className="font-bold text-xs text-white uppercase tracking-wider">
                    {grp.titulo}
                  </h4>
                </div>
                <span className="text-[10px] font-mono text-slate-400 truncate max-w-[120px]">
                  {grp.vistaSql}
                </span>
              </div>

              <div className="p-3 flex-1 overflow-y-auto max-h-56">
                <table className="w-full text-xs text-left">
                  <thead className="text-[10px] uppercase font-mono text-slate-400 border-b border-slate-800">
                    <tr>
                      <th className="pb-1.5 px-2">Cliente / Proveedor</th>
                      <th className="pb-1.5 px-2 text-right">Kilos</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 text-slate-300">
                    {grp.datos.length === 0 ? (
                      <tr>
                        <td colSpan={2} className="py-6 text-center text-slate-500 italic text-[11px]">
                          Sin registros para este grupo
                        </td>
                      </tr>
                    ) : (
                      grp.datos.map((d, idx) => (
                        <tr key={idx} className="hover:bg-slate-800/40">
                          <td className="py-1.5 px-2 font-medium text-white text-[11px] truncate max-w-[170px]">
                            {d.cliente}
                          </td>
                          <td className="py-1.5 px-2 text-right font-mono font-semibold text-cyan-300 text-[11px]">
                            {formatKilos(d.kilos)}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              <div className="p-2.5 bg-slate-950 border-t border-slate-800 flex items-center justify-between text-xs font-bold">
                <span className="text-[10px] uppercase text-slate-400">Total Kilos:</span>
                <span className="font-mono text-amber-300">{formatKilos(totalKilos)}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
