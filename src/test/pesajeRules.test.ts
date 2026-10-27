import test from 'node:test';
import assert from 'node:assert/strict';
import {
  validateCanUpdatePrice,
  calculateWeightedAverage,
  findTheoreticalPrice,
  findRealPrice,
  isDuplicatePesaje,
  checkProviderPriceExists,
  isDateInRange
} from '../utils/pesajeRules.ts';
import { IndexPrecioPesaje, PesajeRegistro, PrecioProveedor } from '../types/pesajes.ts';

test('validateCanUpdatePrice blocks update when not in real prices mode', () => {
  const resultFalse = validateCanUpdatePrice(false);
  assert.equal(resultFalse.allowed, false);
  assert.match(resultFalse.message, /precios reales/);

  const resultTrue = validateCanUpdatePrice(true);
  assert.equal(resultTrue.allowed, true);
});

test('calculateWeightedAverage calculates properly and avoids division by zero', () => {
  assert.equal(calculateWeightedAverage(0, 0), 0);
  assert.equal(calculateWeightedAverage(500000, 2000), 250);
  assert.equal(calculateWeightedAverage(100000, 300), 333.33);
});

test('findTheoreticalPrice retrieves registered price for client and product', () => {
  const samplePrecios: PrecioProveedor[] = [
    {
      indice: 1,
      planta: 'PLACILLA',
      rut: '76.218.764-5',
      razonSocial: 'Sociedad Agricola Galea Ltda.',
      codigoProducto: 'PRD-01',
      precio: 145,
      fechaActualizacion: '2026-01-10'
    },
    {
      indice: 2,
      planta: 'SANTIAGO',
      rut: '76.218.764-5',
      razonSocial: 'Sociedad Agricola Galea Ltda.',
      codigoProducto: 'PRD-01',
      precio: 160,
      fechaActualizacion: '2026-01-10'
    }
  ];

  assert.equal(findTheoreticalPrice(samplePrecios, 'PLACILLA', '76218764-5', 'prd-01'), 145);
  assert.equal(findTheoreticalPrice(samplePrecios, 'SANTIAGO', '76218764-5', 'PRD-01'), 160);
  assert.equal(findTheoreticalPrice(samplePrecios, 'PLACILLA', '99999999-9', 'PRD-01'), 0);
});

test('findRealPrice retrieves indexed real price by folio', () => {
  const sampleIndex: IndexPrecioPesaje[] = [
    { folio: 1001, planta: 'PLACILLA', precio: 155, familia: 'RECAM' },
    { folio: 2001, planta: 'SANTIAGO', precio: 170, familia: 'ALTA' }
  ];

  assert.equal(findRealPrice(sampleIndex, 'PLACILLA', 1001), 155);
  assert.equal(findRealPrice(sampleIndex, 'SANTIAGO', 2001), 170);
  assert.equal(findRealPrice(sampleIndex, 'PLACILLA', 9999), 0);
});

test('isDuplicatePesaje detects duplicate folios and handles Talcahuano time criteria', () => {
  const existing: PesajeRegistro[] = [
    {
      folio: 501,
      planta: 'PLACILLA',
      tipoPesaje: 'Recepcion de carga',
      patente: 'AA1000',
      pesoBruto: 20000,
      pesoTara: 10000,
      pesoNeto: 10000,
      codProducto: 'P01',
      nomProducto: 'Scrap',
      rutCliente: '11111111-1',
      nomCliente: 'Test',
      origen: 'O',
      destino: 'D',
      guiaDespacho: '123',
      fechaIngreso: '2026-02-01',
      horaIngreso: '10:00:00',
      fechaSalida: '2026-02-01',
      horaSalida: '10:30:00',
      observacion: '',
      grupo: 'RECAM'
    },
    {
      folio: 601,
      planta: 'TALCAHUANO',
      tipoPesaje: 'Recepcion de carga',
      patente: 'BB2000',
      pesoBruto: 15000,
      pesoTara: 5000,
      pesoNeto: 10000,
      codProducto: 'P02',
      nomProducto: 'Scrap Thno',
      rutCliente: '22222222-2',
      nomCliente: 'Test Thno',
      origen: 'O',
      destino: 'D',
      guiaDespacho: '456',
      fechaIngreso: '2026-02-01',
      horaIngreso: '11:15:00',
      fechaSalida: '2026-02-01',
      horaSalida: '11:45:00',
      observacion: '',
      grupo: 'ALTA'
    }
  ];

  assert.equal(isDuplicatePesaje(existing, 501, 'PLACILLA'), true);
  assert.equal(isDuplicatePesaje(existing, 502, 'PLACILLA'), false);
  // Talcahuano checks folio AND hora_ingreso
  assert.equal(isDuplicatePesaje(existing, 601, 'TALCAHUANO', '11:15:00'), true);
  assert.equal(isDuplicatePesaje(existing, 601, 'TALCAHUANO', '12:00:00'), false);
});

test('checkProviderPriceExists detects duplicates', () => {
  const precios: PrecioProveedor[] = [
    {
      indice: 1,
      planta: 'PLACILLA',
      rut: '12345678-9',
      razonSocial: 'Prov 1',
      codigoProducto: 'C-01',
      precio: 100,
      fechaActualizacion: '2026-01-01'
    }
  ];

  assert.equal(checkProviderPriceExists(precios, 'PLACILLA', '12345678-9', 'C-01'), true);
  assert.equal(checkProviderPriceExists(precios, 'PLACILLA', '12345678-9', 'C-02'), false);
  assert.equal(checkProviderPriceExists(precios, 'SANTIAGO', '12345678-9', 'C-01'), false);
});

test('isDateInRange validates dates correctly', () => {
  assert.equal(isDateInRange('2026-05-15', '2026-05-01', '2026-05-31'), true);
  assert.equal(isDateInRange('2026-06-01', '2026-05-01', '2026-05-31'), false);
  assert.equal(isDateInRange('2026-04-30', '2026-05-01', '2026-05-31'), false);
});
