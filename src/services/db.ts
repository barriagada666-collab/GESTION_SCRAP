import {
  Cliente,
  IndexPrecioPesaje,
  PesajeRegistro,
  Planta,
  PrecioProveedor,
  Producto,
  UltimoFolioJCRAMM,
  AuditLogEntry,
} from '../types/pesajes';

const STORAGE_KEY_PESAJES = 'pcambiaso_pesajes_v2';
const STORAGE_KEY_PRECIOS = 'pcambiaso_precios_prov_v2';
const STORAGE_KEY_INDEX = 'pcambiaso_index_precios_v2';
const STORAGE_KEY_LAST_FOLIO = 'pcambiaso_last_folio_v2';
const STORAGE_KEY_AUDIT = 'pcambiaso_audit_v2';

export const SEED_CLIENTES: Cliente[] = [
  { rut: '76218764', digito: '5', nombre: 'Sociedad Agricola Galea Ltda.', razonSocial: 'Sociedad Agricola Galea Ltda.', ciudad: 'Valparaíso', planta: 'PLACILLA' },
  { rut: '96512340', digito: '8', nombre: 'Pesquera del Pacífico S.A.', razonSocial: 'Pesquera del Pacífico S.A.', ciudad: 'Talcahuano', planta: 'TALCAHUANO' },
  { rut: '96874520', digito: '1', nombre: 'Agrosuper Alimentos Ltda.', razonSocial: 'Agrosuper Alimentos Ltda.', ciudad: 'Rancagua', planta: 'PLACILLA' },
  { rut: '77321450', digito: 'K', nombre: 'Cencosud Retail S.A. (Jumbo / Santa Isabel)', razonSocial: 'Cencosud Retail S.A.', ciudad: 'Santiago', planta: 'SANTIAGO' },
  { rut: '76134890', digito: '2', nombre: 'Walmart Chile S.A. (Lider / Express)', razonSocial: 'Walmart Chile S.A.', ciudad: 'Santiago', planta: 'SANTIAGO' },
  { rut: '78456120', digito: '3', nombre: 'SMU S.A. (Unimarc)', razonSocial: 'SMU S.A.', ciudad: 'Santiago', planta: 'SANTIAGO' },
  { rut: '81234900', digito: '4', nombre: 'Agrícola San Isidro SpA', razonSocial: 'Agrícola San Isidro SpA', ciudad: 'Quillota', planta: 'PLACILLA' },
  { rut: '79845120', digito: '6', nombre: 'Christian Martino y Cía Ltda.', razonSocial: 'Christian Martino y Cía Ltda.', ciudad: 'Concepción', planta: 'SANTIAGO' },
  { rut: '12456789', digito: 'K', nombre: 'Jaqueline Johnson E.I.R.L.', razonSocial: 'Jaqueline Johnson E.I.R.L.', ciudad: 'Viña del Mar', planta: 'PLACILLA' },
  { rut: '76543210', digito: '9', nombre: 'Reciclajes Industriales JCRAMM Ltda.', razonSocial: 'Reciclajes Industriales JCRAMM Ltda.', ciudad: 'Santiago', planta: 'TODAS' },
  { rut: '96781200', digito: '7', nombre: 'Bodega Central Scrap Cambiaso Stgo', razonSocial: 'Cambiaso Hermanos S.A.C.', ciudad: 'Santiago', planta: 'PLACILLA' },
  { rut: '96781201', digito: '8', nombre: 'Empresas Carozzi S.A.', razonSocial: 'Empresas Carozzi S.A.', ciudad: 'Nos', planta: 'SANTIAGO' },
];

