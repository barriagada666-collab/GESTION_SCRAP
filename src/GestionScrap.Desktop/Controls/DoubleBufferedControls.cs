using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// DataGridView optimizado con doble búfer para evitar parpadeos y desplazamiento fluido con miles de filas.
/// </summary>
public sealed class DoubleBufferedDataGridView : DataGridView
{
    public DoubleBufferedDataGridView()
    {
        DoubleBuffered = true;
        AppTheme.StyleDataGridView(this);
    }
}

public enum ChartRenderMode
{
    AreaComparison,
    HorizontalBars,
    MaterialDonut
}

/// <summary>
/// Control gráfico vectorial GDI+ con doble búfer estilizado con paleta corporativa Cambiaso.
/// </summary>
public sealed class DashboardChartControl : Control
{
    private static readonly CultureInfo EsCl = CultureInfo.GetCultureInfo("es-CL");

    private ChartRenderMode _mode;
    private IReadOnlyList<WeekComparisonDto> _weekData = [];
    private IReadOnlyList<BarChartItemDto> _barData = [];
    private IReadOnlyList<GrupoKilosItem> _donutData = [];
    private string _title = string.Empty;
    private string _seriesActualLabel = "Período actual";
    private string _seriesPrevLabel = "Anterior";

    private static readonly Color[] Palette =
    [
        Color.FromArgb(8, 145, 178),   // Cian técnico
        Color.FromArgb(201, 164, 110), // Dorado Cambiaso
        Color.FromArgb(11, 60, 93),    // Azul petróleo
        Color.FromArgb(5, 150, 105),   // Esmeralda
        Color.FromArgb(37, 99, 235),   // Azul cobalto
        Color.FromArgb(217, 119, 6),   // Ámbar
        Color.FromArgb(100, 116, 139)  // Pizarra
    ];

    public DashboardChartControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    public void SetComparisonData(
        string title,
        IReadOnlyList<WeekComparisonDto> data,
        string seriesActualLabel = "Actual",
        string seriesPrevLabel = "Anterior / YoY")
    {
        _mode = ChartRenderMode.AreaComparison;
        _title = title;
        _weekData = data;
        _seriesActualLabel = seriesActualLabel;
        _seriesPrevLabel = seriesPrevLabel;
        Invalidate();
    }

    public void SetBarData(string title, IReadOnlyList<BarChartItemDto> data)
    {
        _mode = ChartRenderMode.HorizontalBars;
        _title = title;
        _barData = data;
        Invalidate();
    }

    public void SetDonutData(string title, IReadOnlyList<GrupoKilosItem> data)
    {
        _mode = ChartRenderMode.MaterialDonut;
        _title = title;
        _donutData = data;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        using Pen border = new(AppTheme.CardBorder);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using SolidBrush titleBrush = new(AppTheme.TextPrimary);
        g.DrawString(_title, AppTheme.FontSection, titleBrush, 14, 12);

        switch (_mode)
        {
            case ChartRenderMode.AreaComparison:
                DrawAreaComparison(g);
                break;
            case ChartRenderMode.HorizontalBars:
                DrawHorizontalBars(g);
                break;
            case ChartRenderMode.MaterialDonut:
                DrawMaterialDonut(g);
                break;
        }
    }

    private void DrawAreaComparison(Graphics g)
    {
        if (_weekData.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        Rectangle plot = new(56, 56, Math.Max(80, Width - 78), Math.Max(52, Height - 84));
        long maxVal = Math.Max(1, _weekData.Max(w => Math.Max(w.KilosActual, w.KilosAnterior)));

        using Pen gridPen = new(Color.FromArgb(226, 232, 240)) { DashStyle = DashStyle.Dash };
        using SolidBrush labelBrush = new(AppTheme.TextSecondary);
        Font axisFont = AppTheme.FontCaption;

        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Bottom - plot.Height * (i / 4f);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            long tick = maxVal * i / 4;
            g.DrawString(string.Create(EsCl, $"{tick / 1000.0:N0}t"), axisFont, labelBrush, 8, y - 7);
        }

        int count = _weekData.Count;
        PointF[] ptsActual = new PointF[count];
        PointF[] ptsPrev = new PointF[count];

        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? plot.Left + plot.Width / 2f : plot.Left + i * (plot.Width / (float)(count - 1));
            float yA = plot.Bottom - (_weekData[i].KilosActual / (float)maxVal) * plot.Height;
            float yP = plot.Bottom - (_weekData[i].KilosAnterior / (float)maxVal) * plot.Height;
            ptsActual[i] = new PointF(x, yA);
            ptsPrev[i] = new PointF(x, yP);

            using StringFormat sf = new() { Alignment = StringAlignment.Center };
            g.DrawString(_weekData[i].Periodo, axisFont, labelBrush, x, plot.Bottom + 6, sf);
        }

        if (count >= 2)
        {
            List<PointF> areaPts = [new(ptsActual[0].X, plot.Bottom), .. ptsActual, new(ptsActual[^1].X, plot.Bottom)];
            using SolidBrush areaBrush = new(Color.FromArgb(45, 8, 145, 178));
            g.FillPolygon(areaBrush, [.. areaPts]);
        }

        using Pen prevPen = new(AppTheme.AccentGold, 2f) { DashStyle = DashStyle.Dash };
        using Pen actPen = new(AppTheme.AccentCyan, 2.5f);
        if (count >= 2)
        {
            g.DrawLines(prevPen, ptsPrev);
            g.DrawLines(actPen, ptsActual);
        }

