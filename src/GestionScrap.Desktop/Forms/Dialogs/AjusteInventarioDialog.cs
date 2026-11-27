using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo auditable para registrar ajustes autorizados de inventario (+ o -) sobre el libro mayor.
/// </summary>
public sealed class AjusteInventarioDialog : Form
{
    private readonly ComboBox _cmbMaterial = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbSigno = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numCantidad = new() { Dock = DockStyle.Fill, Minimum = 1, Maximum = 5000000, Increment = 100, ThousandsSeparator = true, Value = 100 };
    private readonly TextBox _txtDocumento = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtMotivo = new() { Dock = DockStyle.Fill, Multiline = true, Height = 72, ScrollBars = ScrollBars.Vertical };

    private sealed record MatOpt(Producto Mat) { public override string ToString() => $"{Mat.Codigo} · {Mat.NombreVisible} [{Mat.UnidadMedida}]"; }

    public string SelectedCodProducto { get; private set; } = string.Empty;
    public long SignedCantidadKg { get; private set; }
    public string DocumentoReferencia { get; private set; } = string.Empty;
    public string Motivo { get; private set; } = string.Empty;

    public AjusteInventarioDialog(IReadOnlyList<Producto> materiales, string? presetCodProducto = null)
    {
        Text = "Ajuste Autorizado de Inventario (Libro Mayor)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(540, 340);
        BackColor = AppTheme.CanvasLight;

        AppTheme.ApplyInputStyle(_txtDocumento);
        AppTheme.ApplyInputStyle(_txtMotivo);
        _txtDocumento.PlaceholderText = "Ej: ACTA-INV-2026-01";
        _txtMotivo.PlaceholderText = "Justificación obligatoria del ajuste (toma de inventario, merma verificada, etc.)";

        _cmbMaterial.Font = AppTheme.FontBody;
        foreach (Producto m in materiales.OrderBy(p => p.Codigo, StringComparer.OrdinalIgnoreCase))
        {
            _cmbMaterial.Items.Add(new MatOpt(m));
        }

        if (_cmbMaterial.Items.Count > 0)
        {
            int idx = string.IsNullOrWhiteSpace(presetCodProducto)
                ? 0
                : _cmbMaterial.Items.Cast<MatOpt>().ToList().FindIndex(o => string.Equals(o.Mat.Codigo, presetCodProducto, StringComparison.OrdinalIgnoreCase));
            _cmbMaterial.SelectedIndex = idx >= 0 ? idx : 0;
        }

        _cmbSigno.Font = AppTheme.FontBodyBold;
        _cmbSigno.Items.AddRange(["Ajuste Positivo (+) · Aumentar Stock", "Ajuste Negativo (-) · Disminuir Stock"]);
        _cmbSigno.SelectedIndex = 0;

        _numCantidad.Font = AppTheme.FontBodyBold;

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(body, 0, "Material *:", _cmbMaterial);
        AddRow(body, 1, "Tipo de Ajuste *:", _cmbSigno);
        AddRow(body, 2, "Cantidad *:", _numCantidad);
        AddRow(body, 3, "Documento Ref.:", _txtDocumento);
        AddRow(body, 4, "Motivo Auditable *:", _txtMotivo, 78);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Registrar Ajuste", IconKind.Check, ButtonVariant.Warning, (_, _) => OnSave(), null, null, 0, 34);
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
        if (_cmbMaterial.SelectedItem is not MatOpt matOpt)
        {
            MessageBox.Show(this, "Debe seleccionar un material.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtMotivo.Text))
        {
            MessageBox.Show(this, "El motivo del ajuste es obligatorio para la trazabilidad de auditoría.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtMotivo.Focus();
            return;
        }

        SelectedCodProducto = matOpt.Mat.Codigo;
        long qty = (long)_numCantidad.Value;
        SignedCantidadKg = _cmbSigno.SelectedIndex == 0 ? qty : -qty;
        DocumentoReferencia = _txtDocumento.Text.Trim();
        Motivo = _txtMotivo.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