export const SEED_PRODUCTOS: Producto[] = [
  { codigo: 'PRD-01', descripcion: 'POLIETILENO RECICLADO ALTA DENSIDAD (ALTA)', grupo: 'ALTA' },
  { codigo: 'PRD-02', descripcion: 'POLIETILENO RECAM TRANSPARENTE INDUSTRIAL', grupo: 'RECAM' },
  { codigo: 'PRD-03', descripcion: 'SUPER-BASULIP RESINAS SELECCIONADAS', grupo: 'SUPER-BASULIP' },
  { codigo: 'PRD-04', descripcion: 'FILM STRETCH MIX SUPERMERCADO', grupo: 'MIX' },
  { codigo: 'PRD-05', descripcion: 'SCRAP DESCARTE POLIETILENO MOLIDO', grupo: 'SCRAP' },
  { codigo: 'PRD-06', descripcion: 'POLIETILENO ALTA COLOR SANTIAGO', grupo: 'ALTA' },
  { codigo: 'PRD-07', descripcion: 'CAJAS PLASTICAS AGRÍCOLAS MOLIDAS', grupo: 'RECAM' },
  { codigo: 'PRD-08', descripcion: 'RESIDUOS AGROINDUSTRIALES PESQUERA', grupo: 'GENERAL' },
];

export const SEED_PRECIOS_PROV: PrecioProveedor[] = [
  { indice: 1, planta: 'PLACILLA', rut: '76218764', razonSocial: 'Sociedad Agricola Galea Ltda.', codigoProducto: 'PRD-01', precio: 340, fechaActualizacion: '2026-02-15' },
  { indice: 2, planta: 'PLACILLA', rut: '76218764', razonSocial: 'Sociedad Agricola Galea Ltda.', codigoProducto: 'PRD-02', precio: 410, fechaActualizacion: '2026-02-15' },
  { indice: 3, planta: 'PLACILLA', rut: '76218764', razonSocial: 'Sociedad Agricola Galea Ltda.', codigoProducto: 'PRD-03', precio: 520, fechaActualizacion: '2026-02-15' },
  { indice: 4, planta: 'SANTIAGO', rut: '77321450', razonSocial: 'Cencosud Retail S.A.', codigoProducto: 'PRD-04', precio: 290, fechaActualizacion: '2026-02-20' },
  { indice: 5, planta: 'SANTIAGO', rut: '76134890', razonSocial: 'Walmart Chile S.A.', codigoProducto: 'PRD-04', precio: 310, fechaActualizacion: '2026-02-20' },
  { indice: 6, planta: 'SANTIAGO', rut: '76543210', razonSocial: 'Reciclajes Industriales JCRAMM Ltda.', codigoProducto: 'PRD-02', precio: 430, fechaActualizacion: '2026-02-18' },
  { indice: 7, planta: 'SANTIAGO', rut: '76543210', razonSocial: 'Reciclajes Industriales JCRAMM Ltda.', codigoProducto: 'PRD-03', precio: 550, fechaActualizacion: '2026-02-18' },
  { indice: 8, planta: 'PLACILLA', rut: '96874520', razonSocial: 'Agrosuper Alimentos Ltda.', codigoProducto: 'PRD-01', precio: 330, fechaActualizacion: '2026-02-12' },
  { indice: 9, planta: 'PLACILLA', rut: '12456789', razonSocial: 'Jaqueline Johnson E.I.R.L.', codigoProducto: 'PRD-02', precio: 390, fechaActualizacion: '2026-02-14' },
];

export const SEED_INDEX_PRECIOS: IndexPrecioPesaje[] = [
  { folio: 10420, planta: 'PLACILLA', precio: 350, familia: 'ALTA', actualizadoEn: '2026-02-16' },
  { folio: 10421, planta: 'PLACILLA', precio: 420, familia: 'RECAM', actualizadoEn: '2026-02-16' },
  { folio: 10422, planta: 'PLACILLA', precio: 535, familia: 'SUPER-BASULIP', actualizadoEn: '2026-02-16' },
  { folio: 20550, planta: 'SANTIAGO', precio: 300, familia: 'MIX', actualizadoEn: '2026-02-18' },
  { folio: 20551, planta: 'SANTIAGO', precio: 440, familia: 'RECAM', actualizadoEn: '2026-02-18' },
  { folio: 20552, planta: 'SANTIAGO', precio: 560, familia: 'SUPER-BASULIP', actualizadoEn: '2026-02-18' },
];

