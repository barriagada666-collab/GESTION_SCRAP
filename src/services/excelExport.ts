import * as XLSX from 'xlsx';
import { Planta } from '../types/pesajes';

/**
 * Downloads an XLSX workbook in the browser
 */
function downloadWorkbook(workbook: XLSX.WorkBook, fileName: string) {
  XLSX.writeFile(workbook, fileName);
}

/**
 * Replaces EXPORTAR_EXCEL_PLACILLA, EXPORTAR_EXCEL_STGO, EXPORTAR_EXCEL_THNO
 */
export function exportarExcelComprasTraslados(
  planta: Planta,
  fechaDesde: string,
  fechaHasta: string,
  compras: { codigo: string; descripcion: string; razonSocial: string; kilos: number }[],
  traslados: { folio: number; patente: string; producto: string; fechaEntrada: string; fechaSalida: string; pesoEnt: number; pesoSal: number; neto: number }[],
  totalComprasKilos: number,
  totalTrasladosKilos: number
) {
  const wb = XLSX.utils.book_new();

  // Sheet 1: Compras
  const comprasData: (string | number)[][] = [
    [`CAMBIASO HERMANOS S.A.C. - INFORME COMPRAS SCRAP ${planta}`],
    [`Desde: ${fechaDesde}`, `Hasta: ${fechaHasta}`],
    [],
    ['N°', 'CÓDIGO', 'DESCRIPCIÓN', 'RAZÓN SOCIAL', 'TOTAL KILOS'],
  ];

  compras.forEach((c, idx) => {
    comprasData.push([idx + 1, c.codigo, c.descripcion, c.razonSocial, c.kilos]);
  });

  comprasData.push([]);
  comprasData.push(['', '', '', 'TOTAL GENERAL COMPRAS', totalComprasKilos]);

  const wsCompras = XLSX.utils.aoa_to_sheet(comprasData);
  wsCompras['!cols'] = [{ wch: 6 }, { wch: 15 }, { wch: 35 }, { wch: 35 }, { wch: 18 }];
  XLSX.utils.book_append_sheet(wb, wsCompras, `Compras ${planta}`);

  // Sheet 2: Traslados
  const trasladosData: (string | number)[][] = [
    [`CAMBIASO HERMANOS S.A.C. - TRASLADOS SCRAP ${planta}`],
    [`Desde: ${fechaDesde}`, `Hasta: ${fechaHasta}`],
    [],
    ['N°', 'FOLIO', 'PATENTE', 'PRODUCTO', 'FECHA ENT.', 'FECHA SAL.', 'PESO BRUTO', 'PESO TARA', 'TOTAL KILOS'],
  ];

  traslados.forEach((t, idx) => {
    trasladosData.push([
      idx + 1,
      t.folio,
      t.patente,
      t.producto,
      t.fechaEntrada,
      t.fechaSalida,
      t.pesoEnt,
      t.pesoSal,
      t.neto,
    ]);
  });

  trasladosData.push([]);
  trasladosData.push(['', '', '', '', '', '', '', 'TOTAL KILOS TRASLADOS', totalTrasladosKilos]);

  const wsTraslados = XLSX.utils.aoa_to_sheet(trasladosData);
  wsTraslados['!cols'] = [
    { wch: 6 },
    { wch: 12 },
    { wch: 12 },
    { wch: 35 },
    { wch: 14 },
    { wch: 14 },
    { wch: 14 },
    { wch: 14 },
    { wch: 16 },
  ];
  XLSX.utils.book_append_sheet(wb, wsTraslados, `Traslados ${planta}`);

  downloadWorkbook(wb, `INFORME_SCRAP_${planta}_${fechaDesde}_al_${fechaHasta}.xlsx`);
}

/**
 * Replaces EXP_EXCEL_PL_PRECIO & EXP_EXCEL_STGO_PRECIO
 */
