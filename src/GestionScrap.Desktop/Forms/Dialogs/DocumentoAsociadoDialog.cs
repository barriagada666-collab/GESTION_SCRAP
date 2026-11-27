using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo para asociar documentos comerciales o tributarios (Guías, Facturas, Contratos, Certificados) a una entidad, operación o pesaje.
/// </summary>
public sealed class DocumentoAsociadoDialog : Form
{
    private readonly string _rutEntidad;
    private readonly long? _operacionId;
    private readonly long? _pesajeId;

    private readonly ComboBox _cmbTipo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _txtNumero = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dtpFecha = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly TextBox _txtObservaciones = new() { Dock = DockStyle.Fill, Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };

    public DocumentoAsociado? ResultDocumento { get; private set; }

    public DocumentoAsociadoDialog(string rutEntidad, string contextTitle, long? operacionId = null, long? pesajeId = null)
    {
        _rutEntidad = rutEntidad;
        _operacionId = operacionId;
        _pesajeId = pesajeId;

        Text = $"Asociar Documento · {contextTitle}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 300);
        BackColor = AppTheme.CanvasLight;

        AppTheme.ApplyInputStyle(_txtNumero);
        AppTheme.ApplyInputStyle(_txtObservaciones);
        _cmbTipo.Font = AppTheme.FontBody;
        _cmbTipo.Items.AddRange(["Guía de Despacho", "Factura Electrónica", "Orden de Compra", "Contrato Comercial", "Certificado de Pesaje", "Acta de Recepción"]);
        _cmbTipo.SelectedIndex = 0;
        _dtpFecha.Font = AppTheme.FontBody;

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(body, 0, "Tipo Documento *:", _cmbTipo);
        AddRow(body, 1, "N° Documento *:", _txtNumero);
        AddRow(body, 2, "Fecha Documento:", _dtpFecha);
        AddRow(body, 3, "Observaciones:", _txtObservaciones, 72);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Documento", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnSave);

        CancelButton = btnCancel;
        Controls.Add(body);
        Controls.Add(footer);
    }

    private static void AddRow(TableLayoutPanel t, int row, string label, Control c, int height = 36)
    {
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        t.Controls.Add(new Label
        {
            Text = label,
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        c.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(c, 1, row);
    }

    private void OnSave()
    {
        if (string.IsNullOrWhiteSpace(_txtNumero.Text))
        {
            MessageBox.Show(this, "Debe ingresar el número de documento.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNumero.Focus();
            return;
        }

        ResultDocumento = new DocumentoAsociado
        {
            RutEntidad = _rutEntidad,
            OperacionId = _operacionId,
            PesajeId = _pesajeId,
            TipoDocumento = string.IsNullOrWhiteSpace(_cmbTipo.Text) ? "Guía de Despacho" : _cmbTipo.Text.Trim(),
            NumeroDocumento = _txtNumero.Text.Trim(),
            FechaDocumento = DateOnly.FromDateTime(_dtpFecha.Value),
            Observaciones = _txtObservaciones.Text.Trim()
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
