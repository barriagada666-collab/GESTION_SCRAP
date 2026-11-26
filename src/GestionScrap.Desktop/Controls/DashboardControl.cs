using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Panel ejecutivo inicial con KPIs, gráficos vectoriales GDI+, alertas de precios y últimos folios.
/// </summary>
public sealed class DashboardControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly Action<int> _navigateToTab;

    private readonly Label _lblPeriodoSub = new();
    private readonly Button _btnActualizar;
    private readonly Button _btnVerPrecios;
    private readonly Button _btnMantenedor;
    private readonly Button _btnSyncFirebird;

    private readonly Label _lblKpiMesActualVal;
    private readonly Label _lblKpiMesActualSub;
    private readonly Label _lblKpiMesAnteriorVal;
    private readonly Label _lblKpiMesAnteriorSub;
    private readonly Label _lblKpiJornadaVal;
    private readonly Label _lblKpiJornadaSub;
    private readonly Label _lblKpiAlertasVal;
    private readonly Label _lblKpiAlertasSub;

    private readonly DashboardChartControl _chartWeekly = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartTopProv = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartMaterials = new() { Dock = DockStyle.Fill };

    private readonly DoubleBufferedDataGridView _dgvAlertas = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvUltimos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    public DashboardControl(ScrapApplicationService app, IAuthService auth, Action<int> navigateToTab)
    {
        _app = app;
        _auth = auth;
        _navigateToTab = navigateToTab;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);
        AutoScroll = true;

        Panel topBar = new() { Dock = DockStyle.Top, Height = 54, BackColor = Color.Transparent };
        Panel topLeft = new() { Dock = DockStyle.Fill };
        Label lblTitle = new()
        {
            Text = "Dashboard Ejecutivo · Scrap & Básculas",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(0, 2),
            AutoSize = true
        };
        _lblPeriodoSub.Text = "Cargando resumen operativo...";
        _lblPeriodoSub.Font = AppTheme.FontSubtitle;
        _lblPeriodoSub.ForeColor = AppTheme.TextSecondary;
        _lblPeriodoSub.Location = new Point(2, 28);
        _lblPeriodoSub.AutoSize = true;
        topLeft.Controls.AddRange([lblTitle, _lblPeriodoSub]);

        _btnActualizar = AppTheme.CreateButton(
            "Actualizar Dashboard",
            IconKind.Refresh,
            ButtonVariant.Primary,
            async (_, _) => await ActivateAsync(),
            "Recalcula indicadores, gráficos y alertas desde SQLite",
            Tip,
            182,
            34);
        FlowLayoutPanel topRight = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            WrapContents = false
        };
        topRight.Controls.Add(_btnActualizar);

        topBar.Controls.Add(topLeft);
        topBar.Controls.Add(topRight);

        TableLayoutPanel kpiStrip = new()
        {
            Dock = DockStyle.Top,
            Height = 104,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 6)
        };
        for (int i = 0; i < 4; i++)
        {
            kpiStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        kpiStrip.Controls.Add(UiHelpers.CreateKpiCard("KILOS PERÍODO ACTUAL", AppTheme.AccentCyan, out _lblKpiMesActualVal, out _lblKpiMesActualSub), 0, 0);
        kpiStrip.Controls.Add(UiHelpers.CreateKpiCard("PERÍODO ANTERIOR", AppTheme.AccentBlue, out _lblKpiMesAnteriorVal, out _lblKpiMesAnteriorSub), 1, 0);
        kpiStrip.Controls.Add(UiHelpers.CreateKpiCard("ÚLTIMA JORNADA", AppTheme.AccentEmerald, out _lblKpiJornadaVal, out _lblKpiJornadaSub), 2, 0);
        kpiStrip.Controls.Add(UiHelpers.CreateKpiCard("ALERTAS DE PRECIOS", AppTheme.AccentAmber, out _lblKpiAlertasVal, out _lblKpiAlertasSub), 3, 0);

        TableLayoutPanel chartsRow = new()
        {
            Dock = DockStyle.Top,
            Height = 246,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 8)
        };
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));

        _chartWeekly.Margin = new Padding(4);
        _chartTopProv.Margin = new Padding(4);
        _chartMaterials.Margin = new Padding(4);
        chartsRow.Controls.Add(_chartWeekly, 0, 0);
        chartsRow.Controls.Add(_chartTopProv, 1, 0);
        chartsRow.Controls.Add(_chartMaterials, 2, 0);

        TableLayoutPanel tablesRow = new()
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(800, 220),
            ColumnCount = 2,
            RowCount = 1
        };
        tablesRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        tablesRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));

        Panel pnlAlertas = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(12),
            BorderStyle = BorderStyle.FixedSingle
        };
        Panel pnlAlertHeader = new() { Dock = DockStyle.Top, Height = 38 };
        Label lblAlertTitle = new()
        {
            Text = "Alertas de Tarifas y Precios",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _btnVerPrecios = AppTheme.CreateButton("Revisar Precios", IconKind.DollarSign, ButtonVariant.Warning,
            (_, _) => _navigateToTab(6), "Ir al Informe de Precios", Tip, 138, 30);
        _btnMantenedor = AppTheme.CreateButton("Mantenedor", IconKind.Plus, ButtonVariant.Secondary,
            (_, _) => _navigateToTab(7), "Ir al Mantenedor de Precios", Tip, 128, 30);

        FlowLayoutPanel alertBtns = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };
        alertBtns.Controls.AddRange([_btnVerPrecios, _btnMantenedor]);
        pnlAlertHeader.Controls.Add(lblAlertTitle);
        pnlAlertHeader.Controls.Add(alertBtns);

        _dgvAlertas.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Tipo", nameof(PriceAlertDto.TipoAlerta), 75),
            UiHelpers.AddNumberCol("Folio", nameof(PriceAlertDto.Folio), "N0", 55),
            UiHelpers.AddTextCol("Planta", nameof(PriceAlertDto.Planta), 65),
            UiHelpers.AddTextCol("Cliente", nameof(PriceAlertDto.Cliente), 115),
            UiHelpers.AddTextCol("Cod", nameof(PriceAlertDto.CodProducto), 50),
            UiHelpers.AddNumberCol("Kilos", nameof(PriceAlertDto.Kilos), "N0", 60)
        ]);
        pnlAlertas.Controls.Add(_dgvAlertas);
        pnlAlertas.Controls.Add(pnlAlertHeader);

        Panel pnlUltimos = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(12),
            BorderStyle = BorderStyle.FixedSingle
        };
        Panel pnlUltHeader = new() { Dock = DockStyle.Top, Height = 38 };
        Label lblUltTitle = new()
        {
            Text = "Últimos Folios Ingresados",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _btnSyncFirebird = AppTheme.CreateButton("Importar Básculas", IconKind.Database, ButtonVariant.Success,
            (_, _) => _navigateToTab(8), "Abrir módulo de importación Firebird", Tip, 165, 30);

        FlowLayoutPanel ultBtns = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };
        ultBtns.Controls.Add(_btnSyncFirebird);
        pnlUltHeader.Controls.Add(lblUltTitle);
        pnlUltHeader.Controls.Add(ultBtns);

        _dgvUltimos.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(Pesaje.Folio), "N0", 55),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 65),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 70),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 65),
            UiHelpers.AddTextCol("Cliente", nameof(Pesaje.NomCliente), 120),
            UiHelpers.AddTextCol("Producto", nameof(Pesaje.NomProducto), 110),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 65)
        ]);
        pnlUltimos.Controls.Add(_dgvUltimos);
        pnlUltimos.Controls.Add(pnlUltHeader);

        tablesRow.Controls.Add(pnlAlertas, 0, 0);
        tablesRow.Controls.Add(pnlUltimos, 1, 0);

        Controls.Add(tablesRow);
        Controls.Add(chartsRow);
        Controls.Add(kpiStrip);
        Controls.Add(topBar);

        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canReports = _auth.HasPermission(PermisoSistema.ConsultarInformes);
        bool canImport = _auth.HasPermission(PermisoSistema.ImportarFirebird);
        _btnVerPrecios.Enabled = canReports;
        _btnMantenedor.Enabled = _auth.HasPermission(PermisoSistema.AdministrarMantenedorPrecios) || canReports;
        _btnSyncFirebird.Enabled = canImport;
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            DashboardSummaryDto dto = await _app.GetDashboardSummaryAsync(ct);

            _lblPeriodoSub.Text = $"Comparativa {dto.PeriodoActualLabel} vs {dto.PeriodoAnteriorLabel} · Última actividad {dto.UltimaFechaRegistrada.ToString("dd/MM/yyyy", UiHelpers.EsCl)}";
            _lblKpiMesActualVal.Text = string.Create(UiHelpers.EsCl, $"{dto.TotalKilosMesActual:N0} Kg");
            string signo = dto.VariacionPorcentual >= 0 ? "+" : string.Empty;
            _lblKpiMesActualSub.Text = $"{signo}{dto.VariacionPorcentual.ToString("N1", UiHelpers.EsCl)}% vs período anterior";
            _lblKpiMesActualSub.ForeColor = dto.VariacionPorcentual >= 0 ? AppTheme.AccentEmerald : AppTheme.AccentAmber;

            _lblKpiMesAnteriorVal.Text = string.Create(UiHelpers.EsCl, $"{dto.TotalKilosMesAnterior:N0} Kg");
            _lblKpiMesAnteriorSub.Text = dto.PeriodoAnteriorLabel;

            _lblKpiJornadaVal.Text = string.Create(UiHelpers.EsCl, $"{dto.KilosUltimaJornada:N0} Kg");
            _lblKpiJornadaSub.Text = $"{dto.CamionesUltimaJornada} camiones · {dto.UltimaFechaRegistrada.ToString("dd/MM/yyyy", UiHelpers.EsCl)}";

            _lblKpiAlertasVal.Text = dto.AlertasPrecios.Count.ToString(UiHelpers.EsCl);
            _lblKpiAlertasSub.Text = dto.AlertasPrecios.Count == 0
                ? "Todas las compras valorizadas"
                : "Folios sin tarifa o por congelar";
            _lblKpiAlertasVal.ForeColor = dto.AlertasPrecios.Count > 0 ? AppTheme.AccentAmber : AppTheme.AccentEmerald;

            _chartWeekly.SetComparisonData("Tendencia Semanal (Actual vs Anterior)", dto.ComparativaSemanal);
            _chartTopProv.SetBarData("Top Proveedores del Período", dto.TopProveedores);
            _chartMaterials.SetDonutData("Distribución por Material", dto.KilosPorMaterial);

            _dgvAlertas.DataSource = null;
            _dgvAlertas.DataSource = dto.AlertasPrecios;

            _dgvUltimos.DataSource = null;
            _dgvUltimos.DataSource = dto.UltimosFolios;

            NotifyStatus($"Dashboard actualizado: {dto.TotalKilosMesActual.ToString("N0", UiHelpers.EsCl)} Kg en período {dto.PeriodoActualLabel}.");
        }, NotifyStatus, "Actualizando Dashboard...");
    }
}
