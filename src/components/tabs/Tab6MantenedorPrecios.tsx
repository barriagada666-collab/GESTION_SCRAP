import React, { useState, useMemo } from 'react';
import { DollarSign, Search, PlusCircle, RotateCcw, AlertTriangle, CheckCircle, ExternalLink, Package, Building2 } from 'lucide-react';
import { Planta, Cliente, Producto } from '../../types/pesajes';
import { dbService } from '../../services/db';
import { checkProviderPriceExists, findTheoreticalPrice, formatCLP } from '../../utils/pesajeRules';
import { ModalClientes } from '../modals/ModalClientes';
import { ModalProductos } from '../modals/ModalProductos';

export const Tab6MantenedorPrecios: React.FC = () => {
  const [planta, setPlanta] = useState<'PLACILLA' | 'SANTIAGO'>('PLACILLA');
  const [codProd, setCodProd] = useState('');
  const [nomProd, setNomProd] = useState('');
  const [rutProv, setRutProv] = useState('');
  const [nomProv, setNomProv] = useState('');
  const [precioInput, setPrecioInput] = useState('');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Modals state
  const [isModalClientesOpen, setIsModalClientesOpen] = useState(false);
  const [isModalProductosOpen, setIsModalProductosOpen] = useState(false);

  const preciosRegistrados = useMemo(
    () => dbService.getPreciosProveedor().filter((p) => p.planta === planta),
    [planta, refreshTrigger]
  );

  const handleLimpiarCampos = () => {
    setCodProd('');
    setNomProd('');
    setRutProv('');
    setNomProv('');
    setPrecioInput('');
    setFeedback(null);
  };

  const handleBuscarPrecio = () => {
    if (!rutProv.trim() || !codProd.trim()) {
      setFeedback({
        type: 'error',
        text: 'Debe ingresar el RUT del Proveedor y el Código del Producto para realizar la búsqueda.',
      });
      return;
    }

    const allPrecios = dbService.getPreciosProveedor();
    const precio = findTheoreticalPrice(allPrecios, planta, rutProv, codProd);

    if (precio > 0) {
      setPrecioInput(String(precio));
      setFeedback({
        type: 'info',
        text: `Precio vigente encontrado en ${planta}: ${formatCLP(precio)}/Kg.`,
      });
    } else {
      setPrecioInput('');
      setFeedback({
        type: 'error',
        text: 'El cliente no tiene precio registrado para el producto seleccionado.',
      });
    }
  };

  const handleIngresarPrecio = () => {
    if (!rutProv.trim() || !codProd.trim() || !precioInput.trim()) {
      setFeedback({
        type: 'error',
        text: 'Por favor complete el RUT, Producto y Precio antes de guardar.',
      });
      return;
    }

    const numPrecio = Number(precioInput);
    if (isNaN(numPrecio) || numPrecio <= 0) {
      setFeedback({
        type: 'error',
        text: 'El precio debe ser un número mayor a 0.',
      });
      return;
    }

    const allPrecios = dbService.getPreciosProveedor();
    const yaExiste = checkProviderPriceExists(allPrecios, planta, rutProv, codProd);

    if (yaExiste) {
      setFeedback({
        type: 'error',
        text: 'El precio ya está asociado al proveedor y al material en esta planta.',
      });
      return;
    }

    dbService.addPrecioProveedor(
      planta,
      rutProv.trim(),
      nomProv.trim() || rutProv,
      codProd.trim(),
      numPrecio,
      'OPERADOR_MANT'
    );

    setRefreshTrigger((prev) => prev + 1);
    setFeedback({
      type: 'success',
      text: '¡Precio Ingresado Satisfactoriamente!',
    });
    handleLimpiarCampos();
  };

  const handleSelectCliente = (c: Cliente) => {
    setRutProv(c.rut);
    setNomProv(c.razonSocial);
  };

  const handleSelectProducto = (p: Producto) => {
    setCodProd(p.codigo);
    setNomProd(p.descripcion);
  };

  return (
    <div className="space-y-6">
      {/* Plant Selector Header */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Planta Operativa:</span>
          <div className="flex items-center gap-1.5 bg-slate-800 p-1 rounded-lg border border-slate-700">
            <button
              type="button"
              onClick={() => {
                setPlanta('PLACILLA');
                handleLimpiarCampos();
              }}
              className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                planta === 'PLACILLA'
                  ? 'bg-cyan-500 text-slate-950 shadow-sm'
                  : 'text-slate-300 hover:text-white'
              }`}
            >
              Placilla (precios_proveedor_placilla)
            </button>
            <button
              type="button"
              onClick={() => {
                setPlanta('SANTIAGO');
                handleLimpiarCampos();
              }}
              className={`px-3 py-1.5 rounded-md text-xs font-bold uppercase transition-all ${
                planta === 'SANTIAGO'
                  ? 'bg-cyan-500 text-slate-950 shadow-sm'
                  : 'text-slate-300 hover:text-white'
              }`}
            >
              Santiago (precios_proveedor_stgo)
            </button>
          </div>
        </div>

        <span className="text-xs text-slate-400 font-mono">
          Mantenedor Maestro de Precios Proveedor / Material
        </span>
      </div>

      {/* Input Form Card */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {/* Producto */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                <Package className="w-3.5 h-3.5 text-emerald-400" />
                Código de Producto (txt_cod_prod)
              </label>
              <button
                type="button"
                onClick={() => setIsModalProductosOpen(true)}
                className="text-xs text-cyan-400 hover:underline flex items-center gap-1"
                title="Presione Enter en VBA para abrir modal Productos"
              >
                <ExternalLink className="w-3 h-3" />
                <span>Catálogo Productos [Enter]</span>
              </button>
            </div>
            <input
              type="text"
              value={codProd}
              onChange={(e) => setCodProd(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && setIsModalProductosOpen(true)}
              placeholder="Ej. PRD-01"
              className="w-full px-3.5 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono text-sm focus:outline-none focus:border-cyan-400"
            />
            {nomProd && (
              <p className="text-xs text-emerald-400 font-medium truncate">{nomProd}</p>
            )}
          </div>

          {/* Proveedor */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                <Building2 className="w-3.5 h-3.5 text-cyan-400" />
                RUT Proveedor (txt_rut_prov)
              </label>
              <button
                type="button"
                onClick={() => setIsModalClientesOpen(true)}
                className="text-xs text-cyan-400 hover:underline flex items-center gap-1"
                title="Presione Enter en VBA para abrir modal Clientes"
              >
                <ExternalLink className="w-3 h-3" />
                <span>Buscar Cliente [Enter]</span>
              </button>
            </div>
            <input
              type="text"
              value={rutProv}
              onChange={(e) => setRutProv(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && setIsModalClientesOpen(true)}
              placeholder="Ej. 76218764"
              className="w-full px-3.5 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono text-sm focus:outline-none focus:border-cyan-400"
            />
            {nomProv && (
              <p className="text-xs text-cyan-400 font-medium truncate">{nomProv}</p>
            )}
          </div>

          {/* Razón Social */}
          <div className="space-y-2">
            <label className="text-xs font-semibold text-slate-300 uppercase tracking-wider block">
              Nombre o Razón Social Proveedor (txt_nom_prov)
            </label>
            <input
              type="text"
              value={nomProv}
              onChange={(e) => setNomProv(e.target.value)}
              placeholder="Razón social del proveedor"
              className="w-full px-3.5 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-cyan-400"
            />
          </div>

          {/* Precio */}
          <div className="space-y-2">
            <label className="text-xs font-semibold text-slate-300 uppercase tracking-wider block">
              Precio Unitario Acordado ($/Kg) (txt_precio)
            </label>
            <div className="relative">
              <span className="absolute left-3.5 top-2 text-slate-400 font-bold">$</span>
              <input
                type="number"
                min="0"
                step="1"
                value={precioInput}
                onChange={(e) => setPrecioInput(e.target.value)}
                placeholder="Ej. 420"
                className="w-full pl-8 pr-4 py-2 bg-slate-950 border border-slate-700 rounded-lg text-white font-mono text-sm focus:outline-none focus:border-cyan-400"
              />
            </div>
          </div>
        </div>

        {/* Feedback alert */}
        {feedback && (
          <div
            className={`p-3 rounded-lg text-xs flex items-center gap-2 border ${
              feedback.type === 'success'
                ? 'bg-emerald-950/60 border-emerald-600/60 text-emerald-200'
                : feedback.type === 'error'
                ? 'bg-red-950/60 border-red-600/60 text-red-200'
                : 'bg-cyan-950/60 border-cyan-600/60 text-cyan-200'
            }`}
          >
            {feedback.type === 'success' ? (
              <CheckCircle className="w-4 h-4 shrink-0 text-emerald-400" />
            ) : (
              <AlertTriangle className="w-4 h-4 shrink-0 text-amber-400" />
            )}
            <span>{feedback.text}</span>
          </div>
        )}

        {/* Action Buttons matching VBA UserForm */}
        <div className="flex flex-wrap items-center justify-end gap-3 pt-4 border-t border-slate-800">
          <button
            type="button"
            onClick={handleLimpiarCampos}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Limpiar Campos</span>
          </button>

          <button
            type="button"
            onClick={handleBuscarPrecio}
            className="px-4 py-2 bg-slate-800 hover:bg-cyan-950 hover:border-cyan-700 border border-slate-700 text-cyan-300 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
          >
            <Search className="w-3.5 h-3.5" />
            <span>Buscar Precio Registrado</span>
          </button>

          <button
            type="button"
            onClick={handleIngresarPrecio}
            className="px-5 py-2 bg-cyan-600 hover:bg-cyan-500 text-slate-950 font-bold text-xs rounded-lg flex items-center gap-2 shadow-lg shadow-cyan-600/20 transition-all cursor-pointer"
          >
            <PlusCircle className="w-4 h-4" />
            <span>Ingresar Precio Proveedor</span>
          </button>
        </div>
      </div>

      {/* Precios Vigentes Table */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
        <div className="bg-slate-800/90 border-b border-slate-700/80 px-4 py-3 flex items-center justify-between">
          <h3 className="font-bold text-xs text-white uppercase tracking-wider">
            Maestro de Precios Vigentes en {planta} ({preciosRegistrados.length} registros)
          </h3>
          <span className="text-xs text-slate-400 font-mono">
            precios_proveedor_{planta.toLowerCase()}
          </span>
        </div>

        <div className="overflow-x-auto max-h-80">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-850 text-slate-400 uppercase font-mono sticky top-0">
              <tr>
                <th className="py-2.5 px-3 w-12 text-center">Índice</th>
                <th className="py-2.5 px-3 w-32 font-mono">RUT Proveedor</th>
                <th className="py-2.5 px-3">Razón Social</th>
                <th className="py-2.5 px-3 w-28 font-mono">Cód. Prod.</th>
                <th className="py-2.5 px-3 text-right w-28 font-bold text-amber-400">Precio ($/Kg)</th>
                <th className="py-2.5 px-3 w-28 text-center">Fecha Act.</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {preciosRegistrados.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-500 italic">
                    No hay precios registrados para la planta {planta}.
                  </td>
                </tr>
              ) : (
                preciosRegistrados.map((p) => (
                  <tr key={p.indice} className="hover:bg-slate-800/40">
                    <td className="py-2 px-3 text-center text-slate-500 font-mono">{p.indice}</td>
                    <td className="py-2 px-3 font-mono font-medium text-cyan-300">{p.rut}</td>
                    <td className="py-2 px-3 text-white font-medium">{p.razonSocial}</td>
                    <td className="py-2 px-3 font-mono font-bold text-emerald-400">{p.codigoProducto}</td>
                    <td className="py-2 px-3 text-right font-mono font-bold text-amber-300">
                      {formatCLP(p.precio)}
                    </td>
                    <td className="py-2 px-3 text-center font-mono text-slate-400">{p.fechaActualizacion}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <ModalClientes
        isOpen={isModalClientesOpen}
        onClose={() => setIsModalClientesOpen(false)}
        plantaDefault={planta}
        onSelectCliente={handleSelectCliente}
      />

      <ModalProductos
        isOpen={isModalProductosOpen}
        onClose={() => setIsModalProductosOpen(false)}
        onSelectProducto={handleSelectProducto}
      />
    </div>
  );
};
