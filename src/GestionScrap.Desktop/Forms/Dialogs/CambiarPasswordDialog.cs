using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class CambiarPasswordDialog : Form
{
    private readonly IAuthService _auth;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtActual = new();
    private readonly TextBox _txtNueva = new();
    private readonly TextBox _txtConfirm = new();

    public CambiarPasswordDialog(IAuthService auth)
    {
        _auth = auth;
        Text = "Cambiar Contraseña";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 295);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(ClientSize.Width, ClientSize.Height - 58),
            BackColor = AppTheme.CanvasLight,
            Padding = new Padding(20, 16, 20, 12)
        };

        Label l1 = new() { Text = "Contraseña actual:", Font = AppTheme.FontBodyBold, Location = new Point(20, 16), AutoSize = true };
        _txtActual.SetBounds(20, 40, 430, 28);
        _txtActual.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtActual.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtActual);

        Label l2 = new() { Text = "Nueva contraseña (mín. 8 caracteres, Mayús/Minús/Dígito):", Font = AppTheme.FontBodyBold, Location = new Point(20, 80), AutoSize = true };
        _txtNueva.SetBounds(20, 104, 430, 28);
        _txtNueva.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtNueva.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtNueva);

        Label l3 = new() { Text = "Confirmar nueva contraseña:", Font = AppTheme.FontBodyBold, Location = new Point(20, 144), AutoSize = true };
        _txtConfirm.SetBounds(20, 168, 430, 28);
        _txtConfirm.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtConfirm.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtConfirm);

        body.Controls.AddRange([l1, _txtActual, l2, _txtNueva, l3, _txtConfirm]);

        Button btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar cambio", _toolTip, 0, 34);

        Button btnGuardar = AppTheme.CreateButton("Guardar Clave", IconKind.Key, ButtonVariant.Primary, async (_, _) => await SaveAsync(),
            "Actualizar contraseña de usuario", _toolTip, 0, 34);

        Panel footer = AppTheme.CreateDialogFooter(btnCancelar, btnGuardar);

        Controls.Add(body);
        Controls.Add(footer);
        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;
    }

    private async Task SaveAsync()
    {
        if (!string.Equals(_txtNueva.Text, _txtConfirm.Text, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "La nueva contraseña y su confirmación no coinciden.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            bool ok = await _auth.ChangePasswordAsync(_txtActual.Text, _txtNueva.Text);
            if (!ok)
            {
                MessageBox.Show(this, "La contraseña actual es incorrecta.", "Error de Validación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(this, "Contraseña actualizada correctamente.", "Seguridad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