export function exportarExcelPrecios(
  planta: 'PLACILLA' | 'SANTIAGO',
  fechaDesde: string,
  fechaHasta: string,
  modo: 'TEORICOS' | 'REALES',
  detalle: {
    folio: number;
    codigoProd: string;
    descripcion: string;
    proveedor: string;
    kilos: number;
    precio: number;
    total: number;
    familia: string;
  }[],
  resumen: { grupo: string; kilos: number; precioPromedio: number; totalPesos: number }[]
) {
  const wb = XLSX.utils.book_new();

  // Sheet 1: Detalle Pesajes y Precios
  const rows: (string | number)[][] = [
    [`CAMBIASO HERMANOS - INFORME DE PRECIOS ${modo} (${planta})`],
    [`Periodo: ${fechaDesde} al ${fechaHasta}`],
    [],
    ['RESUMEN POR FAMILIA / GRUPO DE MATERIAL'],
    ['GRUPO', 'TOTAL KILOS', 'PRECIO PROMEDIO', 'TOTAL VALORIZADO'],
  ];

  let totalGeneralKilos = 0;
  let totalGeneralPesos = 0;

  resumen.forEach((r) => {
    rows.push([r.grupo, r.kilos, r.precioPromedio, r.totalPesos]);
    totalGeneralKilos += r.kilos;
    totalGeneralPesos += r.totalPesos;
  });

  rows.push(['TOTALES', totalGeneralKilos, totalGeneralKilos > 0 ? Number((totalGeneralPesos / totalGeneralKilos).toFixed(2)) : 0, totalGeneralPesos]);
  rows.push([]);
  rows.push(['DETALLE DE PESAJES VALORIZADOS']);
  rows.push([
    'N°',
    'FOLIO',
    'CÓDIGO PROD.',
    'DESCRIPCIÓN PRODUCTO',
    'PROVEEDOR',
    'KILOS',
    'PRECIO UNIT.',
    'TOTAL ($)',
    'FAMILIA',
  ]);

  detalle.forEach((d, idx) => {
    rows.push([
      idx + 1,
      d.folio,
      d.codigoProd,
      d.descripcion,
      d.proveedor,
      d.kilos,
      d.precio,
      d.total,
      d.familia,
    ]);
  });

  const ws = XLSX.utils.aoa_to_sheet(rows);
  ws['!cols'] = [
    { wch: 6 },
    { wch: 12 },
    { wch: 15 },
    { wch: 35 },
    { wch: 32 },
    { wch: 14 },
    { wch: 14 },
    { wch: 16 },
    { wch: 16 },
  ];

  XLSX.utils.book_append_sheet(wb, ws, `Precios ${planta}`);
  downloadWorkbook(wb, `INFORME_PRECIOS_${planta}_${modo}_${fechaDesde}.xlsx`);
}

/**
 * Replaces IMP_INF_EXCEL (Informe por Cliente)
 */
export function exportarExcelCliente(
  planta: Planta,
  rut: string,
  razonSocial: string,
  fechaDesde: string,
  fechaHasta: string,
  pesajes: {
    folio: number;
    patente: string;
    producto: string;
    fechaIngreso: string;
    fechaSalida: string;
    pesoBruto: number;
    pesoTara: number;
    pesoNeto: number;
  }[],
  totalKilos: number
) {
  const wb = XLSX.utils.book_new();

  const data: (string | number)[][] = [
    [`CAMBIASO HERMANOS S.A.C. - TICKET CONSOLIDADO DE RECEPCIÓN CLIENTE`],
    [`Planta: ${planta}`, `RUT: ${rut}`, `Razón Social: ${razonSocial}`],
    [`Desde: ${fechaDesde}`, `Hasta: ${fechaHasta}`],
    [],
    ['N°', 'N° FOLIO', 'PATENTE', 'PRODUCTO', 'FECHA ENT.', 'FECHA SAL.', 'PESO BRUTO', 'PESO TARA', 'PESO NETO (KG)'],
  ];

  pesajes.forEach((p, idx) => {
    data.push([
      idx + 1,
      p.folio,
      p.patente,
      p.producto,
      p.fechaIngreso,
      p.fechaSalida,
      p.pesoBruto,
      p.pesoTara,
      p.pesoNeto,
    ]);
  });

  data.push([]);
  data.push(['', '', '', '', '', '', '', 'TOTAL KILOS RECEPCIONADOS', totalKilos]);

  const ws = XLSX.utils.aoa_to_sheet(data);
  ws['!cols'] = [
    { wch: 6 },
    { wch: 12 },
    { wch: 12 },
    { wch: 35 },
    { wch: 14 },
    { wch: 14 },
    { wch: 14 },
    { wch: 14 },
    { wch: 18 },
  ];

  XLSX.utils.book_append_sheet(wb, ws, 'Ticket Cliente');
  downloadWorkbook(wb, `Ticket_Pes_${planta}_${rut}_${fechaDesde}.xlsx`);
}

/**
 * Replaces IMP_INF_JCRAMM (Informe JCRAMM detalle y resumen)
 */
