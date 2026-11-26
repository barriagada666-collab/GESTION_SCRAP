using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 7: Importación directa desde básculas Firebird (.GDB/.FDB) vía TCP/IP con detección de duplicados y ejecución de PA SAP.
/// </summary>
public sealed class ImportacionFirebirdControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly Func<Task>? _onDataChanged;

    private readonly ComboBox _cmbPlanta = new() { Width = 135 };
    private readonly Label _lblEndpoint = new();
    private readonly CheckBox _chkFallbackDemo = new();
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnTestTcp;
    private readonly Button _btnConfig;
    private readonly Button _btnBuscar;
    private readonly Button _btnImportar;
    private readonly Button _btnEjecutarPa;
    private readonly Label _lblEstadoGrid = new();
    private readonly DoubleBufferedDataGridView _dgv = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private IReadOnlyList<FirebirdRemotePesaje> _remoteRecords = [];

    public ImportacionFirebirdControl(
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
        Padding = new Padding(14);

        Panel topCard = UiHelpers.BuildFilterCard(104);
        topCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: true);
        _cmbPlanta.SelectedIndexChanged += (_, _) => UpdateEndpointBanner();

        DateTime today = DateTime.Today;
        _dtpDesde.Value = today.AddDays(-7);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _chkFallbackDemo.Text = "Modo respaldo local si falla red";
        _chkFallbackDemo.Checked = true;
        _chkFallbackDemo.Font = AppTheme.FontBody;
        _chkFallbackDemo.AutoSize = true;
        _chkFallbackDemo.Margin = new Padding(10, 8, 6, 0);

        _lblEndpoint.Font = AppTheme.FontMono;
        _lblEndpoint.ForeColor = AppTheme.TextSecondary;
        _lblEndpoint.AutoSize = true;
        _lblEndpoint.Margin = new Padding(12, 8, 0, 0);

        _btnTestTcp = AppTheme.CreateButton("Probar TCP", IconKind.Check, ButtonVariant.Secondary,
            async (_, _) => await TestTcpAsync(), "Verifica puerto TCP 3050 y metadatos Firebird", Tip, 125, 34);
        _btnConfig = AppTheme.CreateButton("Configurar Red", IconKind.Settings, ButtonVariant.Secondary,
            (_, _) => OpenConfigDialog(), "Configura IPs, rutas .GDB/.FDB y credenciales Firebird", Tip, 145, 34);
        _btnBuscar = AppTheme.CreateButton("Buscar en Báscula", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await BuscarRemotoAsync(), "Consulta pesajes en el servidor Firebird seleccionado", Tip, 165, 34);
        _btnImportar = AppTheme.CreateButton("Importar a SQLite", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await ImportarAsync(), "Guarda los registros en la base central omitiendo duplicados", Tip, 165, 34);
        _btnEjecutarPa = AppTheme.CreateButton("Ejecutar PA SAP", IconKind.Play, ButtonVariant.Warning,
            (_, _) => OpenPaDialog(), "Ejecuta PA_INS_PROV_STGO y PA_INS_MASIVO_EXP_SAP con transacción", Tip, 155, 34);

        TableLayoutPanel rows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) },
            _cmbPlanta,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 8, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 8, 4, 0) },
            _dtpHasta,
            _chkFallbackDemo,
            _lblEndpoint
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r2.Controls.AddRange([_btnTestTcp, _btnConfig, _btnBuscar, _btnImportar, _btnEjecutarPa]);

        rows.Controls.Add(r1, 0, 0);
        rows.Controls.Add(r2, 0, 1);
        topCard.Controls.Add(rows);

        Panel gridCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblEstadoGrid.Text = "Seleccione la planta, verifique red TCP/IP y presione 'Buscar en Báscula'.";
        _lblEstadoGrid.Font = AppTheme.FontSection;
        _lblEstadoGrid.ForeColor = AppTheme.AccentCyan;
        _lblEstadoGrid.Dock = DockStyle.Top;
        _lblEstadoGrid.Height = 28;

        _dgv.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(FirebirdRemotePesaje.Folio), "N0", 55),
            UiHelpers.AddTextCol("Tipo", nameof(FirebirdRemotePesaje.TipoPesajeRaw), 40),
            UiHelpers.AddTextCol("Patente", nameof(FirebirdRemotePesaje.Patente), 65),
            UiHelpers.AddNumberCol("Bruto", nameof(FirebirdRemotePesaje.PesoBruto), "N0", 58),
            UiHelpers.AddNumberCol("Tara", nameof(FirebirdRemotePesaje.PesoTara), "N0", 58),
            UiHelpers.AddNumberCol("Neto Kg", nameof(FirebirdRemotePesaje.PesoNeto), "N0", 62),
            UiHelpers.AddTextCol("Cód", nameof(FirebirdRemotePesaje.CodProducto), 55),
            UiHelpers.AddTextCol("Producto", nameof(FirebirdRemotePesaje.NomProducto), 115),
            UiHelpers.AddTextCol("RUT", nameof(FirebirdRemotePesaje.RutCliente), 75),
            UiHelpers.AddTextCol("Cliente / Proveedor", nameof(FirebirdRemotePesaje.NomCliente), 120),
            UiHelpers.AddTextCol("Origen", nameof(FirebirdRemotePesaje.Origen), 75),
            UiHelpers.AddTextCol("Destino", nameof(FirebirdRemotePesaje.Destino), 75),
            UiHelpers.AddTextCol("Guía", nameof(FirebirdRemotePesaje.GuiaDespacho), 60),
            UiHelpers.AddDateCol("Ingreso", nameof(FirebirdRemotePesaje.FechaIngreso), 70),
            UiHelpers.AddDateCol("Salida", nameof(FirebirdRemotePesaje.FechaSalida), 70)
        ]);

        gridCard.Controls.Add(_dgv);
        gridCard.Controls.Add(_lblEstadoGrid);

        Controls.Add(gridCard);
        Controls.Add(topCard);

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
        bool canImport = _auth.HasPermission(PermisoSistema.ImportarFirebird);
        _btnTestTcp.Enabled = canImport;
        _btnBuscar.Enabled = canImport;
        _btnImportar.Enabled = canImport;
        _btnEjecutarPa.Enabled = _auth.HasPermission(PermisoSistema.EjecutarProcedimientosFirebird);
    }

    private async Task TestTcpAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            var res = await _app.TestFirebirdConnectionAsync(ep);
            MessageBoxIcon icon = res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(this,
                $"{res.Message}\nPing TCP: {res.PingMs} ms\nTablas: {res.TablesCount} | PA: {res.ProceduresCount}",
                $"Diagnóstico Firebird ({ep.Planta.ToShortTitle()})",
                MessageBoxButtons.OK,
                icon);
            NotifyStatus(res.Message);
        }, NotifyStatus, "Probando conexión TCP Firebird...");
    }

    private void OpenConfigDialog()
    {
        using ConfiguracionDbDialog dlg = new(_settingsStore, _settings, _app);
        dlg.ShowDialog(this);
        UpdateEndpointBanner();
    }

    private void OpenPaDialog()
    {
        using EjecutarPaFirebirdDialog dlg = new(_app, GetCurrentEndpoint());
        dlg.ShowDialog(this);
    }

    private async Task BuscarRemotoAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            FirebirdPlantaEndpoint ep = GetCurrentEndpoint();
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            var res = await _app.QueryFirebirdRemoteAsync(ep, desde, hasta, _chkFallbackDemo.Checked);
            _remoteRecords = res.Records;
            _dgv.DataSource = null;
            _dgv.DataSource = _remoteRecords;

            long totalKilos = _remoteRecords.Sum(r => (long)r.PesoNeto);
            _lblEstadoGrid.Text = string.Create(
                UiHelpers.EsCl,
                $"{ep.Planta.ToShortTitle()}: {_remoteRecords.Count} registros ({totalKilos:N0} Kg) · {res.StatusMessage}");
            NotifyStatus(res.StatusMessage);
        }, NotifyStatus, "Consultando báscula Firebird...");
    }

    private async Task ImportarAsync()
    {
        if (_remoteRecords.Count == 0)
        {
            MessageBox.Show(this, "Busque primero registros en Firebird antes de importar.", "Sin Registros", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            (int imported, int duplicates) = await _app.ImportFromFirebirdAsync(planta, _remoteRecords);
            NotifyStatus($"Importación {planta.ToShortTitle()}: {imported} nuevos, {duplicates} duplicados omitidos.");
            MessageBox.Show(
                this,
                $"Importación finalizada para {planta.ToShortTitle()}:\n\n" +
                $"• Registros nuevos importados: {imported}\n" +
                $"• Duplicados omitidos (regla folio/planta): {duplicates}",
                "Importación Firebird",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (imported > 0 && _onDataChanged is not null)
            {
                await _onDataChanged();
            }
        }, NotifyStatus, "Importando pesajes hacia SQLite...");
    }
}
