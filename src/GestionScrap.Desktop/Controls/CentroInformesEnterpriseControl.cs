using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Centro de Informes Empresariales con 9 segmentadores multi-selección, filtros combinados avanzados,
/// período mensual local por defecto (día 1 del mes -> hoy) y 6 reportes 100 % consistentes + detalle y menú contextual.
/// </summary>
public sealed class CentroInformesEnterpriseControl : ScrapTabControlBase
{
    private readonly EnterpriseApplicationService _enterprise;
    private readonly ScrapApplicationService _scrap;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 132 };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly TextBox _txtFolio = new() { Width = 86 };
    private readonly TextBox _txtPatente = new() { Width = 84 };
    private readonly TextBox _txtRut = new() { Width = 96 };
    private readonly TextBox _txtOperador = new() { Width = 92 };
    private readonly CheckBox _chkExacto = new() { Text = "Búsqueda exacta", AutoSize = true };
    private readonly CheckBox _chkSinAnulados = new() { Text = "Ocultar anulados", AutoSize = true, Checked = true };
    private readonly CheckBox _chkSinCeros = new() { Text = "Excluir 0 Kg", AutoSize = true };

    private readonly Button _btnConsultar;
    private readonly Button _btnMesActual;
    private readonly Button _btnHistorico;
    private readonly Button _btnLimpiar;
    private readonly Button _btnExportarTodo;

    private readonly MultiSelectSlicerButton _slProveedores;
    private readonly MultiSelectSlicerButton _slClientes;
    private readonly MultiSelectSlicerButton _slMateriales;
    private readonly MultiSelectSlicerButton _slGrupos;
    private readonly MultiSelectSlicerButton _slPatentes;
    private readonly MultiSelectSlicerButton _slTipos;
    private readonly MultiSelectSlicerButton _slAnios;
    private readonly MultiSelectSlicerButton _slMeses;
    private readonly MultiSelectSlicerButton _slEstados;

    private readonly Label _lblFiltrosActivos = new();
    private readonly Label _lblKpiCantVal;
    private readonly Label _lblKpiCantSub;
    private readonly Label _lblKpiBrutoVal;
    private readonly Label _lblKpiBrutoSub;
    private readonly Label _lblKpiTaraVal;
    private readonly Label _lblKpiTaraSub;
    private readonly Label _lblKpiNetoVal;
    private readonly Label _lblKpiNetoSub;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvPatente = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvMaterial = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DashboardChartControl _chartMateriales = new() { Dock = DockStyle.Fill };
    private readonly DoubleBufferedDataGridView _dgvProveedor = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvCliente = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvRut = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvTipo = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvDetalle = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    private readonly List<ContextMenuStrip> _menus = [];
    private EnterpriseReportResultDto? _lastReport;

    public CentroInformesEnterpriseControl(
        EnterpriseApplicationService enterprise,
        ScrapApplicationService scrap,
        IAuthService auth)
    {
        _enterprise = enterprise;
        _scrap = scrap;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);
        Size = new Size(1280, 760);

        UiHelpers.PopulatePlantaFilterCombo(_cmbPlanta);
        _cmbPlanta.Margin = new Padding(0, 3, 6, 3);

        // Rango por defecto obligatorio: desde día 1 del mes actual local hasta fecha actual local
        _dtpDesde.Value = UiHelpers.CurrentMonthStartLocal;
        _dtpHasta.Value = UiHelpers.CurrentDateLocal;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);
        _dtpDesde.Margin = new Padding(0, 3, 6, 3);
        _dtpHasta.Margin = new Padding(0, 3, 6, 3);

        foreach (TextBox tb in new[] { _txtFolio, _txtPatente, _txtRut, _txtOperador })
        {
            AppTheme.ApplyInputStyle(tb);
            tb.Margin = new Padding(0, 3, 6, 3);
            tb.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LoadReportAsync();
                }
            };
        }

        _txtFolio.PlaceholderText = "Folio exacto/parc.";
        _txtPatente.PlaceholderText = "Patente";
        _txtRut.PlaceholderText = "RUT";
        _txtOperador.PlaceholderText = "Operador/Básc.";

        _chkExacto.Font = AppTheme.FontSmallBold;
        _chkExacto.Margin = new Padding(4, 7, 8, 3);
        _chkSinAnulados.Font = AppTheme.FontSmallBold;
        _chkSinAnulados.Margin = new Padding(4, 7, 8, 3);
        _chkSinCeros.Font = AppTheme.FontSmallBold;
        _chkSinCeros.Margin = new Padding(4, 7, 8, 3);

        _btnConsultar = AppTheme.CreateButton("Consultar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await LoadReportAsync(), "Ejecutar consulta combinada en los 6 informes y detalle", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMesActual = AppTheme.CreateButton("Mes Actual", IconKind.Refresh, ButtonVariant.Secondary,
            async (_, _) =>
            {
                _dtpDesde.Value = UiHelpers.CurrentMonthStartLocal;
                _dtpHasta.Value = UiHelpers.CurrentDateLocal;
                await LoadReportAsync();
            }, "Restablece el rango al mes actual (día 1 hasta hoy)", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnHistorico = AppTheme.CreateButton("Histórico", IconKind.Database, ButtonVariant.Secondary,
            async (_, _) =>
            {
                _dtpDesde.Value = new DateTime(2011, 1, 1);
                _dtpHasta.Value = UiHelpers.CurrentDateLocal;
                await LoadReportAsync();
            }, "Consultar historial completo desde 2011 hasta hoy", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnLimpiar = AppTheme.CreateButton("Limpiar Filtros", IconKind.Close, ButtonVariant.Warning,
            async (_, _) => await ClearAllFiltersAsync(), "Limpia todos los filtros y segmentadores multi-selección", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnExportarTodo = AppTheme.CreateButton("Exportar Informes", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportarInformesCompletos(), "Exportar todos los informes consolidados + detalle filtrado a Excel", Tip, 0, 30, AppTheme.FontButtonCompact);

        // 9 Segmentadores Multi-Selección
        _slProveedores = new MultiSelectSlicerButton("Proveedores", Tip);
        _slClientes = new MultiSelectSlicerButton("Clientes", Tip);
        _slMateriales = new MultiSelectSlicerButton("Materiales", Tip);
        _slGrupos = new MultiSelectSlicerButton("Grupos", Tip);
        _slPatentes = new MultiSelectSlicerButton("Patentes", Tip);
        _slTipos = new MultiSelectSlicerButton("Tipos Pesaje", Tip);
        _slAnios = new MultiSelectSlicerButton("Años", Tip);
        _slMeses = new MultiSelectSlicerButton("Meses", Tip);
        _slEstados = new MultiSelectSlicerButton("Estados", Tip);

        foreach (MultiSelectSlicerButton sl in AllSlicers())
        {
            sl.SelectionChanged += async () => await LoadReportAsync();
        }

        FlowLayoutPanel r1 = UiHelpers.CreateFilterRow(
            UiHelpers.CreateFilterLabel("Planta:", 0), _cmbPlanta,
            UiHelpers.CreateFilterLabel("Desde:"), _dtpDesde,
            UiHelpers.CreateFilterLabel("Hasta:"), _dtpHasta,
            UiHelpers.CreateFilterLabel("Folio:"), _txtFolio,
            UiHelpers.CreateFilterLabel("Patente:"), _txtPatente,
            UiHelpers.CreateFilterLabel("RUT:"), _txtRut,
            UiHelpers.CreateFilterLabel("Oper.:"), _txtOperador);

        FlowLayoutPanel r2 = UiHelpers.CreateFilterRow(
            _slProveedores.Button, _slClientes.Button, _slMateriales.Button, _slGrupos.Button,
            _slPatentes.Button, _slTipos.Button, _slAnios.Button, _slMeses.Button, _slEstados.Button);

        FlowLayoutPanel r3 = UiHelpers.CreateFilterRow(
            _chkExacto, _chkSinAnulados, _chkSinCeros,
            _btnConsultar, _btnMesActual, _btnHistorico, _btnLimpiar, _btnExportarTodo);

        Panel filterCard = UiHelpers.BuildAutoFilterCard(r1, r2, r3);

        Panel activeBar = new()
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 0, 6)
        };
        _lblFiltrosActivos.Dock = DockStyle.Fill;
        _lblFiltrosActivos.Font = AppTheme.FontSmallBold;
        _lblFiltrosActivos.ForeColor = AppTheme.AccentGold;
        _lblFiltrosActivos.AutoEllipsis = true;
        _lblFiltrosActivos.UseMnemonic = false;
        _lblFiltrosActivos.TextAlign = ContentAlignment.MiddleLeft;
        _lblFiltrosActivos.Text = "Filtros activos: cargando período actual...";
        activeBar.Controls.Add(_lblFiltrosActivos);

        TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 94, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 2, 0, 4) };
        for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PESAJES EN FILTRO", AppTheme.AccentBlue, out _lblKpiCantVal, out _lblKpiCantSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("TOTAL BRUTO (KG)", AppTheme.AccentGold, out _lblKpiBrutoVal, out _lblKpiBrutoSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("TOTAL TARA (KG)", AppTheme.TextSecondary, out _lblKpiTaraVal, out _lblKpiTaraSub), 2, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("TOTAL NETO / TONELADAS", AppTheme.AccentEmerald, out _lblKpiNetoVal, out _lblKpiNetoSub), 3, 0);

        ConfigureGrids();

        _tabs.Font = AppTheme.FontBodyBold;
        _tabs.TabPages.Add(BuildGridPage("Por Patente", _dgvPatente));
        _tabs.TabPages.Add(BuildMaterialPage());
        _tabs.TabPages.Add(BuildGridPage("Por Proveedor", _dgvProveedor));
        _tabs.TabPages.Add(BuildGridPage("Por Cliente", _dgvCliente));
        _tabs.TabPages.Add(BuildGridPage("Por RUT (CRM)", _dgvRut));
        _tabs.TabPages.Add(BuildGridPage("Por Tipo de Pesaje", _dgvTipo));
        _tabs.TabPages.Add(BuildGridPage("Detalle de Pesajes", _dgvDetalle));

        Controls.Add(_tabs);
        Controls.Add(kpis);
        Controls.Add(activeBar);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    private MultiSelectSlicerButton[] AllSlicers() =>
        [_slProveedores, _slClientes, _slMateriales, _slGrupos, _slPatentes, _slTipos, _slAnios, _slMeses, _slEstados];

    private void ConfigureGrids()
    {
        foreach (DoubleBufferedDataGridView dgv in new[] { _dgvPatente, _dgvMaterial, _dgvProveedor, _dgvCliente, _dgvRut, _dgvTipo, _dgvDetalle })
        {
            AppTheme.StyleDataGridView(dgv);
            dgv.MultiSelect = true;
        }

        _dgvPatente.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Patente", nameof(InformePatenteRowDto.Patente), 78),
            UiHelpers.AddTextCol("Tipo Vehículo", nameof(InformePatenteRowDto.TipoVehiculo), 85),
            UiHelpers.AddTextCol("Entidad Asociada", nameof(InformePatenteRowDto.EntidadAsociada), 130),
            UiHelpers.AddNumberCol("Pesajes", nameof(InformePatenteRowDto.CantidadPesajes), "N0", 56),
            UiHelpers.AddNumberCol("Bruto Kg", nameof(InformePatenteRowDto.TotalBrutoKg), "N0", 78),
            UiHelpers.AddNumberCol("Tara Kg", nameof(InformePatenteRowDto.TotalTaraKg), "N0", 74),
            UiHelpers.AddNumberCol("Neto Kg", nameof(InformePatenteRowDto.TotalNetoKg), "N0", 80),
            UiHelpers.AddNumberCol("% Part.", nameof(InformePatenteRowDto.ParticipacionPct), "N2", 55),
            UiHelpers.AddDateCol("1er Pesaje", nameof(InformePatenteRowDto.PrimerPesaje), 72),
            UiHelpers.AddDateCol("Último", nameof(InformePatenteRowDto.UltimoPesaje), 72),
            UiHelpers.AddTextCol("Material Top", nameof(InformePatenteRowDto.MaterialPrincipal), 115)
        ]);
        AttachStandardExportMenu(_dgvPatente, "Informe por Patente", "PorPatente");

        _dgvMaterial.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(InformeMaterialRowDto.CodProducto), 75),
            UiHelpers.AddTextCol("Material", nameof(InformeMaterialRowDto.NombreMaterial), 145),
            UiHelpers.AddTextCol("Grupo Material", nameof(InformeMaterialRowDto.GrupoMaterial), 115),
            UiHelpers.AddTextCol("Unid.", nameof(InformeMaterialRowDto.Unidad), 45),
            UiHelpers.AddNumberCol("Pesajes", nameof(InformeMaterialRowDto.CantidadPesajes), "N0", 54),
            UiHelpers.AddNumberCol("Bruto Kg", nameof(InformeMaterialRowDto.TotalBrutoKg), "N0", 76),
            UiHelpers.AddNumberCol("Tara Kg", nameof(InformeMaterialRowDto.TotalTaraKg), "N0", 72),
            UiHelpers.AddNumberCol("Neto Kg", nameof(InformeMaterialRowDto.TotalNetoKg), "N0", 80),
            UiHelpers.AddNumberCol("% Part.", nameof(InformeMaterialRowDto.ParticipacionPct), "N2", 55),
            UiHelpers.AddNumberCol("Stock Actual", nameof(InformeMaterialRowDto.StockActualKg), "N0", 76)
        ]);
        AttachStandardExportMenu(_dgvMaterial, "Informe por Material", "PorMaterial");

        ConfigureEntidadGrid(_dgvProveedor, "Informe por Proveedor", "PorProveedor");
        ConfigureEntidadGrid(_dgvCliente, "Informe por Cliente", "PorCliente");
        ConfigureEntidadGrid(_dgvRut, "Informe por RUT (Rol CRM)", "PorRUT");

        _dgvTipo.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Tipo de Pesaje", nameof(InformeTipoPesajeRowDto.NombreTipo), 150),
            UiHelpers.AddNumberCol("Pesajes", nameof(InformeTipoPesajeRowDto.CantidadPesajes), "N0", 65),
            UiHelpers.AddNumberCol("Bruto Kg", nameof(InformeTipoPesajeRowDto.TotalBrutoKg), "N0", 85),
            UiHelpers.AddNumberCol("Tara Kg", nameof(InformeTipoPesajeRowDto.TotalTaraKg), "N0", 85),
            UiHelpers.AddNumberCol("Neto Kg", nameof(InformeTipoPesajeRowDto.TotalNetoKg), "N0", 90),
            UiHelpers.AddNumberCol("% Part.", nameof(InformeTipoPesajeRowDto.ParticipacionPct), "N2", 62),
            UiHelpers.AddNumberCol("Prom. Neto Kg", nameof(InformeTipoPesajeRowDto.PromedioNetoKg), "N1", 80)
        ]);
        AttachStandardExportMenu(_dgvTipo, "Informe por Tipo de Pesaje", "PorTipoPesaje");

        _dgvDetalle.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 68),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Tipo Pesaje", nameof(Pesaje.TipoPesajeDisplay), 105),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 86),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 68),
            UiHelpers.AddTextCol("RUT", nameof(Pesaje.RutCliente), 78),
            UiHelpers.AddTextCol("Entidad", nameof(Pesaje.NomCliente), 130),
            UiHelpers.AddTextCol("Material", nameof(Pesaje.NomProducto), 125),
            UiHelpers.AddNumberCol("Bruto", nameof(Pesaje.PesoBruto), "N0", 62),
            UiHelpers.AddNumberCol("Tara", nameof(Pesaje.PesoTara), "N0", 60),
            UiHelpers.AddNumberCol("Neto", nameof(Pesaje.PesoNeto), "N0", 64),
            UiHelpers.AddTextCol("Un.", nameof(Pesaje.UnidadMedida), 42),
            UiHelpers.AddTextCol("Operador", nameof(Pesaje.Operador), 76)
        ]);
        AttachDetailContextMenu();
    }

    private void ConfigureEntidadGrid(DataGridView dgv, string title, string sheet)
    {
        dgv.Columns.AddRange(
        [
            UiHelpers.AddTextCol("RUT", nameof(InformeEntidadRowDto.Rut), 82),
            UiHelpers.AddTextCol("Razón Social / Entidad", nameof(InformeEntidadRowDto.Nombre), 155),
            UiHelpers.AddTextCol("Rol CRM", nameof(InformeEntidadRowDto.RolDescripcion), 95),
            UiHelpers.AddTextCol("Ciudad", nameof(InformeEntidadRowDto.Ciudad), 85),
            UiHelpers.AddNumberCol("Pesajes", nameof(InformeEntidadRowDto.CantidadPesajes), "N0", 54),
            UiHelpers.AddNumberCol("Bruto Kg", nameof(InformeEntidadRowDto.TotalBrutoKg), "N0", 76),
            UiHelpers.AddNumberCol("Tara Kg", nameof(InformeEntidadRowDto.TotalTaraKg), "N0", 72),
            UiHelpers.AddNumberCol("Neto Kg", nameof(InformeEntidadRowDto.TotalNetoKg), "N0", 80),
            UiHelpers.AddNumberCol("% Part.", nameof(InformeEntidadRowDto.ParticipacionPct), "N2", 54),
            UiHelpers.AddDateCol("Último", nameof(InformeEntidadRowDto.UltimoPesaje), 72),
            UiHelpers.AddTextCol("Material Top", nameof(InformeEntidadRowDto.MaterialPrincipal), 110)
        ]);
        AttachStandardExportMenu(dgv, title, sheet);
    }

    private void AttachStandardExportMenu(DataGridView dgv, string title, string sheet)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Exportar selección a Excel (.xlsx)", null, (_, _) =>
        {
            if (_auth.HasPermission(PermisoSistema.ExportarExcel))
            {
                UiHelpers.ExportDataGridViewToExcel(this, dgv, _scrap.ExcelExporter, title, sheet, onlySelected: true, NotifyStatus);
            }
        });
        menu.Items.Add("Exportar tabla completa a Excel (.xlsx)", null, (_, _) =>
        {
            if (_auth.HasPermission(PermisoSistema.ExportarExcel))
            {
                UiHelpers.ExportDataGridViewToExcel(this, dgv, _scrap.ExcelExporter, title, sheet, onlySelected: false, NotifyStatus);
            }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copiar valor de celda", null, (_, _) =>
        {
            string? val = dgv.CurrentCell?.Value?.ToString();
            if (!string.IsNullOrEmpty(val))
            {
                Clipboard.SetText(val);
            }
        });
        menu.Items.Add("Actualizar consulta", null, async (_, _) => await LoadReportAsync());
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(dgv, menu);
    }

    private void AttachDetailContextMenu()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Ver historial de cambios del pesaje", null, async (_, _) => await ShowSelectedPesajeHistoryAsync());
        menu.Items.Add("Anular pesaje seleccionado (con reverso inventario)", null, async (_, _) => await AnnulSelectedPesajeAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar filas seleccionadas a Excel (.xlsx)", null, (_, _) => ExportSelectedDetailRows());
        menu.Items.Add("Copiar Folio", null, (_, _) =>
        {
            if (_dgvDetalle.CurrentRow?.DataBoundItem is Pesaje p)
            {
                Clipboard.SetText(p.FolioDisplay);
            }
        });
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvDetalle, menu);
    }

    private static TabPage BuildGridPage(string title, DataGridView dgv)
    {
        TabPage tp = new(title) { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        Panel card = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        card.Controls.Add(dgv);
        tp.Controls.Add(card);
        return tp;
    }

    private TabPage BuildMaterialPage()
    {
        TabPage tp = new("Por Material") { BackColor = AppTheme.CanvasLight, Padding = new Padding(6) };
        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 150;
                split.Panel2MinSize = 140;
                split.SplitterDistance = (int)(split.Height * 0.62);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvMaterial);
        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(_chartMateriales);
        tp.Controls.Add(split);
        return tp;
    }

    public override void RefreshPermissions()
    {
        bool canRead = _auth.HasPermission(PermisoSistema.VerInformeResumen);
        bool canExport = _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnConsultar.Enabled = canRead;
        _btnMesActual.Enabled = canRead;
        _btnHistorico.Enabled = canRead;
        _btnLimpiar.Enabled = canRead;
        _btnExportarTodo.Enabled = canExport;
    }

    public override Task ActivateAsync(CancellationToken ct = default) => LoadReportAsync(ct);

    private async Task ClearAllFiltersAsync()
    {
        _cmbPlanta.SelectedIndex = 0;
        _dtpDesde.Value = UiHelpers.CurrentMonthStartLocal;
        _dtpHasta.Value = UiHelpers.CurrentDateLocal;
        _txtFolio.Clear();
        _txtPatente.Clear();
        _txtRut.Clear();
        _txtOperador.Clear();
        _chkExacto.Checked = false;
        _chkSinAnulados.Checked = true;
        _chkSinCeros.Checked = false;

        foreach (MultiSelectSlicerButton sl in AllSlicers())
        {
            sl.ClearSelection(raiseEvent: false);
        }

        await LoadReportAsync();
    }

    private EnterpriseReportFilterDto BuildFilter()
    {
        HashSet<int>? tipoInts = _slTipos.GetSelectedIntsOrNull();
        HashSet<TipoPesaje>? tipos = tipoInts is null ? null : [.. tipoInts.Select(i => (TipoPesaje)i)];

        HashSet<int>? estInts = _slEstados.GetSelectedIntsOrNull();
        HashSet<EstadoPesaje>? estados = estInts is null ? null : [.. estInts.Select(i => (EstadoPesaje)i)];

        return new EnterpriseReportFilterDto
        {
            Desde = DateOnly.FromDateTime(_dtpDesde.Value),
            Hasta = DateOnly.FromDateTime(_dtpHasta.Value),
            Planta = UiHelpers.GetSelectedNullablePlanta(_cmbPlanta),
            FolioSearch = _txtFolio.Text,
            FolioExacto = _chkExacto.Checked,
            PatenteSearch = _txtPatente.Text,
            PatenteExacta = _chkExacto.Checked,
            RutSearch = _txtRut.Text,
            RutExacto = _chkExacto.Checked,
            OperadorSearch = _txtOperador.Text,
            ExcluirAnulados = _chkSinAnulados.Checked,
            ExcluirTotalesCero = _chkSinCeros.Checked,
            ProveedoresRuts = _slProveedores.GetSelectedKeysOrNull(),
            ClientesRuts = _slClientes.GetSelectedKeysOrNull(),
            MaterialesCodigos = _slMateriales.GetSelectedKeysOrNull(),
            GruposMaterial = _slGrupos.GetSelectedKeysOrNull(),
            PatentesNorm = _slPatentes.GetSelectedKeysOrNull(),
            TiposPesaje = tipos,
            Anios = _slAnios.GetSelectedIntsOrNull(),
            Meses = _slMeses.GetSelectedIntsOrNull(),
            Estados = estados
        };
    }

    private async Task LoadReportAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            _lastReport = await _enterprise.GetEnterpriseReportAsync(BuildFilter(), ct);

            _slProveedores.SetOptions(_lastReport.Facets.Proveedores);
            _slClientes.SetOptions(_lastReport.Facets.Clientes);
            _slMateriales.SetOptions(_lastReport.Facets.Materiales);
            _slGrupos.SetOptions(_lastReport.Facets.GruposMaterial);
            _slPatentes.SetOptions(_lastReport.Facets.Patentes);
            _slTipos.SetOptions(_lastReport.Facets.TiposPesaje);
            _slAnios.SetOptions(_lastReport.Facets.Anios);
            _slMeses.SetOptions(_lastReport.Facets.Meses);
            _slEstados.SetOptions(_lastReport.Facets.Estados);

            _lblFiltrosActivos.Text = $"Período: {_lastReport.PeriodoLabel}  |  Filtros activos: {_lastReport.FiltrosActivosResumen}";

            _lblKpiCantVal.Text = _lastReport.CantidadPesajes.ToString("N0", UiHelpers.EsCl);
            _lblKpiCantSub.Text = $"{_lastReport.PatentesUnicas:N0} patentes · {_lastReport.MaterialesUnicos:N0} materiales · {_lastReport.EntidadesUnicas:N0} RUTs";

            _lblKpiBrutoVal.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalBrutoKg:N0} Kg");
            _lblKpiBrutoSub.Text = "Suma consolidada consistente";

            _lblKpiTaraVal.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalTaraKg:N0} Kg");
            _lblKpiTaraSub.Text = "Suma de taras registradas en báscula";

            _lblKpiNetoVal.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalNetoKg:N0} Kg");
            _lblKpiNetoSub.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalToneladas:N2} Ton · Prom. {_lastReport.PromedioNetoKg:N0} Kg/pesaje");

            _dgvPatente.DataSource = null;
            _dgvPatente.DataSource = _lastReport.PorPatente;

            _dgvMaterial.DataSource = null;
            _dgvMaterial.DataSource = _lastReport.PorMaterial;
            _chartMateriales.SetBarData("Top Materiales Filtrados (Kg Netos)", _lastReport.TopChartItems);

            _dgvProveedor.DataSource = null;
            _dgvProveedor.DataSource = _lastReport.PorProveedor;

            _dgvCliente.DataSource = null;
            _dgvCliente.DataSource = _lastReport.PorCliente;

            _dgvRut.DataSource = null;
            _dgvRut.DataSource = _lastReport.PorRut;

            _dgvTipo.DataSource = null;
            _dgvTipo.DataSource = _lastReport.PorTipoPesaje;

            _dgvDetalle.DataSource = null;
            _dgvDetalle.DataSource = _lastReport.DetallePesajes;

            NotifyStatus($"Centro de Informes: {_lastReport.CantidadPesajes:N0} pesajes · {_lastReport.TotalNetoKg.ToString("N0", UiHelpers.EsCl)} Kg netos.");
        }, NotifyStatus, "Calculando informes empresariales...");
    }

    private void ExportarInformesCompletos()
    {
        if (_lastReport is null)
        {
            MessageBox.Show(this, "Ejecute primero una consulta antes de exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"InformesPesajes_Cambiaso_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _enterprise.ExportEnterpriseReportExcel(path, _lastReport);
            NotifyStatus($"Informes exportados: {saved}");
            MessageBox.Show(this, $"Libro Excel multi-hoja generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSelectedDetailRows()
    {
        if (_lastReport is null || !_auth.HasPermission(PermisoSistema.ExportarExcel))
        {
            return;
        }

        List<Pesaje> sel = [.. _dgvDetalle.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as Pesaje).OfType<Pesaje>()];
        string? path = UiHelpers.PromptSaveExcelPath(this, $"PesajesSeleccion_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string saved = _enterprise.ExportEnterpriseReportExcel(path, _lastReport, sel);
        NotifyStatus($"Selección exportada ({sel.Count} filas): {saved}");
        MessageBox.Show(this, $"Exportación completada:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ShowSelectedPesajeHistoryAsync()
    {
        if (_dgvDetalle.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            IReadOnlyList<PesajeHistorialCambio> hist = await _enterprise.GetPesajeHistorialAsync(p.Id);
            string summary = hist.Count == 0
                ? $"El pesaje Folio {p.FolioDisplay} no registra modificaciones posteriores."
                : string.Join(Environment.NewLine, hist.Select(h => $"[{h.FechaHoraLocal}] {h.Usuario} · {h.Accion}: Neto {h.PesoNetoAnterior:N0} -> {h.PesoNetoNuevo:N0} ({h.EstadoAnterior} -> {h.EstadoNuevo}). {h.Motivo}"));
            MessageBox.Show(this, summary, $"Historial Auditado · Folio {p.FolioDisplay}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, NotifyStatus);
    }

    private async Task AnnulSelectedPesajeAsync()
    {
        if (_dgvDetalle.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            return;
        }

        string? motivo = UiHelpers.PromptAuditReason(this, $"Anular Pesaje Folio {p.FolioDisplay}", "Ingrese el motivo obligatorio para anular el pesaje (revertirá automáticamente el inventario sin borrar el historial):");
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.AnularPesajeAsync(p.Id, motivo);
            await LoadReportAsync();
        }, NotifyStatus, "Anulando pesaje...");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (MultiSelectSlicerButton sl in AllSlicers())
            {
                sl.Dispose();
            }

            foreach (ContextMenuStrip m in _menus)
            {
                m.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
