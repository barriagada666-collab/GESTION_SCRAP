using System.Globalization;
using ClosedXML.Excel;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Export;

/// <summary>
/// Exportador nativo .xlsx con ClosedXML estilizado según la identidad corporativa Cambiaso,
/// con soporte para informes estándar, selecciones específicas, análisis personalizados y bitácora de importaciones.
/// </summary>
public sealed class ClosedXmlExcelExportService : IExcelExportService
{
    private static readonly XLColor PetrolDark = XLColor.FromHtml("#0B3C5D");
    private static readonly XLColor GoldAccent = XLColor.FromHtml("#C9A46E");
    private static readonly XLColor TealAccent = XLColor.FromHtml("#0891B2");
    private static readonly XLColor ZebraFill = XLColor.FromHtml("#F8FAFC");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#E2E8F0");

    public string ExportInformeResumen(
        string filePath,
        string tituloPeriodo,
        IReadOnlyList<(string Planta, int Viajes, long Kilos)> porPlanta,
        IReadOnlyList<(string Grupo, int Viajes, long Kilos, double Porcentaje)> porGrupo,
        IReadOnlyList<(string Rut, string Proveedor, int Viajes, long Kilos, double Porcentaje)> topProveedores,
        IReadOnlyList<Pesaje> detallePesajes)
    {
        using XLWorkbook wb = new();

        IXLWorksheet wsRes = wb.Worksheets.Add("Resumen Ejecutivo");
        WriteBanner(wsRes, "CAMBIASO HNOS. · INFORME RESUMEN DE PESAJES Y SCRAP", $"Período: {tituloPeriodo}", 4);

        int r = 5;
        WriteSectionHeader(wsRes, r++, "DISTRIBUCIÓN POR PLANTA", ["Planta", "Viajes", "Kilos Netos", "% Participación"], TealAccent);
        r++;
        long totPl = porPlanta.Sum(x => x.Kilos);
        foreach ((string Planta, int Viajes, long Kilos) p in porPlanta)
        {
            double pct = totPl > 0 ? p.Kilos * 100.0 / totPl : 0.0;
            WriteRowValues(wsRes, r, [p.Planta, p.Viajes, p.Kilos, pct / 100.0], isZebra: false);
            wsRes.Cell(r, 4).Style.NumberFormat.Format = "0.0%";
            r++;
        }

        r += 2;
        WriteSectionHeader(wsRes, r++, "TOP PROVEEDORES DEL PERÍODO", ["RUT / Razón Social", "Viajes", "Kilos Netos", "% Participación"], PetrolDark);
        r++;
        foreach ((string Rut, string Proveedor, int Viajes, long Kilos, double Porcentaje) tp in topProveedores)
        {
            string label = string.IsNullOrWhiteSpace(tp.Rut) ? tp.Proveedor : $"{tp.Rut} · {tp.Proveedor}";
            WriteRowValues(wsRes, r, [label, tp.Viajes, tp.Kilos, tp.Porcentaje / 100.0], isZebra: false);
            wsRes.Cell(r, 4).Style.NumberFormat.Format = "0.0%";
            r++;
        }

        IXLWorksheet wsGrp = wb.Worksheets.Add("Grupos Material");
        WriteBanner(wsGrp, "CONSOLIDADO POR GRUPO DE MATERIAL", $"Período: {tituloPeriodo}", 4);
        WriteSectionHeader(wsGrp, 5, "GRUPOS DE MATERIAL", ["Grupo Padre", "Viajes", "Kilos Netos", "%"], PetrolDark);
        int gr = 7;
        foreach ((string Grupo, int Viajes, long Kilos, double Porcentaje) g in porGrupo)
        {
            WriteRowValues(wsGrp, gr, [g.Grupo, g.Viajes, g.Kilos, g.Porcentaje / 100.0], isZebra: (gr - 7) % 2 == 1);
            wsGrp.Cell(gr, 4).Style.NumberFormat.Format = "0.0%";
            gr++;
        }

        IXLWorksheet wsDet = wb.Worksheets.Add("Movimientos");
        WriteBanner(wsDet, "DETALLE DE MOVIMIENTOS DEL PERÍODO", $"Período: {tituloPeriodo}", 9);
        WriteHeaders(wsDet, 5, ["Folio", "Planta", "Fecha", "Patente", "RUT", "Proveedor", "Código", "Producto", "Neto Kg"]);
        int dr = 6;
        foreach (Pesaje p in detallePesajes)
        {
            WriteRowValues(
                wsDet,
                dr,
                [p.Folio, p.Planta.ToShortTitle(), p.FechaIngreso, p.Patente, p.RutCliente, p.NomCliente, p.CodProducto, p.NomProducto, p.PesoNeto],
                isZebra: (dr - 6) % 2 == 1);
            dr++;
        }

        wsRes.Columns().AdjustToContents();
        wsGrp.Columns().AdjustToContents();
        wsDet.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
    }

