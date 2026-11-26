using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 3: Análisis Completo y Robusto de Proveedores (Ranking Pareto ABC, Tendencia vs período anterior,
/// Mix de Materiales, Patentes/Orígenes frecuentes y Ficha Histórica de Entregas).
/// </summary>
public sealed class AnalisisProveedoresControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbTipo = new() { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbGrupo = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly TextBox _txtBuscar = new() { Width = 180 };
    private readonly Button _btnAnalizar;
    private readonly Button _btnExportar;

    private readonly Label _lblProvActivosVal;
    private readonly Label _lblProvActivosSub;
    private readonly Label _lblVolumenVal;
    private readonly Label _lblVolumenSub;
    private readonly Label _lblLiderVal;
    private readonly Label _lblLiderSub;
    private readonly Label _lblParetoVal;
    private readonly Label _lblParetoSub;

    private readonly DoubleBufferedDataGridView _dgvRanking = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DashboardChartControl _chartTop10 = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartAbc = new() { Dock = DockStyle.Fill };

    private readonly Label _lblProveedorSeleccionado = new();
    private readonly TabControl _detailTabs = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvMix = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DashboardChartControl _chartEvolucion = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartPatentes = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvOrigenes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvEntregas = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private AnalisisProveedoresResultDto? _lastResult;
    private ProveedorDetalleResultDto? _lastDetail;

    private sealed record PlantaOpt(Planta? Planta, string Label) { public override string ToString() => Label; }
    private sealed record TipoOpt(TipoPesaje? Tipo, string Label) { public override string ToString() => Label; }
    private sealed record GrupoOpt(long? NodeId, string Label) { public override string ToString() => Label; }

    public AnalisisProveedoresControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);

        Panel filterCard = UiHelpers.BuildFilterCard(90);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        _cmbPlanta.Font = AppTheme.FontBody;
        _cmbPlanta.Items.AddRange(
        [
            new PlantaOpt(null, "Todas las Plantas"),
            new PlantaOpt(Planta.Santiago, "Santiago (Placilla 2)"),
            new PlantaOpt(Planta.Placilla, "Placilla"),
            new PlantaOpt(Planta.Talcahuano, "Talcahuano")
        ]);
        _cmbPlanta.SelectedIndex = 0;

        _cmbTipo.Font = AppTheme.FontBody;
        _cmbTipo.Items.AddRange(
        [
            new TipoOpt(TipoPesaje.RecepcionCarga, "Solo Recepción / Compras"),
            new TipoOpt(null, "Todos los Movimientos"),
            new TipoOpt(TipoPesaje.DespachoCarga, "Despachos"),
            new TipoOpt(TipoPesaje.MovimientoInterno, "Movimientos Internos")
        ]);
        _cmbTipo.SelectedIndex = 0;

        _cmbGrupo.Font = AppTheme.FontBody;
        _cmbGrupo.Items.Add(new GrupoOpt(null, "Todos los Grupos"));
        _cmbGrupo.SelectedIndex = 0;

        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = DateTime.Today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        AppTheme.ApplyInputStyle(_txtBuscar);
        _txtBuscar.PlaceholderText = "Filtrar RUT, nombre o patente...";
        _txtBuscar.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadAnalisisAsync();
            }
        };

        _btnAnalizar = AppTheme.CreateButton("Analizar Proveedores", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await LoadAnalisisAsync(), "Ejecuta ranking Pareto ABC, variaciones y detalle de entregas", Tip, 175, 32);
        _btnExportar = AppTheme.CreateButton("Exportar Análisis", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Exporta Ranking ABC y Ficha del Proveedor seleccionado a Excel", Tip, 155, 32);

        TableLayoutPanel fRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        fRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 0) },
            _cmbPlanta,
            new Label { Text = "Mov:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 7, 4, 0) },
            _cmbTipo,
            new Label { Text = "Grupo:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 7, 4, 0) },
            _cmbGrupo,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 7, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(6, 7, 4, 0) },
            _dtpHasta
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r2.Controls.AddRange(
        [
            new Label { Text = "Buscar:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 0) },
            _txtBuscar,
            _btnAnalizar,
            _btnExportar
        ]);

        fRows.Controls.Add(r1, 0, 0);
        fRows.Controls.Add(r2, 0, 1);
        filterCard.Controls.Add(fRows);

        TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 94, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 4, 0, 6) };
        for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PROVEEDORES ACTIVOS", AppTheme.AccentBlue, out _lblProvActivosVal, out _lblProvActivosSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("VOLUMEN ANALIZADO", AppTheme.AccentCyan, out _lblVolumenVal, out _lblVolumenSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PROVEEDOR LÍDER (#1)", AppTheme.AccentGold, out _lblLiderVal, out _lblLiderSub), 2, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PARETO CLASE A (80%)", AppTheme.AccentEmerald, out _lblParetoVal, out _lblParetoSub), 3, 0);

        Size = new Size(1280, 760);

        SplitContainer mainSplit = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6
        };
        mainSplit.HandleCreated += (_, _) =>
        {
            if (mainSplit.Height > 460)
            {
                mainSplit.Panel1MinSize = 210;
                mainSplit.Panel2MinSize = 210;
                mainSplit.SplitterDistance = (int)(mainSplit.Height * 0.52);
            }
        };

        // Top Pane: Ranking Grid + Charts
        TableLayoutPanel topGridCharts = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        topGridCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        topGridCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

        Panel pnlRank = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), Margin = new Padding(0, 0, 4, 0), BorderStyle = BorderStyle.FixedSingle };
        Label lblRankT = new() { Text = "Ranking ABC / Pareto de Proveedores (clic para inspeccionar ficha)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 24 };

        _dgvRanking.Columns.AddRange(
        [
            UiHelpers.AddTextCol("RUT", nameof(ProveedorRankingRowDto.Rut), 75),
            UiHelpers.AddTextCol("Proveedor / Razón Social", nameof(ProveedorRankingRowDto.Nombre), 140),
            UiHelpers.AddTextCol("ABC", nameof(ProveedorRankingRowDto.ClaseAbc), 38),
            UiHelpers.AddNumberCol("Viajes", nameof(ProveedorRankingRowDto.Viajes), "N0", 48),
            UiHelpers.AddNumberCol("Kilos", nameof(ProveedorRankingRowDto.TotalKilos), "N0", 72),
            UiHelpers.AddNumberCol("% Part.", nameof(ProveedorRankingRowDto.PorcentajeParticipacion), "N1", 48),
            UiHelpers.AddNumberCol("% Acum.", nameof(ProveedorRankingRowDto.PorcentajeAcumulado), "N1", 52),
            UiHelpers.AddNumberCol("Kg/Viaje", nameof(ProveedorRankingRowDto.PromedioKgViaje), "N0", 56),
            UiHelpers.AddNumberCol("Var. %", nameof(ProveedorRankingRowDto.VariacionPorcentual), "N1", 48),
            UiHelpers.AddDateCol("Último", nameof(ProveedorRankingRowDto.UltimoEnvio), 66),
            UiHelpers.AddTextCol("Material Top", nameof(ProveedorRankingRowDto.ProductoPrincipal), 100)
        ]);
        _dgvRanking.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvRanking.Columns[e.ColumnIndex].DataPropertyName == nameof(ProveedorRankingRowDto.ClaseAbc))
            {
                string? cls = e.Value?.ToString();
                e.CellStyle!.Font = AppTheme.FontBodyBold;
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.CellStyle.ForeColor = cls switch
                {
                    "A" => AppTheme.AccentEmerald,
                    "B" => AppTheme.AccentBlue,
                    _ => AppTheme.TextSecondary
                };
            }
        };
        _dgvRanking.SelectionChanged += async (_, _) => await LoadSelectedSupplierDetailAsync();

        pnlRank.Controls.Add(_dgvRanking);
        pnlRank.Controls.Add(lblRankT);

        TableLayoutPanel rightCharts = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rightCharts.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        rightCharts.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        _chartTop10.Margin = new Padding(2);
        _chartAbc.Margin = new Padding(2);
        rightCharts.Controls.Add(_chartTop10, 0, 0);
        rightCharts.Controls.Add(_chartAbc, 0, 1);

        topGridCharts.Controls.Add(pnlRank, 0, 0);
        topGridCharts.Controls.Add(rightCharts, 1, 0);
        mainSplit.Panel1.Controls.Add(topGridCharts);

        // Bottom Pane: Ficha Robusta de Proveedor
        Panel pnlFicha = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _lblProveedorSeleccionado.Text = "Ficha Detallada: Seleccione un proveedor del ranking superior";
        _lblProveedorSeleccionado.Font = AppTheme.FontSection;
        _lblProveedorSeleccionado.ForeColor = AppTheme.AccentBlue;
        _lblProveedorSeleccionado.Dock = DockStyle.Top;
        _lblProveedorSeleccionado.Height = 26;

        _detailTabs.Font = AppTheme.FontBodyBold;

        // SubTab 1: Evolución y Mix de Materiales
        TabPage tpMix = new("Evolución Mensual y Mix de Materiales") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        TableLayoutPanel mixSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        mixSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        mixSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        _dgvMix.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Grupo de Material", nameof(GrupoKilosItem.Grupo), 140),
            UiHelpers.AddNumberCol("Viajes", nameof(GrupoKilosItem.Viajes), "N0", 50),
            UiHelpers.AddNumberCol("Kilos", nameof(GrupoKilosItem.Kilos), "N0", 80),
            UiHelpers.AddNumberCol("% Mix", nameof(GrupoKilosItem.Porcentaje), "N1", 50)
        ]);
        mixSplit.Controls.Add(_chartEvolucion, 0, 0);
        mixSplit.Controls.Add(_dgvMix, 1, 0);
        tpMix.Controls.Add(mixSplit);

        // SubTab 2: Patentes y Orígenes
        TabPage tpFlota = new("Patentes / Camiones y Orígenes") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        TableLayoutPanel flotaSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        flotaSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        flotaSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        _dgvOrigenes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Origen / Planta Procedencia", nameof(BarChartItemDto.Label), 140),
            UiHelpers.AddNumberCol("Kilos Acumulados", nameof(BarChartItemDto.Value), "N0", 80)
        ]);
        flotaSplit.Controls.Add(_chartPatentes, 0, 0);
        flotaSplit.Controls.Add(_dgvOrigenes, 1, 0);
        tpFlota.Controls.Add(flotaSplit);

        // SubTab 3: Entregas / Folios
        TabPage tpEntregas = new("Historial de Entregas (Folios)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        _dgvEntregas.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(Pesaje.Folio), "N0", 55),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 65),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 70),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 65),
            UiHelpers.AddTextCol("Cód", nameof(Pesaje.CodProducto), 65),
            UiHelpers.AddTextCol("Producto", nameof(Pesaje.NomProducto), 130),
            UiHelpers.AddTextCol("Origen", nameof(Pesaje.Origen), 80),
            UiHelpers.AddTextCol("Guía", nameof(Pesaje.GuiaDespacho), 60),
            UiHelpers.AddNumberCol("Bruto", nameof(Pesaje.PesoBruto), "N0", 60),
            UiHelpers.AddNumberCol("Tara", nameof(Pesaje.PesoTara), "N0", 60),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 65),
            UiHelpers.AddTextCol("Observación", nameof(Pesaje.Observacion), 95)
        ]);
        tpEntregas.Controls.Add(_dgvEntregas);

        _detailTabs.TabPages.AddRange([tpMix, tpFlota, tpEntregas]);
        pnlFicha.Controls.Add(_detailTabs);
        pnlFicha.Controls.Add(_lblProveedorSeleccionado);
        mainSplit.Panel2.Controls.Add(pnlFicha);

        Controls.Add(mainSplit);
        Controls.Add(kpis);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        _btnAnalizar.Enabled = _auth.HasPermission(PermisoSistema.VerAnalisisProveedores);
        _btnExportar.Enabled = _auth.HasPermission(PermisoSistema.ExportarExcel);
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await LoadGroupFilterAsync(ct);
        await LoadAnalisisAsync(ct);
    }

    private async Task LoadGroupFilterAsync(CancellationToken ct)
    {
        if (!_auth.HasPermission(PermisoSistema.VerGrupoMaterial))
        {
            return;
        }

        IReadOnlyList<MaterialGroupNode> nodes = await _app.GetMaterialGroupTreeAsync(ct);
        long? prevId = (_cmbGrupo.SelectedItem as GrupoOpt)?.NodeId;

        _cmbGrupo.Items.Clear();
        _cmbGrupo.Items.Add(new GrupoOpt(null, "Todos los Grupos"));
        foreach (MaterialGroupNode parent in nodes.Where(n => n.ParentId is null && n.TipoNodo == NodoGrupoTipo.CarpetaGrupo).OrderBy(n => n.Orden))
        {
            _cmbGrupo.Items.Add(new GrupoOpt(parent.Id, parent.Nombre));
        }

        int sel = 0;
        if (prevId.HasValue)
        {
            for (int i = 0; i < _cmbGrupo.Items.Count; i++)
            {
                if (_cmbGrupo.Items[i] is GrupoOpt go && go.NodeId == prevId.Value)
                {
                    sel = i;
                    break;
                }
            }
        }

        _cmbGrupo.SelectedIndex = sel;
    }

    private async Task LoadAnalisisAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta? planta = (_cmbPlanta.SelectedItem as PlantaOpt)?.Planta;
            TipoPesaje? tipo = (_cmbTipo.SelectedItem as TipoOpt)?.Tipo;
            long? groupNodeId = (_cmbGrupo.SelectedItem as GrupoOpt)?.NodeId;
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastResult = await _app.GetAnalisisProveedoresAsync(planta, desde, hasta, tipo, groupNodeId, _txtBuscar.Text, ct);

            _lblProvActivosVal.Text = _lastResult.TotalProveedoresActivos.ToString("N0", UiHelpers.EsCl);
            _lblProvActivosSub.Text = "Proveedores / Clientes con entregas";

            _lblVolumenVal.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.TotalKilos:N0} Kg");
            _lblVolumenSub.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.TotalViajes:N0} viajes · Prom. {_lastResult.PromedioGeneralKgViaje:N0} Kg/viaje");

            _lblLiderVal.Text = _lastResult.ProveedorLiderNombre.Length > 20
                ? _lastResult.ProveedorLiderNombre[..18] + "…"
                : _lastResult.ProveedorLiderNombre;
            _lblLiderSub.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.ProveedorLiderParticipacion:N1}% participación del volumen");

            _lblParetoVal.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.ProveedoresClaseACount} Clase A");
            _lblParetoSub.Text = string.Create(UiHelpers.EsCl, $"Concentran el {_lastResult.ConcentracionClaseAPorcentaje:N1}% del total");

            _chartTop10.SetBarData("Top 10 Proveedores (Kg)", _lastResult.TopProveedoresPareto);
            _chartAbc.SetDonutData("Segmentación Pareto ABC", _lastResult.DistribucionPorClaseAbc);

            _dgvRanking.DataSource = null;
            _dgvRanking.DataSource = _lastResult.Ranking;

            if (_lastResult.Ranking.Count > 0)
            {
                _dgvRanking.Rows[0].Selected = true;
                await LoadSelectedSupplierDetailAsync(ct);
            }
            else
            {
                ClearSupplierDetail();
            }

            NotifyStatus($"Análisis de Proveedores: {_lastResult.TotalProveedoresActivos} activos · {_lastResult.TotalKilos.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Calculando análisis Pareto de proveedores...");
    }

    private void ClearSupplierDetail()
    {
        _lastDetail = null;
        _lblProveedorSeleccionado.Text = "Ficha Detallada: Sin proveedores en el filtro actual";
        _dgvMix.DataSource = null;
        _dgvOrigenes.DataSource = null;
        _dgvEntregas.DataSource = null;
        _chartEvolucion.SetBarData("Evolución Mensual", []);
        _chartPatentes.SetBarData("Patentes Frecuentes", []);
    }

    private async Task LoadSelectedSupplierDetailAsync(CancellationToken ct = default)
    {
        if (_dgvRanking.CurrentRow?.DataBoundItem is not ProveedorRankingRowDto row)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta? planta = (_cmbPlanta.SelectedItem as PlantaOpt)?.Planta;
            TipoPesaje? tipo = (_cmbTipo.SelectedItem as TipoOpt)?.Tipo;
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastDetail = await _app.GetProveedorDetalleAsync(row, planta, desde, hasta, tipo, ct);

            _lblProveedorSeleccionado.Text = string.Create(
                UiHelpers.EsCl,
                $"Ficha: {row.Nombre} ({row.Rut}) · Clase {row.ClaseAbc} · {row.Ciudad} · Tel: {(string.IsNullOrWhiteSpace(row.Telefono) ? "-" : row.Telefono)} · Inactivo: {row.DiasInactivo} días");

            _dgvMix.DataSource = null;
            _dgvMix.DataSource = _lastDetail.MixMateriales;
            _chartEvolucion.SetBarData($"Evolución Mensual · {row.Nombre}", _lastDetail.EvolucionMensual);
            _chartPatentes.SetBarData("Camiones / Patentes Más Frecuentes", _lastDetail.PatentesFrecuentes);

            _dgvOrigenes.DataSource = null;
            _dgvOrigenes.DataSource = _lastDetail.OrigenesFrecuentes;

            _dgvEntregas.DataSource = null;
            _dgvEntregas.DataSource = _lastDetail.Entregas;
        }, NotifyStatus);
    }

    private void Exportar()
    {
        if (_lastResult is null || _lastResult.Ranking.Count == 0)
        {
            MessageBox.Show(this, "No hay proveedores en el análisis actual para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"AnalisisProveedores_ABC_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportAnalisisProveedores(
                path,
                _lastResult.PeriodoLabel,
                [.. _lastResult.Ranking.Select(r => (r.Rut, r.Nombre, r.Ciudad, r.Viajes, r.TotalKilos, r.PorcentajeParticipacion, r.PorcentajeAcumulado, r.ClaseAbc, r.PromedioKgViaje, r.ProductoPrincipal))],
                _lastDetail is not null ? [.. _lastDetail.MixMateriales.Select(m => (m.Grupo, m.Viajes, m.Kilos, m.Porcentaje))] : null,
                _lastDetail?.Entregas,
                _lastDetail?.Resumen.Nombre);

            NotifyStatus($"Análisis exportado en: {saved}");
            MessageBox.Show(this, $"Análisis Pareto de proveedores generado:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
