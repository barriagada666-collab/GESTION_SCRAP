using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class ConfiguracionDbDialog : Form
{
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly ScrapApplicationService _app;
    private readonly ToolTip _toolTip = new();

    private readonly TextBox _txtSqlite = new();
    private readonly ComboBox _cmbPlanta = new();
    private readonly TextBox _txtIp = new();
    private readonly NumericUpDown _numPort = new();
    private readonly TextBox _txtRuta = new();
    private readonly TextBox _txtUser = new();
    private readonly TextBox _txtPass = new();
    private readonly CheckBox _chkFallback = new();

    public ConfiguracionDbDialog(AppSettingsStore store, AppSettings settings, ScrapApplicationService app)
    {
        _settingsStore = store;
        _settings = settings;
        _app = app;

        Text = "Configuración de Bases de Datos (SQLite & Firebird)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 448);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(ClientSize.Width, ClientSize.Height - 58),
            BackColor = AppTheme.CanvasLight
        };

        Label lblSql = new() { Text = "Base de Datos Central SQLite:", Font = AppTheme.FontBodyBold, Location = new Point(16, 14), AutoSize = true };
        _txtSqlite.SetBounds(16, 38, 608, 28);
        _txtSqlite.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtSqlite.ReadOnly = true;
        _txtSqlite.Text = settings.SqliteDatabasePath;
        AppTheme.ApplyInputStyle(_txtSqlite);

        GroupBox grpFb = new()
        {
            Text = "Conexiones Remotas Firebird (TCP/IP)",
            Font = AppTheme.FontSection,
            Location = new Point(16, 78),
            Size = new Size(608, 290),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.White
        };

        Label lp = new() { Text = "Planta:", Font = AppTheme.FontBodyBold, Location = new Point(16, 34), AutoSize = true };
        _cmbPlanta.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPlanta.Font = AppTheme.FontBody;
        _cmbPlanta.SetBounds(130, 30, 230, 28);
        _cmbPlanta.Items.AddRange(["Placilla", "Santiago", "Talcahuano"]);
        _cmbPlanta.SelectedIndexChanged += (_, _) => LoadEndpointFields();

        Label lip = new() { Text = "IP Servidor:", Font = AppTheme.FontBodyBold, Location = new Point(16, 74), AutoSize = true };
        _txtIp.SetBounds(130, 70, 230, 28);
        AppTheme.ApplyInputStyle(_txtIp);

        Label lpo = new() { Text = "Puerto TCP:", Font = AppTheme.FontBodyBold, Location = new Point(378, 74), AutoSize = true };
        _numPort.SetBounds(474, 70, 114, 28);
        _numPort.Font = AppTheme.FontBody;
        _numPort.Maximum = 65535;
        _numPort.Minimum = 1;
        _numPort.Value = 3050;

        Label lru = new() { Text = "Ruta GDB/FDB:", Font = AppTheme.FontBodyBold, Location = new Point(16, 112), AutoSize = true };
        _txtRuta.SetBounds(130, 108, 458, 28);
        AppTheme.ApplyInputStyle(_txtRuta);

        Label lus = new() { Text = "Usuario:", Font = AppTheme.FontBodyBold, Location = new Point(16, 150), AutoSize = true };
        _txtUser.SetBounds(130, 146, 195, 28);
        AppTheme.ApplyInputStyle(_txtUser);

        Label lpw = new() { Text = "Contraseña:", Font = AppTheme.FontBodyBold, Location = new Point(340, 150), AutoSize = true };
        _txtPass.SetBounds(432, 146, 156, 28);
        _txtPass.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtPass);

        _chkFallback.Text = "Permitir modo demostrativo si el servidor no responde en red";
        _chkFallback.Font = AppTheme.FontBody;
        _chkFallback.Location = new Point(16, 188);
        _chkFallback.AutoSize = true;

        Button btnProbar = AppTheme.CreateButton("Probar Conexión TCP", IconKind.Refresh, ButtonVariant.Secondary, async (_, _) => await TestCurrentAsync(),
            "Probar conectividad TCP y consultar metadata Firebird", _toolTip, 0, 34);
        btnProbar.Margin = Padding.Empty;

        FlowLayoutPanel grpFooter = new()
        {
            Dock = DockStyle.Bottom,
            Height = btnProbar.Height + 22,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(16, 4, 18, 12)
        };
        grpFooter.Controls.Add(btnProbar);

        grpFb.Controls.AddRange([lp, _cmbPlanta, lip, _txtIp, lpo, _numPort, lru, _txtRuta, lus, _txtUser, lpw, _txtPass, _chkFallback, grpFooter]);
        body.Controls.AddRange([lblSql, _txtSqlite, grpFb]);

        Button btnCerrar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar sin guardar", _toolTip, 0, 34);

        Button btnGuardar = AppTheme.CreateButton("Guardar Cambios", IconKind.Save, ButtonVariant.Primary, (_, _) =>
        {
            SaveCurrentEndpoint();
            _settingsStore.Save(_settings);
            DialogResult = DialogResult.OK;
            Close();
        }, "Guardar configuración cifrada con DPAPI", _toolTip, 0, 34);

        Panel footer = AppTheme.CreateDialogFooter(btnCerrar, btnGuardar);

        Controls.Add(body);
        Controls.Add(footer);
        AcceptButton = btnGuardar;
        CancelButton = btnCerrar;
        _cmbPlanta.SelectedIndex = 0;
    }

    private FirebirdPlantaEndpoint GetSelectedEndpoint()
    {
        Planta p = (Planta)(_cmbPlanta.SelectedIndex + 1);
        FirebirdPlantaEndpoint? ep = _settings.FirebirdEndpoints.FirstOrDefault(e => e.Planta == p);
        if (ep is null)
        {
            ep = new FirebirdPlantaEndpoint { Planta = p, Nombre = $"Firebird {p.ToShortTitle()}" };
            _settings.FirebirdEndpoints.Add(ep);
        }

        return ep;
    }

    private void LoadEndpointFields()
    {
        FirebirdPlantaEndpoint ep = GetSelectedEndpoint();
        _txtIp.Text = ep.ServidorIp;
        _numPort.Value = ep.Puerto is >= 1 and <= 65535 ? ep.Puerto : 3050;
        _txtRuta.Text = ep.RutaArchivo;
        _txtUser.Text = ep.Usuario;
        _txtPass.Text = ep.Password;
        _chkFallback.Checked = ep.UsarModoLocalSiFallaRed;
    }

    private void SaveCurrentEndpoint()
    {
        FirebirdPlantaEndpoint ep = GetSelectedEndpoint();
        ep.ServidorIp = _txtIp.Text.Trim();
        ep.Puerto = (int)_numPort.Value;
        ep.RutaArchivo = _txtRuta.Text.Trim();
        ep.Usuario = _txtUser.Text.Trim();
        ep.Password = _txtPass.Text;
        ep.UsarModoLocalSiFallaRed = _chkFallback.Checked;
    }

    private async Task TestCurrentAsync()
    {
        SaveCurrentEndpoint();
        FirebirdPlantaEndpoint ep = GetSelectedEndpoint();
        var res = await _app.TestFirebirdConnectionAsync(ep);
        MessageBox.Show(this, res.Message, res.Success ? "Conexión Exitosa" : "Error de Conexión",
            MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
