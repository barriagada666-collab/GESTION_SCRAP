using System.Globalization;
using ClosedXML.Excel;
using GestionScrap.Application.Services;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Export;

/// <summary>
/// Generador nativo de libros Excel (.xlsx) mediante ClosedXML sin dependencia de Office COM.
/// </summary>
public sealed class ClosedXmlExcelExportService : IExcelExportService
{
    private static readonly XLColor HeaderDark = XLColor.FromHtml("#0F172A");
    private static readonly XLColor AccentCyan = XLColor.FromHtml("#0E7490");
    private static readonly XLColor AccentAmber = XLColor.FromHtml("#B45309");
    private static readonly XLColor ZebraFill = XLColor.FromHtml("#F8FAFC");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#E2E8F0");

    public string ExportGrupoMaterial(
        string filePath,
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyList<(string Grupo, long Kilos)> placilla,
        IReadOnlyList<(string Grupo, long Kilos)> santiago,
        IReadOnlyList<(string Grupo, long Kilos)> talcahuano)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Grupos Material");

        WriteBanner(ws, "RESUMEN CONSOLIDADO POR GRUPO DE MATERIAL", $"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}", 3);

        int row = 5;
        row = WritePlantMaterialSection(ws, row, "PLANTA PLACILLA", placilla, AccentCyan);
        row += 2;
        row = WritePlantMaterialSection(ws, row, "PLANTA SANTIAGO", santiago, XLColor.FromHtml("#1D4ED8"));
        row += 2;
        row = WritePlantMaterialSection(ws, row, "PLANTA TALCAHUANO", talcahuano, XLColor.FromHtml("#047857"));
        row += 2;

        long granTotal = placilla.Sum(x => x.Kilos) + santiago.Sum(x => x.Kilos) + talcahuano.Sum(x => x.Kilos);
        ws.Cell(row, 2).Value = "GRAN TOTAL TODAS LAS PLANTAS";
        ws.Cell(row, 3).Value = granTotal;
        StyleTotalRow(ws.Range(row, 1, row, 3), 3);

