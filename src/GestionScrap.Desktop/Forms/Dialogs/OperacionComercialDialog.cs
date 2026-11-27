using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo ERP para crear o editar Órdenes de Compra, Ventas, Recepciones, Despachos o Traslados Internos.
/// </summary>
public sealed class OperacionComercialDialog : Form
{
    private readonly long _existingId;
    private readonly long _existingRealKg;
    private readonly DateTime _existingCreatedUtc;
    private readonly string _existingCreator;

    private readonly TextBox _txtCodigo = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbTipo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpFecha = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbProveedor = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbCliente = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbMaterial = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numEsperado = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 5000000, Increment = 500, ThousandsSeparator = true };
    private readonly ComboBox _cmbVehiculo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtDocumento = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbEstado = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtObservaciones = new() { Dock = DockStyle.Fill, Multiline = true, Height = 58, ScrollBars = ScrollBars.Vertical };

    private sealed record TipoOpt(TipoOperacionComercial Tipo, string Label) { public override string ToString() => Label; }
    private sealed record EstadoOpt(EstadoOperacion Estado, string Label) { public override string ToString() => Label; }
    private sealed record EntOpt(string Rut, string Nombre) { public override string ToString() => string.IsNullOrEmpty(Rut) ? "(Ninguno)" : $"{Nombre} ({Rut})"; }
    private sealed record MatOpt(Producto Mat) { public override string ToString() => $"{Mat.Codigo} · {Mat.NombreVisible} [{Mat.UnidadMedida}]"; }
    private sealed record VehOpt(string Patente, string Label) { public override string ToString() => Label; }

    public OperacionComercial? ResultOperacion { get; private set; }

    public OperacionComercialDialog(
        IReadOnlyList<Cliente> entidades,
        IReadOnlyList<Producto> materiales,
        IReadOnlyList<Vehiculo> vehiculos,
        OperacionComercial? existing = null)
    {
        _existingId = existing?.Id ?? 0;
        _existingRealKg = existing?.CantidadRealConfirmadaKg ?? 0;
        _existingCreatedUtc = existing?.CreadoEnUtc ?? DateTime.UtcNow;
        _existingCreator = existing?.CreadoPor ?? string.Empty;

        Text = existing is null ? "Nueva Operación / Orden ERP" : $"Editar Operación ERP · {existing.Codigo}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(680, 520);
        BackColor = AppTheme.CanvasLight;

        foreach (TextBox tb in new[] { _txtCodigo, _txtDocumento, _txtObservaciones })
        {
            AppTheme.ApplyInputStyle(tb);
        }

        _txtCodigo.PlaceholderText = "Autogenerado si se deja vacío (ej: OC-2026-001)";
        _txtDocumento.PlaceholderText = "Guía, Factura, OC o Folio Ref.";

        _cmbTipo.Font = AppTheme.FontBody;
        foreach (TipoOperacionComercial tp in Enum.GetValues<TipoOperacionComercial>())
        {
            _cmbTipo.Items.Add(new TipoOpt(tp, tp.ToDisplayName()));
        }

        _cmbTipo.SelectedIndex = 0;

        _cmbEstado.Font = AppTheme.FontBody;
        foreach (EstadoOperacion es in Enum.GetValues<EstadoOperacion>())
        {
            _cmbEstado.Items.Add(new EstadoOpt(es, es.ToDisplayName()));
        }

        _cmbEstado.SelectedIndex = 0;

        _cmbProveedor.Font = AppTheme.FontBody;
        _cmbProveedor.Items.Add(new EntOpt(string.Empty, string.Empty));
        foreach (Cliente p in entidades.Where(e => e.EsProveedor).OrderBy(e => e.RazonSocial, StringComparer.OrdinalIgnoreCase))
        {
            _cmbProveedor.Items.Add(new EntOpt(p.RutFormateado, p.RazonSocial));
        }

        _cmbProveedor.SelectedIndex = 0;

        _cmbCliente.Font = AppTheme.FontBody;
        _cmbCliente.Items.Add(new EntOpt(string.Empty, string.Empty));
        foreach (Cliente c in entidades.Where(e => e.EsCliente).OrderBy(e => e.RazonSocial, StringComparer.OrdinalIgnoreCase))
        {
            _cmbCliente.Items.Add(new EntOpt(c.RutFormateado, c.RazonSocial));
        }

        _cmbCliente.SelectedIndex = 0;

        _cmbMaterial.Font = AppTheme.FontBody;
        foreach (Producto m in materiales.Where(m => m.Activo || (existing is not null && string.Equals(m.Codigo, existing.CodProducto, StringComparison.OrdinalIgnoreCase))))
        {
            _cmbMaterial.Items.Add(new MatOpt(m));
        }

        if (_cmbMaterial.Items.Count > 0) _cmbMaterial.SelectedIndex = 0;

        _cmbVehiculo.Font = AppTheme.FontBody;
        _cmbVehiculo.Items.Add(new VehOpt(string.Empty, "(Sin vehículo asignado)"));
        foreach (Vehiculo v in vehiculos.Where(v => v.Activo))
        {
            _cmbVehiculo.Items.Add(new VehOpt(v.PatenteOriginal, $"{v.PatenteOriginal} · {v.TipoVehiculo}"));
        }

        _cmbVehiculo.SelectedIndex = 0;

        _dtpFecha.Font = AppTheme.FontBody;
        _numEsperado.Font = AppTheme.FontBodyBold;

        if (existing is not null)
        {
            _txtCodigo.Text = existing.Codigo;
            _dtpFecha.Value = existing.Fecha.ToDateTime(TimeOnly.MinValue);
            _numEsperado.Value = Math.Clamp(existing.CantidadEsperadaKg, 0, 5000000);
            _txtDocumento.Text = existing.DocumentoAsociado;
            _txtObservaciones.Text = existing.Observaciones;
            SelectMatch(_cmbTipo, i => i is TipoOpt o && o.Tipo == existing.TipoOperacion);
            SelectMatch(_cmbEstado, i => i is EstadoOpt o && o.Estado == existing.Estado);
            SelectMatch(_cmbProveedor, i => i is EntOpt o && !string.IsNullOrEmpty(o.Rut) && RutChileno.Matches(o.Rut, existing.RutProveedor));
            SelectMatch(_cmbCliente, i => i is EntOpt o && !string.IsNullOrEmpty(o.Rut) && RutChileno.Matches(o.Rut, existing.RutCliente));
            SelectMatch(_cmbMaterial, i => i is MatOpt o && string.Equals(o.Mat.Codigo, existing.CodProducto, StringComparison.OrdinalIgnoreCase));
            SelectMatch(_cmbVehiculo, i => i is VehOpt o && !string.IsNullOrEmpty(o.Patente) && PatenteChilena.MatchesExact(o.Patente, existing.Patente));
        }

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 8,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddRow(body, 0, "Código Orden:", _txtCodigo, "Fecha:", _dtpFecha);
        AddRow(body, 1, "Tipo Operación *:", _cmbTipo, "Estado:", _cmbEstado);
        AddSpanRow(body, 2, "Proveedor:", _cmbProveedor);
        AddSpanRow(body, 3, "Cliente:", _cmbCliente);
        AddSpanRow(body, 4, "Material *:", _cmbMaterial);
        AddRow(body, 5, "Cant. Esperada:", _numEsperado, "Documento Ref.:", _txtDocumento);
        AddSpanRow(body, 6, "Vehículo Asig.:", _cmbVehiculo);
        AddSpanRow(body, 7, "Observaciones:", _txtObservaciones, 64);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnSave = AppTheme.CreateButton("Guardar Operación", IconKind.Save, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnSave);

        CancelButton = btnCancel;
        Controls.Add(body);
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
        c1.Margin = new Padding(0, 4, 10, 4);
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

    private static void SelectMatch(ComboBox cmb, Func<object?, bool> predicate)
    {
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            if (predicate(cmb.Items[i]))
            {
                cmb.SelectedIndex = i;
                return;
            }
        }
    }

    private void OnSave()
    {
        if (_cmbMaterial.SelectedItem is not MatOpt matOpt)
        {
            MessageBox.Show(this, "Debe seleccionar un material.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EntOpt? prov = _cmbProveedor.SelectedItem as EntOpt;
        EntOpt? cli = _cmbCliente.SelectedItem as EntOpt;
        VehOpt? veh = _cmbVehiculo.SelectedItem as VehOpt;

        ResultOperacion = new OperacionComercial
        {
            Id = _existingId,
            Codigo = _txtCodigo.Text.Trim(),
            TipoOperacion = (_cmbTipo.SelectedItem as TipoOpt)?.Tipo ?? TipoOperacionComercial.OrdenCompra,
            Fecha = DateOnly.FromDateTime(_dtpFecha.Value),
            RutProveedor = prov?.Rut ?? string.Empty,
            NombreProveedor = prov?.Nombre ?? string.Empty,
            RutCliente = cli?.Rut ?? string.Empty,
            NombreCliente = cli?.Nombre ?? string.Empty,
            CodProducto = matOpt.Mat.Codigo,
            NombreProducto = matOpt.Mat.NombreVisible,
            CantidadEsperadaKg = (long)_numEsperado.Value,
            CantidadRealConfirmadaKg = _existingRealKg,
            UnidadMedida = matOpt.Mat.UnidadMedida,
            Patente = veh?.Patente ?? string.Empty,
            DocumentoAsociado = _txtDocumento.Text.Trim(),
            Estado = (_cmbEstado.SelectedItem as EstadoOpt)?.Estado ?? EstadoOperacion.Abierta,
            Observaciones = _txtObservaciones.Text.Trim(),
            CreadoPor = _existingCreator,
            CreadoEnUtc = _existingCreatedUtc
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
