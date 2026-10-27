import React, { useMemo } from 'react';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line,
  CartesianGrid,
  AreaChart,
  Area,
} from 'recharts';
import {
  TrendingUp,
  TrendingDown,
  Scale,
  Building2,
  Package,
  AlertTriangle,
  ArrowRight,
  Clock,
  CheckCircle,
  Truck,
  DollarSign,
  Layers,
  ChevronRight,
  ShieldAlert,
} from 'lucide-react';
import { dbService } from '../services/db';
import { formatCLP, formatKilos, findTheoreticalPrice, findRealPrice } from '../utils/pesajeRules';

interface DashboardProps {
  onNavigateToTab: (tabIndex: number) => void;
}

export const Dashboard: React.FC<DashboardProps> = ({ onNavigateToTab }) => {
  const pesajes = useMemo(() => dbService.getPesajes(), []);
  const preciosProveedor = useMemo(() => dbService.getPreciosProveedor(), []);
  const indexPrecios = useMemo(() => dbService.getIndexPrecios(), []);

  // Current Month (Febrero 2026) vs Previous Month (Enero 2026)
  const mesActualStr = '2026-02';
  const mesAnteriorStr = '2026-01';

  const pesajesMesActual = useMemo(
    () => pesajes.filter((p) => p.fechaIngreso.startsWith(mesActualStr)),
    [pesajes]
  );

  const pesajesMesAnterior = useMemo(
    () => pesajes.filter((p) => p.fechaIngreso.startsWith(mesAnteriorStr)),
    [pesajes]
  );

  const totalKilosMesActual = useMemo(
    () => pesajesMesActual.reduce((acc, p) => acc + p.pesoNeto, 0),
    [pesajesMesActual]
  );

  const totalKilosMesAnterior = useMemo(
    () => pesajesMesAnterior.reduce((acc, p) => acc + p.pesoNeto, 0),
    [pesajesMesAnterior]
  );

  const variacionPorcentual = useMemo(() => {
    if (totalKilosMesAnterior === 0) return 100;
    const diff = totalKilosMesActual - totalKilosMesAnterior;
    return Number(((diff / totalKilosMesAnterior) * 100).toFixed(1));
  }, [totalKilosMesActual, totalKilosMesAnterior]);

  // Daily Pesajes (Latest day in DB)
  const fechaMasReciente = useMemo(() => {
    if (pesajesMesActual.length === 0) return '2026-02-25';
    return pesajesMesActual.reduce(
      (max, p) => (p.fechaIngreso > max ? p.fechaIngreso : max),
      pesajesMesActual[0].fechaIngreso
    );
  }, [pesajesMesActual]);

  const pesajesHoy = useMemo(
    () => pesajes.filter((p) => p.fechaIngreso === fechaMasReciente),
    [pesajes, fechaMasReciente]
  );

  const kilosHoy = useMemo(
    () => pesajesHoy.reduce((acc, p) => acc + p.pesoNeto, 0),
    [pesajesHoy]
  );

  // 1. KG TOTALES POR PROVEEDOR
  const dataProveedores = useMemo(() => {
    const map = new Map<string, number>();
    pesajesMesActual.forEach((p) => {
      // Short display name
      const name = p.nomCliente
        .replace(' (Jumbo / Santa Isabel)', '')
        .replace(' (Lider / Express)', '')
        .replace(' S.A.', '')
        .replace(' Ltda.', '')
        .replace(' SpA', '');
      map.set(name, (map.get(name) || 0) + p.pesoNeto);
    });

    return Array.from(map.entries())
      .map(([name, kilos]) => ({ name, kilos }))
      .sort((a, b) => b.kilos - a.kilos)
      .slice(0, 6);
  }, [pesajesMesActual]);

  // 2. KG TOTALES POR MATERIAL
  const dataMateriales = useMemo(() => {
    const map = new Map<string, number>();
    pesajesMesActual.forEach((p) => {
      const g = p.grupo || 'OTROS';
      map.set(g, (map.get(g) || 0) + p.pesoNeto);
    });

    return Array.from(map.entries())
      .map(([name, kilos]) => ({ name, value: kilos }))
      .sort((a, b) => b.value - a.value);
  }, [pesajesMesActual]);

  const COLORS_MATERIALES = ['#06b6d4', '#f59e0b', '#10b981', '#6366f1', '#ec4899', '#8b5cf6'];

  // 3. COMPARATIVA MES EN CURSO VS MES ANTERIOR (Por Semanas)
  const dataComparativaMeses = useMemo(() => {
    // Break each month into 4 weeks: 1-7, 8-14, 15-21, 22-31
    const semanas = [
      { name: 'Semana 1 (1-7)', actual: 0, anterior: 0 },
      { name: 'Semana 2 (8-14)', actual: 0, anterior: 0 },
      { name: 'Semana 3 (15-21)', actual: 0, anterior: 0 },
      { name: 'Semana 4 (22-28)', actual: 0, anterior: 0 },
    ];

    pesajesMesActual.forEach((p) => {
      const day = parseInt(p.fechaIngreso.split('-')[2], 10);
      if (day <= 7) semanas[0].actual += p.pesoNeto;
      else if (day <= 14) semanas[1].actual += p.pesoNeto;
      else if (day <= 21) semanas[2].actual += p.pesoNeto;
      else semanas[3].actual += p.pesoNeto;
    });

    pesajesMesAnterior.forEach((p) => {
      const day = parseInt(p.fechaIngreso.split('-')[2], 10);
      if (day <= 7) semanas[0].anterior += p.pesoNeto;
      else if (day <= 14) semanas[1].anterior += p.pesoNeto;
      else if (day <= 21) semanas[2].anterior += p.pesoNeto;
      else semanas[3].anterior += p.pesoNeto;
    });

    return semanas;
  }, [pesajesMesActual, pesajesMesAnterior]);

  // 4. ÚLTIMOS FOLIOS PROCESADOS
  const ultimosFolios = useMemo(() => {
    return [...pesajes]
      .sort((a, b) => b.folio - a.folio)
      .slice(0, 6);
  }, [pesajes]);

  // 5. ALERTAS DE PRECIOS DESACTUALIZADOS / FALTANTES
  const alertasPrecios = useMemo(() => {
    const list: {
      tipo: 'SIN_PRECIO' | 'PENDIENTE_CONGELAR';
      folio: number;
      planta: 'PLACILLA' | 'SANTIAGO';
      cliente: string;
      producto: string;
      kilos: number;
      mensaje: string;
    }[] = [];

    // Inspect recent month pesajes for Placilla and Santiago
    pesajesMesActual.forEach((p) => {
      if (p.planta === 'PLACILLA' || p.planta === 'SANTIAGO') {
        const precioTeorico = findTheoreticalPrice(preciosProveedor, p.planta, p.rutCliente, p.codProducto);
        const precioReal = findRealPrice(indexPrecios, p.planta, p.folio);

        if (precioTeorico === 0 && precioReal === 0) {
          list.push({
            tipo: 'SIN_PRECIO',
            folio: p.folio,
            planta: p.planta,
            cliente: p.nomCliente,
            producto: p.codProducto,
            kilos: p.pesoNeto,
            mensaje: `El proveedor no registra precio para ${p.codProducto} en ${p.planta}.`,
          });
        } else if (precioReal === 0 && precioTeorico > 0) {
          list.push({
            tipo: 'PENDIENTE_CONGELAR',
            folio: p.folio,
            planta: p.planta,
            cliente: p.nomCliente,
            producto: p.codProducto,
            kilos: p.pesoNeto,
            mensaje: `Precio teórico pactado ($${precioTeorico}) pendiente de indexar como precio real.`,
          });
        }
      }
    });

    return list.slice(0, 5);
  }, [pesajesMesActual, preciosProveedor, indexPrecios]);

  return (
    <div className="space-y-6 animate-in fade-in duration-200">
      {/* Welcome Banner */}
      <div className="bg-linear-to-r from-slate-900 via-slate-850 to-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-4 relative z-10">
          <div>
            <div className="flex items-center gap-2 mb-1.5">
              <span className="px-2.5 py-0.5 rounded-full text-[11px] font-mono font-bold bg-cyan-950 text-cyan-400 border border-cyan-800">
                SCADA EN TIEMPO REAL
              </span>
              <span className="text-xs text-slate-400 font-mono">
                Actualizado al {fechaMasReciente}
              </span>
            </div>
            <h2 className="text-2xl font-black text-white tracking-tight">
              Dashboard General de Pesajes & Control de Scrap
            </h2>
            <p className="text-sm text-slate-400 max-w-2xl mt-1">
              Consolidación centralizada de las básculas de Placilla, Santiago y Talcahuano con análisis de variaciones mensuales, kilos por proveedor y alertas de precios.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => onNavigateToTab(5)}
              className="px-4 py-2 bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-200 text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <DollarSign className="w-4 h-4 text-amber-400" />
              <span>Ver Precios</span>
            </button>
            <button
              type="button"
              onClick={() => onNavigateToTab(7)}
              className="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-slate-950 text-xs font-bold rounded-lg flex items-center gap-1.5 transition-colors shadow-lg shadow-cyan-600/20 cursor-pointer"
            >
              <Layers className="w-4 h-4" />
              <span>Importar Firebird</span>
            </button>
          </div>
        </div>
      </div>

      {/* 4 Main KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Card 1: Mes en Curso */}
        <div className="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg relative overflow-hidden flex flex-col justify-between">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block font-mono">
              Total Kilos Mes en Curso
            </span>
            <div className="flex items-baseline gap-2 mt-2">
              <span className="text-2xl font-black font-mono text-cyan-400">
                {formatKilos(totalKilosMesActual)}
              </span>
            </div>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs">
            <span className="text-slate-400">Vs Mes Anterior:</span>
            <span
              className={`font-mono font-bold flex items-center gap-1 ${
                variacionPorcentual >= 0 ? 'text-emerald-400' : 'text-rose-400'
              }`}
            >
              {variacionPorcentual >= 0 ? (
                <TrendingUp className="w-3.5 h-3.5" />
              ) : (
                <TrendingDown className="w-3.5 h-3.5" />
              )}
              {variacionPorcentual >= 0 ? `+${variacionPorcentual}%` : `${variacionPorcentual}%`}
            </span>
          </div>
        </div>

        {/* Card 2: Mes Anterior */}
        <div className="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg flex flex-col justify-between">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block font-mono">
              Kilos Mes Anterior (Enero)
            </span>
            <div className="flex items-baseline gap-2 mt-2">
              <span className="text-2xl font-black font-mono text-slate-200">
                {formatKilos(totalKilosMesAnterior)}
              </span>
            </div>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-400">
            <span>Diferencia Neta:</span>
            <span className="font-mono font-semibold text-cyan-300">
              +{formatKilos(totalKilosMesActual - totalKilosMesAnterior)}
            </span>
          </div>
        </div>

        {/* Card 3: Pesajes Diarios */}
        <div className="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block font-mono">
                Pesajes Diarios Activos
              </span>
              <span className="text-[10px] font-mono text-emerald-400 bg-emerald-950/80 px-1.5 py-0.5 rounded border border-emerald-800/60">
                Última Jornada
              </span>
            </div>
            <div className="flex items-baseline gap-2 mt-2">
              <span className="text-2xl font-black font-mono text-amber-400">
                {formatKilos(kilosHoy)}
              </span>
            </div>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-400">
            <span>Camiones Procesados:</span>
            <span className="font-mono font-bold text-white">
              {pesajesHoy.length} camiones
            </span>
          </div>
        </div>

        {/* Card 4: Alertas de Precios */}
        <div className="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block font-mono">
                Alertas de Precios
              </span>
              <span className="p-1 rounded bg-amber-950 text-amber-400">
                <AlertTriangle className="w-4 h-4" />
              </span>
            </div>
            <div className="flex items-baseline gap-2 mt-2">
              <span className="text-2xl font-black font-mono text-rose-400">
                {alertasPrecios.length} pendientes
              </span>
            </div>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-800/80 flex items-center justify-between text-xs">
            <span className="text-slate-400">Revisión requerida:</span>
            <button
              type="button"
              onClick={() => onNavigateToTab(6)}
              className="text-amber-400 hover:underline font-semibold flex items-center gap-1 cursor-pointer"
            >
              <span>Resolver</span>
              <ArrowRight className="w-3 h-3" />
            </button>
          </div>
        </div>
      </div>

      {/* Row 1: Charts (Mes Actual vs Anterior & Kilos por Proveedor) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Chart 1: Comparativa Mes Actual vs Mes Anterior */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div>
              <h3 className="font-bold text-sm text-white flex items-center gap-2">
                <Scale className="w-4 h-4 text-cyan-400" />
                <span>Comparativa: Mes en Curso vs Mes Anterior</span>
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Evolución de kilos recepcionados por semana ({mesActualStr} vs {mesAnteriorStr})
              </p>
            </div>
            <span className="text-xs font-mono text-cyan-300 bg-cyan-950/60 px-2 py-0.5 rounded border border-cyan-800">
              +{variacionPorcentual}% Variación
            </span>
          </div>

          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dataComparativaMeses} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="gradActual" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#06b6d4" stopOpacity={0.6} />
                    <stop offset="95%" stopColor="#06b6d4" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="gradAnterior" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#64748b" stopOpacity={0.4} />
                    <stop offset="95%" stopColor="#64748b" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#334155" opacity={0.6} />
                <XAxis dataKey="name" stroke="#94a3b8" fontSize={11} tickLine={false} />
                <YAxis
                  stroke="#94a3b8"
                  fontSize={11}
                  tickFormatter={(val) => `${val / 1000}k`}
                  tickLine={false}
                />
                <Tooltip
                  contentStyle={{ backgroundColor: '#0f172a', borderColor: '#334155', borderRadius: '8px' }}
                  formatter={(val: unknown) => [formatKilos(Number(val) || 0), 'Kilos']}
                />
                <Legend wrapperStyle={{ fontSize: '11px', paddingTop: '8px' }} />
                <Area
                  type="monotone"
                  dataKey="actual"
                  name="Mes en Curso (Febrero)"
                  stroke="#06b6d4"
                  strokeWidth={2.5}
                  fillOpacity={1}
                  fill="url(#gradActual)"
                />
                <Area
                  type="monotone"
                  dataKey="anterior"
                  name="Mes Anterior (Enero)"
                  stroke="#64748b"
                  strokeWidth={2}
                  strokeDasharray="4 4"
                  fillOpacity={1}
                  fill="url(#gradAnterior)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Chart 2: KG Totales por Proveedores */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div>
              <h3 className="font-bold text-sm text-white flex items-center gap-2">
                <Building2 className="w-4 h-4 text-amber-400" />
                <span>KG Totales por Proveedores (Top 6)</span>
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Volumen acumulado recepcionado por proveedor en el mes
              </p>
            </div>
            <button
              type="button"
              onClick={() => onNavigateToTab(2)}
              className="text-xs text-cyan-400 hover:underline flex items-center gap-1"
            >
              <span>Ver Clientes</span>
              <ChevronRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={dataProveedores}
                layout="vertical"
                margin={{ top: 5, right: 30, left: 40, bottom: 5 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#334155" opacity={0.6} horizontal={false} />
                <XAxis
                  type="number"
                  stroke="#94a3b8"
                  fontSize={11}
                  tickFormatter={(val) => `${val / 1000}k`}
                />
                <YAxis
                  type="category"
                  dataKey="name"
                  stroke="#cbd5e1"
                  fontSize={10}
                  width={110}
                  tickLine={false}
                />
                <Tooltip
                  contentStyle={{ backgroundColor: '#0f172a', borderColor: '#334155', borderRadius: '8px' }}
                  formatter={(val: unknown) => [formatKilos(Number(val) || 0), 'Kilos Totales']}
                />
                <Bar dataKey="kilos" name="Kilos" fill="#f59e0b" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* Row 2: Distribution por Material & Alertas de Precios */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Chart 3: KG Totales por Material */}
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div>
              <h3 className="font-bold text-sm text-white flex items-center gap-2">
                <Package className="w-4 h-4 text-emerald-400" />
                <span>KG Totales por Material</span>
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Distribución por grupo de scrap
              </p>
            </div>
            <button
              type="button"
              onClick={() => onNavigateToTab(0)}
              className="text-xs text-cyan-400 hover:underline flex items-center gap-1"
            >
              <span>Ver Grupos</span>
              <ChevronRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="h-56 w-full flex items-center justify-center">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={dataMateriales}
                  cx="50%"
                  cy="50%"
                  innerRadius={45}
                  outerRadius={75}
                  paddingAngle={3}
                  dataKey="value"
                >
                  {dataMateriales.map((entry, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={COLORS_MATERIALES[index % COLORS_MATERIALES.length]}
                    />
                  ))}
                </Pie>
                <Tooltip
                  contentStyle={{ backgroundColor: '#0f172a', borderColor: '#334155', borderRadius: '8px' }}
                  formatter={(val: unknown) => [formatKilos(Number(val) || 0), 'Kilos']}
                />
              </PieChart>
            </ResponsiveContainer>
          </div>

          {/* Custom legend */}
          <div className="grid grid-cols-2 gap-2 pt-2 border-t border-slate-800/80 text-xs">
            {dataMateriales.map((m, idx) => (
              <div key={m.name} className="flex items-center gap-2">
                <span
                  className="w-2.5 h-2.5 rounded-full shrink-0"
                  style={{ backgroundColor: COLORS_MATERIALES[idx % COLORS_MATERIALES.length] }}
                />
                <span className="text-slate-300 truncate text-[11px] font-medium">{m.name}:</span>
                <span className="text-slate-400 font-mono text-[11px] ml-auto">
                  {formatKilos(m.value)}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Widget 2: Alertas de Precios Desactualizados */}
        <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl space-y-4 flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 className="font-bold text-sm text-white flex items-center gap-2">
                  <ShieldAlert className="w-4 h-4 text-rose-400" />
                  <span>Alertas de Precios Desactualizados y Faltantes</span>
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">
                  Pesajes que requieren asignación o congelamiento de tarifa en el maestro
                </p>
              </div>
              <button
                type="button"
                onClick={() => onNavigateToTab(6)}
                className="px-3 py-1 bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 border border-amber-500/30 rounded-lg text-xs font-semibold flex items-center gap-1 transition-colors cursor-pointer"
              >
                <span>Mantenedor Precios</span>
                <ArrowRight className="w-3 h-3" />
              </button>
            </div>

            <div className="space-y-2.5 pt-3">
              {alertasPrecios.length === 0 ? (
                <div className="p-6 text-center text-slate-500 text-xs flex flex-col items-center gap-2">
                  <CheckCircle className="w-6 h-6 text-emerald-400" />
                  <span>Todos los precios de pesajes están actualizados y verificados.</span>
                </div>
              ) : (
                alertasPrecios.map((a, idx) => (
                  <div
                    key={idx}
                    className="p-3 bg-slate-950/70 border border-slate-800 hover:border-slate-700 rounded-lg flex items-start justify-between gap-3 transition-colors"
                  >
                    <div className="flex items-start gap-2.5">
                      <span
                        className={`p-1.5 rounded mt-0.5 ${
                          a.tipo === 'SIN_PRECIO'
                            ? 'bg-rose-950 text-rose-400'
                            : 'bg-amber-950 text-amber-400'
                        }`}
                      >
                        <AlertTriangle className="w-3.5 h-3.5" />
                      </span>
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-mono font-bold text-xs text-white">
                            Folio #{a.folio}
                          </span>
                          <span className="text-[10px] font-mono px-1.5 py-0.2 rounded bg-slate-800 text-slate-300">
                            {a.planta}
                          </span>
                          <span className="text-xs text-slate-300 font-medium truncate max-w-xs">
                            {a.cliente}
                          </span>
                        </div>
                        <p className="text-xs text-slate-400 mt-0.5">{a.mensaje}</p>
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={() => onNavigateToTab(5)}
                      className="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs rounded font-medium shrink-0 flex items-center gap-1 transition-colors"
                    >
                      <span>Corregir</span>
                      <ChevronRight className="w-3 h-3 text-slate-400" />
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>

          <div className="p-3 bg-slate-950 border border-slate-800/80 rounded-lg flex items-center justify-between text-xs text-slate-400">
            <span>Regla de negocio: No se permite exportar informes valorizados con tarifas en $0.</span>
            <button
              type="button"
              onClick={() => onNavigateToTab(5)}
              className="text-cyan-400 hover:underline font-semibold"
            >
              Ir a Informe de Precios &rarr;
            </button>
          </div>
        </div>
      </div>

      {/* Row 3: Últimos Folios Procesados */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-850/80 border-b border-slate-800 px-5 py-3.5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-cyan-400" />
            <h3 className="font-bold text-xs text-white uppercase tracking-wider">
              Últimos Folios de Pesajes Procesados en Básculas
            </h3>
          </div>
          <button
            type="button"
            onClick={() => onNavigateToTab(1)}
            className="text-xs text-cyan-400 hover:underline flex items-center gap-1"
          >
            <span>Ver Compras & Traslados</span>
            <ChevronRight className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-900 text-slate-400 uppercase font-mono border-b border-slate-800">
              <tr>
                <th className="py-2.5 px-4 w-20">Folio</th>
                <th className="py-2.5 px-3 w-28">Planta</th>
                <th className="py-2.5 px-3 w-24">Patente</th>
                <th className="py-2.5 px-3">Proveedor / Cliente</th>
                <th className="py-2.5 px-3">Producto Scrap</th>
                <th className="py-2.5 px-3 w-28">Fecha / Hora</th>
                <th className="py-2.5 px-4 text-right w-32 font-bold">Peso Neto</th>
                <th className="py-2.5 px-3 w-24 text-center">Tipo</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/80">
              {ultimosFolios.map((p) => (
                <tr key={p.folio} className="hover:bg-slate-800/40 transition-colors">
                  <td className="py-2.5 px-4 font-mono font-bold text-cyan-300">#{p.folio}</td>
                  <td className="py-2.5 px-3">
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold ${
                        p.planta === 'PLACILLA'
                          ? 'bg-cyan-950 text-cyan-300 border border-cyan-800'
                          : p.planta === 'SANTIAGO'
                          ? 'bg-blue-950 text-blue-300 border border-blue-800'
                          : 'bg-emerald-950 text-emerald-300 border border-emerald-800'
                      }`}
                    >
                      {p.planta}
                    </span>
                  </td>
                  <td className="py-2.5 px-3 font-mono font-semibold text-white">{p.patente}</td>
                  <td className="py-2.5 px-3 font-medium text-slate-200">{p.nomCliente}</td>
                  <td className="py-2.5 px-3 text-slate-300">{p.nomProducto}</td>
                  <td className="py-2.5 px-3 text-slate-400 font-mono text-[11px]">
                    {p.fechaIngreso} {p.horaIngreso.slice(0, 5)}
                  </td>
                  <td className="py-2.5 px-4 text-right font-mono font-bold text-emerald-400">
                    {formatKilos(p.pesoNeto)}
                  </td>
                  <td className="py-2.5 px-3 text-center">
                    <span
                      className={`px-2 py-0.5 rounded-full text-[10px] font-semibold ${
                        p.esTraslado
                          ? 'bg-amber-950 text-amber-300 border border-amber-800/60'
                          : 'bg-slate-800 text-slate-300'
                      }`}
                    >
                      {p.esTraslado ? 'Traslado' : 'Recepción'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
