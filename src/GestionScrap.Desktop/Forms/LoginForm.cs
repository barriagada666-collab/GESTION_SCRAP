using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Forms;

public sealed class LoginForm : Form
{
    private readonly IAuthService _authService;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtUser = new();
    private readonly TextBox _txtPass = new();
    private readonly CheckBox _chkShowPass = new();
    private readonly Label _lblError = new();
    private readonly Button _btnLogin;
    private readonly Button _btnExit;

    public LoginForm(IAuthService authService)
    {
        _authService = authService;

        Text = "Gestión de Scrap · Inicio de Sesión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 405);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(20, 14, 20, 14)
        };

        PictureBox picLogo = new()
        {
            Size = new Size(56, 56),
            Location = new Point(20, 20),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppTheme.LoadAppLogo(),
            BackColor = Color.Transparent
        };

        Label lblTitle = new()
        {
            Text = "GESTIÓN DE SCRAP",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 20),
            AutoSize = true
        };

        Label lblSub = new()
        {
            Text = "Control de Pesajes, Precios e Informes · v4.0",
            Font = AppTheme.FontSubtitle,
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(89, 50),
            AutoSize = true
        };

        header.Controls.AddRange([picLogo, lblTitle, lblSub]);

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 18),
            BackColor = AppTheme.CanvasLight
        };

        Label lblUser = new() { Text = "Usuario:", Font = AppTheme.FontBodyBold, ForeColor = AppTheme.TextPrimary, Location = new Point(28, 20), AutoSize = true };
        _txtUser.SetBounds(28, 44, 404, 28);
        _txtUser.AccessibleName = "Usuario";
        AppTheme.ApplyInputStyle(_txtUser);

        Label lblPass = new() { Text = "Contraseña:", Font = AppTheme.FontBodyBold, ForeColor = AppTheme.TextPrimary, Location = new Point(28, 86), AutoSize = true };
        _txtPass.SetBounds(28, 110, 404, 28);
        _txtPass.UseSystemPasswordChar = true;
        _txtPass.AccessibleName = "Contraseña";
        AppTheme.ApplyInputStyle(_txtPass);

        _chkShowPass.Text = "Mostrar contraseña";
        _chkShowPass.Font = AppTheme.FontBody;
        _chkShowPass.ForeColor = AppTheme.TextSecondary;
        _chkShowPass.Location = new Point(28, 146);
        _chkShowPass.AutoSize = true;
        _chkShowPass.CheckedChanged += (_, _) => _txtPass.UseSystemPasswordChar = !_chkShowPass.Checked;

        _lblError.SetBounds(28, 174, 404, 38);
        _lblError.ForeColor = AppTheme.AccentRed;
        _lblError.Font = AppTheme.FontBodyBold;
        _lblError.Visible = false;

        Label lblHint = new()
        {
            Text = "Perfiles: Administrador | Supervisor | Operador | Auditor",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(28, 218),
            AutoSize = true
        };

        _btnExit = AppTheme.CreateButton("Salir", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar la aplicación", _toolTip, 120, 36);
        _btnExit.Location = new Point(122, 252);

        _btnLogin = AppTheme.CreateButton("Ingresar al Sistema", IconKind.Lock, ButtonVariant.Primary, async (_, _) => await AttemptLoginAsync(),
            "Validar credenciales e iniciar sesión", _toolTip, 180, 36);
        _btnLogin.Location = new Point(252, 252);

        AcceptButton = _btnLogin;
        CancelButton = _btnExit;

        body.Controls.AddRange([lblUser, _txtUser, lblPass, _txtPass, _chkShowPass, _lblError, lblHint, _btnExit, _btnLogin]);

        Controls.Add(body);
        Controls.Add(header);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtUser.Focus();
    }

    private async Task AttemptLoginAsync()
    {
        _lblError.Visible = false;
        _btnLogin.Enabled = false;
        _btnExit.Enabled = false;
        try
        {
            var result = await _authService.LoginAsync(_txtUser.Text, _txtPass.Text);
            if (result.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _lblError.Text = result.Message;
            _lblError.Visible = true;
            _txtPass.Clear();
            _txtPass.Focus();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Error al autenticar: {ex.Message}";
            _lblError.Visible = true;
        }
        finally
        {
            _btnLogin.Enabled = true;
            _btnExit.Enabled = true;
        }
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