    public string ExportGrupoMaterialJerarquico(
        string filePath,
        string periodoLabel,
        IReadOnlyList<(int Nivel, string Nombre, string Tipo, string CodigoProducto, int Viajes, long Kilos, double Porcentaje)> nodosPlanos,
        long totalKilos)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Árbol Grupos Material");
        WriteBanner(ws, "CAMBIASO HNOS. · INFORME JERÁRQUICO POR GRUPO DE MATERIAL", periodoLabel, 6);
        WriteHeaders(ws, 5, ["Jerarquía / Nodo", "Nivel / Tipo", "Código Prod.", "Viajes", "Kilos Netos", "% del Total"]);

        int r = 6;
        foreach (var n in nodosPlanos)
        {
            string indent = n.Nivel == 0 ? string.Empty : new string(' ', n.Nivel * 4) + "└─ ";
            WriteRowValues(ws, r, [indent + n.Nombre, n.Tipo, n.CodigoProducto, n.Viajes, n.Kilos, n.Porcentaje / 100.0], isZebra: (r - 6) % 2 == 1);
            ws.Cell(r, 6).Style.NumberFormat.Format = "0.00%";

            if (n.Nivel == 0)
            {
                IXLRange rowRange = ws.Range(r, 1, r, 6);
                rowRange.Style.Font.Bold = true;
                rowRange.Style.Fill.BackgroundColor = TotalFill;
            }

            r++;
        }