export const SEED_PESAJES: PesajeRegistro[] = [
  // Placilla Compras
  {
    folio: 10420,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'HR-8921',
    pesoBruto: 28450,
    pesoTara: 14200,
    pesoNeto: 14250,
    codProducto: 'PRD-01',
    nomProducto: 'POLIETILENO RECICLADO ALTA DENSIDAD (ALTA)',
    rutCliente: '76218764',
    nomCliente: 'Sociedad Agricola Galea Ltda.',
    origen: 'Valparaíso',
    destino: 'Planta Placilla',
    guiaDespacho: 'GD-8841',
    fechaIngreso: '2026-02-23',
    horaIngreso: '08:45:00',
    fechaSalida: '2026-02-23',
    horaSalida: '09:20:00',
    observacion: 'Descarga completa tolva 2',
    grupo: 'ALTA',
  },
  {
    folio: 10421,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'KJ-3312',
    pesoBruto: 31200,
    pesoTara: 13500,
    pesoNeto: 17700,
    codProducto: 'PRD-02',
    nomProducto: 'POLIETILENO RECAM TRANSPARENTE INDUSTRIAL',
    rutCliente: '76218764',
    nomCliente: 'Sociedad Agricola Galea Ltda.',
    origen: 'Casablanca',
    destino: 'Planta Placilla',
    guiaDespacho: 'GD-8845',
    fechaIngreso: '2026-02-23',
    horaIngreso: '10:10:00',
    fechaSalida: '2026-02-23',
    horaSalida: '10:55:00',
    observacion: 'Material limpio sin humedad',
    grupo: 'RECAM',
  },
  {
    folio: 10422,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'PF-4490',
    pesoBruto: 25800,
    pesoTara: 12100,
    pesoNeto: 13700,
    codProducto: 'PRD-03',
    nomProducto: 'SUPER-BASULIP RESINAS SELECCIONADAS',
    rutCliente: '96874520',
    nomCliente: 'Agrosuper Alimentos Ltda.',
    origen: 'Rancagua',
    destino: 'Planta Placilla',
    guiaDespacho: 'AG-9912',
    fechaIngreso: '2026-02-24',
    horaIngreso: '11:30:00',
    fechaSalida: '2026-02-24',
    horaSalida: '12:15:00',
    observacion: 'Certificado de calidad adjunto',
    grupo: 'SUPER-BASULIP',
  },
  {
    folio: 10423,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'LP-7714',
    pesoBruto: 22400,
    pesoTara: 11000,
    pesoNeto: 11400,
    codProducto: 'PRD-04',
    nomProducto: 'FILM STRETCH MIX SUPERMERCADO',
    rutCliente: '12456789',
    nomCliente: 'Jaqueline Johnson E.I.R.L.',
    origen: 'Viña del Mar',
    destino: 'Planta Placilla',
    guiaDespacho: 'JJ-103',
    fechaIngreso: '2026-02-25',
    horaIngreso: '14:20:00',
    fechaSalida: '2026-02-25',
    horaSalida: '15:05:00',
    observacion: 'Fardos compactados',
    grupo: 'MIX',
  },
  // Placilla Traslados (Recepcion desde Bodega Santiago)
  {
    folio: 10425,
    planta: 'PLACILLA',
    tipoPesaje: 'Movimiento Interno',
    patente: 'TR-5520',
    pesoBruto: 32000,
    pesoTara: 12500,
    pesoNeto: 19500,
    codProducto: 'PRD-05',
    nomProducto: 'SCRAP DESCARTE POLIETILENO MOLIDO',
    rutCliente: '96781200',
    nomCliente: 'Bodega Central Scrap Cambiaso Stgo',
    origen: 'Bodega Santiago',
    destino: 'Placilla Silos',
    guiaDespacho: 'TRAS-401',
    fechaIngreso: '2026-02-24',
    horaIngreso: '16:00:00',
    fechaSalida: '2026-02-24',
    horaSalida: '16:45:00',
    observacion: 'Traslado interno Scrap Santiago a Placilla',
    grupo: 'SCRAP',
    esTraslado: true,
  },

  // Santiago Compras
  {
    folio: 20550,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'GG-9012',
    pesoBruto: 34100,
    pesoTara: 15100,
    pesoNeto: 19000,
    codProducto: 'PRD-04',
    nomProducto: 'FILM STRETCH MIX SUPERMERCADO',
    rutCliente: '77321450',
    nomCliente: 'Cencosud Retail S.A. (Jumbo / Santa Isabel)',
    origen: 'Centro Distribución Lo Aguirre',
    destino: 'Planta Santiago',
    guiaDespacho: 'CEN-55120',
    fechaIngreso: '2026-02-23',
    horaIngreso: '07:30:00',
    fechaSalida: '2026-02-23',
    horaSalida: '08:25:00',
    observacion: 'Retail Stgo turno mañana',
    grupo: 'MIX',
  },
  {
    folio: 20551,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'WK-1288',
    pesoBruto: 29500,
    pesoTara: 13000,
    pesoNeto: 16500,
    codProducto: 'PRD-02',
    nomProducto: 'POLIETILENO RECAM TRANSPARENTE INDUSTRIAL',
    rutCliente: '76134890',
    nomCliente: 'Walmart Chile S.A. (Lider / Express)',
    origen: 'CD El Peñón San Bernardo',
    destino: 'Planta Santiago',
    guiaDespacho: 'WAL-901',
    fechaIngreso: '2026-02-24',
    horaIngreso: '09:15:00',
    fechaSalida: '2026-02-24',
    horaSalida: '10:00:00',
    observacion: 'Retail Empresas fardos blancos',
    grupo: 'RECAM',
  },
  {
    folio: 20552,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'CC-8822',
    pesoBruto: 27900,
    pesoTara: 12800,
    pesoNeto: 15100,
    codProducto: 'PRD-03',
    nomProducto: 'SUPER-BASULIP RESINAS SELECCIONADAS',
    rutCliente: '76543210',
    nomCliente: 'Reciclajes Industriales JCRAMM Ltda.',
    origen: 'Quilicura',
    destino: 'Planta Santiago',
    guiaDespacho: 'JC-441',
    fechaIngreso: '2026-02-25',
    horaIngreso: '11:40:00',
    fechaSalida: '2026-02-25',
    horaSalida: '12:30:00',
    observacion: 'Pesaje directo balanza 1',
    grupo: 'SUPER-BASULIP',
  },
  {
    folio: 20553,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'BB-6631',
    pesoBruto: 26000,
    pesoTara: 13200,
    pesoNeto: 12800,
    codProducto: 'PRD-06',
    nomProducto: 'POLIETILENO ALTA COLOR SANTIAGO',
    rutCliente: '79845120',
    nomCliente: 'Christian Martino y Cía Ltda.',
    origen: 'Concepción (Regiones)',
    destino: 'Planta Santiago',
    guiaDespacho: 'CM-220',
    fechaIngreso: '2026-02-25',
    horaIngreso: '13:50:00',
    fechaSalida: '2026-02-25',
    horaSalida: '14:40:00',
    observacion: 'Transporte regional consolidado',
    grupo: 'ALTA',
  },
  // Santiago Traslados Scrap
  {
    folio: 20554,
    planta: 'SANTIAGO',
    tipoPesaje: 'Despacho de Carga',
    patente: 'TS-9911',
    pesoBruto: 35000,
    pesoTara: 14000,
    pesoNeto: 21000,
    codProducto: 'PRD-05',
    nomProducto: 'SCRAP DESCARTE POLIETILENO MOLIDO',
    rutCliente: '96781200',
    nomCliente: 'Bodega Central Scrap Cambiaso Stgo',
    origen: 'Planta Santiago',
    destino: 'Placilla Valparaíso',
    guiaDespacho: 'TRAS-ST-101',
    fechaIngreso: '2026-02-24',
    horaIngreso: '15:00:00',
    fechaSalida: '2026-02-24',
    horaSalida: '15:45:00',
    observacion: 'Traslado Scrap Santiago hacia Placilla',
    grupo: 'SCRAP',
    esTraslado: true,
  },

  // Talcahuano Compras
  {
    folio: 30101,
    planta: 'TALCAHUANO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'TK-4412',
    pesoBruto: 36200,
    pesoTara: 14200,
    pesoNeto: 22000,
    codProducto: 'PRD-08',
    nomProducto: 'RESIDUOS AGROINDUSTRIALES PESQUERA',
    rutCliente: '96512340',
    nomCliente: 'Pesquera del Pacífico S.A.',
    origen: 'San Vicente Puerto',
    destino: 'Bodega Thno',
    guiaDespacho: 'PESQ-881',
    fechaIngreso: '2026-02-23',
    horaIngreso: '08:15:00',
    fechaSalida: '2026-02-23',
    horaSalida: '09:00:00',
    observacion: 'Pesaje recepcionado desde terminal pesquero',
    grupo: 'GENERAL',
  },
  {
    folio: 30102,
    planta: 'TALCAHUANO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'WZ-7709',
    pesoBruto: 28900,
    pesoTara: 13100,
    pesoNeto: 15800,
    codProducto: 'PRD-01',
    nomProducto: 'POLIETILENO RECICLADO ALTA DENSIDAD (ALTA)',
    rutCliente: '96512340',
    nomCliente: 'Pesquera del Pacífico S.A.',
    origen: 'Talcahuano',
    destino: 'Bodega Thno',
    guiaDespacho: 'PESQ-882',
    fechaIngreso: '2026-02-24',
    horaIngreso: '10:45:00',
    fechaSalida: '2026-02-24',
    horaSalida: '11:35:00',
    observacion: 'Material lavado seco',
    grupo: 'ALTA',
  },
  {
    folio: 30103,
    planta: 'TALCAHUANO',
    tipoPesaje: 'Despacho de Carga',
    patente: 'TH-9022',
    pesoBruto: 33400,
    pesoTara: 13900,
    pesoNeto: 19500,
    codProducto: 'PRD-05',
    nomProducto: 'SCRAP DESCARTE POLIETILENO MOLIDO',
    rutCliente: '96781200',
    nomCliente: 'Bodega Central Scrap Cambiaso Stgo',
    origen: 'Bodega Talcahuano',
    destino: 'Santiago Bodega Central',
    guiaDespacho: 'TRAS-TH-05',
    fechaIngreso: '2026-02-25',
    horaIngreso: '14:15:00',
    fechaSalida: '2026-02-25',
    horaSalida: '15:10:00',
    observacion: 'Traslado a Santiago',
    grupo: 'SCRAP',
    esTraslado: true,
  },

  // Mes Anterior (Enero 2026) para comparativa histórica
  {
    folio: 9810,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'HR-8921',
    pesoBruto: 27000,
    pesoTara: 14000,
    pesoNeto: 13000,
    codProducto: 'PRD-01',
    nomProducto: 'POLIETILENO RECICLADO ALTA DENSIDAD (ALTA)',
    rutCliente: '76218764',
    nomCliente: 'Sociedad Agricola Galea Ltda.',
    origen: 'Valparaíso',
    destino: 'Planta Placilla',
    guiaDespacho: 'GD-7710',
    fechaIngreso: '2026-01-12',
    horaIngreso: '09:00:00',
    fechaSalida: '2026-01-12',
    horaSalida: '09:40:00',
    observacion: 'Histórico mes anterior',
    grupo: 'ALTA',
  },
  {
    folio: 9811,
    planta: 'PLACILLA',
    tipoPesaje: 'Recepcion de carga',
    patente: 'PF-4490',
    pesoBruto: 24500,
    pesoTara: 12500,
    pesoNeto: 12000,
    codProducto: 'PRD-03',
    nomProducto: 'SUPER-BASULIP RESINAS SELECCIONADAS',
    rutCliente: '96874520',
    nomCliente: 'Agrosuper Alimentos Ltda.',
    origen: 'Rancagua',
    destino: 'Planta Placilla',
    guiaDespacho: 'AG-8810',
    fechaIngreso: '2026-01-15',
    horaIngreso: '11:00:00',
    fechaSalida: '2026-01-15',
    horaSalida: '11:45:00',
    observacion: 'Histórico mes anterior',
    grupo: 'SUPER-BASULIP',
  },
  {
    folio: 19400,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'GG-9012',
    pesoBruto: 31000,
    pesoTara: 15000,
    pesoNeto: 16000,
    codProducto: 'PRD-04',
    nomProducto: 'FILM STRETCH MIX SUPERMERCADO',
    rutCliente: '77321450',
    nomCliente: 'Cencosud Retail S.A. (Jumbo / Santa Isabel)',
    origen: 'CD Lo Aguirre',
    destino: 'Planta Santiago',
    guiaDespacho: 'CEN-4401',
    fechaIngreso: '2026-01-18',
    horaIngreso: '08:30:00',
    fechaSalida: '2026-01-18',
    horaSalida: '09:15:00',
    observacion: 'Histórico mes anterior',
    grupo: 'MIX',
  },
  {
    folio: 19401,
    planta: 'SANTIAGO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'WK-1288',
    pesoBruto: 28000,
    pesoTara: 13000,
    pesoNeto: 15000,
    codProducto: 'PRD-02',
    nomProducto: 'POLIETILENO RECAM TRANSPARENTE INDUSTRIAL',
    rutCliente: '76134890',
    nomCliente: 'Walmart Chile S.A. (Lider / Express)',
    origen: 'San Bernardo',
    destino: 'Planta Santiago',
    guiaDespacho: 'WAL-700',
    fechaIngreso: '2026-01-22',
    horaIngreso: '10:15:00',
    fechaSalida: '2026-01-22',
    horaSalida: '11:00:00',
    observacion: 'Histórico mes anterior',
    grupo: 'RECAM',
  },
  {
    folio: 29800,
    planta: 'TALCAHUANO',
    tipoPesaje: 'Recepcion de carga',
    patente: 'TK-4412',
    pesoBruto: 33000,
    pesoTara: 14000,
    pesoNeto: 19000,
    codProducto: 'PRD-08',
    nomProducto: 'RESIDUOS AGROINDUSTRIALES PESQUERA',
    rutCliente: '96512340',
    nomCliente: 'Pesquera del Pacífico S.A.',
    origen: 'San Vicente Puerto',
    destino: 'Bodega Thno',
    guiaDespacho: 'PESQ-770',
    fechaIngreso: '2026-01-25',
    horaIngreso: '09:40:00',
    fechaSalida: '2026-01-25',
    horaSalida: '10:30:00',
    observacion: 'Histórico mes anterior',
    grupo: 'GENERAL',
  },
];