        FinalizeSheet(ws, 3);
        SaveWorkbook(wb, filePath);
        return filePath;
    }

    public string ExportComprasTraslados(
        string filePath,
        Planta planta,
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyList<(string Codigo, string Descripcion, string RazonSocial, long Kilos)> compras,
        IReadOnlyList<(long Folio, string Patente, string Producto, DateOnly FechaEnt, DateOnly FechaSal, int Bruto, int Tara, int Neto)> traslados,
        long totalCompras,
        long totalTraslados)
    {
        using XLWorkbook wb = new();

        // Hoja 1: Compras
        IXLWorksheet wsCompras = wb.Worksheets.Add($"Compras {planta.ToShortTitle()}");
        WriteBanner(wsCompras, $"INFORME COMPRAS SCRAP - PLANTA {planta.ToDisplayName()}", $"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}", 5);

        string[] hdrCompras = ["N°", "CÓDIGO", "DESCRIPCIÓN PRODUCTO", "RAZÓN SOCIAL / PROVEEDOR", "TOTAL KILOS"];
        WriteTableHeaders(wsCompras, 5, hdrCompras, AccentCyan);

        int r = 6;
        for (int i = 0; i < compras.Count; i++)
        {
            var c = compras[i];
            wsCompras.Cell(r, 1).Value = i + 1;
            wsCompras.Cell(r, 2).Value = c.Codigo;
            wsCompras.Cell(r, 3).Value = c.Descripcion;
            wsCompras.Cell(r, 4).Value = c.RazonSocial;
            wsCompras.Cell(r, 5).Value = c.Kilos;
            wsCompras.Cell(r, 5).Style.NumberFormat.Format = "#,##0";
            if (i % 2 == 1)
            {
                wsCompras.Range(r, 1, r, 5).Style.Fill.BackgroundColor = ZebraFill;
            }
            r++;
        }

        wsCompras.Cell(r, 4).Value = "TOTAL GENERAL COMPRAS";
        if (compras.Count > 0)
        {
            wsCompras.Cell(r, 5).FormulaA1 = $"SUM(E6:E{r - 1})";
        }
        else
        {
            wsCompras.Cell(r, 5).Value = totalCompras;
        }
        StyleTotalRow(wsCompras.Range(r, 1, r, 5), 5);
        FinalizeSheet(wsCompras, 5);

        // Hoja 2: Traslados
        IXLWorksheet wsTras = wb.Worksheets.Add($"Traslados {planta.ToShortTitle()}");
        WriteBanner(wsTras, $"TRASLADOS Y MOVIMIENTOS - PLANTA {planta.ToDisplayName()}", $"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}", 9);

        string[] hdrTras = ["N°", "FOLIO", "PATENTE", "PRODUCTO", "FECHA ENT.", "FECHA SAL.", "PESO BRUTO", "PESO TARA", "TOTAL KILOS"];
        WriteTableHeaders(wsTras, 5, hdrTras, AccentAmber);

        r = 6;
        for (int i = 0; i < traslados.Count; i++)
        {
            var t = traslados[i];
            wsTras.Cell(r, 1).Value = i + 1;
            wsTras.Cell(r, 2).Value = t.Folio;
            wsTras.Cell(r, 3).Value = t.Patente;
            wsTras.Cell(r, 4).Value = t.Producto;
            wsTras.Cell(r, 5).Value = t.FechaEnt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            wsTras.Cell(r, 6).Value = t.FechaSal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            wsTras.Cell(r, 7).Value = t.Bruto;
            wsTras.Cell(r, 8).Value = t.Tara;
            wsTras.Cell(r, 9).Value = t.Neto;
            wsTras.Range(r, 7, r, 9).Style.NumberFormat.Format = "#,##0";
            if (i % 2 == 1)
            {
                wsTras.Range(r, 1, r, 9).Style.Fill.BackgroundColor = ZebraFill;
            }
            r++;
        }

        wsTras.Cell(r, 8).Value = "TOTAL TRASLADOS";
        if (traslados.Count > 0)
        {
            wsTras.Cell(r, 9).FormulaA1 = $"SUM(I6:I{r - 1})";
        }
        else
        {
            wsTras.Cell(r, 9).Value = totalTraslados;
        }
        StyleTotalRow(wsTras.Range(r, 1, r, 9), 9);
        FinalizeSheet(wsTras, 9);

        SaveWorkbook(wb, filePath);
        return filePath;
    }

    public string ExportInformeCliente(
        string filePath,
        Planta planta,
        string rut,
        string razonSocial,
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyList<(long Folio, string Patente, string Producto, DateOnly FechaIng, DateOnly FechaSal, int Bruto, int Tara, int Neto)> pesajes,
        long totalKilos)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Ticket Cliente");

        WriteBanner(ws, $"TICKET CONSOLIDADO DE RECEPCIÓN - {planta.ToDisplayName()}",
            $"Cliente: {razonSocial} | RUT: {rut} | Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}", 9);

        string[] headers = ["N°", "N° FOLIO", "PATENTE", "PRODUCTO", "FECHA ENT.", "FECHA SAL.", "PESO BRUTO", "PESO TARA", "PESO NETO (KG)"];
        WriteTableHeaders(ws, 5, headers, AccentCyan);

        int r = 6;
        for (int i = 0; i < pesajes.Count; i++)
        {
            var p = pesajes[i];
            ws.Cell(r, 1).Value = i + 1;
            ws.Cell(r, 2).Value = p.Folio;
            ws.Cell(r, 3).Value = p.Patente;
            ws.Cell(r, 4).Value = p.Producto;
            ws.Cell(r, 5).Value = p.FechaIng.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(r, 6).Value = p.FechaSal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(r, 7).Value = p.Bruto;
            ws.Cell(r, 8).Value = p.Tara;
            ws.Cell(r, 9).Value = p.Neto;
            ws.Range(r, 7, r, 9).Style.NumberFormat.Format = "#,##0";
            if (i % 2 == 1)
            {
                ws.Range(r, 1, r, 9).Style.Fill.BackgroundColor = ZebraFill;
            }
            r++;
        }

        ws.Cell(r, 8).Value = "TOTAL KILOS RECEPCIONADOS";
        ws.Cell(r, 9).FormulaA1 = pesajes.Count > 0 ? $"SUM(I6:I{r - 1})" : totalKilos.ToString(CultureInfo.InvariantCulture);
        StyleTotalRow(ws.Range(r, 1, r, 9), 9);

        FinalizeSheet(ws, 9);
        SaveWorkbook(wb, filePath);
        return filePath;
    }

    public string ExportInformeJcramm(
        string filePath,
        IReadOnlyList<(DateOnly Fecha, long Folio, string CodProd, string Descripcion, string Patente, string Proveedor, string Ciudad, int Kilos)> detalle,
        IReadOnlyList<(string Producto, long Kilos)> resumen,
        long totalDetalle,
        long totalResumen)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Informe JCRAMM");

        WriteBanner(ws, "INFORME DE PESAJE JCRAMM", $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", 8);

        int row = 5;
        ws.Cell(row, 1).Value = "RESUMEN POR MATERIAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        WriteTableHeaders(ws, row, ["N°", "DESCRIPCIÓN PRODUCTO", "TOTAL KILOS"], AccentCyan);
        row++;
        int startRes = row;
        for (int i = 0; i < resumen.Count; i++)
        {
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = resumen[i].Producto;
            ws.Cell(row, 3).Value = resumen[i].Kilos;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }
        ws.Cell(row, 2).Value = "TOTAL KILOS RESUMEN";
        ws.Cell(row, 3).FormulaA1 = resumen.Count > 0 ? $"SUM(C{startRes}:C{row - 1})" : totalResumen.ToString(CultureInfo.InvariantCulture);
        StyleTotalRow(ws.Range(row, 1, row, 3), 3);
        row += 2;

        ws.Cell(row, 1).Value = "DETALLE DE PESAJES REGISTRADOS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        WriteTableHeaders(ws, row, ["N°", "FECHA", "FOLIO", "DESCRIPCIÓN PRODUCTO", "PATENTE", "PROVEEDOR", "CIUDAD", "KILOS"], HeaderDark);
        row++;
        int startDet = row;
        for (int i = 0; i < detalle.Count; i++)
        {
            var d = detalle[i];
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = d.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(row, 3).Value = d.Folio;
            ws.Cell(row, 4).Value = d.Descripcion;
            ws.Cell(row, 5).Value = d.Patente;
            ws.Cell(row, 6).Value = d.Proveedor;
            ws.Cell(row, 7).Value = d.Ciudad;
            ws.Cell(row, 8).Value = d.Kilos;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            if (i % 2 == 1)
            {
                ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = ZebraFill;
            }
            row++;
        }

        ws.Cell(row, 7).Value = "TOTAL KILOS DETALLE";
        ws.Cell(row, 8).FormulaA1 = detalle.Count > 0 ? $"SUM(H{startDet}:H{row - 1})" : totalDetalle.ToString(CultureInfo.InvariantCulture);
        StyleTotalRow(ws.Range(row, 1, row, 8), 8);

        FinalizeSheet(ws, 8);
        SaveWorkbook(wb, filePath);
        return filePath;
    }

    public string ExportInformePrecios(
        string filePath,
        Planta planta,
        DateOnly desde,
        DateOnly hasta,
        ModoPrecio modo,
        IReadOnlyList<(long Folio, string CodigoProd, string Descripcion, string Proveedor, int Kilos, decimal Precio, decimal Total, string Familia)> detalle,
        IReadOnlyList<(string Grupo, long Kilos, decimal PrecioPromedio, decimal TotalPesos)> resumen)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add($"Precios {planta.ToShortTitle()}");

        WriteBanner(ws, $"INFORME DE PRECIOS {modo.ToString().ToUpperInvariant()} - {planta.ToDisplayName()}",
            $"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}", 9);

        int row = 5;
        ws.Cell(row, 1).Value = "RESUMEN POR FAMILIA DE MATERIAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        WriteTableHeaders(ws, row, ["GRUPO", "TOTAL KILOS", "PRECIO PROM. PONDERADO", "TOTAL VALORIZADO ($)"], AccentAmber);
        row++;
        int resStart = row;
        foreach (var r in resumen)
        {
            ws.Cell(row, 1).Value = r.Grupo;
            ws.Cell(row, 2).Value = r.Kilos;
            ws.Cell(row, 3).Value = r.PrecioPromedio;
            ws.Cell(row, 4).Value = r.TotalPesos;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 3).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Cell(row, 4).Style.NumberFormat.Format = "$ #,##0";
            row++;
        }

        long totK = resumen.Sum(x => x.Kilos);
        decimal totP = resumen.Sum(x => x.TotalPesos);
        ws.Cell(row, 1).Value = "TOTALES RESUMEN";
        ws.Cell(row, 2).FormulaA1 = $"SUM(B{resStart}:B{row - 1})";
        ws.Cell(row, 3).Value = BusinessRules.CalculateWeightedAverage(totP, totK);
        ws.Cell(row, 4).FormulaA1 = $"SUM(D{resStart}:D{row - 1})";
        StyleTotalRow(ws.Range(row, 1, row, 4), 4);
        ws.Cell(row, 3).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Cell(row, 4).Style.NumberFormat.Format = "$ #,##0";
        row += 2;

        ws.Cell(row, 1).Value = "DETALLE DE PESAJES VALORIZADOS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        WriteTableHeaders(ws, row, ["N°", "FOLIO", "CÓDIGO PROD.", "DESCRIPCIÓN PRODUCTO", "PROVEEDOR", "KILOS", "PRECIO UNIT.", "TOTAL ($)", "FAMILIA"], HeaderDark);
        row++;
        int detStart = row;
        for (int i = 0; i < detalle.Count; i++)
        {
            var d = detalle[i];
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = d.Folio;
            ws.Cell(row, 3).Value = d.CodigoProd;
            ws.Cell(row, 4).Value = d.Descripcion;
            ws.Cell(row, 5).Value = d.Proveedor;
            ws.Cell(row, 6).Value = d.Kilos;
            ws.Cell(row, 7).Value = d.Precio;
            ws.Cell(row, 8).FormulaA1 = $"F{row}*G{row}";
            ws.Cell(row, 9).Value = d.Familia;

            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "$ #,##0";
            ws.Cell(row, 8).Style.NumberFormat.Format = "$ #,##0";
            if (i % 2 == 1)
            {
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = ZebraFill;
            }
            row++;
        }

        ws.Cell(row, 5).Value = "TOTALES DETALLE";
        if (detalle.Count > 0)
        {
            ws.Cell(row, 6).FormulaA1 = $"SUM(F{detStart}:F{row - 1})";
            ws.Cell(row, 8).FormulaA1 = $"SUM(H{detStart}:H{row - 1})";
        }
        StyleTotalRow(ws.Range(row, 1, row, 9), 8);
        ws.Cell(row, 8).Style.NumberFormat.Format = "$ #,##0";

        FinalizeSheet(ws, 9);
        SaveWorkbook(wb, filePath);
        return filePath;
    }

    public string ExportAuditLogs(string filePath, IReadOnlyList<AuditLogEntry> logs)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Auditoría");
        WriteBanner(ws, "REGISTRO DE AUDITORÍA DE SEGURIDAD Y OPERACIONES", $"Exportado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", 6);

        WriteTableHeaders(ws, 5, ["ID", "FECHA / HORA (LOCAL)", "USUARIO", "ACCIÓN", "ENTIDAD", "DETALLE"], HeaderDark);
        int row = 6;
        for (int i = 0; i < logs.Count; i++)
        {
            AuditLogEntry l = logs[i];
            ws.Cell(row, 1).Value = l.Id;
            ws.Cell(row, 2).Value = l.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            ws.Cell(row, 3).Value = l.Usuario;
            ws.Cell(row, 4).Value = l.Accion;
            ws.Cell(row, 5).Value = l.Entidad;
            ws.Cell(row, 6).Value = l.Detalle;
            if (i % 2 == 1)
            {
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ZebraFill;
            }
            row++;
        }

        FinalizeSheet(ws, 6);
        SaveWorkbook(wb, filePath);
        return filePath;
    }

    private static int WritePlantMaterialSection(
        IXLWorksheet ws,
        int startRow,
        string title,
        IReadOnlyList<(string Grupo, long Kilos)> rows,
        XLColor color)
    {
        ws.Cell(startRow, 1).Value = title;
        ws.Cell(startRow, 1).Style.Font.Bold = true;
        ws.Cell(startRow, 1).Style.Font.FontColor = color;
        startRow++;

        WriteTableHeaders(ws, startRow, ["N°", "GRUPO DE MATERIAL", "TOTAL KILOS"], color);
        startRow++;
        int dataStart = startRow;

        for (int i = 0; i < rows.Count; i++)
        {
            ws.Cell(startRow, 1).Value = i + 1;
            ws.Cell(startRow, 2).Value = rows[i].Grupo;
            ws.Cell(startRow, 3).Value = rows[i].Kilos;
            ws.Cell(startRow, 3).Style.NumberFormat.Format = "#,##0";
            startRow++;
        }

        ws.Cell(startRow, 2).Value = $"TOTAL {title}";
        if (rows.Count > 0)
        {
            ws.Cell(startRow, 3).FormulaA1 = $"SUM(C{dataStart}:C{startRow - 1})";
        }
        else
        {
            ws.Cell(startRow, 3).Value = 0;
        }
        StyleTotalRow(ws.Range(startRow, 1, startRow, 3), 3);
        return startRow;
    }

    private static void WriteBanner(IXLWorksheet ws, string title, string subtitle, int colSpan)
    {
        IXLRange titleRange = ws.Range(1, 1, 1, colSpan);
        titleRange.Merge();
        titleRange.Value = title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = HeaderDark;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        IXLRange subRange = ws.Range(2, 1, 2, colSpan);
        subRange.Merge();
        subRange.Value = subtitle;
        subRange.Style.Font.FontSize = 10;
        subRange.Style.Font.FontColor = XLColor.FromHtml("#334155");
        subRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        ws.Row(2).Height = 20;
    }

    private static void WriteTableHeaders(IXLWorksheet ws, int row, string[] headers, XLColor bgColor)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            IXLCell cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = bgColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        ws.Row(row).Height = 22;
    }

    private static void StyleTotalRow(IXLRange range, int numericCol)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = TotalFill;
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        range.Worksheet.Cell(range.RangeAddress.FirstAddress.RowNumber, numericCol).Style.NumberFormat.Format = "#,##0";
    }

    private static void FinalizeSheet(IXLWorksheet ws, int colCount)
    {
        ws.Columns(1, colCount).AdjustToContents(6, 55);
        ws.SheetView.FreezeRows(1);
    }

    private static void SaveWorkbook(XLWorkbook wb, string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        wb.SaveAs(filePath);
    }
}
