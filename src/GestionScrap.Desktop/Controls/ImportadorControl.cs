using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 4: Importador simplificado de 1 clic con flujo claro Origen -> Destino y control detallado
/// e historial persistente de cada sincronización (Firebird WMDBSPC.GDB / PWAREHOUSE.FDB y Access .mdb).
/// </summary>
public sealed class ImportadorControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly Func<Task>? _onDataChanged;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // SubTab 1: Importador Simple Firebird (Origen -> Destino)
    private readonly ComboBox _cmbPlanta = new() { Width = 140 };
    private readonly Label _lblFlowOrigin = new();
    private readonly Label _lblFlowDest = new();
    private readonly CheckBox _chkTodoHistorial = new();
    private readonly CheckBox _chkOmitirCeros = new();
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnTestTcp;
    private readonly Button _btnConfig;
    private readonly Button _btnVistaPrevia;
    private readonly Button _btnImportarDirecto;
    private readonly Button _btnExportarVista;
    private readonly Label _lblResumenExtract = new();
    private readonly DoubleBufferedDataGridView _dgv = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    // SubTab 2: Access MDB
    private readonly TextBox _txtMdbPath = new();
    private readonly Label _lblMdbDestino = new();
    private readonly Button _btnBrowseMdb;
    private readonly Button _btnMigrateMdb;
    private readonly TextBox _txtMdbLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    // SubTab 3: Control e Historial Detallado (Desde -> Hacia)
    private readonly Label _lblHistCountVal;
    private readonly Label _lblHistCountSub;
    private readonly Label _lblDbRowsVal;
    private readonly Label _lblDbRowsSub;
    private readonly Label _lblDbKilosVal;
    private readonly Label _lblDbKilosSub;
    private readonly Label _lblLastSyncVal;
    private readonly Label _lblLastSyncSub;
    private readonly TextBox _txtFiltroHist = new() { Width = 220 };
    private readonly Button _btnRefreshHist;
    private readonly Button _btnExportHist;
    private readonly DoubleBufferedDataGridView _dgvHistory = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    private FirebirdExtractPackage? _currentPackage;
    private IReadOnlyList<ImportacionHistorial> _allHistory = [];

    public ImportadorControl(
        ScrapApplicationService app,
        IAuthService auth,
        AppSettingsStore settingsStore,
        AppSettings settings,
        Func<Task>? onDataChanged = null)
    {
        _app = app;
        _auth = auth;
        _settingsStore = settingsStore;
        _settings = settings;
        _onDataChanged = onDataChanged;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);
        Size = new Size(1280, 760);
        _tabs.Font = AppTheme.FontBodyBold;

        // =========================================================================
        // 1. IMPORTACIÓN SIMPLE FIREBIRD (ORIGEN -> DESTINO)
        // =========================================================================
        TabPage tpFirebird = new("Importar Firebird (WMDBSPC.GDB)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        Panel flowCard = new()
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 6)
        };
        TableLayoutPanel flowGrid = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        flowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        flowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
        flowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));

        _lblFlowOrigin.Dock = DockStyle.Fill;
        _lblFlowOrigin.Font = AppTheme.FontMono;
        _lblFlowOrigin.ForeColor = AppTheme.AccentGold;
        _lblFlowOrigin.AutoEllipsis = true;
        _lblFlowOrigin.TextAlign = ContentAlignment.MiddleLeft;

        Label lblArrow = new()
        {
            Text = "➔",
            Font = AppTheme.FontLoginTitle,
            ForeColor = Color.FromArgb(103, 232, 249),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblFlowDest.Dock = DockStyle.Fill;
        _lblFlowDest.Font = AppTheme.FontMono;
        _lblFlowDest.ForeColor = Color.FromArgb(167, 243, 208);
        _lblFlowDest.AutoEllipsis = true;
        _lblFlowDest.TextAlign = ContentAlignment.MiddleLeft;

        flowGrid.Controls.Add(_lblFlowOrigin, 0, 0);
        flowGrid.Controls.Add(lblArrow, 1, 0);
        flowGrid.Controls.Add(_lblFlowDest, 2, 0);
        flowCard.Controls.Add(flowGrid);

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: true);
        UiHelpers.SelectPlanta(_cmbPlanta, Planta.Santiago);
        _cmbPlanta.Margin = new Padding(0, 3, 6, 3);
        _cmbPlanta.SelectedIndexChanged += (_, _) => UpdateEndpointBanner();

        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = DateTime.Today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);
        _dtpDesde.Margin = new Padding(0, 3, 6, 3);
        _dtpHasta.Margin = new Padding(0, 3, 6, 3);

        _chkTodoHistorial.Text = "Importar todo (sin rango)";
        _chkTodoHistorial.Checked = true;
        _chkTodoHistorial.Font = AppTheme.FontBodyBold;
        _chkTodoHistorial.AutoSize = true;
        _chkTodoHistorial.Margin = new Padding(8, 7, 8, 3);
        _chkTodoHistorial.CheckedChanged += (_, _) =>
        {
            _dtpDesde.Enabled = !_chkTodoHistorial.Checked;
            _dtpHasta.Enabled = !_chkTodoHistorial.Checked;
        };
        _dtpDesde.Enabled = false;
        _dtpHasta.Enabled = false;

        _chkOmitirCeros.Text = "Omitir 0 Kg";
        _chkOmitirCeros.Checked = false;
        _chkOmitirCeros.Font = AppTheme.FontBody;
        _chkOmitirCeros.AutoSize = true;
        _chkOmitirCeros.Margin = new Padding(6, 7, 6, 3);

        _btnTestTcp = AppTheme.CreateButton("Probar Conexión", IconKind.Check, ButtonVariant.Secondary,
            async (_, _) => await TestTcpAsync(), "Verifica conectividad TCP 3050, tablas y pesajes reales en origen", Tip, 0, 32);
        _btnConfig = AppTheme.CreateButton("Configurar Red", IconKind.Settings, ButtonVariant.Secondary,
            (_, _) => OpenConfigDialog(), "Configura servidores Firebird, rutas .GDB/.FDB y credenciales", Tip, 0, 32);
        _btnVistaPrevia = AppTheme.CreateButton("Vista Previa", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await BuscarRemotoAsync(), "Consulta previa opcional de los registros antes de guardar", Tip, 0, 32);
        _btnImportarDirecto = AppTheme.CreateButton("Importar Directo", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await ImportarDirectoAsync(), "Sincronización simple en 1 clic: extrae del origen e importa al SQLite destino", Tip, 0, 32);
        _btnExportarVista = AppTheme.CreateButton("Exportar Vista", IconKind.Excel, ButtonVariant.Gold,
            (_, _) => ExportarVistaPrevia(), "Exporta la vista previa o solo las filas seleccionadas a Excel", Tip, 0, 32);

        FlowLayoutPanel r1 = UiHelpers.CreateFilterRow(
            UiHelpers.CreateFilterLabel("Origen / Planta:", 0),
            _cmbPlanta,
            _chkTodoHistorial,
            UiHelpers.CreateFilterLabel("Desde:"),
            _dtpDesde,
            UiHelpers.CreateFilterLabel("Hasta:"),
            _dtpHasta,
            _chkOmitirCeros);

        FlowLayoutPanel r2 = UiHelpers.CreateFilterRow(
            _btnImportarDirecto, _btnVistaPrevia, _btnTestTcp, _btnConfig, _btnExportarVista);

        Panel topCard = UiHelpers.BuildAutoFilterCard(r1, r2);

        Panel gridCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _lblResumenExtract.Text = "Use 'Importar Directo' para sincronizar en 1 paso desde WMDBSPC.GDB hacia su SQLite local, o 'Vista Previa' para inspeccionar primero.";
        _lblResumenExtract.Font = AppTheme.FontSection;
        _lblResumenExtract.ForeColor = AppTheme.SidebarDark;
        _lblResumenExtract.AutoEllipsis = true;
        _lblResumenExtract.Dock = DockStyle.Top;
        _lblResumenExtract.Height = 30;

        _dgv.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(FirebirdRemotePesaje.Folio), "N0", 52),
            UiHelpers.AddTextCol("Tipo", nameof(FirebirdRemotePesaje.TipoPesajeRaw), 40),
            UiHelpers.AddTextCol("Patente", nameof(FirebirdRemotePesaje.Patente), 65),
            UiHelpers.AddNumberCol("Bruto", nameof(FirebirdRemotePesaje.PesoBruto), "N0", 56),
            UiHelpers.AddNumberCol("Tara", nameof(FirebirdRemotePesaje.PesoTara), "N0", 56),
            UiHelpers.AddNumberCol("Neto Kg", nameof(FirebirdRemotePesaje.PesoNeto), "N0", 60),
            UiHelpers.AddTextCol("Cód", nameof(FirebirdRemotePesaje.CodProducto), 60),
            UiHelpers.AddTextCol("Producto", nameof(FirebirdRemotePesaje.NomProducto), 120),
            UiHelpers.AddTextCol("RUT", nameof(FirebirdRemotePesaje.RutCliente), 72),
            UiHelpers.AddTextCol("Proveedor / Cliente", nameof(FirebirdRemotePesaje.NomCliente), 125),
            UiHelpers.AddTextCol("Origen", nameof(FirebirdRemotePesaje.Origen), 75),
            UiHelpers.AddTextCol("Destino", nameof(FirebirdRemotePesaje.Destino), 75),
            UiHelpers.AddTextCol("Chofer", nameof(FirebirdRemotePesaje.Chofer), 80),
            UiHelpers.AddDateCol("Ingreso", nameof(FirebirdRemotePesaje.FechaIngreso), 68)
        ]);

        gridCard.Controls.Add(_dgv);
        gridCard.Controls.Add(_lblResumenExtract);
        tpFirebird.Controls.Add(gridCard);
        tpFirebird.Controls.Add(topCard);
        tpFirebird.Controls.Add(flowCard);

        // =========================================================================
        // 2. ACCESS MDB
        // =========================================================================
        TabPage tpMdb = new("Importar Access (.mdb / .accdb)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };
        Panel cardMdb = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(16), BorderStyle = BorderStyle.FixedSingle };
        Label lblMdbTitle = new() { Text = "Migración Simple desde Microsoft Access (BD_SCRAP.mdb)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 28 };
        _lblMdbDestino.Text = $"Destino local SQLite: {settings.SqliteDatabasePath}";
        _lblMdbDestino.Font = AppTheme.FontSubtitle;
        _lblMdbDestino.ForeColor = AppTheme.AccentBlue;
        _lblMdbDestino.Dock = DockStyle.Top;
        _lblMdbDestino.Height = 24;

        TableLayoutPanel mdbRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 10)
        };
        mdbRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mdbRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mdbRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mdbRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _txtMdbPath.Dock = DockStyle.Fill;
        _txtMdbPath.Margin = new Padding(0, 5, 10, 0);
        _txtMdbPath.Text = settings.LastMdbPath;
        AppTheme.ApplyInputStyle(_txtMdbPath);

        _btnBrowseMdb = AppTheme.CreateButton("Examinar .MDB", IconKind.Document, ButtonVariant.Secondary,
            (_, _) => BrowseMdb(), "Seleccionar archivo BD_SCRAP.mdb o .accdb", Tip, 0, 34);
        _btnBrowseMdb.Margin = new Padding(0, 0, 8, 0);

        _btnMigrateMdb = AppTheme.CreateButton("Importar Access", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await RunMdbMigrationAsync(), "Ejecutar migración desde archivo Access y registrar en historial", Tip, 0, 34);
        _btnMigrateMdb.Margin = new Padding(0, 0, 0, 0);

        mdbRow.Controls.Add(_txtMdbPath, 0, 0);
        mdbRow.Controls.Add(_btnBrowseMdb, 1, 0);
        mdbRow.Controls.Add(_btnMigrateMdb, 2, 0);

        _txtMdbLog.Dock = DockStyle.Fill;
        _txtMdbLog.Font = AppTheme.FontMono;
        _txtMdbLog.BackColor = AppTheme.HeaderDark;
        _txtMdbLog.ForeColor = Color.FromArgb(226, 232, 240);

        cardMdb.Controls.Add(_txtMdbLog);
        cardMdb.Controls.Add(mdbRow);
        cardMdb.Controls.Add(_lblMdbDestino);
        cardMdb.Controls.Add(lblMdbTitle);
        tpMdb.Controls.Add(cardMdb);

        // =========================================================================
        // 3. CONTROL E HISTORIAL DE IMPORTACIONES (DESDE -> HACIA)
        // =========================================================================
        TabPage tpControl = new("Control e Historial (Desde -> Hacia)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        TableLayoutPanel histKpis = new() { Dock = DockStyle.Top, Height = 98, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 0, 0, 6) };
        for (int i = 0; i < 4; i++) histKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        histKpis.Controls.Add(UiHelpers.CreateKpiCard("IMPORTACIONES REGISTRADAS", AppTheme.AccentBlue, out _lblHistCountVal, out _lblHistCountSub), 0, 0);
        histKpis.Controls.Add(UiHelpers.CreateKpiCard("PESAJES EN BASE DESTINO", AppTheme.AccentCyan, out _lblDbRowsVal, out _lblDbRowsSub), 1, 0);
        histKpis.Controls.Add(UiHelpers.CreateKpiCard("VOLUMEN ALMACENADO", AppTheme.AccentEmerald, out _lblDbKilosVal, out _lblDbKilosSub), 2, 0);
        histKpis.Controls.Add(UiHelpers.CreateKpiCard("ÚLTIMA SINCRONIZACIÓN", AppTheme.AccentGold, out _lblLastSyncVal, out _lblLastSyncSub), 3, 0);

        AppTheme.ApplyInputStyle(_txtFiltroHist);
        _txtFiltroHist.Margin = new Padding(0, 4, 8, 3);
        _txtFiltroHist.PlaceholderText = "Filtrar origen, planta o usuario...";
        _txtFiltroHist.TextChanged += (_, _) => ApplyHistoryFilter();

        _btnRefreshHist = AppTheme.CreateButton("Actualizar Control", IconKind.Refresh, ButtonVariant.Primary,
            async (_, _) => await RefreshHistoryAndStatsAsync(), "Recargar historial Origen -> Destino y estadísticas SQLite", Tip, 0, 32);
        _btnExportHist = AppTheme.CreateButton("Exportar Control", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportarControlHistorial(), "Exporta historial completo o filas seleccionadas de importaciones", Tip, 0, 32);

        FlowLayoutPanel histFlow = UiHelpers.CreateFilterRow(
            UiHelpers.CreateFilterLabel("Buscar:", 0),
            _txtFiltroHist,
            _btnRefreshHist,
            _btnExportHist);
        Panel histToolbar = UiHelpers.BuildAutoFilterCard(histFlow);

        Panel histGridCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _dgvHistory.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Fecha / Hora", nameof(ImportacionHistorial.FechaHoraLocal), 95),
            UiHelpers.AddTextCol("Usuario", nameof(ImportacionHistorial.Usuario), 65),
            UiHelpers.AddTextCol("Tipo", nameof(ImportacionHistorial.TipoOrigen), 72),
            UiHelpers.AddTextCol("Desde (Origen Exacto)", nameof(ImportacionHistorial.OrigenDetalle), 175),
            UiHelpers.AddTextCol("Planta", nameof(ImportacionHistorial.PlantaDestino), 68),
            UiHelpers.AddTextCol("Hacia (Destino SQLite)", nameof(ImportacionHistorial.DestinoDetalle), 150),
            UiHelpers.AddTextCol("Rango", nameof(ImportacionHistorial.RangoFiltro), 88),
            UiHelpers.AddNumberCol("Leídos", nameof(ImportacionHistorial.LeidosOrigen), "N0", 52),
            UiHelpers.AddNumberCol("Nuevos", nameof(ImportacionHistorial.ImportadosNuevos), "N0", 52),
            UiHelpers.AddNumberCol("Dup.", nameof(ImportacionHistorial.DuplicadosOmitidos), "N0", 45),
            UiHelpers.AddNumberCol("Kilos Imp.", nameof(ImportacionHistorial.KilosImportados), "N0", 72),
            UiHelpers.AddTextCol("Estado", nameof(ImportacionHistorial.Estado), 72)
        ]);
        histGridCard.Controls.Add(_dgvHistory);

        tpControl.Controls.Add(histGridCard);
        tpControl.Controls.Add(histToolbar);
        tpControl.Controls.Add(histKpis);

        _tabs.TabPages.AddRange([tpFirebird, tpMdb, tpControl]);
        Controls.Add(_tabs);

        UpdateEndpointBanner();
        RefreshPermissions();
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        UpdateEndpointBanner();
        await RefreshHistoryAndStatsAsync(ct);
    }

    private FirebirdPlantaEndpoint GetCurrentEndpoint()
    {
        Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
        FirebirdPlantaEndpoint? ep = _settings.FirebirdEndpoints.FirstOrDefault(e => e.Planta == planta);
        if (ep is null)
        {
            ep = AppSettingsStore.CreateDefaultEndpoints().First(e => e.Planta == planta);
            _settings.FirebirdEndpoints.Add(ep);
        }

        return ep;
    }

    private void UpdateEndpointBanner()
    {
        FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
        _lblFlowOrigin.Text = $"DESDE: Firebird ({ep.ServidorIp}:{ep.Puerto}) · {ep.RutaArchivo}";
        string dest = string.IsNullOrWhiteSpace(_settings.SqliteDatabasePath) ? "Base Local SQLite (WAL)" : _settings.SqliteDatabasePath;
        _lblFlowDest.Text = $"HACIA: SQLite [{ep.Planta.ToShortTitle()}] · {dest}";
        _lblMdbDestino.Text = $"Destino local SQLite: {dest}";
    }

    public override void RefreshPermissions()
    {
        bool canImp = _auth.HasPermission(PermisoSistema.ImportarDatos);
        bool canExport = _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnTestTcp.Enabled = canImp;
        _btnVistaPrevia.Enabled = canImp;
        _btnImportarDirecto.Enabled = canImp;
        _btnBrowseMdb.Enabled = canImp;
        _btnMigrateMdb.Enabled = canImp;
        _btnRefreshHist.Enabled = canImp;
        _btnExportHist.Enabled = canImp && canExport;
        _btnExportarVista.Enabled = canExport;
        _btnConfig.Enabled = canImp || _auth.HasPermission(PermisoSistema.MantenimientoBaseDatos);
    }

    private async Task TestTcpAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            var res = await _app.TestFirebirdConnectionAsync(ep);
            MessageBox.Show(this,
                $"{res.Message}\nPing: {res.PingMs} ms\nTablas: {res.TablesCount}\nPesajes disponibles: {res.PesajesCount:N0} ({res.DateBounds})",
                $"Diagnóstico Firebird ({ep.Planta.ToShortTitle()})",
                MessageBoxButtons.OK,
                res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            NotifyStatus(res.Message);
        }, NotifyStatus, "Probando conexión con servidor Firebird...");
    }

    private void OpenConfigDialog()
    {
        using ConfiguracionDbDialog dlg = new(_settingsStore, _settings, _app);
        dlg.ShowDialog(this);
        UpdateEndpointBanner();
    }

    private async Task BuscarRemotoAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            DateOnly? desde = _chkTodoHistorial.Checked ? null : DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly? hasta = _chkTodoHistorial.Checked ? null : DateOnly.FromDateTime(_dtpHasta.Value);

            _currentPackage = await _app.QueryFirebirdPackageAsync(ep, desde, hasta, ep.UsarModoLocalSiFallaRed);
            IEnumerable<FirebirdRemotePesaje> preview = _chkOmitirCeros.Checked
                ? _currentPackage.Pesajes.Where(p => p.PesoNeto > 0 || p.PesoBruto > p.PesoTara)
                : _currentPackage.Pesajes;

            List<FirebirdRemotePesaje> previewList = [.. preview];
            _dgv.DataSource = null;
            _dgv.DataSource = previewList;

            long kilos = previewList.Sum(r => (long)r.PesoNeto);
            _lblResumenExtract.Text = string.Create(
                UiHelpers.EsCl,
                $"{ep.Planta.ToShortTitle()}: {previewList.Count:N0} pesajes en vista previa ({kilos:N0} Kg), {_currentPackage.Clientes.Count:N0} clientes y {_currentPackage.Productos.Count:N0} productos.");
            NotifyStatus(_currentPackage.StatusMessage);
        }, NotifyStatus, "Consultando Firebird WMDBSPC...");
    }

    private async Task ImportarDirectoAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            DateOnly? desde = _chkTodoHistorial.Checked ? null : DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly? hasta = _chkTodoHistorial.Checked ? null : DateOnly.FromDateTime(_dtpHasta.Value);

            FirebirdExtractPackage pkg;
            int impCount, dupCount, cliCount, prodCount;

            if (_currentPackage is not null && _currentPackage.Pesajes.Count > 0)
            {
                pkg = _currentPackage;
                string rango = desde.HasValue && hasta.HasValue ? $"{desde.Value:dd/MM/yyyy} - {hasta.Value:dd/MM/yyyy}" : "Todo el historial";
                var res = await _app.ImportFirebirdPackageAsync(ep.Planta, pkg, ep, rango, _chkOmitirCeros.Checked);
                impCount = res.ImportedPesajes;
                dupCount = res.Duplicates;
                cliCount = res.ClientesUpserted;
                prodCount = res.ProductosUpserted;
            }
            else
            {
                var res = await _app.ImportDirectFromFirebirdAsync(ep, desde, hasta, ep.UsarModoLocalSiFallaRed, _chkOmitirCeros.Checked);
                pkg = res.Package;
                _currentPackage = pkg;
                impCount = res.ImportedPesajes;
                dupCount = res.Duplicates;
                cliCount = res.ClientesUpserted;
                prodCount = res.ProductosUpserted;

                _dgv.DataSource = null;
                _dgv.DataSource = pkg.Pesajes;
            }

            string summary = $"Sincronización completada:\n\n" +
                             $"• Origen: {ep.ServidorIp}:{ep.Puerto} · {ep.RutaArchivo}\n" +
                             $"• Destino: SQLite [{ep.Planta.ToShortTitle()}]\n" +
                             $"• Pesajes nuevos importados: {impCount.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Duplicados omitidos: {dupCount.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Clientes sincronizados: {cliCount.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Productos sincronizados: {prodCount.ToString("N0", UiHelpers.EsCl)}";

            await RefreshHistoryAndStatsCoreAsync(CancellationToken.None);
            NotifyStatus($"Importados {impCount:N0} pesajes nuevos desde {ep.Planta.ToShortTitle()}.");
            MessageBox.Show(this, summary, "Importación Completada", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if ((impCount > 0 || cliCount > 0) && _onDataChanged is not null)
            {
                await _onDataChanged();
            }

            _currentPackage = null;
        }, NotifyStatus, "Sincronizando origen Firebird hacia SQLite...");
    }

    private void ExportarVistaPrevia()
    {
        if (_dgv.DataSource is not IEnumerable<FirebirdRemotePesaje> bound)
        {
            MessageBox.Show(this, "Consulte una vista previa antes de exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<FirebirdRemotePesaje> selected = [.. _dgv.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as FirebirdRemotePesaje).OfType<FirebirdRemotePesaje>()];
        List<FirebirdRemotePesaje> all = [.. bound];
        if (all.Count == 0)
        {
            MessageBox.Show(this, "No hay registros en vista previa para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<FirebirdRemotePesaje> target = selected.Count > 1 ? selected : all;
        string? path = UiHelpers.PromptSaveExcelPath(this, $"VistaPrevia_Firebird_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            string saved = _app.ExcelExporter.ExportReportePersonalizado(
                path,
                new ReportePersonalizadoPayload(
                    $"Vista Previa Firebird · {ep.Planta.ToShortTitle()}",
                    $"Origen: {ep.ServidorIp}:{ep.Puerto} · {ep.RutaArchivo}",
                    [
                        ("Origen", $"{ep.ServidorIp}:{ep.Puerto}", ep.RutaArchivo),
                        ("Registros Exportados", $"{target.Count:N0}", selected.Count > 1 ? "Selección de filas" : "Vista completa"),
                        ("Kilos Netos", $"{target.Sum(x => (long)x.PesoNeto):N0} Kg", string.Empty)
                    ],
                    [
                        new ExportTableSection(
                            "Vista Previa",
                            "Pesajes Leídos desde Firebird",
                            ["Folio", "Tipo", "Patente", "Bruto", "Tara", "Neto Kg", "Código", "Producto", "RUT", "Cliente", "Origen", "Destino", "Chofer", "Ingreso"],
                            [.. target.Select(r => new object?[] { r.Folio, r.TipoPesajeRaw, r.Patente, r.PesoBruto, r.PesoTara, r.PesoNeto, r.CodProducto, r.NomProducto, r.RutCliente, r.NomCliente, r.Origen, r.Destino, r.Chofer, r.FechaIngreso })])
                    ]));

            NotifyStatus($"Vista previa exportada: {saved}");
            MessageBox.Show(this, $"Excel generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseMdb()
    {
        using OpenFileDialog ofd = new()
        {
            Title = "Seleccionar archivo Microsoft Access",
            Filter = "Microsoft Access (*.mdb;*.accdb)|*.mdb;*.accdb|Todos (*.*)|*.*"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _txtMdbPath.Text = ofd.FileName;
        }
    }

    private async Task RunMdbMigrationAsync()
    {
        string path = _txtMdbPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Seleccione un archivo .mdb / .accdb existente.", "Archivo Inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            _settings.LastMdbPath = path;
            _settingsStore.Save(_settings);

            _txtMdbLog.Text = $"[{DateTime.Now:HH:mm:ss}] Origen Access: {path}{Environment.NewLine}" +
                              $"[{DateTime.Now:HH:mm:ss}] Destino SQLite: {_settings.SqliteDatabasePath}{Environment.NewLine}";
            var res = await _app.ImportFromAccessMdbAsync(path);

            _txtMdbLog.AppendText(string.Create(
                CultureInfo.CurrentCulture,
                $"[{DateTime.Now:HH:mm:ss}] Migración Access completada:{Environment.NewLine}" +
                $"  • Pesajes importados : {res.PesajesImported:N0}{Environment.NewLine}" +
                $"  • Clientes           : {res.ClientesImported:N0}{Environment.NewLine}" +
                $"  • Productos          : {res.ProductosImported:N0}{Environment.NewLine}"));

            foreach (string w in res.Warnings)
            {
                _txtMdbLog.AppendText($"  [AVISO] {w}{Environment.NewLine}");
            }

            await RefreshHistoryAndStatsCoreAsync(CancellationToken.None);

            if (_onDataChanged is not null)
            {
                await _onDataChanged();
            }

            NotifyStatus($"Access MDB migrado: {res.PesajesImported} pesajes, {res.ClientesImported} clientes.");
        }, NotifyStatus, "Ejecutando migración Access MDB...");
    }

    private async Task RefreshHistoryAndStatsAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () => await RefreshHistoryAndStatsCoreAsync(ct), NotifyStatus);
    }

    private async Task RefreshHistoryAndStatsCoreAsync(CancellationToken ct)
    {
        _allHistory = await _app.GetImportHistoryAsync(250, ct);
        DatabaseResumenStatsDto stats = await _app.GetDatabaseResumenStatsAsync(ct);

        _lblHistCountVal.Text = stats.TotalImportaciones.ToString("N0", UiHelpers.EsCl);
        _lblHistCountSub.Text = "Ejecuciones auditadas Origen -> Destino";

        _lblDbRowsVal.Text = string.Create(UiHelpers.EsCl, $"{stats.TotalPesajes:N0} pesajes");
        _lblDbRowsSub.Text = stats.FechaMinima.HasValue && stats.FechaMaxima.HasValue
            ? $"{stats.FechaMinima:dd/MM/yyyy} al {stats.FechaMaxima:dd/MM/yyyy}"
            : "Base sin pesajes aún";

        _lblDbKilosVal.Text = string.Create(UiHelpers.EsCl, $"{stats.TotalKilos:N0} Kg");
        _lblDbKilosSub.Text = string.Create(UiHelpers.EsCl, $"{stats.TotalClientes} clientes · {stats.TotalProductos} productos");

        _lblLastSyncVal.Text = stats.UltimaImportacionUtc.HasValue
            ? stats.UltimaImportacionUtc.Value.ToLocalTime().ToString("dd/MM HH:mm", UiHelpers.EsCl)
            : "Sin registros";
        _lblLastSyncSub.Text = stats.UltimoOrigen.Length > 34 ? stats.UltimoOrigen[..32] + "…" : stats.UltimoOrigen;

        ApplyHistoryFilter();
    }

    private void ApplyHistoryFilter()
    {
        string term = _txtFiltroHist.Text.Trim();
        List<ImportacionHistorial> filtered = string.IsNullOrEmpty(term)
            ? [.. _allHistory]
            : [.. _allHistory.Where(h =>
                h.OrigenDetalle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                h.TipoOrigen.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                h.PlantaDestino.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                h.Usuario.Contains(term, StringComparison.OrdinalIgnoreCase))];

        _dgvHistory.DataSource = null;
        _dgvHistory.DataSource = filtered;
    }

    private void ExportarControlHistorial()
    {
        List<ImportacionHistorial> selected = [.. _dgvHistory.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as ImportacionHistorial).OfType<ImportacionHistorial>()];
        List<ImportacionHistorial> visible = [.. (_dgvHistory.DataSource as IEnumerable<ImportacionHistorial> ?? _allHistory)];
        List<ImportacionHistorial> toExport = selected.Count > 1 ? selected : visible;

        if (toExport.Count == 0)
        {
            MessageBox.Show(this, "No hay registros de importación para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"Control_Importaciones_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportImportHistory(path, toExport);
            NotifyStatus($"Control de importaciones exportado: {saved}");
            MessageBox.Show(this, $"Control de importaciones exportado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
