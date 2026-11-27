using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo CRM unificado para crear o editar Clientes, Proveedores o Ambos por RUT sin duplicación.
/// </summary>
public sealed class EntidadComercialDialog : Form
{
    private readonly TextBox _txtRut = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtRazonSocial = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtNombreComercial = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _chkCliente = new() { Text = "Es Cliente", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly CheckBox _chkProveedor = new() { Text = "Es Proveedor", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly CheckBox _chkActivo = new() { Text = "Entidad Activa", AutoSize = true, Checked = true, Font = AppTheme.FontBodyBold };
    private readonly TextBox _txtGiro = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDireccion = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtComuna = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtCiudad = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtContacto = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtTelefono = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtEmail = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtCondicion = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbPlanta = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtObservaciones = new() { Dock = DockStyle.Fill, Multiline = true, Height = 58, ScrollBars = ScrollBars.Vertical };

    private readonly bool _isEdit;

    public Cliente? ResultEntity { get; private set; }

    public EntidadComercialDialog(Cliente? existing = null)
    {
        _isEdit = existing is not null;

        Text = _isEdit ? $"Editar Entidad CRM · {existing!.RutFormateado}" : "Nueva Entidad Comercial CRM (Cliente / Proveedor)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(690, 560);
        BackColor = AppTheme.CanvasLight;

        foreach (TextBox tb in new[] { _txtRut, _txtRazonSocial, _txtNombreComercial, _txtGiro, _txtDireccion, _txtComuna, _txtCiudad, _txtContacto, _txtTelefono, _txtEmail, _txtCondicion, _txtObservaciones })
        {
            AppTheme.ApplyInputStyle(tb);
        }

        _txtRut.PlaceholderText = "Ej: 76218764-5";
        _txtCondicion.PlaceholderText = "Ej: Pago 30 días / Retiro planta";
        UiHelpers.PopulatePlantaFilterCombo(_cmbPlanta);

        if (existing is not null)
        {
            _txtRut.Text = existing.RutFormateado;
            _txtRut.ReadOnly = true;
            _txtRazonSocial.Text = existing.RazonSocial;
            _txtNombreComercial.Text = existing.NombreComercial;
            _chkCliente.Checked = existing.EsCliente;
            _chkProveedor.Checked = existing.EsProveedor;
            _chkActivo.Checked = existing.Activo;
            _txtGiro.Text = existing.Giro;
            _txtDireccion.Text = existing.Direccion;
            _txtComuna.Text = existing.Comuna;
            _txtCiudad.Text = existing.Ciudad;
            _txtContacto.Text = existing.ContactoNombre;
            _txtTelefono.Text = existing.Telefono;
            _txtEmail.Text = existing.Email;
            _txtCondicion.Text = existing.CondicionComercial;
            _txtObservaciones.Text = existing.Observaciones;

            if (existing.PlantaAsociada.HasValue)
            {
                for (int i = 0; i < _cmbPlanta.Items.Count; i++)
                {
                    if (_cmbPlanta.Items[i]?.ToString()?.Contains(existing.PlantaAsociada.Value.ToString(), StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _cmbPlanta.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        TableLayoutPanel form = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 9,
            Padding = new Padding(16, 14, 16, 10),
            AutoScroll = true
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        FlowLayoutPanel roleRow = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _chkCliente.Margin = new Padding(0, 4, 14, 0);
        _chkProveedor.Margin = new Padding(0, 4, 14, 0);
        _chkActivo.Margin = new Padding(0, 4, 0, 0);
        roleRow.Controls.AddRange([_chkCliente, _chkProveedor, _chkActivo]);

        AddRow(form, 0, "RUT *:", _txtRut, "Planta Asoc.:", _cmbPlanta);
        AddSpanRow(form, 1, "Roles / Estado:", roleRow);
        AddSpanRow(form, 2, "Razón Social *:", _txtRazonSocial);
        AddRow(form, 3, "Nombre Com.:", _txtNombreComercial, "Giro:", _txtGiro);
        AddSpanRow(form, 4, "Dirección:", _txtDireccion);
        AddRow(form, 5, "Comuna:", _txtComuna, "Ciudad:", _txtCiudad);
        AddRow(form, 6, "Contacto:", _txtContacto, "Teléfono:", _txtTelefono);
        AddRow(form, 7, "Email:", _txtEmail, "Cond. Com.:", _txtCondicion);
        AddSpanRow(form, 8, "Observaciones:", _txtObservaciones, 64);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Entidad", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnSave);

        CancelButton = btnCancel;
        Controls.Add(form);
        Controls.Add(footer);
    }

    private static Label MkLabel(string text) => new()
    {
        Text = text,
        Font = AppTheme.FontBodyBold,
        ForeColor = AppTheme.TextPrimary,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void AddRow(TableLayoutPanel t, int row, string l1, Control c1, string l2, Control c2)
    {
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        t.Controls.Add(MkLabel(l1), 0, row);
        c1.Margin = new Padding(0, 4, 12, 4);
        t.Controls.Add(c1, 1, row);
        t.Controls.Add(MkLabel(l2), 2, row);
        c2.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(c2, 3, row);
    }

    private static void AddSpanRow(TableLayoutPanel t, int row, string label, Control c, int height = 36)
    {
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        t.Controls.Add(MkLabel(label), 0, row);
        c.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(c, 1, row);
        t.SetColumnSpan(c, 3);
    }

    private void OnSave()
    {
        if (!_chkCliente.Checked && !_chkProveedor.Checked)
        {
            MessageBox.Show(this, "Debe marcar al menos un rol: Cliente, Proveedor o Ambos.", "Validación CRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        (bool valid, string body, string dv, _, string msg) = RutChileno.ValidateAndFormat(_txtRut.Text);
        if (!valid)
        {
            MessageBox.Show(this, msg, "RUT Inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtRut.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtRazonSocial.Text))
        {
            MessageBox.Show(this, "La Razón Social es obligatoria.", "Validación CRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtRazonSocial.Focus();
            return;
        }

        ResultEntity = new Cliente
        {
            Rut = body,
            Digito = dv,
            RutNormalizado = body,
            Nombre = string.IsNullOrWhiteSpace(_txtNombreComercial.Text) ? _txtRazonSocial.Text.Trim() : _txtNombreComercial.Text.Trim(),
            RazonSocial = _txtRazonSocial.Text.Trim(),
            NombreComercial = _txtNombreComercial.Text.Trim(),
            Giro = _txtGiro.Text.Trim(),
            Direccion = _txtDireccion.Text.Trim(),
            Comuna = _txtComuna.Text.Trim(),
            Ciudad = _txtCiudad.Text.Trim(),
            ContactoNombre = _txtContacto.Text.Trim(),
            Telefono = _txtTelefono.Text.Trim(),
            Email = _txtEmail.Text.Trim(),
            CondicionComercial = _txtCondicion.Text.Trim(),
            Observaciones = _txtObservaciones.Text.Trim(),
            EsCliente = _chkCliente.Checked,
            EsProveedor = _chkProveedor.Checked,
            Activo = _chkActivo.Checked,
            PlantaAsociada = UiHelpers.GetSelectedNullablePlanta(_cmbPlanta)
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
