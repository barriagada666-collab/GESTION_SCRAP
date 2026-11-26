using System.Globalization;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 4: Importador multi-origen desde servidores Firebird (WMDBSPC.GDB / PWAREHOUSE.FDB)
/// y migración histórica desde Microsoft Access (.mdb/.accdb).
/// </summary>
public sealed class ImportadorControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly Func<Task>? _onDataChanged;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Firebird Tab
    private readonly ComboBox _cmbPlanta = new() { Width = 140 };
    private readonly Label _lblEndpoint = new();
    private readonly CheckBox _chkTodoHistorial = new();
    private readonly CheckBox _chkFallbackDemo = new();
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnTestTcp;
    private readonly Button _btnConfig;
    private readonly Button _btnBuscar;
    private readonly Button _btnImportar;
    private readonly Label _lblResumenExtract = new();
    private readonly DoubleBufferedDataGridView _dgv = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    // Access MDB Tab
    private readonly TextBox _txtMdbPath = new();
    private readonly Button _btnBrowseMdb;
    private readonly Button _btnMigrateMdb;
    private readonly TextBox _txtMdbLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    private FirebirdExtractPackage? _currentPackage;

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
        _tabs.Font = AppTheme.FontBodyBold;

        // 1. SubTab Firebird (WMDBSPC.GDB)
        TabPage tpFirebird = new("Importar desde Firebird (WMDBSPC.GDB / PWAREHOUSE.FDB)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        Panel topCard = UiHelpers.BuildFilterCard(104);
        topCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: true);
        UiHelpers.SelectPlanta(_cmbPlanta, Planta.Santiago);
        _cmbPlanta.SelectedIndexChanged += (_, _) => UpdateEndpointBanner();

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _chkTodoHistorial.Text = "Importar todo (sin rango)";
        _chkTodoHistorial.Checked = true;
        _chkTodoHistorial.Font = AppTheme.FontBodyBold;
        _chkTodoHistorial.AutoSize = true;
        _chkTodoHistorial.Margin = new Padding(8, 8, 6, 0);
        _chkTodoHistorial.CheckedChanged += (_, _) =>
        {
            _dtpDesde.Enabled = !_chkTodoHistorial.Checked;
            _dtpHasta.Enabled = !_chkTodoHistorial.Checked;
        };
        _dtpDesde.Enabled = false;
        _dtpHasta.Enabled = false;

        _chkFallbackDemo.Text = "Respaldo demo si falla red";
        _chkFallbackDemo.Checked = false;
        _chkFallbackDemo.Font = AppTheme.FontBody;
        _chkFallbackDemo.AutoSize = true;
        _chkFallbackDemo.Margin = new Padding(8, 8, 6, 0);

        _lblEndpoint.Font = AppTheme.FontMono;
        _lblEndpoint.ForeColor = AppTheme.AccentBlue;
        _lblEndpoint.AutoSize = true;
        _lblEndpoint.Margin = new Padding(10, 9, 0, 0);

        _btnTestTcp = AppTheme.CreateButton("Probar Conexión", IconKind.Check, ButtonVariant.Secondary,
            async (_, _) => await TestTcpAsync(), "Verifica conectividad TCP 3050, tablas y cantidad real de pesajes en WMDBSPC.GDB", Tip, 150, 32);
        _btnConfig = AppTheme.CreateButton("Configurar Red", IconKind.Settings, ButtonVariant.Secondary,
            (_, _) => OpenConfigDialog(), "Configura IPs, rutas .GDB/.FDB y credenciales Firebird", Tip, 142, 32);
        _btnBuscar = AppTheme.CreateButton("Cargar desde Firebird", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await BuscarRemotoAsync(), "Extrae vista previa de DBPESAJES, DBCLIENTES y DBPRODUCTOS", Tip, 175, 32);
        _btnImportar = AppTheme.CreateButton("Importar a Base Local", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await ImportarAsync(), "Importa clientes, productos y pesajes evitando duplicados en SQLite", Tip, 185, 32);

        TableLayoutPanel rows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 4, 0) },
            _cmbPlanta,
            _chkTodoHistorial,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(6, 8, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(6, 8, 4, 0) },
            _dtpHasta,
            _chkFallbackDemo
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r2.Controls.AddRange([_btnTestTcp, _btnConfig, _btnBuscar, _btnImportar, _lblEndpoint]);

        rows.Controls.Add(r1, 0, 0);
        rows.Controls.Add(r2, 0, 1);
        topCard.Controls.Add(rows);

        Panel gridCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _lblResumenExtract.Text = "Presione 'Cargar desde Firebird' e 'Importar a Base Local' para sincronizar WMDBSPC.GDB hacia su SQLite local.";
        _lblResumenExtract.Font = AppTheme.FontSection;
        _lblResumenExtract.ForeColor = AppTheme.SidebarDark;
        _lblResumenExtract.Dock = DockStyle.Top;
        _lblResumenExtract.Height = 28;

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

        // 2. SubTab Access MDB
        TabPage tpMdb = new("Importar desde Microsoft Access (.mdb / .accdb)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };
        Panel cardMdb = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(16), BorderStyle = BorderStyle.FixedSingle };
        Label lblMdbTitle = new() { Text = "Migrador de Base Histórica Access (BD_SCRAP.mdb)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 28 };
        Label lblMdbSub = new() { Text = "Lee automáticamente tablas históricas con soporte integrado 32/64 bits (SysWOW64) e importa pesajes, clientes y productos.", Font = AppTheme.FontSubtitle, ForeColor = AppTheme.TextSecondary, Dock = DockStyle.Top, Height = 26 };

        Panel mdbRow = new() { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 6, 0, 6) };
        _txtMdbPath.SetBounds(0, 6, 520, 28);
        _txtMdbPath.Text = settings.LastMdbPath;
        AppTheme.ApplyInputStyle(_txtMdbPath);

        _btnBrowseMdb = AppTheme.CreateButton("Examinar .MDB", IconKind.Document, ButtonVariant.Secondary,
            (_, _) => BrowseMdb(), "Seleccionar archivo BD_SCRAP.mdb o .accdb", Tip, 148, 32);
        _btnBrowseMdb.Location = new Point(530, 4);

        _btnMigrateMdb = AppTheme.CreateButton("Importar Access", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await RunMdbMigrationAsync(), "Ejecutar migración desde el archivo Access seleccionado", Tip, 165, 32);
        _btnMigrateMdb.Location = new Point(686, 4);
        mdbRow.Controls.AddRange([_txtMdbPath, _btnBrowseMdb, _btnMigrateMdb]);

        _txtMdbLog.Dock = DockStyle.Fill;
        _txtMdbLog.Font = AppTheme.FontMono;
        _txtMdbLog.BackColor = AppTheme.HeaderDark;
        _txtMdbLog.ForeColor = Color.FromArgb(226, 232, 240);

        cardMdb.Controls.Add(_txtMdbLog);
        cardMdb.Controls.Add(mdbRow);
        cardMdb.Controls.Add(lblMdbSub);
        cardMdb.Controls.Add(lblMdbTitle);
        tpMdb.Controls.Add(cardMdb);

        _tabs.TabPages.AddRange([tpFirebird, tpMdb]);
        Controls.Add(_tabs);

        UpdateEndpointBanner();
        RefreshPermissions();
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
        _lblEndpoint.Text = $"{ep.ServidorIp}:{ep.Puerto} -> {ep.RutaArchivo}";
        _chkFallbackDemo.Checked = ep.UsarModoLocalSiFallaRed;
    }

    public override void RefreshPermissions()
    {
        bool canImp = _auth.HasPermission(PermisoSistema.ImportarDatos);
        _btnTestTcp.Enabled = canImp;
        _btnBuscar.Enabled = canImp;
        _btnImportar.Enabled = canImp;
        _btnBrowseMdb.Enabled = canImp;
        _btnMigrateMdb.Enabled = canImp;
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

            _currentPackage = await _app.QueryFirebirdPackageAsync(ep, desde, hasta, _chkFallbackDemo.Checked);
            _dgv.DataSource = null;
            _dgv.DataSource = _currentPackage.Pesajes;

            long kilos = _currentPackage.Pesajes.Sum(r => (long)r.PesoNeto);
            _lblResumenExtract.Text = string.Create(
                UiHelpers.EsCl,
                $"{ep.Planta.ToShortTitle()}: {_currentPackage.Pesajes.Count:N0} pesajes ({kilos:N0} Kg), {_currentPackage.Clientes.Count:N0} clientes y {_currentPackage.Productos.Count:N0} productos listos para importar.");
            NotifyStatus(_currentPackage.StatusMessage);
        }, NotifyStatus, "Consultando Firebird WMDBSPC...");
    }

    private async Task ImportarAsync()
    {
        if (_currentPackage is null || (_currentPackage.Pesajes.Count == 0 && _currentPackage.Clientes.Count == 0))
        {
            await BuscarRemotoAsync();
            if (_currentPackage is null || (_currentPackage.Pesajes.Count == 0 && _currentPackage.Clientes.Count == 0))
            {
                return;
            }
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            var res = await _app.ImportFirebirdPackageAsync(planta, _currentPackage);
            string summary = $"Sincronización completada ({planta.ToShortTitle()}):\n\n" +
                             $"• Pesajes nuevos importados: {res.ImportedPesajes.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Pesajes ya existentes omitidos: {res.Duplicates.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Clientes / Proveedores sincronizados: {res.ClientesUpserted.ToString("N0", UiHelpers.EsCl)}\n" +
                             $"• Productos actualizados: {res.ProductosUpserted.ToString("N0", UiHelpers.EsCl)}";

            NotifyStatus($"Importados {res.ImportedPesajes:N0} pesajes nuevos desde Firebird ({planta.ToShortTitle()}).");
            MessageBox.Show(this, summary, "Importación Firebird Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if ((res.ImportedPesajes > 0 || res.ClientesUpserted > 0) && _onDataChanged is not null)
            {
                await _onDataChanged();
            }
        }, NotifyStatus, "Importando registros hacia SQLite WAL...");
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

            _txtMdbLog.Text = $"[{DateTime.Now:HH:mm:ss}] Iniciando migración desde: {path}{Environment.NewLine}";
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

            if (_onDataChanged is not null)
            {
                await _onDataChanged();
            }

            NotifyStatus($"Access MDB migrado: {res.PesajesImported} pesajes, {res.ClientesImported} clientes.");
        }, NotifyStatus, "Ejecutando migración Access MDB...");
    }
}