export class PesajesDbService {
  private pesajes: PesajeRegistro[] = [];
  private preciosProveedor: PrecioProveedor[] = [];
  private indexPrecios: IndexPrecioPesaje[] = [];
  private ultimoFolioJCRAMM: UltimoFolioJCRAMM = { folio: 10400, fecha: '2026-02-20' };
  private auditLogs: AuditLogEntry[] = [];

  constructor() {
    this.loadState();
  }

  private loadState() {
    try {
      const p = localStorage.getItem(STORAGE_KEY_PESAJES);
      this.pesajes = p ? JSON.parse(p) : [...SEED_PESAJES];

      const pr = localStorage.getItem(STORAGE_KEY_PRECIOS);
      this.preciosProveedor = pr ? JSON.parse(pr) : [...SEED_PRECIOS_PROV];

      const ip = localStorage.getItem(STORAGE_KEY_INDEX);
      this.indexPrecios = ip ? JSON.parse(ip) : [...SEED_INDEX_PRECIOS];

      const lf = localStorage.getItem(STORAGE_KEY_LAST_FOLIO);
      this.ultimoFolioJCRAMM = lf ? JSON.parse(lf) : { folio: 10400, fecha: '2026-02-20' };

      const au = localStorage.getItem(STORAGE_KEY_AUDIT);
      this.auditLogs = au ? JSON.parse(au) : [
        {
          id: 'aud-1',
          timestamp: new Date().toISOString(),
          usuario: 'OPERADOR_SCADA',
          accion: 'IMPORTACION_FIREBIRD',
          detalle: 'Sistema inicializado con base de datos unificada',
          entidad: 'SISTEMA',
        }
      ];
    } catch {
      this.pesajes = [...SEED_PESAJES];
      this.preciosProveedor = [...SEED_PRECIOS_PROV];
      this.indexPrecios = [...SEED_INDEX_PRECIOS];
      this.ultimoFolioJCRAMM = { folio: 10400, fecha: '2026-02-20' };
      this.auditLogs = [];
    }
  }

