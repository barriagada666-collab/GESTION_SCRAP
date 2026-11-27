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

        Text = "Cambiaso · Gestión de Scrap · Inicio de Sesión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 445);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 114,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(20, 14, 20, 14)
        };
        header.Paint += (_, e) =>
        {
            using Pen goldBar = new(AppTheme.AccentGold, 3f);
            e.Graphics.DrawLine(goldBar, 0, header.Height - 2, header.Width, header.Height - 2);
        };

        PictureBox picLogo = new()
        {
            Size = new Size(68, 68),
            Location = new Point(20, 22),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppTheme.LoadAppLogo(),
            BackColor = Color.Transparent
        };

        Label lblTitle = new()
        {
            Text = "GESTIÓN DE SCRAP",
            Font = AppTheme.FontLoginTitle,
            ForeColor = Color.White,
            Location = new Point(100, 18),
            AutoSize = true
        };

        Label lblBrand = new()
        {
            Text = "CAMBIASO DESDE 1875 · .NET 10",
            Font = AppTheme.FontSmallBold,
            ForeColor = AppTheme.AccentGold,
            Location = new Point(101, 48),
            AutoSize = true
        };

        Label lblSub = new()
        {
            Text = "Control de Pesajes, Grupos y Proveedores",
            Font = AppTheme.FontSubtitle,
            ForeColor = Color.FromArgb(186, 210, 228),
            Location = new Point(101, 70),
            AutoSize = true
        };

        header.Controls.AddRange([picLogo, lblTitle, lblBrand, lblSub]);

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(ClientSize.Width, ClientSize.Height - header.Height),
            Padding = new Padding(28, 18, 28, 12),
            BackColor = AppTheme.CanvasLight
        };

        Label lblUser = new() { Text = "Usuario:", Font = AppTheme.FontBodyBold, ForeColor = AppTheme.TextPrimary, Location = new Point(28, 18), AutoSize = true };
        _txtUser.SetBounds(28, 42, 444, 28);
        _txtUser.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtUser.AccessibleName = "Usuario";
        _txtUser.PlaceholderText = "Ingrese nombre de usuario (ej: barriagada)";
        AppTheme.ApplyInputStyle(_txtUser);

        Label lblPass = new() { Text = "Contraseña:", Font = AppTheme.FontBodyBold, ForeColor = AppTheme.TextPrimary, Location = new Point(28, 84), AutoSize = true };
        _txtPass.SetBounds(28, 108, 444, 28);
        _txtPass.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtPass.UseSystemPasswordChar = true;
        _txtPass.AccessibleName = "Contraseña";
        AppTheme.ApplyInputStyle(_txtPass);

        _chkShowPass.Text = "Mostrar contraseña";
        _chkShowPass.Font = AppTheme.FontBody;
        _chkShowPass.ForeColor = AppTheme.TextSecondary;
        _chkShowPass.Location = new Point(28, 146);
        _chkShowPass.AutoSize = true;
        _chkShowPass.CheckedChanged += (_, _) => _txtPass.UseSystemPasswordChar = !_chkShowPass.Checked;

        _lblError.SetBounds(28, 176, 444, 40);
        _lblError.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _lblError.ForeColor = AppTheme.AccentRed;
        _lblError.Font = AppTheme.FontBodyBold;
        _lblError.Visible = false;

        Label lblHint = new()
        {
            Text = "Acceso controlado por perfiles de seguridad RBAC.",
            Font = AppTheme.FontSmallItalic,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(28, 224),
            AutoSize = true
        };

        _btnExit = AppTheme.CreateButton("Salir", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar la aplicación", _toolTip, 110, 36);

        _btnLogin = AppTheme.CreateButton("Ingresar al Sistema", IconKind.Lock, ButtonVariant.Primary, async (_, _) => await AttemptLoginAsync(),
            "Validar credenciales e iniciar sesión", _toolTip, 0, 36);

        Panel footer = AppTheme.CreateDialogFooter(_btnExit, _btnLogin);
        footer.Padding = new Padding(28, 12, 28, 14);

        AcceptButton = _btnLogin;
        CancelButton = _btnExit;

        body.Controls.AddRange([lblUser, _txtUser, lblPass, _txtPass, _chkShowPass, _lblError, lblHint]);

        Controls.Add(body);
        Controls.Add(footer);
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
            UseWaitCursor = true;
            var res = await _authService.LoginAsync(_txtUser.Text, _txtPass.Text);
            if (!res.Success)
            {
                _lblError.Text = res.Message;
                _lblError.Visible = true;
                _txtPass.SelectAll();
                _txtPass.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        finally
        {
            UseWaitCursor = false;
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
