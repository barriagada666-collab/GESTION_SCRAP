using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class EjecutarPaFirebirdDialog : Form
{
    private readonly ScrapApplicationService _app;
    private readonly FirebirdPlantaEndpoint _endpoint;
    private readonly ToolTip _toolTip = new();
    private readonly CheckBox _chkDemoFallback = new();
    private readonly TextBox _txtLog = new();
    private readonly Button _btnEjecutar;
    private readonly Button _btnCerrar;

    public EjecutarPaFirebirdDialog(ScrapApplicationService app, FirebirdPlantaEndpoint endpoint)
    {
        _app = app;
        _endpoint = endpoint;

        Text = "Ejecutar Procedimientos Almacenados Firebird";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 420);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Label lblDesc = new()
        {
            Text = $"Ejecuta transaccionalmente (BeginTrans / Commit / Rollback) sobre {endpoint.ServidorIp}:{endpoint.Puerto}:\n" +
                   "1. PA_INS_PROV_STGO (Sincronización de Proveedores)\n" +
                   "2. PA_INS_MASIVO_EXP_SAP (Cola de exportación SAP)",
            Font = AppTheme.FontBody,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(16, 14),
            Size = new Size(588, 60)
        };

        _chkDemoFallback.Text = "Permitir simulación controlada si el servidor Firebird remoto no está accesible";
        _chkDemoFallback.Checked = endpoint.UsarModoLocalSiFallaRed;
        _chkDemoFallback.Font = AppTheme.FontBody;
        _chkDemoFallback.Location = new Point(16, 78);
        _chkDemoFallback.AutoSize = true;

        _txtLog.Multiline = true;
        _txtLog.ReadOnly = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.BackColor = AppTheme.HeaderDark;
        _txtLog.ForeColor = Color.FromArgb(52, 211, 153);
        _txtLog.Font = AppTheme.FontMono;
        _txtLog.SetBounds(16, 108, 588, 245);
        _txtLog.Text = "Esperando inicio de ejecución transaccional...";

        _btnCerrar = AppTheme.CreateButton("Cerrar", IconKind.Close, ButtonVariant.Secondary, (_, _) => Close(),
            "Cerrar ventana", _toolTip, 110, 34);
        _btnCerrar.Location = new Point(318, 368);

        _btnEjecutar = AppTheme.CreateButton("Iniciar Ejecución", IconKind.Play, ButtonVariant.Primary, async (_, _) => await RunAsync(),
            "Ejecutar procedimientos almacenados con Commit/Rollback", _toolTip, 168, 34);
        _btnEjecutar.Location = new Point(436, 368);

        Controls.AddRange([lblDesc, _chkDemoFallback, _txtLog, _btnCerrar, _btnEjecutar]);
        CancelButton = _btnCerrar;
    }

    private async Task RunAsync()
    {
        _btnEjecutar.Enabled = false;
        _btnCerrar.Enabled = false;
        _txtLog.Clear();

        Progress<string> progress = new(line => _txtLog.AppendText(line + Environment.NewLine));

        try
        {
            var res = await _app.ExecuteFirebirdStoredProceduresAsync(_endpoint, _chkDemoFallback.Checked, progress);
            if (res.Success)
            {
                MessageBox.Show(this, "Procedimientos almacenados confirmados (COMMIT) exitosamente.", "Proceso Terminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, $"Fallo durante la ejecución. Se aplicó ROLLBACK.\n\n{res.ErrorMessage}", "Operación Fallida",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _txtLog.AppendText($"[ERROR] {ex.Message}{Environment.NewLine}");
            MessageBox.Show(this, ex.Message, "Error de Permisos o Ejecución", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnEjecutar.Enabled = true;
            _btnCerrar.Enabled = true;
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