  private persist() {
    try {
      localStorage.setItem(STORAGE_KEY_PESAJES, JSON.stringify(this.pesajes));
      localStorage.setItem(STORAGE_KEY_PRECIOS, JSON.stringify(this.preciosProveedor));
      localStorage.setItem(STORAGE_KEY_INDEX, JSON.stringify(this.indexPrecios));
      localStorage.setItem(STORAGE_KEY_LAST_FOLIO, JSON.stringify(this.ultimoFolioJCRAMM));
      localStorage.setItem(STORAGE_KEY_AUDIT, JSON.stringify(this.auditLogs));
    } catch (e) {
      console.warn('LocalStorage error:', e);
    }
  }

  public getPesajes(): PesajeRegistro[] {
    return [...this.pesajes];
  }

  public getClientes(): Cliente[] {
    return [...SEED_CLIENTES];
  }

  public getProductos(): Producto[] {
    return [...SEED_PRODUCTOS];
  }

  public getPreciosProveedor(): PrecioProveedor[] {
    return [...this.preciosProveedor];
  }

  public getIndexPrecios(): IndexPrecioPesaje[] {
    return [...this.indexPrecios];
  }

  public getUltimoFolioJCRAMM(): UltimoFolioJCRAMM {
    return { ...this.ultimoFolioJCRAMM };
  }

