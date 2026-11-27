using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 1: Informe Resumen Ejecutivo, Filtros Avanzados, Segmentación Dinámica, Comparativos YoY/MoM,
/// Fichas de Estadísticas y Exportación Completa o por Selección.
/// </summary>
public sealed class InformeResumenControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly Action<int> _navigateToTab;

    private readonly ComboBox _cmbPlanta = new() { Width = 140 };
    private readonly ComboBox _cmbTipo = new() { Width = 140 };
    private readonly ComboBox _cmbGrupo = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbSegmento = new() { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly TextBox _txtProveedor = new() { Width = 150 };
    private readonly TextBox _txtProducto = new() { Width = 135 };
    private readonly CheckBox _chkExcluirCeros = new() { Text = "Excluir totales en 0", AutoSize = true };
    private readonly CheckBox _chkSinTraslados = new() { Text = "Sin traslados", AutoSize = true };

    private readonly Button _btnConsultar;
    private readonly Button _btnTodoHistorial;
    private readonly Button _btnExportar;
    private readonly Button _btnExportarSeleccion;
    private readonly Button _btnLimpiarFiltros;
    private readonly Button _btnImportarAhora;
    private readonly Button _btnVerTodosMovs;

    private readonly Panel _emptyBanner = new();
    private readonly Label _lblKpiKilosVal;
    private readonly Label _lblKpiKilosSub;
    private readonly Label _lblKpiViajesVal;
    private readonly Label _lblKpiViajesSub;
    private readonly Label _lblKpiYoYVal;
    private readonly Label _lblKpiYoYSub;
    private readonly Label _lblKpiStatsVal;
    private readonly Label _lblKpiStatsSub;
    private readonly Label _lblFichaStats = new();

    private readonly TabControl _reportTabs = new() { Dock = DockStyle.Fill };

    // SubTab 1: Resumen & Gráficos
    private readonly DashboardChartControl _chartTendencia = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartTopProv = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartGrupos = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvPlantas = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvUltimos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    // SubTab 2: Comparativo Mes a Mes (MoM) & Año a Año (YoY)
    private readonly DashboardChartControl _chartYoY = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvAnual = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvMensual = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    // SubTab 3: Segmentación Dinámica y Explorador
    private readonly Label _lblSegStatus = new();
    private readonly DoubleBufferedDataGridView _dgvSegmentacion = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvMovimientos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    private InformeResumenDto? _lastDto;
    private IReadOnlyList<MaterialGroupNode> _nodes = [];
    private Dictionary<string, string> _productGroupMap = new(StringComparer.OrdinalIgnoreCase);

    private sealed record GrupoOpt(long? NodeId, string Label) { public override string ToString() => Label; }
    private sealed record SegmentoOpt(SegmentacionDimension Dimension, string Label) { public override string ToString() => Label; }

    public InformeResumenControl(ScrapApplicationService app, IAuthService auth, Action<int> navigateToTab)
    {
        _app = app;
        _auth = auth;
        _navigateToTab = navigateToTab;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);
        Size = new Size(1280, 760);

        _cmbPlanta.Width = 136;
        _cmbPlanta.Margin = new Padding(0, 3, 6, 3);
        _cmbTipo.Width = 138;
        _cmbTipo.Margin = new Padding(0, 3, 6, 3);
        _cmbGrupo.Width = 158;
        _cmbGrupo.Margin = new Padding(0, 3, 6, 3);
        _cmbSegmento.Width = 142;
        _cmbSegmento.Margin = new Padding(0, 3, 6, 3);
        _txtProveedor.Width = 150;
        _txtProveedor.Margin = new Padding(0, 3, 6, 3);
        _txtProducto.Width = 128;
        _txtProducto.Margin = new Padding(0, 3, 6, 3);

        UiHelpers.PopulatePlantaFilterCombo(_cmbPlanta);
        UiHelpers.PopulateTipoFilterCombo(_cmbTipo, defaultRecepcion: false);

        _cmbGrupo.Font = AppTheme.FontBody;
        _cmbGrupo.Items.Add(new GrupoOpt(null, "Todos los Grupos"));
        _cmbGrupo.SelectedIndex = 0;

        _cmbSegmento.Font = AppTheme.FontBody;
        _cmbSegmento.Items.AddRange(
        [
            new SegmentoOpt(SegmentacionDimension.GrupoMaterial, "Grupo Material"),
            new SegmentoOpt(SegmentacionDimension.Planta, "Planta"),
            new SegmentoOpt(SegmentacionDimension.TipoPesaje, "Tipo Movimiento"),
            new SegmentoOpt(SegmentacionDimension.Proveedor, "Proveedor / Cliente"),
            new SegmentoOpt(SegmentacionDimension.Producto, "Producto"),
            new SegmentoOpt(SegmentacionDimension.Origen, "Origen"),
            new SegmentoOpt(SegmentacionDimension.Mes, "Mes"),
            new SegmentoOpt(SegmentacionDimension.Anio, "Año")
        ]);
        _cmbSegmento.SelectedIndex = 0;

        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = DateTime.Today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);
        _dtpDesde.Margin = new Padding(0, 3, 6, 3);
        _dtpHasta.Margin = new Padding(0, 3, 6, 3);

        AppTheme.ApplyInputStyle(_txtProveedor);
        _txtProveedor.PlaceholderText = "Proveedor / RUT / Patente";
        AppTheme.ApplyInputStyle(_txtProducto);
        _txtProducto.PlaceholderText = "Cód / Producto";

        _chkExcluirCeros.Font = AppTheme.FontBodyBold;
        _chkExcluirCeros.Margin = new Padding(8, 7, 8, 3);
        _chkExcluirCeros.CheckedChanged += async (_, _) => await ActivateAsync();

        _chkSinTraslados.Font = AppTheme.FontBody;
        _chkSinTraslados.Margin = new Padding(6, 7, 6, 3);
        _chkSinTraslados.CheckedChanged += async (_, _) => await ActivateAsync();

        _btnConsultar = AppTheme.CreateButton("Analizar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ActivateAsync(), "Ejecuta análisis, KPIs, comparativos y segmentación", Tip, 0, 32);
        _btnTodoHistorial = AppTheme.CreateButton("Histórico", IconKind.Refresh, ButtonVariant.Secondary,
            async (_, _) =>
            {
                _dtpDesde.Value = new DateTime(2011, 1, 1);
                _dtpHasta.Value = DateTime.Today;
                await ActivateAsync();
            }, "Amplía el rango de consulta desde 2011 hasta hoy", Tip, 0, 32);
        _btnLimpiarFiltros = AppTheme.CreateButton("Limpiar", IconKind.Close, ButtonVariant.Secondary,
            async (_, _) => await ClearFiltersAsync(), "Restablece todos los filtros", Tip, 0, 32);
        _btnExportar = AppTheme.CreateButton("Exportar Resumen", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportarResumenDirecto(), "Exporta informe resumen completo a Excel (.xlsx)", Tip, 0, 32);
        _btnExportarSeleccion = AppTheme.CreateButton("Exportar Selección...", IconKind.Save, ButtonVariant.Gold,
            (_, _) => ExportarPersonalizado(), "Exportar solo las filas seleccionadas o armar un informe personalizado", Tip, 0, 32);

        FlowLayoutPanel r1 = UiHelpers.CreateFilterRow(
            UiHelpers.CreateFilterLabel("Planta:", 0),
            _cmbPlanta,
            UiHelpers.CreateFilterLabel("Mov:"),
            _cmbTipo,
            UiHelpers.CreateFilterLabel("Grupo:"),
            _cmbGrupo,
            UiHelpers.CreateFilterLabel("Desde:"),
            _dtpDesde,
            UiHelpers.CreateFilterLabel("Hasta:"),
            _dtpHasta);

        FlowLayoutPanel r2 = UiHelpers.CreateFilterRow(
            UiHelpers.CreateFilterLabel("Segmentar:", 0),
            _cmbSegmento,
            UiHelpers.CreateFilterLabel("Prov:"),
            _txtProveedor,
            UiHelpers.CreateFilterLabel("Prod:"),
            _txtProducto,
            _chkExcluirCeros,
            _chkSinTraslados);

        FlowLayoutPanel r3 = UiHelpers.CreateFilterRow(
            _btnConsultar, _btnTodoHistorial, _btnLimpiarFiltros, _btnExportar, _btnExportarSeleccion);

        Panel filterCard = UiHelpers.BuildAutoFilterCard(r1, r2, r3);

        _emptyBanner.Dock = DockStyle.Top;
        _emptyBanner.Height = 50;
        _emptyBanner.BackColor = Color.FromArgb(254, 249, 195);
        _emptyBanner.Padding = new Padding(12, 6, 12, 6);
        _emptyBanner.Visible = false;
        Label lblBannerMsg = new()
        {
            Text = "Aún no hay pesajes registrados en SQLite. Importe los datos reales desde WMDBSPC.GDB:",
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.HeaderDark,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _btnImportarAhora = AppTheme.CreateButton("Ir a Importador Firebird", IconKind.Import, ButtonVariant.Gold,
            (_, _) => _navigateToTab(3), "Abrir módulo Importador WMDBSPC.GDB", Tip, 0, 34);
        _btnImportarAhora.Margin = new Padding(0, 2, 0, 0);
        FlowLayoutPanel bannerRight = new() { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        bannerRight.Controls.Add(_btnImportarAhora);
        _emptyBanner.Controls.Add(lblBannerMsg);
        _emptyBanner.Controls.Add(bannerRight);

        TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 98, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 4, 0, 4) };
        for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        kpis.Controls.Add(UiHelpers.CreateKpiCard("VOLUMEN DEL PERÍODO", AppTheme.AccentCyan, out _lblKpiKilosVal, out _lblKpiKilosSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("VIAJES / PROMEDIO Y MEDIANA", AppTheme.AccentBlue, out _lblKpiViajesVal, out _lblKpiViajesSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("COMPARATIVO AÑO A AÑO (YoY)", AppTheme.AccentGold, out _lblKpiYoYVal, out _lblKpiYoYSub), 2, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("RITMO DIARIO Y COBERTURA", AppTheme.AccentEmerald, out _lblKpiStatsVal, out _lblKpiStatsSub), 3, 0);

        Panel statsBanner = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 2, 0, 6)
        };
        _lblFichaStats.Dock = DockStyle.Fill;
        _lblFichaStats.Font = AppTheme.FontSmallBold;
        _lblFichaStats.ForeColor = AppTheme.AccentGold;
        _lblFichaStats.AutoEllipsis = true;
        _lblFichaStats.TextAlign = ContentAlignment.MiddleLeft;
        _lblFichaStats.Text = "Ficha Estadística: cargando indicadores del período...";
        statsBanner.Controls.Add(_lblFichaStats);

        _reportTabs.Font = AppTheme.FontBodyBold;

        // Tab 1: Resumen Ejecutivo y Gráficos
        TabPage tpResumen = new("Resumen Ejecutivo y Gráficos") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        TableLayoutPanel charts = new() { Dock = DockStyle.Top, Height = 215, ColumnCount = 3, RowCount = 1 };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        _chartTendencia.Margin = new Padding(3);
        _chartTopProv.Margin = new Padding(3);
        _chartGrupos.Margin = new Padding(3);
        charts.Controls.Add(_chartTendencia, 0, 0);
        charts.Controls.Add(_chartTopProv, 1, 0);
        charts.Controls.Add(_chartGrupos, 2, 0);

        TableLayoutPanel bottom = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

        Panel pnlPlantas = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Margin = new Padding(3), Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblPlTitle = new() { Text = "Resumen por Planta", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 24 };
        _dgvPlantas.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Planta", nameof(PlantaResumenItem.NombrePlanta), 100),
            UiHelpers.AddNumberCol("Viajes", nameof(PlantaResumenItem.Viajes), "N0", 55),
            UiHelpers.AddNumberCol("Kilos", nameof(PlantaResumenItem.Kilos), "N0", 85),
            UiHelpers.AddNumberCol("%", nameof(PlantaResumenItem.Porcentaje), "N1", 50)
        ]);
        pnlPlantas.Controls.Add(_dgvPlantas);
        pnlPlantas.Controls.Add(lblPlTitle);

        Panel pnlUltimos = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Margin = new Padding(3), Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblUltTitle = new() { Text = "Últimos Movimientos (multi-selección habilitada)", Font = AppTheme.FontSection, ForeColor = AppTheme.HeaderDark, Dock = DockStyle.Top, Height = 24 };
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
        tpResumen.Controls.Add(bottom);
        tpResumen.Controls.Add(charts);

        // Tab 2: Comparativo Mes a Mes (MoM) y Año a Año (YoY)
        TabPage tpComp = new("Comparativo Año a Año (YoY) y Mes a Mes (MoM)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        TableLayoutPanel compTop = new() { Dock = DockStyle.Top, Height = 215, ColumnCount = 2, RowCount = 1 };
        compTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        compTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        _chartYoY.Margin = new Padding(3);

        Panel pnlAnual = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Margin = new Padding(3), Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblAnualTitle = new() { Text = "Comparativo Año a Año (YoY)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 24 };
        _dgvAnual.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Año", nameof(ComparativoAnualDto.Anio), "0", 48),
            UiHelpers.AddNumberCol("Viajes", nameof(ComparativoAnualDto.Viajes), "N0", 56),
            UiHelpers.AddNumberCol("Kilos", nameof(ComparativoAnualDto.Kilos), "N0", 85),
            UiHelpers.AddNumberCol("Ton.", nameof(ComparativoAnualDto.Toneladas), "N1", 62),
            UiHelpers.AddNumberCol("Año Ant. Kg", nameof(ComparativoAnualDto.KilosAnioAnterior), "N0", 82),
            UiHelpers.AddNumberCol("Var YoY %", nameof(ComparativoAnualDto.VariacionAnualPct), "N1", 62),
            UiHelpers.AddNumberCol("% Hist.", nameof(ComparativoAnualDto.ParticipacionHistoricaPct), "N1", 52),
            UiHelpers.AddNumberCol("Prom Kg/V", nameof(ComparativoAnualDto.PromedioKgViaje), "N0", 65)
        ]);
        _dgvAnual.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvAnual.Columns[e.ColumnIndex].DataPropertyName == nameof(ComparativoAnualDto.VariacionAnualPct))
            {
                UiHelpers.FormatVarianceCell(e);
            }
        };
        pnlAnual.Controls.Add(_dgvAnual);
        pnlAnual.Controls.Add(lblAnualTitle);
        compTop.Controls.Add(_chartYoY, 0, 0);
        compTop.Controls.Add(pnlAnual, 1, 0);

        Panel pnlMensual = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Margin = new Padding(3, 6, 3, 0), Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblMensualTitle = new() { Text = "Comparativo Mes a Mes (MoM) vs Mismo Mes Año Anterior (YoY)", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 24 };
        _dgvMensual.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Mes", nameof(ComparativoMensualDto.MesLabel), 85),
            UiHelpers.AddNumberCol("Viajes", nameof(ComparativoMensualDto.Viajes), "N0", 55),
            UiHelpers.AddNumberCol("Kilos Mes", nameof(ComparativoMensualDto.KilosActual), "N0", 90),
            UiHelpers.AddNumberCol("Mes Ant.", nameof(ComparativoMensualDto.KilosMesAnterior), "N0", 85),
            UiHelpers.AddNumberCol("Var MoM %", nameof(ComparativoMensualDto.VariacionMoMPct), "N1", 68),
            UiHelpers.AddNumberCol("Mismo Mes Año Ant.", nameof(ComparativoMensualDto.KilosAnioAnterior), "N0", 95),
            UiHelpers.AddNumberCol("Var YoY %", nameof(ComparativoMensualDto.VariacionYoYPct), "N1", 68),
            UiHelpers.AddNumberCol("Prom Kg/Viaje", nameof(ComparativoMensualDto.PromedioKgViaje), "N0", 70)
        ]);
        _dgvMensual.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 &&
                (_dgvMensual.Columns[e.ColumnIndex].DataPropertyName == nameof(ComparativoMensualDto.VariacionMoMPct) ||
                 _dgvMensual.Columns[e.ColumnIndex].DataPropertyName == nameof(ComparativoMensualDto.VariacionYoYPct)))
            {
                UiHelpers.FormatVarianceCell(e);
            }
        };
        pnlMensual.Controls.Add(_dgvMensual);
        pnlMensual.Controls.Add(lblMensualTitle);

        tpComp.Controls.Add(pnlMensual);
        tpComp.Controls.Add(compTop);

        // Tab 3: Segmentación Dinámica y Explorador de Datos
        TabPage tpSeg = new("Segmentación y Análisis de Datos") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        SplitContainer segSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        segSplit.HandleCreated += (_, _) =>
        {
            if (segSplit.Height > 320)
            {
                segSplit.Panel1MinSize = 140;
                segSplit.Panel2MinSize = 140;
                segSplit.SplitterDistance = (int)(segSplit.Height * 0.45);
            }
        };

        Panel pnlSegTop = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Panel segHdr = new() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 0, 0, 4) };
        Label lblSegTitle = new() { Text = "Segmentación Dinámica (seleccione una o varias filas para filtrar/exportar)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        _btnVerTodosMovs = AppTheme.CreateButton("Ver Todos", IconKind.Refresh, ButtonVariant.Secondary,
            (_, _) =>
            {
                _dgvSegmentacion.ClearSelection();
                BindMovimientosGrid(_lastDto?.MovimientosFiltrados ?? []);
            }, "Mostrar todos los movimientos filtrados sin filtro por segmento", Tip, 0, 32);
        _btnVerTodosMovs.Margin = new Padding(8, 2, 0, 0);
        FlowLayoutPanel segHdrRight = new() { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        segHdrRight.Controls.Add(_btnVerTodosMovs);
        segHdr.Controls.Add(lblSegTitle);
        segHdr.Controls.Add(segHdrRight);

        _dgvSegmentacion.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Segmento", nameof(SegmentacionItemDto.Segmento), 160),
            UiHelpers.AddNumberCol("Viajes", nameof(SegmentacionItemDto.Viajes), "N0", 55),
            UiHelpers.AddNumberCol("Kilos Actual", nameof(SegmentacionItemDto.KilosActual), "N0", 90),
            UiHelpers.AddNumberCol("% Part.", nameof(SegmentacionItemDto.ParticipacionPct), "N1", 55),
            UiHelpers.AddNumberCol("Prom Kg/V", nameof(SegmentacionItemDto.PromedioKgViaje), "N0", 65),
            UiHelpers.AddNumberCol("Período Ant.", nameof(SegmentacionItemDto.KilosPeriodoAnterior), "N0", 85),
            UiHelpers.AddNumberCol("Var %", nameof(SegmentacionItemDto.VariacionPct), "N1", 55),
            UiHelpers.AddNumberCol("Prov. Únicos", nameof(SegmentacionItemDto.ProveedoresUnicos), "N0", 60)
        ]);
        _dgvSegmentacion.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvSegmentacion.Columns[e.ColumnIndex].DataPropertyName == nameof(SegmentacionItemDto.VariacionPct))
            {
                UiHelpers.FormatVarianceCell(e);
            }
        };
        _dgvSegmentacion.SelectionChanged += (_, _) => ApplySegmentSelectionFilter();

        pnlSegTop.Controls.Add(_dgvSegmentacion);
        pnlSegTop.Controls.Add(segHdr);

        Panel pnlSegBot = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _lblSegStatus.Text = "Movimientos Filtrados (Ctrl/Shift + clic para exportar filas específicas)";
        _lblSegStatus.Font = AppTheme.FontSection;
        _lblSegStatus.ForeColor = AppTheme.AccentBlue;
        _lblSegStatus.Dock = DockStyle.Top;
        _lblSegStatus.Height = 24;

        _dgvMovimientos.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(Pesaje.Folio), "N0", 55),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 65),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 70),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 60),
            UiHelpers.AddTextCol("RUT", nameof(Pesaje.RutCliente), 75),
            UiHelpers.AddTextCol("Proveedor", nameof(Pesaje.NomCliente), 135),
            UiHelpers.AddTextCol("Grupo Material", nameof(Pesaje.GrupoMaterial), 115),
            UiHelpers.AddTextCol("Cód", nameof(Pesaje.CodProducto), 68),
            UiHelpers.AddTextCol("Producto", nameof(Pesaje.NomProducto), 120),
            UiHelpers.AddTextCol("Origen", nameof(Pesaje.Origen), 80),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 68)
        ]);
        pnlSegBot.Controls.Add(_dgvMovimientos);
        pnlSegBot.Controls.Add(_lblSegStatus);

        segSplit.Panel1.Controls.Add(pnlSegTop);
        segSplit.Panel2.Controls.Add(pnlSegBot);
        tpSeg.Controls.Add(segSplit);

        _reportTabs.TabPages.AddRange([tpResumen, tpComp, tpSeg]);

        Controls.Add(_reportTabs);
        Controls.Add(statsBanner);
        Controls.Add(kpis);
        Controls.Add(_emptyBanner);
        Controls.Add(filterCard);

        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canRead = _auth.HasPermission(PermisoSistema.VerInformeResumen);
        bool canExport = _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnConsultar.Enabled = canRead;
        _btnTodoHistorial.Enabled = canRead;
        _btnLimpiarFiltros.Enabled = canRead;
        _btnVerTodosMovs.Enabled = canRead;
        _btnExportar.Enabled = canExport;
        _btnExportarSeleccion.Enabled = canExport;
        _btnImportarAhora.Enabled = _auth.HasPermission(PermisoSistema.ImportarDatos);
    }

    private async Task ClearFiltersAsync()
    {
        _cmbPlanta.SelectedIndex = 0;
        _cmbTipo.SelectedIndex = 0;
        _cmbGrupo.SelectedIndex = 0;
        _cmbSegmento.SelectedIndex = 0;
        _txtProveedor.Clear();
        _txtProducto.Clear();
        _chkExcluirCeros.Checked = false;
        _chkSinTraslados.Checked = false;
        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = DateTime.Today;
        await ActivateAsync();
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await LoadGrupoComboAsync(ct);

            InformeResumenFilterDto filter = new(
                Planta: UiHelpers.GetSelectedNullablePlanta(_cmbPlanta),
                TipoPesaje: UiHelpers.GetSelectedNullableTipo(_cmbTipo),
                Desde: DateOnly.FromDateTime(_dtpDesde.Value),
                Hasta: DateOnly.FromDateTime(_dtpHasta.Value),
                GrupoNodeId: (_cmbGrupo.SelectedItem as GrupoOpt)?.NodeId,
                ProveedorFiltro: _txtProveedor.Text,
                ProductoFiltro: _txtProducto.Text,
                Segmentacion: (_cmbSegmento.SelectedItem as SegmentoOpt)?.Dimension ?? SegmentacionDimension.GrupoMaterial,
                ExcluirTotalesCero: _chkExcluirCeros.Checked,
                ExcluirTrasladosInternos: _chkSinTraslados.Checked);

            _lastDto = await _app.GetInformeResumenAsync(filter, ct);
            _emptyBanner.Visible = _lastDto.TotalViajes == 0 && _lastDto.UltimosFolios.Count == 0;

            _lblKpiKilosVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.TotalKilos:N0} Kg");
            string sigPrev = _lastDto.VariacionPorcentual >= 0 ? "+" : string.Empty;
            _lblKpiKilosSub.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.Estadisticas.TotalToneladas:N1} Ton · {sigPrev}{_lastDto.VariacionPorcentual:N1}% vs período ant.");
            _lblKpiKilosSub.ForeColor = _lastDto.VariacionPorcentual >= 0 ? AppTheme.AccentEmerald : AppTheme.AccentAmber;

            _lblKpiViajesVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.TotalViajes:N0} viajes");
            _lblKpiViajesSub.Text = string.Create(UiHelpers.EsCl, $"Prom {_lastDto.PromedioKilosViaje:N0} · Med {_lastDto.Estadisticas.MedianaKgViaje:N0} Kg");

            string sigYoY = _lastDto.Estadisticas.VariacionAnualYoYPct >= 0 ? "+" : string.Empty;
            _lblKpiYoYVal.Text = string.Create(UiHelpers.EsCl, $"{sigYoY}{_lastDto.Estadisticas.VariacionAnualYoYPct:N1}% YoY");
            _lblKpiYoYVal.ForeColor = _lastDto.Estadisticas.VariacionAnualYoYPct >= 0 ? AppTheme.AccentEmerald : AppTheme.AccentAmber;
            _lblKpiYoYSub.Text = string.Create(UiHelpers.EsCl, $"Mismo rango año ant.: {_lastDto.Estadisticas.KilosMismoPeriodoAnioAnterior:N0} Kg");

            _lblKpiStatsVal.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.Estadisticas.PromedioDiarioKg:N0} Kg/día");
            _lblKpiStatsSub.Text = string.Create(UiHelpers.EsCl, $"{_lastDto.Estadisticas.DiasOperativos} días · {_lastDto.ProveedoresActivos} prov · {_lastDto.Estadisticas.PatentesUnicas} pat.");

            _lblFichaStats.Text = string.Create(
                UiHelpers.EsCl,
                $"Estadísticas [{_lastDto.PeriodoActualLabel}]  |  Mín: {_lastDto.Estadisticas.PesoMinimoKg:N0} Kg  ·  Máx: {_lastDto.Estadisticas.PesoMaximoKg:N0} Kg  ·  Desv.Est.: {_lastDto.Estadisticas.DesviacionEstandarKg:N0} Kg  ·  Mejor Mes: {_lastDto.Estadisticas.MejorMesLabel} ({_lastDto.Estadisticas.MejorMesKilos:N0} Kg)  ·  Mejor Año: {_lastDto.Estadisticas.MejorAnioLabel}");

            _chartTendencia.SetComparisonData("Tendencia Comparativa de Kilos", _lastDto.Tendencia, "Período Actual", "Comparación Prev./YoY");
            _chartTopProv.SetBarData("Top Proveedores / Clientes", _lastDto.TopProveedores);
            _chartGrupos.SetDonutData("Participación por Grupo", _lastDto.PorGrupo);

            List<WeekComparisonDto> monthlyChartSeries = [.. _lastDto.PorMes
                .OrderBy(m => m.Anio)
                .ThenBy(m => m.Mes)
                .TakeLast(12)
                .Select(m => new WeekComparisonDto($"{m.Mes:D2}/{m.Anio % 100:D2}", m.KilosActual, m.KilosAnioAnterior))];
            _chartYoY.SetComparisonData("Evolución Mensual vs Mismo Mes Año Anterior", monthlyChartSeries, "Mes Actual", "Año Anterior (YoY)");

            _dgvPlantas.DataSource = null;
            _dgvPlantas.DataSource = _lastDto.PorPlanta;

            _dgvUltimos.DataSource = null;
            _dgvUltimos.DataSource = _lastDto.UltimosFolios;

            _dgvAnual.DataSource = null;
            _dgvAnual.DataSource = _lastDto.PorAnio;

            _dgvMensual.DataSource = null;
            _dgvMensual.DataSource = _lastDto.PorMes;

            _dgvSegmentacion.DataSource = null;
            _dgvSegmentacion.DataSource = _lastDto.Segmentacion;

            BindMovimientosGrid(_lastDto.MovimientosFiltrados);

            NotifyStatus($"Informe Resumen: {_lastDto.TotalViajes:N0} viajes / {_lastDto.TotalKilos.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Calculando analítica e indicadores...");
    }

    private async Task LoadGrupoComboAsync(CancellationToken ct)
    {
        if (!_auth.HasPermission(PermisoSistema.VerGrupoMaterial))
        {
            return;
        }

        _nodes = await _app.GetMaterialGroupTreeAsync(ct);
        _productGroupMap = BusinessRules.BuildProductToRootGroupMap(_nodes);
        long? prev = (_cmbGrupo.SelectedItem as GrupoOpt)?.NodeId;

        _cmbGrupo.Items.Clear();
        _cmbGrupo.Items.Add(new GrupoOpt(null, "Todos los Grupos"));
        foreach (MaterialGroupNode root in _nodes.Where(n => n.ParentId is null && n.TipoNodo == NodoGrupoTipo.CarpetaGrupo).OrderBy(n => n.Orden))
        {
            _cmbGrupo.Items.Add(new GrupoOpt(root.Id, root.Nombre));
        }

        int sel = 0;
        if (prev.HasValue)
        {
            for (int i = 0; i < _cmbGrupo.Items.Count; i++)
            {
                if (_cmbGrupo.Items[i] is GrupoOpt go && go.NodeId == prev.Value)
                {
                    sel = i;
                    break;
                }
            }
        }

        _cmbGrupo.SelectedIndex = sel;
    }

    private void ApplySegmentSelectionFilter()
    {
        if (_lastDto is null)
        {
            return;
        }

        List<SegmentacionItemDto> selectedSegs = [.. _dgvSegmentacion.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as SegmentacionItemDto)
            .OfType<SegmentacionItemDto>()];

        if (selectedSegs.Count == 0)
        {
            BindMovimientosGrid(_lastDto.MovimientosFiltrados);
            return;
        }

        HashSet<string> keys = new(selectedSegs.Select(s => s.Clave), StringComparer.OrdinalIgnoreCase);
        SegmentacionDimension dim = (_cmbSegmento.SelectedItem as SegmentoOpt)?.Dimension ?? SegmentacionDimension.GrupoMaterial;

        List<Pesaje> filtered = [.. _lastDto.MovimientosFiltrados.Where(p => keys.Contains(ResolveSegmentKeyForPesaje(p, dim)))];
        BindMovimientosGrid(filtered, $"Segmento(s) seleccionados: {filtered.Count:N0} pesajes ({filtered.Sum(x => (long)x.PesoNeto).ToString("N0", UiHelpers.EsCl)} Kg)");
    }

    private string ResolveSegmentKeyForPesaje(Pesaje p, SegmentacionDimension dim) => dim switch
    {
        SegmentacionDimension.Planta => p.Planta.ToString(),
        SegmentacionDimension.TipoPesaje => p.TipoPesaje.ToString(),
        SegmentacionDimension.Proveedor => string.IsNullOrWhiteSpace(Domain.ValueObjects.RutChileno.NormalizeBody(p.RutCliente))
            ? p.NomCliente.Trim().ToUpperInvariant()
            : Domain.ValueObjects.RutChileno.NormalizeBody(p.RutCliente),
        SegmentacionDimension.Producto => p.CodProducto.Trim().ToUpperInvariant(),
        SegmentacionDimension.Origen => string.IsNullOrWhiteSpace(p.Origen) ? "SIN ORIGEN" : p.Origen.Trim().ToUpperInvariant(),
        SegmentacionDimension.Mes => $"{p.FechaIngreso.Year:D4}-{p.FechaIngreso.Month:D2}",
        SegmentacionDimension.Anio => p.FechaIngreso.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => (_productGroupMap.TryGetValue(p.CodProducto.Trim().ToUpperInvariant(), out string? grp) && !string.IsNullOrWhiteSpace(grp)
            ? grp : p.GrupoMaterial).Trim().ToUpperInvariant()
    };

    private void BindMovimientosGrid(IReadOnlyList<Pesaje> items, string? customLabel = null)
    {
        _dgvMovimientos.DataSource = null;
        _dgvMovimientos.DataSource = items;
        _lblSegStatus.Text = customLabel ?? $"Movimientos Filtrados ({items.Count:N0} registros · Shift/Ctrl + clic para exportar selección)";
    }

    private void ExportarResumenDirecto()
    {
        if (_lastDto is null || (_lastDto.TotalViajes == 0 && _lastDto.MovimientosFiltrados.Count == 0))
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
                _lastDto.MovimientosFiltrados);

            NotifyStatus($"Resumen exportado en: {saved}");
            MessageBox.Show(this, $"Informe resumen generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportarPersonalizado()
    {
        if (_lastDto is null || (_lastDto.TotalViajes == 0 && _lastDto.MovimientosFiltrados.Count == 0))
        {
            MessageBox.Show(this, "Ejecute primero una consulta con datos antes de exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<Pesaje> selectedPesajes = GetSelectedMovimientos();
        List<SegmentacionItemDto> selectedSegs = [.. _dgvSegmentacion.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as SegmentacionItemDto).OfType<SegmentacionItemDto>()];
        List<ComparativoMensualDto> selectedMonths = [.. _dgvMensual.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ComparativoMensualDto).OfType<ComparativoMensualDto>()];
        List<ComparativoAnualDto> selectedYears = [.. _dgvAnual.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ComparativoAnualDto).OfType<ComparativoAnualDto>()];
        int totalSelectedCount = selectedPesajes.Count + selectedSegs.Count + selectedMonths.Count + selectedYears.Count;

        string[] sections =
        [
            "KPIs y Ficha Estadística",
            "Resumen por Planta",
            "Comparativo Año a Año (YoY)",
            "Comparativo Mes a Mes (MoM)",
            "Segmentación Dinámica",
            "Detalle de Movimientos"
        ];

        using ExportacionPersonalizadaDialog dlg = new(
            "Cambiaso · Análisis Personalizado de Scrap",
            totalSelectedCount,
            sections,
            _chkExcluirCeros.Checked);

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"AnalisisPersonalizado_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            IReadOnlyList<string> inc = dlg.SeccionesSeleccionadas;
            bool onlySel = dlg.SoloSeleccionActual;
            bool exZero = dlg.ExcluirCeros;

            List<(string Indicador, string Valor, string Detalle)> kpis = [];
            if (inc.Contains("KPIs y Ficha Estadística"))
            {
                kpis.Add(("Volumen Período", $"{_lastDto.TotalKilos:N0} Kg ({_lastDto.Estadisticas.TotalToneladas:N2} Ton)", $"Var. período previo: {_lastDto.VariacionPorcentual:N1}%"));
                kpis.Add(("Variación Anual (YoY)", $"{_lastDto.Estadisticas.VariacionAnualYoYPct:N1}%", $"Año ant.: {_lastDto.Estadisticas.KilosMismoPeriodoAnioAnterior:N0} Kg"));
                kpis.Add(("Viajes / Promedio / Mediana", $"{_lastDto.TotalViajes:N0} viajes", $"Prom {_lastDto.PromedioKilosViaje:N0} Kg · Med {_lastDto.Estadisticas.MedianaKgViaje:N0} Kg"));
                kpis.Add(("Rango Pesajes y Desviación", $"Mín {_lastDto.Estadisticas.PesoMinimoKg:N0} / Máx {_lastDto.Estadisticas.PesoMaximoKg:N0} Kg", $"Desv.Est.: {_lastDto.Estadisticas.DesviacionEstandarKg:N0} Kg"));
                kpis.Add(("Mejor Mes y Año Histórico", _lastDto.Estadisticas.MejorMesLabel, $"{_lastDto.Estadisticas.MejorMesKilos:N0} Kg (Año top: {_lastDto.Estadisticas.MejorAnioLabel})"));
            }

            List<ExportTableSection> tableSections = [];
            if (inc.Contains("Resumen por Planta"))
            {
                IEnumerable<PlantaResumenItem> rows = exZero ? _lastDto.PorPlanta.Where(x => x.Kilos > 0) : _lastDto.PorPlanta;
                tableSections.Add(new ExportTableSection(
                    "Por Planta",
                    "Resumen por Planta",
                    ["Planta", "Viajes", "Kilos Netos", "% Participación"],
                    [.. rows.Select(x => new object?[] { x.NombrePlanta, x.Viajes, x.Kilos, x.Porcentaje })]));
            }

            if (inc.Contains("Comparativo Año a Año (YoY)"))
            {
                IEnumerable<ComparativoAnualDto> src = (onlySel && selectedYears.Count > 0) ? selectedYears : _lastDto.PorAnio;
                if (exZero) src = src.Where(x => x.Kilos > 0);
                tableSections.Add(new ExportTableSection(
                    "Comparativo YoY",
                    "Comparativo Año a Año",
                    ["Año", "Viajes", "Kilos", "Toneladas", "Año Anterior Kg", "Var YoY %", "% Histórico", "Prom Kg/Viaje", "Proveedores"],
                    [.. src.Select(x => new object?[] { x.Anio, x.Viajes, x.Kilos, x.Toneladas, x.KilosAnioAnterior, x.VariacionAnualPct, x.ParticipacionHistoricaPct, x.PromedioKgViaje, x.ProveedoresActivos })]));
            }

            if (inc.Contains("Comparativo Mes a Mes (MoM)"))
            {
                IEnumerable<ComparativoMensualDto> src = (onlySel && selectedMonths.Count > 0) ? selectedMonths : _lastDto.PorMes;
                if (exZero) src = src.Where(x => x.KilosActual > 0);
                tableSections.Add(new ExportTableSection(
                    "Comparativo MoM",
                    "Comparativo Mes a Mes (MoM y YoY)",
                    ["Mes", "Viajes", "Kilos Actual", "Mes Anterior", "Var MoM %", "Mismo Mes Año Ant.", "Var YoY %", "Prom Kg/Viaje"],
                    [.. src.Select(x => new object?[] { x.MesLabel, x.Viajes, x.KilosActual, x.KilosMesAnterior, x.VariacionMoMPct, x.KilosAnioAnterior, x.VariacionYoYPct, x.PromedioKgViaje })]));
            }

            if (inc.Contains("Segmentación Dinámica"))
            {
                IEnumerable<SegmentacionItemDto> src = (onlySel && selectedSegs.Count > 0) ? selectedSegs : _lastDto.Segmentacion;
                if (exZero) src = src.Where(x => x.KilosActual > 0);
                tableSections.Add(new ExportTableSection(
                    "Segmentación",
                    $"Segmentación por {_cmbSegmento.SelectedItem}",
                    ["Segmento", "Viajes", "Kilos Actual", "% Part.", "Prom Kg/V", "Período Ant.", "Var %", "Proveedores Únicos"],
                    [.. src.Select(x => new object?[] { x.Segmento, x.Viajes, x.KilosActual, x.ParticipacionPct, x.PromedioKgViaje, x.KilosPeriodoAnterior, x.VariacionPct, x.ProveedoresUnicos })]));
            }

            if (inc.Contains("Detalle de Movimientos"))
            {
                IEnumerable<Pesaje> src = (onlySel && selectedPesajes.Count > 0)
                    ? selectedPesajes
                    : (_dgvMovimientos.DataSource as IEnumerable<Pesaje> ?? _lastDto.MovimientosFiltrados);
                if (exZero) src = src.Where(p => p.PesoNeto > 0);
                tableSections.Add(new ExportTableSection(
                    "Detalle Movimientos",
                    onlySel ? "Movimientos Seleccionados" : "Movimientos Filtrados",
                    ["Folio", "Planta", "Fecha", "Patente", "RUT", "Proveedor", "Grupo", "Código", "Producto", "Origen", "Neto Kg"],
                    [.. src.Select(p => new object?[] { p.Folio, p.Planta.ToShortTitle(), p.FechaIngreso, p.Patente, p.RutCliente, p.NomCliente, p.GrupoMaterial, p.CodProducto, p.NomProducto, p.Origen, p.PesoNeto })]));
            }

            string saved = _app.ExcelExporter.ExportReportePersonalizado(
                path,
                new ReportePersonalizadoPayload(dlg.TituloPersonalizado, _lastDto.PeriodoActualLabel, kpis, tableSections));

            NotifyStatus($"Análisis personalizado exportado: {saved}");
            MessageBox.Show(this, $"Informe personalizado exportado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<Pesaje> GetSelectedMovimientos()
    {
        IEnumerable<Pesaje> fromSegExplorer = _dgvMovimientos.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as Pesaje).OfType<Pesaje>();
        IEnumerable<Pesaje> fromUltimos = _dgvUltimos.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as Pesaje).OfType<Pesaje>();
        return [.. fromSegExplorer.Concat(fromUltimos).DistinctBy(p => (p.Planta, p.Folio, p.HoraIngreso))];
    }
}