        ws.Cell(r + 1, 4).Value = "TOTAL GENERAL:";
        ws.Cell(r + 1, 4).Style.Font.Bold = true;
        ws.Cell(r + 1, 5).Value = totalKilos;
        ws.Cell(r + 1, 5).Style.Font.Bold = true;
        ws.Cell(r + 1, 5).Style.NumberFormat.Format = "#,##0";

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
    }

    public string ExportAnalisisProveedores(
        string filePath,
        string periodoLabel,
        IReadOnlyList<(string Rut, string RazonSocial, string Ciudad, int Viajes, long Kilos, double Participacion, double Acumulado, string ClaseAbc, double PromedioKg, string ProductoTop)> ranking,
        IReadOnlyList<(string Grupo, int Viajes, long Kilos, double Porcentaje)>? desgloseSeleccionado,
        IReadOnlyList<Pesaje>? entregasSeleccionado,
        string? proveedorSeleccionadoNombre)
    {
        using XLWorkbook wb = new();

        IXLWorksheet wsRank = wb.Worksheets.Add("Ranking Pareto ABC");
        WriteBanner(wsRank, "CAMBIASO HNOS. · ANÁLISIS ROBUSTO DE PROVEEDORES (PARETO ABC)", periodoLabel, 10);
        WriteHeaders(wsRank, 5, ["RUT", "Proveedor / Razón Social", "Ciudad", "Clase ABC", "Viajes", "Kilos Netos", "% Partic.", "% Acum.", "Prom. Kg/Viaje", "Grupo Principal"]);

        int r = 6;
        foreach (var item in ranking)
        {
            WriteRowValues(
                wsRank,
                r,
                [item.Rut, item.RazonSocial, item.Ciudad, item.ClaseAbc, item.Viajes, item.Kilos, item.Participacion / 100.0, item.Acumulado / 100.0, item.PromedioKg, item.ProductoTop],
                isZebra: (r - 6) % 2 == 1);

            wsRank.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsRank.Cell(r, 7).Style.NumberFormat.Format = "0.00%";
            wsRank.Cell(r, 8).Style.NumberFormat.Format = "0.00%";
            r++;
        }

        if (!string.IsNullOrWhiteSpace(proveedorSeleccionadoNombre) &&
            desgloseSeleccionado is not null &&
            entregasSeleccionado is not null)
        {
            IXLWorksheet wsDet = wb.Worksheets.Add("Ficha Proveedor");
            WriteBanner(wsDet, $"FICHA PROVEEDOR: {proveedorSeleccionadoNombre}", periodoLabel, 8);
            WriteSectionHeader(wsDet, 5, "MIX DE MATERIALES", ["Grupo Material", "Viajes", "Kilos", "%"], TealAccent);
            int mr = 7;
            foreach (var g in desgloseSeleccionado)
            {
                WriteRowValues(wsDet, mr, [g.Grupo, g.Viajes, g.Kilos, g.Porcentaje / 100.0], isZebra: (mr - 7) % 2 == 1);
                wsDet.Cell(mr, 4).Style.NumberFormat.Format = "0.0%";
                mr++;
            }

            mr += 2;
            WriteHeaders(wsDet, mr++, ["Folio", "Planta", "Fecha", "Patente", "Producto", "Origen", "Bruto", "Neto Kg"]);
            foreach (Pesaje p in entregasSeleccionado)
            {
                WriteRowValues(wsDet, mr, [p.Folio, p.Planta.ToShortTitle(), p.FechaIngreso, p.Patente, p.NomProducto, p.Origen, p.PesoBruto, p.PesoNeto], isZebra: mr % 2 == 1);
                mr++;
            }

            wsDet.Columns().AdjustToContents();
        }

        wsRank.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
    }

    public string ExportAuditLogs(string filePath, IReadOnlyList<AuditLogEntry> logs)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Auditoría");
        WriteBanner(ws, "CAMBIASO HNOS. · BITÁCORA DE AUDITORÍA DEL SISTEMA", $"Exportado: {DateTime.Now:dd/MM/yyyy HH:mm}", 6);
        WriteHeaders(ws, 5, ["ID", "Fecha UTC", "Usuario", "Acción", "Entidad", "Detalle"]);

        int r = 6;
        foreach (AuditLogEntry log in logs)
        {
            WriteRowValues(ws, r, [log.Id, log.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), log.Usuario, log.Accion, log.Entidad, log.Detalle], isZebra: (r - 6) % 2 == 1);
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
    }

    public string ExportImportHistory(string filePath, IReadOnlyList<ImportacionHistorial> history)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Control Importaciones");
        WriteBanner(ws, "CAMBIASO HNOS. · CONTROL Y TRAZABILIDAD DE IMPORTACIONES (ORIGEN -> DESTINO)", $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", 12);
        WriteHeaders(ws, 5, ["ID", "Fecha / Hora", "Usuario", "Tipo Origen", "Desde (Origen Exacto)", "Planta Destino", "Hacia (SQLite)", "Rango", "Leídos", "Nuevos", "Duplicados", "Kilos Importados"]);

        int r = 6;
        foreach (ImportacionHistorial item in history)
        {
            WriteRowValues(
                ws,
                r,
                [
                    item.Id,
                    item.FechaHoraLocal,
                    item.Usuario,
                    item.TipoOrigen,
                    item.OrigenDetalle,
                    item.PlantaDestino,
                    item.DestinoDetalle,
                    item.RangoFiltro,
                    item.LeidosOrigen,
                    item.ImportadosNuevos,
                    item.DuplicadosOmitidos,
                    item.KilosImportados
                ],
                isZebra: (r - 6) % 2 == 1);
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
    }

    public string ExportReportePersonalizado(string filePath, ReportePersonalizadoPayload payload)
    {
        using XLWorkbook wb = new();
        HashSet<string> usedSheetNames = new(StringComparer.OrdinalIgnoreCase);

        if (payload.EstadisticasKpis.Count > 0)
        {
            string kpiSheetName = SanitizeSheetName("KPIs y Estadísticas", usedSheetNames);
            IXLWorksheet wsKpi = wb.Worksheets.Add(kpiSheetName);
            WriteBanner(wsKpi, payload.TituloReporte.ToUpperInvariant(), payload.SubtituloFiltros, 3);
            WriteSectionHeader(wsKpi, 5, "INDICADORES CLAVE Y FICHAS ESTADÍSTICAS", ["Indicador", "Valor", "Contexto / Detalle"], PetrolDark);
            int r = 7;
            foreach ((string Indicador, string Valor, string Detalle) kpi in payload.EstadisticasKpis)
            {
                WriteRowValues(wsKpi, r, [kpi.Indicador, kpi.Valor, kpi.Detalle], isZebra: (r - 7) % 2 == 1);
                wsKpi.Cell(r, 1).Style.Font.Bold = true;
                r++;
            }

            wsKpi.Columns().AdjustToContents();
        }

        foreach (ExportTableSection sec in payload.Secciones)
        {
            string sheetName = SanitizeSheetName(sec.SheetName, usedSheetNames);
            IXLWorksheet ws = wb.Worksheets.Add(sheetName);
            int colCount = Math.Max(3, sec.Headers.Length);
            WriteBanner(ws, payload.TituloReporte.ToUpperInvariant(), $"{sec.SectionTitle} · {payload.SubtituloFiltros}", colCount);
            WriteHeaders(ws, 5, sec.Headers);

            int r = 6;
            foreach (object?[] rowVals in sec.Rows)
            {
                WriteRowValues(ws, r, rowVals, isZebra: (r - 6) % 2 == 1);
                r++;
            }

            ws.Columns().AdjustToContents();
        }

        if (wb.Worksheets.Count == 0)
        {
            IXLWorksheet empty = wb.Worksheets.Add("Sin Datos");
            WriteBanner(empty, payload.TituloReporte, payload.SubtituloFiltros, 3);
            empty.Cell(5, 1).Value = "No hubo filas o secciones seleccionadas para exportar.";
        }

        wb.SaveAs(filePath);
        return filePath;
    }

    private static string SanitizeSheetName(string rawName, HashSet<string> used)
    {
        char[] invalid = ['\\', '/', '*', '?', ':', '[', ']'];
        string cleaned = new([.. rawName.Where(c => !invalid.Contains(c))]);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Hoja";
        }

        if (cleaned.Length > 28)
        {
            cleaned = cleaned[..28];
        }

        string candidate = cleaned;
        int idx = 2;
        while (!used.Add(candidate))
        {
            string suffix = $" ({idx++})";
            int maxBase = Math.Max(1, 31 - suffix.Length);
            candidate = (cleaned.Length > maxBase ? cleaned[..maxBase] : cleaned) + suffix;
        }

        return candidate;
    }

    private static void WriteRowValues(IXLWorksheet ws, int row, object?[] values, bool isZebra)
    {
        for (int i = 0; i < values.Length; i++)
        {
            IXLCell cell = ws.Cell(row, i + 1);
            object? val = values[i];
            switch (val)
            {
                case null:
                    cell.Value = string.Empty;
                    break;
                case DateOnly dOnly:
                    cell.Value = dOnly.ToDateTime(TimeOnly.MinValue);
                    cell.Style.DateFormat.Format = "dd-mm-yyyy";
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    cell.Style.DateFormat.Format = "dd-mm-yyyy HH:mm";
                    break;
                case int iv:
                    cell.Value = iv;
                    cell.Style.NumberFormat.Format = "#,##0";
                    break;
                case long lv:
                    cell.Value = lv;
                    cell.Style.NumberFormat.Format = "#,##0";
                    break;
                case double dv:
                    cell.Value = dv;
                    cell.Style.NumberFormat.Format = "#,##0.0";
                    break;
                case decimal mv:
                    cell.Value = mv;
                    cell.Style.NumberFormat.Format = "#,##0.0";
                    break;
                default:
                    cell.Value = val.ToString() ?? string.Empty;
                    break;
            }
        }

        if (isZebra && values.Length > 0)
        {
            ws.Range(row, 1, row, values.Length).Style.Fill.BackgroundColor = ZebraFill;
        }
    }

    private static void WriteBanner(IXLWorksheet ws, string title, string subtitle, int spanCols)
    {
        IXLRange titleRange = ws.Range(1, 1, 1, spanCols);
        titleRange.Merge();
        titleRange.Value = title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 13;
        titleRange.Style.Font.FontColor = GoldAccent;
        titleRange.Style.Fill.BackgroundColor = PetrolDark;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        IXLRange subRange = ws.Range(2, 1, 2, spanCols);
        subRange.Merge();
        subRange.Value = subtitle;
        subRange.Style.Font.FontSize = 10;
        subRange.Style.Font.FontColor = XLColor.White;
        subRange.Style.Fill.BackgroundColor = TealAccent;
        ws.Row(2).Height = 20;
    }

    private static void WriteSectionHeader(IXLWorksheet ws, int row, string sectionTitle, string[] headers, XLColor color)
    {
        ws.Cell(row, 1).Value = sectionTitle;
        ws.Range(row, 1, row, headers.Length).Merge().Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = color;
        ws.Range(row, 1, row, headers.Length).Style.Font.FontColor = XLColor.White;
        WriteHeaders(ws, row + 1, headers);
    }

    private static void WriteHeaders(IXLWorksheet ws, int row, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            IXLCell c = ws.Cell(row, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = PetrolDark;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }
}
