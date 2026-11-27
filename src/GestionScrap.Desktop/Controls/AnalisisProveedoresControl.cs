using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 3: Análisis Completo y Robusto de Proveedores (Ranking Pareto ABC, Tendencia vs período anterior,
/// exclusión de totales en cero, selección múltiple y exportación personalizada).
/// </summary>
public sealed class AnalisisProveedoresControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 140 };
    private readonly ComboBox _cmbTipo = new() { Width = 155 };
    private readonly ComboBox _cmbGrupo = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly TextBox _txtBuscar = new() { Width = 180 };
    private readonly CheckBox _chkExcluirCeros = new() { Text = "Excluir totales en 0", AutoSize = true };
    private readonly Button _btnAnalizar;
    private readonly Button _btnExportar;
    private readonly Button _btnExportarSeleccion;

    private readonly Label _lblProvActivosVal;
    private readonly Label _lblProvActivosSub;
    private readonly Label _lblVolumenVal;
    private readonly Label _lblVolumenSub;
    private readonly Label _lblLiderVal;
    private readonly Label _lblLiderSub;
    private readonly Label _lblParetoVal;
    private readonly Label _lblParetoSub;

    private readonly DoubleBufferedDataGridView _dgvRanking = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DashboardChartControl _chartTop10 = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartAbc = new() { Dock = DockStyle.Fill };

    private readonly Label _lblProveedorSeleccionado = new();
    private readonly TabControl _detailTabs = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvMix = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DashboardChartControl _chartEvolucion = new() { Dock = DockStyle.Fill };
    private readonly DashboardChartControl _chartPatentes = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvOrigenes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvEntregas = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    private readonly List<ContextMenuStrip> _menus = [];
    private AnalisisProveedoresResultDto? _lastResult;
    private ProveedorDetalleResultDto? _lastDetail;

    private sealed record GrupoOpt(long? NodeId, string Label) { public override string ToString() => Label; }

    public AnalisisProveedoresControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);

        UiHelpers.PopulatePlantaFilterCombo(_cmbPlanta);
        UiHelpers.PopulateTipoFilterCombo(_cmbTipo, defaultRecepcion: true);
        _cmbPlanta.Margin = new Padding(0, 3, 6, 3);
        _cmbTipo.Margin = new Padding(0, 3, 6, 3);

        _cmbGrupo.Font = AppTheme.FontBody;
        _cmbGrupo.Margin = new Padding(0, 3, 6, 3);
        _cmbGrupo.Items.Add(new GrupoOpt(null, "Todos los Grupos"));
        _cmbGrupo.SelectedIndex = 0;

        _dtpDesde.Value = UiHelpers.CurrentMonthStartLocal;
        _dtpHasta.Value = UiHelpers.CurrentDateLocal;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);
        _dtpDesde.Margin = new Padding(0, 3, 6, 3);
        _dtpHasta.Margin = new Padding(0, 3, 6, 3);

        AppTheme.ApplyInputStyle(_txtBuscar);
        _txtBuscar.Margin = new Padding(0, 4, 8, 3);
        _txtBuscar.PlaceholderText = "Filtrar RUT, nombre o patente...";
        _txtBuscar.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadAnalisisAsync();
            }
        };

        _chkExcluirCeros.Font = AppTheme.FontBodyBold;
        _chkExcluirCeros.Margin = new Padding(6, 7, 10, 3);
        _chkExcluirCeros.CheckedChanged += async (_, _) => await LoadAnalisisAsync();

        _btnAnalizar = AppTheme.CreateButton("Analizar Proveedores", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await LoadAnalisisAsync(), "Ejecuta ranking Pareto ABC, variaciones y detalle de entregas", Tip, 0, 32);
        _btnExportar = AppTheme.CreateButton("Exportar Todo", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportarDirecto(), "Exporta Ranking ABC y Ficha del Proveedor seleccionado a Excel", Tip, 0, 32);
        _btnExportarSeleccion = AppTheme.CreateButton("Exportar Selección...", IconKind.Save, ButtonVariant.Gold,
            (_, _) => ExportarPersonalizado(), "Exportar solo proveedores o entregas seleccionadas / secciones elegidas", Tip, 0, 32);

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
            UiHelpers.CreateFilterLabel("Buscar:", 0),
            _txtBuscar,
            _chkExcluirCeros,
            _btnAnalizar,
            _btnExportar,
            _btnExportarSeleccion);

        Panel filterCard = UiHelpers.BuildAutoFilterCard(r1, r2);

        TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 98, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 4, 0, 6) };
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

        TableLayoutPanel topGridCharts = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        topGridCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        topGridCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

        Panel pnlRank = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), Margin = new Padding(0, 0, 4, 0), BorderStyle = BorderStyle.FixedSingle };
        Label lblRankT = new() { Text = "Ranking ABC / Pareto de Proveedores (Ctrl/Shift + clic para seleccionar múltiples)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, AutoEllipsis = true, Dock = DockStyle.Top, Height = 28 };

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
            if (e.RowIndex < 0)
            {
                return;
            }

            string prop = _dgvRanking.Columns[e.ColumnIndex].DataPropertyName;
            if (prop == nameof(ProveedorRankingRowDto.ClaseAbc) && e.CellStyle is not null)
            {
                string? cls = e.Value?.ToString();
                e.CellStyle.Font = AppTheme.FontBodyBold;
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.CellStyle.ForeColor = cls switch
                {
                    "A" => AppTheme.AccentEmerald,
                    "B" => AppTheme.AccentBlue,
                    _ => AppTheme.TextSecondary
                };
            }
            else if (prop == nameof(ProveedorRankingRowDto.VariacionPorcentual))
            {
                UiHelpers.FormatVarianceCell(e);
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

        Panel pnlFicha = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _lblProveedorSeleccionado.Text = "Ficha Detallada: Seleccione un proveedor del ranking superior";
        _lblProveedorSeleccionado.Font = AppTheme.FontSection;
        _lblProveedorSeleccionado.ForeColor = AppTheme.AccentBlue;
        _lblProveedorSeleccionado.AutoEllipsis = true;
        _lblProveedorSeleccionado.Dock = DockStyle.Top;
        _lblProveedorSeleccionado.Height = 28;

        _detailTabs.Font = AppTheme.FontBodyBold;

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

        AttachGridMenu(_dgvRanking, "Ranking Pareto Proveedores", "RankingProveedores");
        AttachGridMenu(_dgvEntregas, "Historial de Entregas Proveedor", "EntregasProveedor");

        Controls.Add(mainSplit);
        Controls.Add(kpis);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    private void AttachGridMenu(DataGridView dgv, string title, string sheet)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Exportar selección a Excel (.xlsx)", null, (_, _) =>
        {
            if (_auth.HasPermission(PermisoSistema.ExportarExcel))
            {
                UiHelpers.ExportDataGridViewToExcel(this, dgv, _app.ExcelExporter, title, sheet, onlySelected: true, NotifyStatus);
            }
        });
        menu.Items.Add("Exportación Personalizada...", null, (_, _) => ExportarPersonalizado());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copiar valor de celda", null, (_, _) =>
        {
            string? val = dgv.CurrentCell?.Value?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                Clipboard.SetText(val);
            }
        });
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(dgv, menu);
    }

    public override void RefreshPermissions()
    {
        bool canExport = _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnAnalizar.Enabled = _auth.HasPermission(PermisoSistema.VerAnalisisProveedores);
        _btnExportar.Enabled = canExport;
        _btnExportarSeleccion.Enabled = canExport;
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
            Planta? planta = UiHelpers.GetSelectedNullablePlanta(_cmbPlanta);
            TipoPesaje? tipo = UiHelpers.GetSelectedNullableTipo(_cmbTipo);
            long? groupNodeId = (_cmbGrupo.SelectedItem as GrupoOpt)?.NodeId;
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastResult = await _app.GetAnalisisProveedoresAsync(
                planta, desde, hasta, tipo, groupNodeId, _txtBuscar.Text, _chkExcluirCeros.Checked, ct);

            _lblProvActivosVal.Text = _lastResult.TotalProveedoresActivos.ToString("N0", UiHelpers.EsCl);
            _lblProvActivosSub.Text = _chkExcluirCeros.Checked ? "Proveedores con > 0 Kg" : "Proveedores / Clientes activos";

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
            Planta? planta = UiHelpers.GetSelectedNullablePlanta(_cmbPlanta);
            TipoPesaje? tipo = UiHelpers.GetSelectedNullableTipo(_cmbTipo);
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastDetail = await _app.GetProveedorDetalleAsync(row, planta, desde, hasta, tipo, _chkExcluirCeros.Checked, ct);

            _lblProveedorSeleccionado.Text = string.Create(
                UiHelpers.EsCl,
                $"Ficha: {row.Nombre} ({row.Rut}) · Clase {row.ClaseAbc} · {row.Ciudad} · Var: {row.VariacionPorcentual:N1}% · Inactivo: {row.DiasInactivo} días");

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

    private void ExportarDirecto()
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

    private void ExportarPersonalizado()
    {
        if (_lastResult is null || _lastResult.Ranking.Count == 0)
        {
            MessageBox.Show(this, "No hay proveedores en el análisis actual para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<ProveedorRankingRowDto> selRanking = [.. _dgvRanking.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ProveedorRankingRowDto).OfType<ProveedorRankingRowDto>()];
        List<Pesaje> selEntregas = [.. _dgvEntregas.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as Pesaje).OfType<Pesaje>()];
        int selCount = selRanking.Count + selEntregas.Count;

        string[] sections =
        [
            "KPIs Pareto de Proveedores",
            "Ranking Pareto ABC",
            "Mix Materiales del Proveedor",
            "Evolución Mensual del Proveedor",
            "Orígenes Frecuentes",
            "Entregas / Folios del Proveedor"
        ];

        using ExportacionPersonalizadaDialog dlg = new(
            "Cambiaso · Análisis Personalizado de Proveedores",
            selCount,
            sections,
            _chkExcluirCeros.Checked,
            selCount > 0 ? $"{selRanking.Count} proveedor(es), {selEntregas.Count} entrega(s)" : null);

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"Proveedores_Personalizado_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            bool onlySel = dlg.SoloSeleccionActual;
            bool exZero = dlg.ExcluirCeros;
            IReadOnlyList<string> inc = dlg.SeccionesSeleccionadas;

            IEnumerable<ProveedorRankingRowDto> rankingRows = onlySel && selRanking.Count > 0 ? selRanking : _lastResult.Ranking;
            if (exZero) rankingRows = rankingRows.Where(r => r.TotalKilos > 0);
            List<ProveedorRankingRowDto> finalRank = [.. rankingRows];

            List<(string Indicador, string Valor, string Detalle)> kpis = [];
            if (inc.Contains("KPIs Pareto de Proveedores"))
            {
                kpis.Add(("Proveedores Analizados", finalRank.Count.ToString("N0", UiHelpers.EsCl), _lastResult.PeriodoLabel));
                kpis.Add(("Volumen Total", $"{finalRank.Sum(r => r.TotalKilos):N0} Kg", $"{finalRank.Sum(r => r.Viajes):N0} viajes"));
                kpis.Add(("Proveedor Líder", _lastResult.ProveedorLiderNombre, $"{_lastResult.ProveedorLiderParticipacion:N1}% del total"));
                kpis.Add(("Concentración Clase A", $"{_lastResult.ProveedoresClaseACount} proveedores", $"{_lastResult.ConcentracionClaseAPorcentaje:N1}% del volumen"));
            }

            List<ExportTableSection> exportSections = [];
            if (inc.Contains("Ranking Pareto ABC"))
            {
                exportSections.Add(new ExportTableSection(
                    "Ranking Pareto",
                    onlySel ? "Proveedores Seleccionados" : "Ranking Completo Pareto ABC",
                    ["RUT", "Proveedor", "Ciudad", "ABC", "Viajes", "Kilos Netos", "% Part.", "% Acum.", "Kg/Viaje", "Var % Periodo", "Último Envío", "Días Inactivo", "Material Top"],
                    [.. finalRank.Select(r => new object?[]
                    {
                        r.Rut, r.Nombre, r.Ciudad, r.ClaseAbc, r.Viajes, r.TotalKilos,
                        r.PorcentajeParticipacion, r.PorcentajeAcumulado, r.PromedioKgViaje,
                        r.VariacionPorcentual, r.UltimoEnvio, r.DiasInactivo, r.ProductoPrincipal
                    })]));
            }

            if (_lastDetail is not null)
            {
                if (inc.Contains("Mix Materiales del Proveedor"))
                {
                    IEnumerable<GrupoKilosItem> mix = exZero ? _lastDetail.MixMateriales.Where(m => m.Kilos > 0) : _lastDetail.MixMateriales;
                    exportSections.Add(new ExportTableSection(
                        "Mix Materiales",
                        $"Mix de {_lastDetail.Resumen.Nombre}",
                        ["Grupo Material", "Viajes", "Kilos Netos", "% Mix"],
                        [.. mix.Select(m => new object?[] { m.Grupo, m.Viajes, m.Kilos, m.Porcentaje })]));
                }

                if (inc.Contains("Evolución Mensual del Proveedor"))
                {
                    IEnumerable<BarChartItemDto> ev = exZero ? _lastDetail.EvolucionMensual.Where(e => e.Value > 0) : _lastDetail.EvolucionMensual;
                    exportSections.Add(new ExportTableSection(
                        "Evolución Mensual",
                        $"Evolución {_lastDetail.Resumen.Nombre}",
                        ["Mes", "Kilos Netos"],
                        [.. ev.Select(e => new object?[] { e.Label, e.Value })]));
                }

                if (inc.Contains("Orígenes Frecuentes"))
                {
                    IEnumerable<BarChartItemDto> origs = exZero ? _lastDetail.OrigenesFrecuentes.Where(o => o.Value > 0) : _lastDetail.OrigenesFrecuentes;
                    exportSections.Add(new ExportTableSection(
                        "Orígenes",
                        $"Orígenes de {_lastDetail.Resumen.Nombre}",
                        ["Origen", "Kilos Acumulados"],
                        [.. origs.Select(o => new object?[] { o.Label, o.Value })]));
                }

                if (inc.Contains("Entregas / Folios del Proveedor"))
                {
                    IEnumerable<Pesaje> ent = onlySel && selEntregas.Count > 0 ? selEntregas : _lastDetail.Entregas;
                    if (exZero) ent = ent.Where(p => p.PesoNeto > 0);
                    exportSections.Add(new ExportTableSection(
                        "Entregas Proveedor",
                        $"Folios de {_lastDetail.Resumen.Nombre}",
                        ["Folio", "Planta", "Fecha", "Patente", "Código", "Producto", "Origen", "Bruto", "Tara", "Neto Kg", "Observación"],
                        [.. ent.Select(p => new object?[] { p.Folio, p.Planta.ToShortTitle(), p.FechaIngreso, p.Patente, p.CodProducto, p.NomProducto, p.Origen, p.PesoBruto, p.PesoTara, p.PesoNeto, p.Observacion })]));
                }
            }

            string saved = _app.ExcelExporter.ExportReportePersonalizado(
                path,
                new ReportePersonalizadoPayload(dlg.TituloPersonalizado, _lastResult.PeriodoLabel, kpis, exportSections));

            NotifyStatus($"Exportación personalizada generada: {saved}");
            MessageBox.Show(this, $"Excel de proveedores generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (ContextMenuStrip m in _menus)
            {
                m.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
