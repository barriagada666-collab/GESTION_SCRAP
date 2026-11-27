using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo CRUD completo para materiales del catálogo maestro (código único, grupo, unidad, tolerancia y estado).
/// </summary>
public sealed class MaterialEditorDialog : Form
{
    private readonly TextBox _txtCodigo = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtNombre = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDescripcion = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbGrupo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _cmbUnidad = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numTolerancia = new() { Dock = DockStyle.Fill, DecimalPlaces = 2, Minimum = 0, Maximum = 100, Increment = 0.5M, Value = 5.0M };
    private readonly CheckBox _chkActivo = new() { Text = "Material Activo para Pesajes y Órdenes", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly TextBox _txtInfo = new() { Dock = DockStyle.Fill, Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };

    public Producto? ResultMaterial { get; private set; }

    public MaterialEditorDialog(IEnumerable<string> availableGroups, Producto? existing = null)
    {
        Text = existing is null ? "Nuevo Material Maestro" : $"Editar Material · {existing.Codigo}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 420);
        BackColor = AppTheme.CanvasLight;

        foreach (TextBox tb in new[] { _txtCodigo, _txtNombre, _txtDescripcion, _txtInfo })
        {
            AppTheme.ApplyInputStyle(tb);
        }

        _cmbGrupo.Font = AppTheme.FontBody;
        foreach (string g in availableGroups.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            _cmbGrupo.Items.Add(g);
        }

        if (_cmbGrupo.Items.Count == 0)
        {
            _cmbGrupo.Items.Add("GENERAL");
        }

        _cmbUnidad.Font = AppTheme.FontBody;
        _cmbUnidad.Items.AddRange(["KG", "TON"]);
        _cmbUnidad.SelectedIndex = 0;

        if (existing is not null)
        {
            _txtCodigo.Text = existing.Codigo;
            _txtCodigo.ReadOnly = true;
            _txtNombre.Text = existing.NombreVisible;
            _txtDescripcion.Text = existing.Descripcion;
            _cmbGrupo.Text = existing.Grupo;
            int uIdx = _cmbUnidad.Items.IndexOf(existing.UnidadMedida.ToUpperInvariant());
            _cmbUnidad.SelectedIndex = uIdx >= 0 ? uIdx : 0;
            _numTolerancia.Value = (decimal)Math.Clamp(existing.ToleranciaDiferenciaPct, 0.0, 100.0);
            _chkActivo.Checked = existing.Activo;
            _txtInfo.Text = existing.InformacionAdicional;
        }
        else
        {
            _cmbGrupo.SelectedIndex = 0;
        }

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(body, 0, "Código Único *:", _txtCodigo);
        AddRow(body, 1, "Nombre Material *:", _txtNombre);
        AddRow(body, 2, "Descripción:", _txtDescripcion);
        AddRow(body, 3, "Grupo de Material:", _cmbGrupo);
        AddRow(body, 4, "Unidad de Medida:", _cmbUnidad);
        AddRow(body, 5, "Tolerancia Dif. (%):", _numTolerancia);
        AddRow(body, 6, "Estado:", _chkActivo);
        AddRow(body, 7, "Info. Adicional:", _txtInfo, 70);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Material", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
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
        if (string.IsNullOrWhiteSpace(_txtCodigo.Text))
        {
            MessageBox.Show(this, "El código único del material es obligatorio.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCodigo.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show(this, "El nombre del material es obligatorio.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNombre.Focus();
            return;
        }

        ResultMaterial = new Producto
        {
            Codigo = _txtCodigo.Text.Trim().ToUpperInvariant(),
            Nombre = _txtNombre.Text.Trim().ToUpperInvariant(),
            Descripcion = string.IsNullOrWhiteSpace(_txtDescripcion.Text)
                ? _txtNombre.Text.Trim().ToUpperInvariant()
                : _txtDescripcion.Text.Trim().ToUpperInvariant(),
            Grupo = string.IsNullOrWhiteSpace(_cmbGrupo.Text) ? "GENERAL" : _cmbGrupo.Text.Trim().ToUpperInvariant(),
            UnidadMedida = _cmbUnidad.SelectedItem?.ToString() ?? "KG",
            ToleranciaDiferenciaPct = (double)_numTolerancia.Value,
            Activo = _chkActivo.Checked,
            InformacionAdicional = _txtInfo.Text.Trim()
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
