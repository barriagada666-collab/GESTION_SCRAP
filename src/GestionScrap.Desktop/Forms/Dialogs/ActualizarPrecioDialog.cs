using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class ActualizarPrecioDialog : Form
{
    private readonly ScrapApplicationService _app;
    private readonly Planta _planta;
    private readonly ModoPrecio _modo;
    private readonly long _folio;
    private readonly string _familia;
    private readonly ToolTip _toolTip = new();
    private readonly NumericUpDown _numPrecio = new();

    public ActualizarPrecioDialog(
        ScrapApplicationService app,
        Planta planta,
        ModoPrecio modo,
        long folio,
        string codProducto,
        string descripcion,
        string proveedor,
        string familia,
        decimal precioActual)
    {
        _app = app;
        _planta = planta;
        _modo = modo;
        _folio = folio;
        _familia = familia;

        Text = $"Actualizar Precio Real · Folio #{folio}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(490, 315);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel card = new()
        {
            Location = new Point(16, 16),
            Size = new Size(458, 225),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16)
        };

        Label lblInfo = new()
        {
            Text = $"Folio: #{folio}   |   Planta: {planta.ToShortTitle()}   |   Familia: {familia}\n" +
                   $"Producto: {codProducto} - {descripcion}\n" +
                   $"Proveedor: {proveedor}\n" +
                   $"Precio Actual Indexado: ${precioActual:N0}",
            Font = AppTheme.FontBody,
            ForeColor = AppTheme.TextPrimary,
            Location = new Point(16, 16),
            Size = new Size(424, 90)
        };

        Label lblNuevo = new()
        {
            Text = "Nuevo Precio Unitario ($/Kg):",
            Font = AppTheme.FontBodyBold,
            Location = new Point(16, 118),
            AutoSize = true
        };

        _numPrecio.SetBounds(16, 144, 210, 30);
        _numPrecio.Font = new Font("Consolas", 11F, FontStyle.Bold);
        _numPrecio.Maximum = 9999999;
        _numPrecio.Minimum = 1;
        _numPrecio.ThousandsSeparator = true;
        _numPrecio.Value = precioActual >= 1 ? Math.Min(9999999, precioActual) : 100;

        Label lblModoWarn = new()
        {
            Text = modo == ModoPrecio.Reales
                ? "Esta modificación actualizará el precio real indexado para este folio."
                : "ADVERTENCIA: Debe cambiar a modo 'Precios Reales' para poder guardar.",
            Font = AppTheme.FontBody,
            ForeColor = modo == ModoPrecio.Reales ? AppTheme.AccentEmerald : AppTheme.AccentRed,
            Location = new Point(16, 184),
            Size = new Size(424, 30)
        };

        card.Controls.AddRange([lblInfo, lblNuevo, _numPrecio, lblModoWarn]);

        Button btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar edición", _toolTip, 115, 34);
        btnCancelar.Location = new Point(214, 260);

        Button btnGuardar = AppTheme.CreateButton("Guardar Precio", IconKind.Save, ButtonVariant.Warning, async (_, _) => await GuardarAsync(),
            "Confirmar nuevo precio real", _toolTip, 138, 34);
        btnGuardar.Location = new Point(336, 260);
        btnGuardar.Enabled = modo == ModoPrecio.Reales;

        Controls.AddRange([card, btnCancelar, btnGuardar]);
        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;
    }

    private async Task GuardarAsync()
    {
        try
        {
            await _app.ActualizarPrecioRealIndividualAsync(_planta, _modo, _folio, _numPrecio.Value, _familia);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Validación de Precios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
