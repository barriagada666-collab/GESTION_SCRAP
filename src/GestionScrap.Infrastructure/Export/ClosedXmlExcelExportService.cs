using System.Globalization;
using ClosedXML.Excel;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Infrastructure.Export;

/// <summary>
/// Exportador nativo .xlsx con ClosedXML estilizado según la identidad corporativa Cambiaso.
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
        long totPl = porPlanta.Sum(x => x.Kilos);
        foreach ((string Planta, int Viajes, long Kilos) p in porPlanta)
        {
            double pct = totPl > 0 ? p.Kilos * 100.0 / totPl : 0.0;
            wsRes.Cell(r, 1).Value = p.Planta;
            wsRes.Cell(r, 2).Value = p.Viajes;
            wsRes.Cell(r, 3).Value = p.Kilos;
            wsRes.Cell(r, 4).Value = pct / 100.0;
            wsRes.Cell(r, 2).Style.NumberFormat.Format = "#,##0";
            wsRes.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            wsRes.Cell(r, 4).Style.NumberFormat.Format = "0.0%";
            r++;
        }

        r += 2;
        WriteSectionHeader(wsRes, r++, "TOP PROVEEDORES DEL PERÍODO", ["RUT / Razón Social", "Viajes", "Kilos Netos", "% Participación"], PetrolDark);
        foreach ((string Rut, string Proveedor, int Viajes, long Kilos, double Porcentaje) tp in topProveedores)
        {
            wsRes.Cell(r, 1).Value = $"{tp.Rut} · {tp.Proveedor}";
            wsRes.Cell(r, 2).Value = tp.Viajes;
            wsRes.Cell(r, 3).Value = tp.Kilos;
            wsRes.Cell(r, 4).Value = tp.Porcentaje / 100.0;
            wsRes.Cell(r, 2).Style.NumberFormat.Format = "#,##0";
            wsRes.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            wsRes.Cell(r, 4).Style.NumberFormat.Format = "0.0%";
            r++;
        }

        IXLWorksheet wsGrp = wb.Worksheets.Add("Grupos Material");
        WriteBanner(wsGrp, "CONSOLIDADO POR GRUPO DE MATERIAL", $"Período: {tituloPeriodo}", 4);
        WriteSectionHeader(wsGrp, 5, "GRUPOS DE MATERIAL", ["Grupo Padre", "Viajes", "Kilos Netos", "%"], PetrolDark);
        int gr = 6;
        foreach ((string Grupo, int Viajes, long Kilos, double Porcentaje) g in porGrupo)
        {
            wsGrp.Cell(gr, 1).Value = g.Grupo;
            wsGrp.Cell(gr, 2).Value = g.Viajes;
            wsGrp.Cell(gr, 3).Value = g.Kilos;
            wsGrp.Cell(gr, 4).Value = g.Porcentaje / 100.0;
            wsGrp.Cell(gr, 2).Style.NumberFormat.Format = "#,##0";
            wsGrp.Cell(gr, 3).Style.NumberFormat.Format = "#,##0";
            wsGrp.Cell(gr, 4).Style.NumberFormat.Format = "0.0%";
            if ((gr - 6) % 2 == 1) wsGrp.Range(gr, 1, gr, 4).Style.Fill.BackgroundColor = ZebraFill;
            gr++;
        }

        IXLWorksheet wsDet = wb.Worksheets.Add("Últimos Movimientos");
        WriteBanner(wsDet, "DETALLE DE MOVIMIENTOS RECIENTES", $"Período: {tituloPeriodo}", 9);
        WriteHeaders(wsDet, 5, ["Folio", "Planta", "Fecha", "Patente", "RUT", "Proveedor", "Código", "Producto", "Neto Kg"]);
        int dr = 6;
        foreach (Pesaje p in detallePesajes)
        {
            wsDet.Cell(dr, 1).Value = p.Folio;
            wsDet.Cell(dr, 2).Value = p.Planta.ToShortTitle();
            wsDet.Cell(dr, 3).Value = p.FechaIngreso.ToDateTime(TimeOnly.MinValue);
            wsDet.Cell(dr, 3).Style.DateFormat.Format = "dd-mm-yyyy";
            wsDet.Cell(dr, 4).Value = p.Patente;
            wsDet.Cell(dr, 5).Value = p.RutCliente;
            wsDet.Cell(dr, 6).Value = p.NomCliente;
            wsDet.Cell(dr, 7).Value = p.CodProducto;
            wsDet.Cell(dr, 8).Value = p.NomProducto;
            wsDet.Cell(dr, 9).Value = p.PesoNeto;
            wsDet.Cell(dr, 9).Style.NumberFormat.Format = "#,##0";
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
            ws.Cell(r, 1).Value = indent + n.Nombre;
            ws.Cell(r, 2).Value = n.Tipo;
            ws.Cell(r, 3).Value = n.CodigoProducto;
            ws.Cell(r, 4).Value = n.Viajes;
            ws.Cell(r, 5).Value = n.Kilos;
            ws.Cell(r, 6).Value = n.Porcentaje / 100.0;

            ws.Cell(r, 4).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 6).Style.NumberFormat.Format = "0.00%";

            if (n.Nivel == 0)
            {
                IXLRange rowRange = ws.Range(r, 1, r, 6);
                rowRange.Style.Font.Bold = true;
                rowRange.Style.Fill.BackgroundColor = TotalFill;
            }
            else if ((r - 6) % 2 == 1)
            {
                ws.Range(r, 1, r, 6).Style.Fill.BackgroundColor = ZebraFill;
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
            wsRank.Cell(r, 1).Value = item.Rut;
            wsRank.Cell(r, 2).Value = item.RazonSocial;
            wsRank.Cell(r, 3).Value = item.Ciudad;
            wsRank.Cell(r, 4).Value = item.ClaseAbc;
            wsRank.Cell(r, 5).Value = item.Viajes;
            wsRank.Cell(r, 6).Value = item.Kilos;
            wsRank.Cell(r, 7).Value = item.Participacion / 100.0;
            wsRank.Cell(r, 8).Value = item.Acumulado / 100.0;
            wsRank.Cell(r, 9).Value = item.PromedioKg;
            wsRank.Cell(r, 10).Value = item.ProductoTop;

            wsRank.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            wsRank.Cell(r, 5).Style.NumberFormat.Format = "#,##0";
            wsRank.Cell(r, 6).Style.NumberFormat.Format = "#,##0";
            wsRank.Cell(r, 7).Style.NumberFormat.Format = "0.00%";
            wsRank.Cell(r, 8).Style.NumberFormat.Format = "0.00%";
            wsRank.Cell(r, 9).Style.NumberFormat.Format = "#,##0.0";
            if ((r - 6) % 2 == 1) wsRank.Range(r, 1, r, 10).Style.Fill.BackgroundColor = ZebraFill;
            r++;
        }

        if (!string.IsNullOrWhiteSpace(proveedorSeleccionadoNombre) &&
            desgloseSeleccionado is not null &&
            entregasSeleccionado is not null)
        {
            IXLWorksheet wsDet = wb.Worksheets.Add("Ficha Proveedor");
            WriteBanner(wsDet, $"FICHA PROVEEDOR: {proveedorSeleccionadoNombre}", periodoLabel, 8);
            WriteSectionHeader(wsDet, 5, "MIX DE MATERIALES", ["Grupo Material", "Viajes", "Kilos", "%"], TealAccent);
            int mr = 6;
            foreach (var g in desgloseSeleccionado)
            {
                wsDet.Cell(mr, 1).Value = g.Grupo;
                wsDet.Cell(mr, 2).Value = g.Viajes;
                wsDet.Cell(mr, 3).Value = g.Kilos;
                wsDet.Cell(mr, 4).Value = g.Porcentaje / 100.0;
                wsDet.Cell(mr, 2).Style.NumberFormat.Format = "#,##0";
                wsDet.Cell(mr, 3).Style.NumberFormat.Format = "#,##0";
                wsDet.Cell(mr, 4).Style.NumberFormat.Format = "0.0%";
                mr++;
            }

            mr += 2;
            WriteHeaders(wsDet, mr++, ["Folio", "Planta", "Fecha", "Patente", "Producto", "Origen", "Bruto", "Neto Kg"]);
            foreach (Pesaje p in entregasSeleccionado)
            {
                wsDet.Cell(mr, 1).Value = p.Folio;
                wsDet.Cell(mr, 2).Value = p.Planta.ToShortTitle();
                wsDet.Cell(mr, 3).Value = p.FechaIngreso.ToDateTime(TimeOnly.MinValue);
                wsDet.Cell(mr, 3).Style.DateFormat.Format = "dd-mm-yyyy";
                wsDet.Cell(mr, 4).Value = p.Patente;
                wsDet.Cell(mr, 5).Value = p.NomProducto;
                wsDet.Cell(mr, 6).Value = p.Origen;
                wsDet.Cell(mr, 7).Value = p.PesoBruto;
                wsDet.Cell(mr, 8).Value = p.PesoNeto;
                wsDet.Cell(mr, 7).Style.NumberFormat.Format = "#,##0";
                wsDet.Cell(mr, 8).Style.NumberFormat.Format = "#,##0";
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
            ws.Cell(r, 1).Value = log.Id;
            ws.Cell(r, 2).Value = log.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            ws.Cell(r, 3).Value = log.Usuario;
            ws.Cell(r, 4).Value = log.Accion;
            ws.Cell(r, 5).Value = log.Entidad;
            ws.Cell(r, 6).Value = log.Detalle;
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
        return filePath;
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
