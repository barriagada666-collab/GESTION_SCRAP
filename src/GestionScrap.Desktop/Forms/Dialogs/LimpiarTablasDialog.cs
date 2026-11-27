using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class LimpiarTablasDialog : Form
{
    private readonly ScrapApplicationService _app;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtConfirm = new();

    public int PurgedCount { get; private set; }

    public LimpiarTablasDialog(ScrapApplicationService app)
    {
        _app = app;
        Text = "Purga Crítica de Tablas de Pesaje";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(515, 295);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(ClientSize.Width, ClientSize.Height - 58),
            BackColor = AppTheme.CanvasLight
        };

        Panel warnBox = new()
        {
            Location = new Point(16, 16),
            Size = new Size(483, 108),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(254, 242, 242),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12)
        };

        Label lblWarn = new()
        {
            Text = "ATENCIÓN: Esta acción eliminará de forma irreversible TODOS los registros de pesajes almacenados en la base de datos central.\n\n" +
                   "La operación quedará registrada en la bitácora de auditoría.",
            Font = AppTheme.FontBodyBold,
            ForeColor = Color.FromArgb(153, 27, 27),
            Dock = DockStyle.Fill
        };
        warnBox.Controls.Add(lblWarn);

        Label lblPrompt = new()
        {
            Text = "Escriba exactamente la palabra ELIMINAR para confirmar:",
            Font = AppTheme.FontBodyBold,
            Location = new Point(16, 138),
            AutoSize = true
        };

        _txtConfirm.SetBounds(16, 166, 483, 30);
        _txtConfirm.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtConfirm.TextAlign = HorizontalAlignment.Center;
        AppTheme.ApplyInputStyle(_txtConfirm);
        _txtConfirm.Font = AppTheme.FontMonoBold;

        body.Controls.AddRange([warnBox, lblPrompt, _txtConfirm]);

        Button btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar purga", _toolTip, 0, 34);

        Button btnPurgar = AppTheme.CreateButton("Confirmar Purga", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await ConfirmPurgeAsync(),
            "Eliminar registros de pesaje", _toolTip, 0, 34);

        Panel footer = AppTheme.CreateDialogFooter(btnCancelar, btnPurgar);

        Controls.Add(body);
        Controls.Add(footer);
        CancelButton = btnCancelar;
    }

    private async Task ConfirmPurgeAsync()
    {
        try
        {
            PurgedCount = await _app.PurgeAllPesajesAsync(_txtConfirm.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Validación de Seguridad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
