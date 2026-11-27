using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo de configuración administrable de tipos de pesaje (flujo 1 o 2 pasos e impacto en inventario).
/// </summary>
public sealed class TipoPesajeConfigDialog : Form
{
    private readonly long _existingId;
    private readonly TextBox _txtCodigo = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtNombre = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbTipoBase = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkDosMediciones = new() { Text = "Requiere 2 mediciones (Entrada y Salida)", AutoSize = true, Font = AppTheme.FontBodyBold };
    private readonly CheckBox _chkAfectaInv = new() { Text = "Afecta Inventario al confirmarse", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly ComboBox _cmbDir = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numOrden = new() { Dock = DockStyle.Fill, Minimum = 1, Maximum = 999, Value = 10 };
    private readonly CheckBox _chkActivo = new() { Text = "Tipo Activo", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };

    private sealed record TipoBaseOpt(TipoPesaje Tipo, string Label) { public override string ToString() => Label; }
    private sealed record DirOpt(int Dir, string Label) { public override string ToString() => Label; }

    public TipoPesajeConfig? ResultConfig { get; private set; }

    public TipoPesajeConfigDialog(TipoPesajeConfig? existing = null)
    {
        _existingId = existing?.Id ?? 0;
        Text = existing is null ? "Nuevo Tipo de Pesaje" : $"Editar Tipo de Pesaje · {existing.Codigo}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(540, 390);
        BackColor = AppTheme.CanvasLight;

        AppTheme.ApplyInputStyle(_txtCodigo);
        AppTheme.ApplyInputStyle(_txtNombre);

        _cmbTipoBase.Font = AppTheme.FontBody;
        foreach (TipoPesaje tp in Enum.GetValues<TipoPesaje>())
        {
            _cmbTipoBase.Items.Add(new TipoBaseOpt(tp, tp.ToDisplayName()));
        }

        _cmbTipoBase.SelectedIndex = 0;

        _cmbDir.Font = AppTheme.FontBody;
        _cmbDir.Items.AddRange(
        [
            new DirOpt(1, "Ingreso a Inventario (+)"),
            new DirOpt(-1, "Egreso de Inventario (-)"),
            new DirOpt(0, "Neutro (No mueve stock)")
        ]);
        _cmbDir.SelectedIndex = 0;

        _chkAfectaInv.CheckedChanged += (_, _) => _cmbDir.Enabled = _chkAfectaInv.Checked;

        if (existing is not null)
        {
            _txtCodigo.Text = existing.Codigo;
            _txtNombre.Text = existing.Nombre;
            for (int i = 0; i < _cmbTipoBase.Items.Count; i++)
            {
                if (_cmbTipoBase.Items[i] is TipoBaseOpt o && o.Tipo == existing.TipoBase)
                {
                    _cmbTipoBase.SelectedIndex = i;
                    break;
                }
            }

            _chkDosMediciones.Checked = existing.RequiereDosMediciones;
            _chkAfectaInv.Checked = existing.AfectaInventario;
            _cmbDir.Enabled = existing.AfectaInventario;
            _cmbDir.SelectedIndex = existing.DireccionInventario > 0 ? 0 : existing.DireccionInventario < 0 ? 1 : 2;
            _numOrden.Value = Math.Clamp(existing.Orden, 1, 999);
            _chkActivo.Checked = existing.Activo;
        }

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(body, 0, "Código *:", _txtCodigo);
        AddRow(body, 1, "Nombre *:", _txtNombre);
        AddRow(body, 2, "Categoría Base:", _cmbTipoBase);
        AddRow(body, 3, "Medición:", _chkDosMediciones);
        AddRow(body, 4, "Inventario:", _chkAfectaInv);
        AddRow(body, 5, "Sentido Stock:", _cmbDir);
        AddRow(body, 6, "Orden:", _numOrden);
        AddRow(body, 7, "Estado:", _chkActivo);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Tipo", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnSave);

        CancelButton = btnCancel;
        Controls.Add(body);
        Controls.Add(footer);
    }

    private static void AddRow(TableLayoutPanel t, int row, string label, Control c)
    {
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
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
        if (string.IsNullOrWhiteSpace(_txtCodigo.Text) || string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show(this, "El código y nombre del tipo de pesaje son obligatorios.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int dir = _chkAfectaInv.Checked ? ((_cmbDir.SelectedItem as DirOpt)?.Dir ?? 0) : 0;
        ResultConfig = new TipoPesajeConfig
        {
            Id = _existingId,
            Codigo = _txtCodigo.Text.Trim().ToUpperInvariant(),
            Nombre = _txtNombre.Text.Trim(),
            TipoBase = (_cmbTipoBase.SelectedItem as TipoBaseOpt)?.Tipo ?? TipoPesaje.RecepcionCarga,
            RequiereDosMediciones = _chkDosMediciones.Checked,
            AfectaInventario = _chkAfectaInv.Checked && dir != 0,
            DireccionInventario = dir,
            Orden = (int)_numOrden.Value,
            Activo = _chkActivo.Checked
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
