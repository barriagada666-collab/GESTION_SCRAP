export type Planta = 'PLACILLA' | 'SANTIAGO' | 'TALCAHUANO';

export type TipoPesaje = 
  | 'Recepcion de carga'
  | 'Despacho de Carga'
  | 'Movimiento Interno';

export interface PesajeRegistro {
  folio: number;
  planta: Planta;
  tipoPesaje: TipoPesaje;
  patente: string;
  pesoBruto: number;
  pesoTara: number;
  pesoNeto: number;
  codProducto: string;
  nomProducto: string;
  rutCliente: string;
  nomCliente: string;
  origen: string;
  destino: string;
  guiaDespacho: string;
  fechaIngreso: string; // YYYY-MM-DD
  horaIngreso: string;  // HH:mm:ss
  fechaSalida: string;  // YYYY-MM-DD
  horaSalida: string;   // HH:mm:ss
  observacion: string;
  grupo: 'SUPER-BASULIP' | 'RECAM' | 'ALTA' | 'MIX' | 'SCRAP' | 'GENERAL' | string;
  esTraslado?: boolean;
}

export interface Cliente {
  rut: string;
  digito?: string;
  nombre: string;
  razonSocial: string;
  ciudad?: string;
  planta: Planta | 'TODAS';
}

export interface Producto {
  codigo: string;
  descripcion: string;
  grupo: 'SUPER-BASULIP' | 'RECAM' | 'ALTA' | 'MIX' | 'SCRAP' | 'GENERAL';
  unidadMedida?: string;
}

export interface PrecioProveedor {
  indice: number;
  planta: 'PLACILLA' | 'SANTIAGO';
  rut: string;
  razonSocial: string;
  codigoProducto: string;
  precio: number;
  fechaActualizacion: string;
}

export interface IndexPrecioPesaje {
  folio: number;
  planta: 'PLACILLA' | 'SANTIAGO';
  precio: number;
  familia: string;
  actualizadoEn?: string;
}

export interface UltimoFolioJCRAMM {
  folio: number;
  fecha: string;
}

export interface FirebirdRemotePesaje {
  flpesaje: number;
  tpesaje: number | string;
  patente: string;
  bruto: number;
  tara: number;
  neto: number;
  producto: string;
  descProducto?: string;
  cliente: string;
  nomCliente?: string;
  origen: string;
  destino: string;
  gdespacho: string;
  fechaing: string;
  horaing: string;
  fechasal: string;
  horasal: string;
  observa1: string;
}

export interface AuditLogEntry {
  id: string;
  timestamp: string;
  usuario: string;
  accion: 'ACTUALIZACION_PRECIO' | 'CREACION_PRECIO' | 'IMPORTACION_FIREBIRD' | 'EJECUCION_PA' | 'PURGA_TABLAS' | 'EXPORTACION_EXCEL';
  detalle: string;
  entidad: string;
  registroId?: string | number;
}