        using SolidBrush dotAct = new(AppTheme.AccentCyan);
        using SolidBrush dotPrev = new(AppTheme.AccentGold);
        for (int i = 0; i < count; i++)
        {
            g.FillEllipse(dotPrev, ptsPrev[i].X - 3.5f, ptsPrev[i].Y - 3.5f, 7, 7);
            g.FillEllipse(dotAct, ptsActual[i].X - 4f, ptsActual[i].Y - 4f, 8, 8);
        }

        // Leyenda en fila dedicada debajo del título (sin solapamiento)
        int ax = 14;
        int ly = 33;
        int aWidth = TextRenderer.MeasureText(g, _seriesActualLabel, axisFont).Width + 24;
        g.FillRectangle(dotAct, ax, ly + 2, 10, 10);
        g.DrawString(_seriesActualLabel, axisFont, labelBrush, ax + 14, ly);
        int bx = ax + aWidth;
        g.FillRectangle(dotPrev, bx, ly + 2, 10, 10);
        g.DrawString(_seriesPrevLabel, axisFont, labelBrush, bx + 14, ly);
    }

    private void DrawHorizontalBars(Graphics g)
    {
        if (_barData.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        int top = 42;
        int availH = Math.Max(40, Height - top - 10);
        int maxRows = Math.Max(1, availH / 22);
        int visibleCount = Math.Min(_barData.Count, maxRows);
        long maxVal = Math.Max(1, _barData.Take(visibleCount).Max(b => b.Value));
        int rowH = Math.Min(30, Math.Max(20, availH / visibleCount));

        Font f = AppTheme.FontSmall;
        int maxValueTextW = _barData.Take(visibleCount).Max(b => TextRenderer.MeasureText(g, string.Create(EsCl, $"{b.Value:N0} Kg"), f).Width) + 14;
        int labelW = Math.Clamp(Width / 3, 115, 185);
        int barMaxW = Math.Max(44, Width - labelW - maxValueTextW - 34);

        using SolidBrush textBrush = new(AppTheme.TextPrimary);
        using SolidBrush subBrush = new(AppTheme.TextSecondary);
        using StringFormat sfLabel = new() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < visibleCount; i++)
        {
            int y = top + i * rowH;
            RectangleF labelRect = new(14, y + 1, labelW - 6, rowH - 2);
            g.DrawString(_barData[i].Label, f, textBrush, labelRect, sfLabel);

            int bw = Math.Max(4, (int)(_barData[i].Value * (double)barMaxW / maxVal));
            using SolidBrush barBrush = new(Palette[i % Palette.Length]);
            g.FillRectangle(barBrush, labelW + 16, y + 4, bw, Math.Max(10, rowH - 9));
            g.DrawString(string.Create(EsCl, $"{_barData[i].Value:N0} Kg"), f, subBrush, labelW + 22 + bw, y + 3);
        }
    }

    private void DrawMaterialDonut(Graphics g)
    {
        if (_donutData.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        long total = Math.Max(1, _donutData.Sum(d => d.Kilos));
        int topPad = 42;
        int availH = Math.Max(48, Height - topPad - 12);
        int diam = Math.Max(48, Math.Min(availH, (int)(Width * 0.36)));
        int circleY = topPad + Math.Max(0, (availH - diam) / 2);
        Rectangle rect = new(18, circleY, diam, diam);

        float startAngle = -90f;
        for (int i = 0; i < _donutData.Count; i++)
        {
            float sweep = (float)(_donutData[i].Kilos * 360.0 / total);
            using SolidBrush slice = new(Palette[i % Palette.Length]);
            g.FillPie(slice, rect, startAngle, Math.Max(1f, sweep));
            startAngle += sweep;
        }

        int hole = (int)(diam * 0.56);
        int offset = (diam - hole) / 2;
        using (SolidBrush holeBrush = new(BackColor))
        {
            g.FillEllipse(holeBrush, rect.X + offset, rect.Y + offset, hole, hole);
        }

        Font f = AppTheme.FontCaption;
        using SolidBrush textBrush = new(AppTheme.TextPrimary);
        using StringFormat sfLegend = new() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
        int lx = rect.Right + 16;
        int legendW = Math.Max(60, Width - lx - 24);
        int maxItems = Math.Max(1, Math.Min(7, availH / 18));
        int showCount = Math.Min(maxItems, _donutData.Count);
        int step = Math.Min(21, Math.Max(16, availH / showCount));
        int ly = topPad + Math.Max(0, (availH - (showCount * step)) / 2);

        for (int i = 0; i < showCount; i++)
        {
            using SolidBrush swatch = new(Palette[i % Palette.Length]);
            g.FillRectangle(swatch, lx, ly + 4, 9, 9);
            double pct = _donutData[i].Kilos * 100.0 / total;
            string lbl = string.Create(EsCl, $"{_donutData[i].Grupo} ({pct:N1}%)");
            g.DrawString(lbl, f, textBrush, new RectangleF(lx + 14, ly, legendW, step), sfLegend);
            ly += step;
        }
    }

    private void DrawEmpty(Graphics g)
    {
        using SolidBrush muted = new(AppTheme.TextSecondary);
        using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Sin datos en el período seleccionado", AppTheme.FontBody, muted, ClientRectangle, sf);
    }
}
