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
        ClientSize = new Size(580, 405);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Label lblSql = new() { Text = "Base de Datos Central SQLite:", Font = AppTheme.FontBodyBold, Location = new Point(16, 16), AutoSize = true };
        _txtSqlite.SetBounds(16, 40, 548, 26);
        _txtSqlite.ReadOnly = true;
        _txtSqlite.Text = settings.SqliteDatabasePath;
        AppTheme.ApplyInputStyle(_txtSqlite);

        GroupBox grpFb = new()
        {
            Text = "Conexiones Remotas Firebird (TCP/IP)",
            Font = AppTheme.FontSection,
            Location = new Point(16, 80),
            Size = new Size(548, 254),
            BackColor = Color.White
        };

        Label lp = new() { Text = "Planta:", Font = AppTheme.FontBodyBold, Location = new Point(16, 32), AutoSize = true };
        _cmbPlanta.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPlanta.Font = AppTheme.FontBody;
        _cmbPlanta.SetBounds(120, 28, 220, 28);
        _cmbPlanta.Items.AddRange(["Placilla", "Santiago", "Talcahuano"]);
        _cmbPlanta.SelectedIndexChanged += (_, _) => LoadEndpointFields();

        Label lip = new() { Text = "IP Servidor:", Font = AppTheme.FontBodyBold, Location = new Point(16, 70), AutoSize = true };
        _txtIp.SetBounds(120, 66, 220, 26);
        AppTheme.ApplyInputStyle(_txtIp);

        Label lpo = new() { Text = "Puerto TCP:", Font = AppTheme.FontBodyBold, Location = new Point(356, 70), AutoSize = true };
        _numPort.SetBounds(440, 66, 90, 26);
        _numPort.Maximum = 65535;
        _numPort.Minimum = 1;
        _numPort.Value = 3050;

        Label lru = new() { Text = "Ruta GDB/FDB:", Font = AppTheme.FontBodyBold, Location = new Point(16, 106), AutoSize = true };
        _txtRuta.SetBounds(120, 102, 410, 26);
        AppTheme.ApplyInputStyle(_txtRuta);

        Label lus = new() { Text = "Usuario:", Font = AppTheme.FontBodyBold, Location = new Point(16, 142), AutoSize = true };
        _txtUser.SetBounds(120, 138, 170, 26);
        AppTheme.ApplyInputStyle(_txtUser);

        Label lpw = new() { Text = "Contraseña:", Font = AppTheme.FontBodyBold, Location = new Point(304, 142), AutoSize = true };
        _txtPass.SetBounds(386, 138, 144, 26);
        _txtPass.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtPass);

        _chkFallback.Text = "Permitir modo demostrativo si el servidor no responde en red";
        _chkFallback.Font = AppTheme.FontBody;
        _chkFallback.Location = new Point(16, 176);
        _chkFallback.AutoSize = true;

        Button btnProbar = AppTheme.CreateButton("Probar Conexión TCP", IconKind.Refresh, ButtonVariant.Secondary, async (_, _) => await TestCurrentAsync(),
            "Probar conectividad TCP y consultar metadata Firebird", _toolTip, 185, 32);
        btnProbar.Location = new Point(345, 208);

        grpFb.Controls.AddRange([lp, _cmbPlanta, lip, _txtIp, lpo, _numPort, lru, _txtRuta, lus, _txtUser, lpw, _txtPass, _chkFallback, btnProbar]);

        Button btnCerrar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar sin guardar", _toolTip, 115, 34);
        btnCerrar.Location = new Point(308, 352);

        Button btnGuardar = AppTheme.CreateButton("Guardar Cambios", IconKind.Save, ButtonVariant.Primary, (_, _) =>
        {
            SaveCurrentEndpoint();
            _settingsStore.Save(_settings);
            DialogResult = DialogResult.OK;
            Close();
        }, "Guardar configuración cifrada con DPAPI", _toolTip, 135, 34);
        btnGuardar.Location = new Point(429, 352);

        Controls.AddRange([lblSql, _txtSqlite, grpFb, btnCerrar, btnGuardar]);
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