  public getAuditLogs(): AuditLogEntry[] {
    return [...this.auditLogs];
  }

  public setUltimoFolioJCRAMM(folio: number, fecha: string, usuario = 'OPERADOR') {
    this.ultimoFolioJCRAMM = { folio, fecha };
    this.auditLogs.unshift({
      id: `aud-${Date.now()}`,
      timestamp: new Date().toISOString(),
      usuario,
      accion: 'EXPORTACION_EXCEL',
      detalle: `Actualizado último folio JCRAMM a ${folio} (${fecha})`,
      entidad: 'ultimo_folio',
      registroId: folio,
    });
    this.persist();
  }

  public addPrecioProveedor(
    planta: 'PLACILLA' | 'SANTIAGO',
    rut: string,
    razonSocial: string,
    codigoProducto: string,
    precio: number,
    usuario = 'OPERADOR'
  ): { success: boolean; message: string } {
    const nextIndice =
      this.preciosProveedor.length > 0
        ? Math.max(...this.preciosProveedor.map((p) => p.indice)) + 1
        : 1;

    const nuevo: PrecioProveedor = {
      indice: nextIndice,
      planta,
      rut,
      razonSocial,
      codigoProducto,
      precio,
      fechaActualizacion: new Date().toISOString().split('T')[0],
    };

    this.preciosProveedor.push(nuevo);
    this.auditLogs.unshift({
      id: `aud-${Date.now()}`,
      timestamp: new Date().toISOString(),
      usuario,
      accion: 'CREACION_PRECIO',
      detalle: `Ingresado precio ${precio} para proveedor ${razonSocial} (${rut}), prod ${codigoProducto} en ${planta}`,
      entidad: `precios_proveedor_${planta.toLowerCase()}`,
      registroId: nextIndice,
    });
    this.persist();
    return { success: true, message: 'Precio ingresado satisfactoriamente.' };
  }

