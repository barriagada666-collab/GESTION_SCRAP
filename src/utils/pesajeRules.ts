/**
 * Pure business logic rules migrated from VBA modules (INFORMES, SQL, Conexion)
 */
import { IndexPrecioPesaje, PesajeRegistro, Planta, PrecioProveedor } from '../types/pesajes';

/**
 * Validates if the user is in "Precios Reales" mode before allowing individual price update.
 * Equivalent to VBA: If .opc_precios_reales.Value Then ... Else MsgBox "Para actualizar el precio debe buscar por opción precios reales"
 */
export function validateCanUpdatePrice(isRealPriceMode: boolean): { allowed: boolean; message: string } {
  if (!isRealPriceMode) {
    return {
      allowed: false,
      message: 'Para actualizar el precio debe buscar por opción precios reales y buscar nuevamente.',
    };
  }
  return { allowed: true, message: 'Validación exitosa.' };
}

/**
 * Calculates weighted average price = Total Pesos / Total Kilos
 * Avoids division by zero.
 */
export function calculateWeightedAverage(totalPesos: number, totalKilos: number): number {
  if (totalKilos <= 0) return 0;
  return Number((totalPesos / totalKilos).toFixed(2));
}

/**
 * Searches theoretical price for a given client RUT and Product Code
 * Equivalent to VBA: buscar_precio_pl / buscar_precio_stgo
 */
export function findTheoreticalPrice(
  precios: PrecioProveedor[],
  planta: 'PLACILLA' | 'SANTIAGO',
  rut: string,
  codProducto: string
): number {
  const cleanRut = rut.trim().toUpperCase().replace(/[^0-9K]/g, '');
  const cleanCod = codProducto.trim().toUpperCase();

  const found = precios.find(
    (p) =>
      p.planta === planta &&
      p.rut.trim().toUpperCase().replace(/[^0-9K]/g, '') === cleanRut &&
      p.codigoProducto.trim().toUpperCase() === cleanCod
  );

  return found ? found.precio : 0;
}

/**
 * Searches real frozen price by Folio in the indexed table
 * Equivalent to VBA: buscar_precio_pl_2 / buscar_precio_stgo_2
 */
export function findRealPrice(
  indexPrecios: IndexPrecioPesaje[],
  planta: 'PLACILLA' | 'SANTIAGO',
  folio: number
): number {
  const found = indexPrecios.find((ip) => ip.planta === planta && ip.folio === folio);
  return found ? found.precio : 0;
}

/**
 * Checks if a pesaje record already exists to prevent duplicate insertion.
 * Placilla / Santiago: check by folio.
 * Talcahuano: check by folio AND hora_ingreso (as defined in VBA get_pesaje_THNO).
 */
export function isDuplicatePesaje(
  existingPesajes: PesajeRegistro[],
  folio: number,
  planta: Planta,
  horaIngreso?: string
): boolean {
  if (planta === 'TALCAHUANO') {
    return existingPesajes.some(
      (p) => p.planta === 'TALCAHUANO' && p.folio === folio && (!horaIngreso || p.horaIngreso === horaIngreso)
    );
  }
  return existingPesajes.some((p) => p.planta === planta && p.folio === folio);
}

/**
 * Checks whether a provider price already exists before inserting.
 * Equivalent to VBA: comprobar_precio_prov_pl / comprobar_precio_prov_stgo
 */
export function checkProviderPriceExists(
  precios: PrecioProveedor[],
  planta: 'PLACILLA' | 'SANTIAGO',
  rut: string,
  codigoProducto: string
): boolean {
  const cleanRut = rut.trim().toUpperCase().replace(/[^0-9K]/g, '');
  const cleanCod = codigoProducto.trim().toUpperCase();

  return precios.some(
    (p) =>
      p.planta === planta &&
      p.rut.trim().toUpperCase().replace(/[^0-9K]/g, '') === cleanRut &&
      p.codigoProducto.trim().toUpperCase() === cleanCod
  );
}

/**
 * Filter pesajes within date range inclusive (YYYY-MM-DD)
 */
export function isDateInRange(dateStr: string, fromDateStr: string, toDateStr: string): boolean {
  const d = new Date(dateStr).getTime();
  const from = new Date(fromDateStr).getTime();
  const to = new Date(toDateStr).getTime();
  return d >= from && d <= to;
}

/**
 * Format currency in Chilean Pesos (CLP) standard
 */
export function formatCLP(val: number): string {
  return new Intl.NumberFormat('es-CL', {
    style: 'currency',
    currency: 'CLP',
    maximumFractionDigits: 0,
  }).format(val);
}

/**
 * Format number with thousand separators
 */
export function formatKilos(val: number): string {
  return new Intl.NumberFormat('es-CL', {
    maximumFractionDigits: 0,
  }).format(val) + ' Kg';
}
