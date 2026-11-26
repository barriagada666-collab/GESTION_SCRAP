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
        ClientSize = new Size(410, 270);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Label l1 = new() { Text = "Contraseña actual:", Font = AppTheme.FontBodyBold, Location = new Point(20, 18), AutoSize = true };
        _txtActual.SetBounds(20, 42, 370, 26);
        _txtActual.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtActual);

        Label l2 = new() { Text = "Nueva contraseña (mín. 8 caracteres, Mayús/Minús/Dígito):", Font = AppTheme.FontBodyBold, Location = new Point(20, 78), AutoSize = true };
        _txtNueva.SetBounds(20, 102, 370, 26);
        _txtNueva.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtNueva);

        Label l3 = new() { Text = "Confirmar nueva contraseña:", Font = AppTheme.FontBodyBold, Location = new Point(20, 138), AutoSize = true };
        _txtConfirm.SetBounds(20, 162, 370, 26);
        _txtConfirm.UseSystemPasswordChar = true;
        AppTheme.ApplyInputStyle(_txtConfirm);

        Button btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar cambio", _toolTip, 110, 34);
        btnCancelar.Location = new Point(126, 210);

        Button btnGuardar = AppTheme.CreateButton("Guardar Clave", IconKind.Key, ButtonVariant.Primary, async (_, _) => await SaveAsync(),
            "Actualizar contraseña de usuario", _toolTip, 146, 34);
        btnGuardar.Location = new Point(244, 210);

        Controls.AddRange([l1, _txtActual, l2, _txtNueva, l3, _txtConfirm, btnCancelar, btnGuardar]);
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