  public updateOrSetIndexPrecio(
    folio: number,
    planta: 'PLACILLA' | 'SANTIAGO',
    precio: number,
    familia: string,
    usuario = 'OPERADOR'
  ): { success: boolean; message: string } {
    const existingIndex = this.indexPrecios.findIndex(
      (ip) => ip.folio === folio && ip.planta === planta
    );

    if (existingIndex >= 0) {
      this.indexPrecios[existingIndex].precio = precio;
      this.indexPrecios[existingIndex].actualizadoEn = new Date().toISOString().split('T')[0];
    } else {
      this.indexPrecios.push({
        folio,
        planta,
        precio,
        familia,
        actualizadoEn: new Date().toISOString().split('T')[0],
      });
    }

    this.auditLogs.unshift({
      id: `aud-${Date.now()}`,
      timestamp: new Date().toISOString(),
      usuario,
      accion: 'ACTUALIZACION_PRECIO',
      detalle: `Actualizado precio real a $${precio} para Folio ${folio} (${planta})`,
      entidad: `index_pesaje_${planta.toLowerCase()}`,
      registroId: folio,
    });
    this.persist();
    return { success: true, message: 'Precio real actualizado correctamente.' };
  }

  public bulkFreezeTheoreticalPrices(
    items: { folio: number; precio: number; familia: string }[],
    planta: 'PLACILLA' | 'SANTIAGO',
    usuario = 'OPERADOR'
  ): number {
    let count = 0;
    for (const item of items) {
      const exists = this.indexPrecios.some((ip) => ip.planta === planta && ip.folio === item.folio);
      if (!exists && item.precio > 0) {
        this.indexPrecios.push({
          folio: item.folio,
          planta,
          precio: item.precio,
          familia: item.familia,
          actualizadoEn: new Date().toISOString().split('T')[0],
        });
        count++;
      }
    }
    if (count > 0) {
      this.auditLogs.unshift({
        id: `aud-${Date.now()}`,
        timestamp: new Date().toISOString(),
        usuario,
        accion: 'ACTUALIZACION_PRECIO',
        detalle: `Congelados masivamente ${count} precios teóricos como reales en ${planta}`,
        entidad: `index_pesaje_${planta.toLowerCase()}`,
      });
      this.persist();
    }
    return count;
  }

