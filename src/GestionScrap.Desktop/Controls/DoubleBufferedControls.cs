using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GestionScrap.Application.DTOs;
using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// DataGridView optimizado con doble búfer para evitar parpadeos y acelerar el desplazamiento con miles de filas.
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
/// Control gráfico vectorial GDI+ con doble búfer para el Dashboard operativo.
/// </summary>
public sealed class DashboardChartControl : Control
{
    private ChartRenderMode _mode;
    private IReadOnlyList<WeekComparisonDto> _weekData = [];
    private IReadOnlyList<GrupoEspecialItemDto> _barData = [];
    private IReadOnlyList<GrupoKilosItem> _donutData = [];
    private string _title = string.Empty;

    private static readonly Color[] Palette =
    [
        Color.FromArgb(8, 145, 178),
        Color.FromArgb(217, 119, 6),
        Color.FromArgb(5, 150, 105),
        Color.FromArgb(79, 70, 229),
        Color.FromArgb(219, 39, 119),
        Color.FromArgb(100, 116, 139)
    ];

    public DashboardChartControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    public void SetComparisonData(string title, IReadOnlyList<WeekComparisonDto> data)
    {
        _mode = ChartRenderMode.AreaComparison;
        _title = title;
        _weekData = data;
        Invalidate();
    }

    public void SetBarData(string title, IReadOnlyList<GrupoEspecialItemDto> data)
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

        Rectangle plot = new(56, 46, Math.Max(80, Width - 80), Math.Max(60, Height - 92));
        long maxVal = Math.Max(1, _weekData.Max(w => Math.Max(w.KilosActual, w.KilosAnterior)));

        using Pen gridPen = new(Color.FromArgb(226, 232, 240)) { DashStyle = DashStyle.Dash };
        using SolidBrush labelBrush = new(AppTheme.TextSecondary);
        using Font axisFont = new("Segoe UI", 8F);

        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Bottom - plot.Height * (i / 4f);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            long tick = maxVal * i / 4;
            g.DrawString($"{tick / 1000.0:N0}k", axisFont, labelBrush, 8, y - 7);
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
            g.DrawString(_weekData[i].Semana, axisFont, labelBrush, x, plot.Bottom + 6, sf);
        }

        if (count >= 2)
        {
            List<PointF> areaPts = [new(ptsActual[0].X, plot.Bottom), .. ptsActual, new(ptsActual[^1].X, plot.Bottom)];
            using SolidBrush areaBrush = new(Color.FromArgb(45, 8, 145, 178));
            g.FillPolygon(areaBrush, [.. areaPts]);
        }

        using Pen prevPen = new(Color.FromArgb(148, 163, 184), 2f) { DashStyle = DashStyle.Dash };
        using Pen actPen = new(AppTheme.AccentCyan, 2.5f);
        if (count >= 2)
        {
            g.DrawLines(prevPen, ptsPrev);
            g.DrawLines(actPen, ptsActual);
        }

        using SolidBrush dotAct = new(AppTheme.AccentCyan);
        using SolidBrush dotPrev = new(Color.FromArgb(148, 163, 184));
        for (int i = 0; i < count; i++)
        {
            g.FillEllipse(dotPrev, ptsPrev[i].X - 3.5f, ptsPrev[i].Y - 3.5f, 7, 7);
            g.FillEllipse(dotAct, ptsActual[i].X - 4f, ptsActual[i].Y - 4f, 8, 8);
        }

        // Legend
        g.FillRectangle(dotAct, Width - 210, 14, 10, 10);
        g.DrawString("Mes actual", axisFont, labelBrush, Width - 196, 12);
        g.FillRectangle(dotPrev, Width - 110, 14, 10, 10);
        g.DrawString("Mes anterior", axisFont, labelBrush, Width - 96, 12);
    }

    private void DrawHorizontalBars(Graphics g)
    {
        if (_barData.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        long maxVal = Math.Max(1, _barData.Max(b => b.Kilos));
        int top = 46;
        int rowH = Math.Min(34, Math.Max(22, (Height - 64) / _barData.Count));
        int labelW = Math.Min(170, Width / 3);
        int barMaxW = Math.Max(50, Width - labelW - 95);

        using Font f = new("Segoe UI", 8.5F);
        using SolidBrush textBrush = new(AppTheme.TextPrimary);
        using SolidBrush subBrush = new(AppTheme.TextSecondary);

        for (int i = 0; i < _barData.Count; i++)
        {
            int y = top + i * rowH;
            string name = _barData[i].Cliente.Length > 24 ? _barData[i].Cliente[..22] + "…" : _barData[i].Cliente;
            g.DrawString(name, f, textBrush, 14, y + 3);

            int bw = Math.Max(4, (int)(_barData[i].Kilos * (double)barMaxW / maxVal));
            using SolidBrush barBrush = new(Palette[i % Palette.Length]);
            g.FillRectangle(barBrush, labelW + 16, y + 3, bw, rowH - 10);
            g.DrawString($"{_barData[i].Kilos:N0} Kg", f, subBrush, labelW + 22 + bw, y + 3);
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
        int diam = Math.Max(60, Math.Min(Height - 70, Width / 2 - 20));
        Rectangle rect = new(24, 50, diam, diam);

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

        using Font f = new("Segoe UI", 8.5F);
        using SolidBrush textBrush = new(AppTheme.TextPrimary);
        int lx = rect.Right + 20;
        int ly = 54;
        for (int i = 0; i < Math.Min(6, _donutData.Count); i++)
        {
            using SolidBrush swatch = new(Palette[i % Palette.Length]);
            g.FillRectangle(swatch, lx, ly + 3, 10, 10);
            double pct = _donutData[i].Kilos * 100.0 / total;
            g.DrawString($"{_donutData[i].Grupo} ({pct:N1}%)", f, textBrush, lx + 15, ly);
            ly += 22;
        }
    }

    private void DrawEmpty(Graphics g)
    {
        using SolidBrush muted = new(AppTheme.TextSecondary);
        using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Sin datos en el periodo", AppTheme.FontBody, muted, ClientRectangle, sf);
    }
}