export function exportarExcelJCRAMM(
  detalle: {
    fecha: string;
    folio: number;
    codProd: string;
    descripcion: string;
    patente: string;
    proveedor: string;
    ciudad: string;
    kilos: number;
  }[],
  resumen: { producto: string; kilos: number }[],
  totalKilosDetalle: number,
  totalKilosResumen: number
) {
  const wb = XLSX.utils.book_new();

  const data: (string | number)[][] = [
    ['CAMBIASO HERMANOS S.A.C. - INFORME DE PESAJE JCRAMM'],
    [`Generado el: ${new Date().toLocaleDateString('es-CL')}`],
    [],
    ['RESUMEN POR MATERIAL / PRODUCTO'],
    ['PRODUCTO', 'TOTAL KILOS'],
  ];

  resumen.forEach((r) => {
    data.push([r.producto, r.kilos]);
  });
  data.push(['TOTAL KILOS RESUMEN', totalKilosResumen]);
  data.push([]);
  data.push(['DETALLE DE PESAJES REGISTRADOS (SUPERIOR A ÚLTIMO FOLIO)']);
  data.push(['N°', 'FECHA', 'FOLIO', 'DESCRIPCIÓN PRODUCTO', 'PATENTE', 'PROVEEDOR', 'CIUDAD', 'KILOS']);

  detalle.forEach((d, idx) => {
    data.push([idx + 1, d.fecha, d.folio, d.descripcion, d.patente, d.proveedor, d.ciudad, d.kilos]);
  });

  data.push([]);
  data.push(['', '', '', '', '', '', 'TOTAL KILOS DETALLE', totalKilosDetalle]);

  const ws = XLSX.utils.aoa_to_sheet(data);
  ws['!cols'] = [
    { wch: 6 },
    { wch: 14 },
    { wch: 12 },
    { wch: 35 },
    { wch: 12 },
    { wch: 32 },
    { wch: 18 },
    { wch: 16 },
  ];

  XLSX.utils.book_append_sheet(wb, ws, 'Informe JCRAMM');
  downloadWorkbook(wb, `Informe_JCRAMM_${Date.now()}.xlsx`);
}

/**
 * Replaces EXP_EXCEL_GRUPO_MAT_PL
 */
export function exportarExcelGruposMaterial(
  fechaDesde: string,
  fechaHasta: string,
  gruposPlacilla: { grupo: string; kilos: number }[],
  gruposSantiago: { grupo: string; kilos: number }[],
  gruposTalcahuano: { grupo: string; kilos: number }[]
) {
  const wb = XLSX.utils.book_new();

  const data: (string | number)[][] = [
    ['CAMBIASO HERMANOS S.A.C. - RESUMEN CONSOLIDADO POR GRUPO DE MATERIAL'],
    [`Periodo: ${fechaDesde} al ${fechaHasta}`],
    [],
    ['PLANTA PLACILLA'],
    ['N°', 'GRUPO DE MATERIAL', 'TOTAL KILOS'],
  ];

  let totPl = 0;
  gruposPlacilla.forEach((g, idx) => {
    data.push([idx + 1, g.grupo, g.kilos]);
    totPl += g.kilos;
  });
  data.push(['', 'TOTAL PLACILLA', totPl]);
  data.push([]);

  data.push(['PLANTA SANTIAGO']);
  data.push(['N°', 'GRUPO DE MATERIAL', 'TOTAL KILOS']);
  let totStgo = 0;
  gruposSantiago.forEach((g, idx) => {
    data.push([idx + 1, g.grupo, g.kilos]);
    totStgo += g.kilos;
  });
  data.push(['', 'TOTAL SANTIAGO', totStgo]);
  data.push([]);

  data.push(['PLANTA TALCAHUANO']);
  data.push(['N°', 'GRUPO DE MATERIAL', 'TOTAL KILOS']);
  let totThno = 0;
  gruposTalcahuano.forEach((g, idx) => {
    data.push([idx + 1, g.grupo, g.kilos]);
    totThno += g.kilos;
  });
  data.push(['', 'TOTAL TALCAHUANO', totThno]);
  data.push([]);
  data.push(['', 'GRAN TOTAL TODAS LAS PLANTAS', totPl + totStgo + totThno]);

  const ws = XLSX.utils.aoa_to_sheet(data);
  ws['!cols'] = [{ wch: 6 }, { wch: 30 }, { wch: 18 }];
  XLSX.utils.book_append_sheet(wb, ws, 'Grupos Material');
  downloadWorkbook(wb, `Resumen_Grupos_Material_${fechaDesde}_al_${fechaHasta}.xlsx`);
}