  public importPesajesFromFirebird(
    nuevos: PesajeRegistro[],
    planta: Planta,
    usuario = 'OPERADOR'
  ): { importedCount: number; duplicatesCount: number } {
    let imported = 0;
    let duplicates = 0;

    for (const r of nuevos) {
      const isDup =
        planta === 'TALCAHUANO'
          ? this.pesajes.some(
              (p) => p.planta === 'TALCAHUANO' && p.folio === r.folio && p.horaIngreso === r.horaIngreso
            )
          : this.pesajes.some((p) => p.planta === planta && p.folio === r.folio);

      if (isDup) {
        duplicates++;
      } else {
        this.pesajes.push(r);
        imported++;
      }
    }

    if (imported > 0) {
      this.auditLogs.unshift({
        id: `aud-${Date.now()}`,
        timestamp: new Date().toISOString(),
        usuario,
        accion: 'IMPORTACION_FIREBIRD',
        detalle: `Importados ${imported} registros desde Firebird ${planta} (${duplicates} duplicados omitidos)`,
        entidad: `PESAJE_${planta}`,
      });
      this.persist();
    }

    return { importedCount: imported, duplicatesCount: duplicates };
  }

  public purgeAllPesajeTables(usuario = 'ADMIN'): { success: boolean; clearedCount: number } {
    const count = this.pesajes.length;
    this.pesajes = [];
    this.auditLogs.unshift({
      id: `aud-${Date.now()}`,
      timestamp: new Date().toISOString(),
      usuario,
      accion: 'PURGA_TABLAS',
      detalle: `Vaciado total de las 4 tablas de pesajes (${count} registros eliminados)`,
      entidad: 'ALL_PESAJES',
    });
    this.persist();
    return { success: true, clearedCount: count };
  }

  public resetToFactoryDefaults() {
    this.pesajes = [...SEED_PESAJES];
    this.preciosProveedor = [...SEED_PRECIOS_PROV];
    this.indexPrecios = [...SEED_INDEX_PRECIOS];
    this.ultimoFolioJCRAMM = { folio: 10400, fecha: '2026-02-20' };
    this.auditLogs = [
      {
        id: `aud-${Date.now()}`,
        timestamp: new Date().toISOString(),
        usuario: 'SISTEMA',
        accion: 'PURGA_TABLAS',
        detalle: 'Restablecidos datos demostrativos de fábrica',
        entidad: 'SISTEMA',
      }
    ];
    this.persist();
  }
}

export const dbService = new PesajesDbService();
