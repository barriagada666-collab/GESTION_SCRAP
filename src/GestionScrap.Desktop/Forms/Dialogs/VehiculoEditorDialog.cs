using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo CRUD de vehículos y patentes con normalización automática contra duplicados.
/// </summary>
public sealed class VehiculoEditorDialog : Form
{
    private readonly long _existingId;
    private readonly TextBox _txtPatente = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbTipo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _txtMarca = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtModelo = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _numCapacidad = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 150000, Increment = 500, ThousandsSeparator = true };
    private readonly ComboBox _cmbEntidad = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkActivo = new() { Text = "Vehículo Activo", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly TextBox _txtObservaciones = new() { Dock = DockStyle.Fill, Multiline = true, Height = 58, ScrollBars = ScrollBars.Vertical };

    private sealed record EntidadOpt(string Rut, string Nombre)
    {
        public override string ToString() => string.IsNullOrEmpty(Rut) ? "(Sin entidad asociada)" : $"{Nombre} ({Rut})";
    }

    public Vehiculo? ResultVehiculo { get; private set; }

    public VehiculoEditorDialog(IReadOnlyList<Cliente> entidades, Vehiculo? existing = null)
    {
        _existingId = existing?.Id ?? 0;
        Text = existing is null ? "Nuevo Vehículo / Patente" : $"Editar Vehículo · {existing.PatenteOriginal}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 420);
        BackColor = AppTheme.CanvasLight;

        foreach (TextBox tb in new[] { _txtPatente, _txtMarca, _txtModelo, _txtObservaciones })
        {
            AppTheme.ApplyInputStyle(tb);
        }

        _txtPatente.PlaceholderText = "Ej: AB-CD-12 o ABCD12";
        _cmbTipo.Font = AppTheme.FontBody;
        _cmbTipo.Items.AddRange(["Camión", "Tracto Camión", "Camión Tolva", "Rampla", "Camioneta", "Furgón"]);
        _cmbTipo.SelectedIndex = 0;

        _cmbEntidad.Font = AppTheme.FontBody;
        _cmbEntidad.Items.Add(new EntidadOpt(string.Empty, string.Empty));
        foreach (Cliente e in entidades.OrderBy(c => c.RazonSocial, StringComparer.OrdinalIgnoreCase))
        {
            _cmbEntidad.Items.Add(new EntidadOpt(e.RutFormateado, e.RazonSocial));
        }

        _cmbEntidad.SelectedIndex = 0;

        if (existing is not null)
        {
            _txtPatente.Text = existing.PatenteOriginal;
            _cmbTipo.Text = existing.TipoVehiculo;
            _txtMarca.Text = existing.Marca;
            _txtModelo.Text = existing.Modelo;
            _numCapacidad.Value = Math.Clamp(existing.CapacidadCargaKg, 0, 150000);
            _chkActivo.Checked = existing.Activo;
            _txtObservaciones.Text = existing.Observaciones;

            for (int i = 1; i < _cmbEntidad.Items.Count; i++)
            {
                if (_cmbEntidad.Items[i] is EntidadOpt eo && RutChileno.Matches(eo.Rut, existing.RutEntidadAsociada))
                {
                    _cmbEntidad.SelectedIndex = i;
                    break;
                }
            }
        }

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(body, 0, "Patente *:", _txtPatente);
        AddRow(body, 1, "Tipo de Vehículo:", _cmbTipo);
        AddRow(body, 2, "Marca:", _txtMarca);
        AddRow(body, 3, "Modelo:", _txtModelo);
        AddRow(body, 4, "Capacidad (Kg):", _numCapacidad);
        AddRow(body, 5, "Entidad Asociada:", _cmbEntidad);
        AddRow(body, 6, "Estado:", _chkActivo);
        AddRow(body, 7, "Observaciones:", _txtObservaciones, 64);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Vehículo", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
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
        if (!PatenteChilena.IsValidFormat(_txtPatente.Text))
        {
            MessageBox.Show(this, "Ingrese una patente válida (5 a 10 caracteres alfanuméricos).", "Patente Inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPatente.Focus();
            return;
        }

        EntidadOpt? ent = _cmbEntidad.SelectedItem as EntidadOpt;
        string norm = PatenteChilena.Normalize(_txtPatente.Text);

        ResultVehiculo = new Vehiculo
        {
            Id = _existingId,
            PatenteOriginal = PatenteChilena.FormatDisplay(norm),
            PatenteNormalizada = norm,
            TipoVehiculo = string.IsNullOrWhiteSpace(_cmbTipo.Text) ? "Camión" : _cmbTipo.Text.Trim(),
            Marca = _txtMarca.Text.Trim(),
            Modelo = _txtModelo.Text.Trim(),
            CapacidadCargaKg = (int)_numCapacidad.Value,
            RutEntidadAsociada = ent?.Rut ?? string.Empty,
            NombreEntidadAsociada = ent?.Nombre ?? string.Empty,
            Activo = _chkActivo.Checked,
            Observaciones = _txtObservaciones.Text.Trim()
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
