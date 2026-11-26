using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 1: Informe Resumen Ejecutivo consolidado por Planta, Tipo de Pesaje, Grupo de Material y Proveedores.
/// </summary>
public sealed class InformeResumenControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly Action<int> _navigateToTab;

    private readonly ComboBox _cmbPlanta = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbTipo = new() { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnTodoHistorial;
    private readonly Button _btnExportar;
    private readonly Button _btnImportarAhora;

    private readonly Label _lblSubPeriodo = new();
    private readonly Panel _emptyBanner = new();
    private readonly Label _lblKpiKilosVal;
    private readonly Label _lblKpiKilosSub;
    private readonly Label _lblKpiViajesVal;
    private readonly Label _lblKpiViajesSub;
    private readonly Label _lblKpiCatalogVal;
    private readonly Label _lblKpiCatalogSub;
    private readonly Label _lblKpiUltimaVal;
    private readonly Label _lblKpiUltimaSub;

    private readonly DashboardChartControl _chartTendencia = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartTopProv = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartGrupos = new() { Dock = DockStyle.Fill };

    private readonly DoubleBufferedDataGridView _dgvPlantas = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvUltimos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private InformeResumenDto? _lastDto;

    private sealed record PlantaOption(Planta? Planta, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record TipoOption(TipoPesaje? Tipo, string Label)
    {
        public override string ToString() => Label;
    }

    public InformeResumenControl(ScrapApplicationService app, IAuthService auth, Action<int> navigateToTab)
    {
        _app = app;
        _auth = auth;
        _navigateToTab = navigateToTab;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);
        AutoScroll = true;

        Panel filterCard = UiHelpers.BuildFilterCard(88);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        _cmbPlanta.Font = AppTheme.FontBody;
        _cmbPlanta.Items.AddRange(
        [
            new PlantaOption(null, "Todas las Plantas"),
            new PlantaOption(Planta.Santiago, "Santiago (Placilla 2)"),
            new PlantaOption(Planta.Placilla, "Placilla"),
            new PlantaOption(Planta.Talcahuano, "Talcahuano")
        ]);
        _cmbPlanta.SelectedIndex = 0;

        _cmbTipo.Font = AppTheme.FontBody;
        _cmbTipo.Items.AddRange(
        [
            new TipoOption(null, "Todos los Tipos"),
            new TipoOption(TipoPesaje.RecepcionCarga, "Recepción / Compras"),
            new TipoOption(TipoPesaje.DespachoCarga, "Despacho de Carga"),
            new TipoOption(TipoPesaje.MovimientoInterno, "Movimiento Interno")
        ]);
        _cmbTipo.SelectedIndex = 0;

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _btnConsultar = AppTheme.CreateButton("Actualizar Resumen", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ActivateAsync(), "Calcula el informe resumen con los filtros seleccionados", Tip, 165, 32);
        _btnTodoHistorial = AppTheme.CreateButton("Histórico", IconKind.Refresh, ButtonVariant.Secondary,
            async (_, _) =>
            {
                _dtpDesde.Value = new DateTime(2011, 1, 1);
                _dtpHasta.Value = DateTime.Today;
                await ActivateAsync();
            }, "Amplía el rango desde 2011 hasta hoy", Tip, 110, 32);
        _btnExportar = AppTheme.CreateButton("Exportar Excel", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Genera informe resumen multi-hoja (.xlsx)", Tip, 145, 32);

        TableLayoutPanel filterRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 0) },
            _cmbPlanta,
            new Label { Text = "Movimiento:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 7, 4, 0) },
            _cmbTipo,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 7, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 7, 4, 0) },
            _dtpHasta
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        _lblSubPeriodo.Text = "Resumen ejecutivo operativo";
        _lblSubPeriodo.Font = AppTheme.FontSubtitle;
        _lblSubPeriodo.ForeColor = AppTheme.TextSecondary;
        _lblSubPeriodo.AutoSize = true;
        _lblSubPeriodo.Margin = new Padding(14, 8, 0, 0);
        r2.Controls.AddRange([_btnConsultar, _btnTodoHistorial, _btnExportar, _lblSubPeriodo]);

        filterRows.Controls.Add(r1, 0, 0);
        filterRows.Controls.Add(r2, 0, 1);
        filterCard.Controls.Add(filterRows);

        // Banner contextual si aún no se importaron datos desde WMDBSPC.GDB
        _emptyBanner.Dock = DockStyle.Top;
        _emptyBanner.Height = 46;
        _emptyBanner.BackColor = Color.FromArgb(254, 249, 195);
        _emptyBanner.Padding = new Padding(12, 6, 12, 6);
        _emptyBanner.Visible = false;
        Label lblBannerMsg = new()
        {
            Text = "Aún no hay pesajes registrados en SQLite. Importe los datos reales desde WMDBSPC.GDB:",
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.HeaderDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _btnImportarAhora = AppTheme.CreateButton("Ir a Importador Firebird", IconKind.Import, ButtonVariant.Gold,
            (_, _) => _navigateToTab(3), "Abrir el módulo Importador conectado a \\\\192.168.2.56\\bdwm\\placilla2\\WMDBSPC.GDB", Tip, 200, 32);
        FlowLayoutPanel bannerRight = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
        bannerRight.Controls.Add(_btnImportarAhora);
        _emptyBanner.Controls.Add(lblBannerMsg);
        _emptyBanner.Controls.Add(bannerRight);

        TableLayoutPanel kpis = new()
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 6)
        };
        for (int i = 0; i < 4; i++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        kpis.Controls.Add(UiHelpers.CreateKpiCard("VOLUMEN TOTAL PERÍODO", AppTheme.AccentCyan, out _lblKpiKilosVal, out _lblKpiKilosSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PESAJES / PROMEDIO", AppTheme.AccentBlue, out _lblKpiViajesVal, out _lblKpiViajesSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PROVEEDORES Y MATERIALES", AppTheme.AccentGold, out _lblKpiCatalogVal, out _lblKpiCatalogSub), 2, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("ÚLTIMA JORNADA", AppTheme.AccentEmerald, out _lblKpiUltimaVal, out _lblKpiUltimaSub), 3, 0);

        TableLayoutPanel charts = new()
        {
            Dock = DockStyle.Top,
            Height = 238,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 6)
        };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        _chartTendencia.Margin = new Padding(4);
        _chartTopProv.Margin = new Padding(4);
        _chartGrupos.Margin = new Padding(4);
        charts.Controls.Add(_chartTendencia, 0, 0);
        charts.Controls.Add(_chartTopProv, 1, 0);
        charts.Controls.Add(_chartGrupos, 2, 0);

        TableLayoutPanel bottom = new()
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(820, 220),
            ColumnCount = 2,
            RowCount = 1
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

        Panel pnlPlantas = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        Label lblPlTitle = new() { Text = "Resumen por Planta", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 26 };
        _dgvPlantas.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Planta", nameof(PlantaResumenItem.NombrePlanta), 100),
            UiHelpers.AddNumberCol("Viajes", nameof(PlantaResumenItem.Viajes), "N0", 55),
            UiHelpers.AddNumberCol("Kilos", nameof(PlantaResumenItem.Kilos), "N0", 85),
            UiHelpers.AddNumberCol("%", nameof(PlantaResumenItem.Porcentaje), "N1", 50)
        ]);
        pnlPlantas.Controls.Add(_dgvPlantas);
        pnlPlantas.Controls.Add(lblPlTitle);

        Panel pnlUltimos = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        Label lblUltTitle = new() { Text = "Últimos Movimientos Registrados", Font = AppTheme.FontSection, ForeColor = AppTheme.HeaderDark, Dock = DockStyle.Top, Height = 26 };
        _dgvUltimos.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(Pesaje.Folio), "N0", 55),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 65),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 70),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 60),
            UiHelpers.AddTextCol("Proveedor", nameof(Pesaje.NomCliente), 130),
            UiHelpers.AddTextCol("Grupo Material", nameof(Pesaje.GrupoMaterial), 115),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 65)
        ]);
        pnlUltimos.Controls.Add(_dgvUltimos);
        pnlUltimos.Controls.Add(lblUltTitle);

        bottom.Controls.Add(pnlPlantas, 0, 0);
        bottom.Controls.Add(pnlUltimos, 1, 0);

        Controls.Add(bottom);
        Controls.Add(charts);
        Controls.Add(kpis);
        Controls.Add(_emptyBanner);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        _btnConsultar.Enabled = _auth.HasPermission(PermisoSistema.VerInformeResumen);
        _btnTodoHistorial.Enabled = _btnConsultar.Enabled;
        _btnExportar.Enabled = _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnImportarAhora.Enabled = _auth.HasPermission(PermisoSistema.ImportarDatos);
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta? planta = (_cmbPlanta.SelectedItem as PlantaOption)?.Planta;
            TipoPesaje? tipo = (_cmbTipo.SelectedItem as TipoOption)?.Tipo;
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastDto = await _app.GetInformeResumenAsync(planta, tipo, desde, hasta, ct);
            _emptyBanner.Visible = _lastDto.TotalViajes == 0 && _lastDto.UltimosFolios.Count == 0;

            _lblSubPeriodo.Text = $"Período: {_lastDto.PeriodoActualLabel}  vs  {_lastDto.PeriodoAnteriorLabel}";
            _lblKpiKilosVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.TotalKilos:N0} Kg");
            string sig = _lastDto.VariacionPorcentual >= 0 ? "+" : string.Empty;
            _lblKpiKilosSub.Text = $"{sig}{_lastDto.VariacionPorcentual.ToString("N1", UiHelpers.EsCl)}% vs período previo";
            _lblKpiKilosSub.ForeColor = _lastDto.VariacionPorcentual >= 0 ? AppTheme.AccentEmerald : AppTheme.AccentAmber;

            _lblKpiViajesVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.TotalViajes:N0} viajes");
            _lblKpiViajesSub.Text = string.Create(UiHelpers.EsCl, $"Promedio {_lastDto.PromedioKilosViaje:N0} Kg / viaje");

            _lblKpiCatalogVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.ProveedoresActivos} prov. / {_lastDto.ProductosActivos} prod.");
            _lblKpiCatalogSub.Text = "Activos en el rango filtrado";

            _lblKpiUltimaVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.KilosUltimaFecha:N0} Kg");
            _lblKpiUltimaSub.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.ViajesUltimaFecha} entregas · {_lastDto.UltimaFecha:dd/MM/yyyy}");

            _chartTendencia.SetComparisonData("Tendencia Comparativa de Kilos", _lastDto.Tendencia);
            _chartTopProv.SetBarData("Principales Proveedores / Clientes", _lastDto.TopProveedores);
            _chartGrupos.SetDonutData("Participación por Grupo", _lastDto.PorGrupo);

            _dgvPlantas.DataSource = null;
            _dgvPlantas.DataSource = _lastDto.PorPlanta;

            _dgvUltimos.DataSource = null;
            _dgvUltimos.DataSource = _lastDto.UltimosFolios;

            NotifyStatus($"Informe Resumen: {_lastDto.TotalViajes:N0} viajes / {_lastDto.TotalKilos.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Calculando Informe Resumen...");
    }

    private void Exportar()
    {
        if (_lastDto is null || (_lastDto.TotalViajes == 0 && _lastDto.UltimosFolios.Count == 0))
        {
            MessageBox.Show(this, "No hay datos en el período actual para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"InformeResumen_Cambiaso_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportInformeResumen(
                path,
                _lastDto.PeriodoActualLabel,
                [.. _lastDto.PorPlanta.Select(x => (x.NombrePlanta, x.Viajes, x.Kilos))],
                [.. _lastDto.PorGrupo.Select(x => (x.Grupo, x.Viajes, x.Kilos, x.Porcentaje))],
                [.. _lastDto.TopProveedores.Select(x => (string.Empty, x.Label, 0, x.Value, _lastDto.TotalKilos > 0 ? Math.Round(x.Value * 100.0 / _lastDto.TotalKilos, 1) : 0.0))],
                _lastDto.UltimosFolios);

            NotifyStatus($"Resumen exportado en: {saved}");
            MessageBox.Show(this, $"Informe resumen generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
