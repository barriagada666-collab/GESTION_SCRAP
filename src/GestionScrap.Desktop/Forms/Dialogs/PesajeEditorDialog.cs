using System.Globalization;
using GestionScrap.Application.DTOs;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.ValueObjects;

namespace GestionScrap.Desktop.Forms.Dialogs;

public enum PesajeDialogMode
{
    SingleStepCreate = 1,
    TwoStepStart = 2,
    TwoStepComplete = 3,
    AuthorizedCorrection = 4
}

/// <summary>
/// Diálogo integral de pesajes industriales: soporta registro completo (1 paso), inicio de 1ª medición,
/// cierre de 2ª medición (conservando fecha/hora y valor de 1ª medición) y corrección autorizada con auditoría.
/// </summary>
public sealed class PesajeEditorDialog : Form
{
    private readonly PesajeDialogMode _mode;
    private readonly Pesaje? _existing;

    private readonly ComboBox _cmbPlanta = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbTipoPesaje = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtFolioCodigo = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbVehiculo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbMaterial = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbEntidad = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbOperacion = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpFecha = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _dtpHora = new() { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Time, ShowUpDown = true };
    private readonly NumericUpDown _numBruto = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 500000, Increment = 50, ThousandsSeparator = true };
    private readonly NumericUpDown _numTara = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 500000, Increment = 50, ThousandsSeparator = true };
    private readonly NumericUpDown _numEsperado = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 500000, Increment = 50, ThousandsSeparator = true };
    private readonly Label _lblNetoCalc = new() { Dock = DockStyle.Fill, Font = AppTheme.FontKpiValue, ForeColor = AppTheme.AccentEmerald, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TextBox _txtBascula = new() { Dock = DockStyle.Fill, Text = "Báscula Principal" };
    private readonly TextBox _txtGuia = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtChofer = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtOrigen = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDestino = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtObservacion = new() { Dock = DockStyle.Fill, Multiline = true, Height = 52, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _txtMotivo = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _chkExcepcionTol = new() { Text = "Autorizar diferencia sobre tolerancia", AutoSize = true, Font = AppTheme.FontSmallBold };

    private sealed record TipoCfgOpt(TipoPesajeConfig Cfg) { public override string ToString() => $"{Cfg.Nombre} ({Cfg.ModoMedicion})"; }
    private sealed record VehOpt(Vehiculo Veh) { public override string ToString() => $"{Veh.PatenteOriginal} · {Veh.TipoVehiculo} ({Veh.NombreEntidadAsociada})"; }
    private sealed record MatOpt(Producto Mat) { public override string ToString() => $"{Mat.Codigo} · {Mat.NombreVisible} [{Mat.UnidadMedida}]"; }
    private sealed record EntOpt(string Rut, string Nombre) { public override string ToString() => string.IsNullOrEmpty(Rut) ? "(Sin entidad CRM)" : $"{Nombre} ({Rut})"; }
    private sealed record OpOpt(long? Id, string Label) { public override string ToString() => Label; }

    public PesajeRegistroRequestDto? ResultRequest { get; private set; }
    public int SegundaMedicionKg { get; private set; }
    public DateOnly FechaSalida { get; private set; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly HoraSalida { get; private set; } = TimeOnly.FromDateTime(DateTime.Now);
    public string MotivoCambio { get; private set; } = string.Empty;
    public bool AutorizacionExcepcion => _chkExcepcionTol.Checked;
    public string ObservacionTexto => _txtObservacion.Text.Trim();

    public PesajeEditorDialog(
        PesajeDialogMode mode,
        IReadOnlyList<TipoPesajeConfig> tiposConfig,
        IReadOnlyList<Vehiculo> vehiculos,
        IReadOnlyList<Producto> materiales,
        IReadOnlyList<Cliente> entidades,
        IReadOnlyList<OperacionComercial> operaciones,
        bool canAuthorize,
        Pesaje? existing = null,
        long? presetOperacionId = null)
    {
        _mode = mode;
        _existing = existing;

        Text = mode switch
        {
            PesajeDialogMode.TwoStepStart => "Iniciar Pesaje (1ª Medición - Entrada)",
            PesajeDialogMode.TwoStepComplete => $"Completar 2ª Medición · Folio {existing?.FolioDisplay}",
            PesajeDialogMode.AuthorizedCorrection => $"Corrección Autorizada de Pesaje · Folio {existing?.FolioDisplay}",
            _ => "Nuevo Pesaje Industrial (Medición Completa)"
        };

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(790, 625);
        BackColor = AppTheme.CanvasLight;

        foreach (TextBox tb in new[] { _txtFolioCodigo, _txtBascula, _txtGuia, _txtChofer, _txtOrigen, _txtDestino, _txtObservacion, _txtMotivo })
        {
            AppTheme.ApplyInputStyle(tb);
        }

        _txtFolioCodigo.PlaceholderText = "Autogenerado si se deja vacío";
        _txtMotivo.PlaceholderText = "Motivo auditable obligatorio para correcciones o excepciones";
        UiHelpers.PopulatePlantaCombo(_cmbPlanta);

        _cmbTipoPesaje.Font = AppTheme.FontBody;
        foreach (TipoPesajeConfig cfg in tiposConfig.Where(t => t.Activo || (existing?.TipoPesajeConfigId == t.Id)))
        {
            _cmbTipoPesaje.Items.Add(new TipoCfgOpt(cfg));
        }

        if (_cmbTipoPesaje.Items.Count > 0)
        {
            int preferred = mode == PesajeDialogMode.TwoStepStart
                ? _cmbTipoPesaje.Items.Cast<TipoCfgOpt>().ToList().FindIndex(x => x.Cfg.RequiereDosMediciones)
                : 0;
            _cmbTipoPesaje.SelectedIndex = preferred >= 0 ? preferred : 0;
        }

        _cmbVehiculo.Font = AppTheme.FontBody;
        foreach (Vehiculo v in vehiculos.Where(v => v.Activo || (existing is not null && PatenteChilena.MatchesExact(v.PatenteNormalizada, existing.Patente))))
        {
            _cmbVehiculo.Items.Add(new VehOpt(v));
        }

        if (_cmbVehiculo.Items.Count > 0) _cmbVehiculo.SelectedIndex = 0;

        _cmbMaterial.Font = AppTheme.FontBody;
        foreach (Producto m in materiales.Where(m => m.Activo || (existing is not null && string.Equals(m.Codigo, existing.CodProducto, StringComparison.OrdinalIgnoreCase))))
        {
            _cmbMaterial.Items.Add(new MatOpt(m));
        }

        if (_cmbMaterial.Items.Count > 0) _cmbMaterial.SelectedIndex = 0;

        _cmbEntidad.Font = AppTheme.FontBody;
        _cmbEntidad.Items.Add(new EntOpt(string.Empty, string.Empty));
        foreach (Cliente e in entidades.OrderBy(c => c.RazonSocial, StringComparer.OrdinalIgnoreCase))
        {
            _cmbEntidad.Items.Add(new EntOpt(e.RutFormateado, e.RazonSocial));
        }

        _cmbEntidad.SelectedIndex = 0;

        _cmbOperacion.Font = AppTheme.FontBody;
        _cmbOperacion.Items.Add(new OpOpt(null, "(Sin operación ERP vinculada)"));
        foreach (OperacionComercial op in operaciones.Where(o => o.Estado != EstadoOperacion.Anulada || o.Id == existing?.OperacionId))
        {
            _cmbOperacion.Items.Add(new OpOpt(op.Id, $"{op.Codigo} · {op.TipoDescripcion} ({op.CodProducto})"));
        }

        _cmbOperacion.SelectedIndex = 0;

        _dtpFecha.Font = AppTheme.FontBody;
        _dtpHora.Font = AppTheme.FontBody;
        _numBruto.Font = AppTheme.FontBodyBold;
        _numTara.Font = AppTheme.FontBodyBold;
        _numEsperado.Font = AppTheme.FontBody;
        _chkExcepcionTol.Enabled = canAuthorize;

        _numBruto.ValueChanged += (_, _) => UpdateNetPreview();
        _numTara.ValueChanged += (_, _) => UpdateNetPreview();

        if (existing is not null)
        {
            UiHelpers.SelectPlanta(_cmbPlanta, existing.Planta);
            _txtFolioCodigo.Text = existing.FolioDisplay;
            _txtFolioCodigo.ReadOnly = true;
            _dtpFecha.Value = mode == PesajeDialogMode.TwoStepComplete ? DateTime.Today : existing.FechaIngreso.ToDateTime(TimeOnly.MinValue);
            _dtpHora.Value = mode == PesajeDialogMode.TwoStepComplete ? DateTime.Now : DateTime.Today.Add(existing.HoraIngreso.ToTimeSpan());
            _txtBascula.Text = existing.Bascula;
            _txtGuia.Text = existing.GuiaDespacho;
            _txtChofer.Text = existing.Chofer;
            _txtOrigen.Text = existing.Origen;
            _txtDestino.Text = existing.Destino;
            _txtObservacion.Text = existing.Observacion;
            _numEsperado.Value = Math.Clamp(existing.PesoEsperadoKg ?? 0, 0, 500000);

            if (mode == PesajeDialogMode.TwoStepComplete)
            {
                _numBruto.Value = Math.Clamp(existing.PrimeraMedicionKg ?? existing.PesoBruto, 0, 500000);
                _numBruto.Enabled = false;
                _numTara.Value = 0;
            }
            else
            {
                _numBruto.Value = Math.Clamp(existing.PesoBruto, 0, 500000);
                _numTara.Value = Math.Clamp(existing.PesoTara, 0, 500000);
            }

            SelectComboMatch(_cmbTipoPesaje, i => i is TipoCfgOpt o && (o.Cfg.Id == existing.TipoPesajeConfigId || o.Cfg.TipoBase == existing.TipoPesaje));
            SelectComboMatch(_cmbVehiculo, i => i is VehOpt o && PatenteChilena.MatchesExact(o.Veh.PatenteNormalizada, existing.Patente));
            SelectComboMatch(_cmbMaterial, i => i is MatOpt o && string.Equals(o.Mat.Codigo, existing.CodProducto, StringComparison.OrdinalIgnoreCase));
            SelectComboMatch(_cmbEntidad, i => i is EntOpt o && !string.IsNullOrEmpty(o.Rut) && RutChileno.Matches(o.Rut, existing.RutCliente));
            SelectComboMatch(_cmbOperacion, i => i is OpOpt o && o.Id == existing.OperacionId);
        }
        else if (presetOperacionId.HasValue)
        {
            SelectComboMatch(_cmbOperacion, i => i is OpOpt o && o.Id == presetOperacionId.Value);
            OperacionComercial? op = operaciones.FirstOrDefault(o => o.Id == presetOperacionId.Value);
            if (op is not null)
            {
                _numEsperado.Value = Math.Clamp(op.CantidadEsperadaKg, 0, 500000);
                _txtGuia.Text = op.DocumentoAsociado;
                SelectComboMatch(_cmbMaterial, i => i is MatOpt o && string.Equals(o.Mat.Codigo, op.CodProducto, StringComparison.OrdinalIgnoreCase));
                string rutRef = !string.IsNullOrWhiteSpace(op.RutProveedor) ? op.RutProveedor : op.RutCliente;
                SelectComboMatch(_cmbEntidad, i => i is EntOpt o && !string.IsNullOrEmpty(o.Rut) && RutChileno.Matches(o.Rut, rutRef));
                if (!string.IsNullOrWhiteSpace(op.Patente))
                {
                    SelectComboMatch(_cmbVehiculo, i => i is VehOpt o && PatenteChilena.MatchesExact(o.Veh.PatenteNormalizada, op.Patente));
                }
            }
        }

        if (mode == PesajeDialogMode.TwoStepStart)
        {
            _numTara.Enabled = false;
            _numTara.Value = 0;
        }
        else if (mode == PesajeDialogMode.TwoStepComplete)
        {
            _cmbPlanta.Enabled = false;
            _cmbTipoPesaje.Enabled = false;
            _cmbVehiculo.Enabled = false;
            _cmbMaterial.Enabled = false;
            _cmbEntidad.Enabled = false;
            _cmbOperacion.Enabled = false;
            _numEsperado.Enabled = false;
        }

        UpdateNetPreview();

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 12,
            Padding = new Padding(16, 12, 16, 8),
            AutoScroll = true
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        string lblMed1 = mode switch
        {
            PesajeDialogMode.TwoStepStart => "1ª Medición (Kg) *:",
            PesajeDialogMode.TwoStepComplete => "1ª Medición (Fija):",
            _ => "Peso Bruto (Kg) *:"
        };
        string lblMed2 = mode == PesajeDialogMode.TwoStepComplete ? "2ª Medición (Kg) *:" : "Peso Tara (Kg):";

        AddRow(body, 0, "Planta:", _cmbPlanta, "Folio / Código:", _txtFolioCodigo);
        AddSpanRow(body, 1, "Tipo de Pesaje *:", _cmbTipoPesaje);
        AddSpanRow(body, 2, "Vehículo / Patente *:", _cmbVehiculo);
        AddSpanRow(body, 3, "Material *:", _cmbMaterial);
        AddSpanRow(body, 4, "Cliente / Prov.:", _cmbEntidad);
        AddSpanRow(body, 5, "Operación ERP:", _cmbOperacion);
        AddRow(body, 6, mode == PesajeDialogMode.TwoStepComplete ? "Fecha Salida:" : "Fecha Ingreso:", _dtpFecha, "Hora:", _dtpHora);
        AddRow(body, 7, lblMed1, _numBruto, lblMed2, _numTara);
        AddRow(body, 8, "PESO NETO CALC.:", _lblNetoCalc, "Peso Esperado:", _numEsperado);
        AddRow(body, 9, "Báscula:", _txtBascula, "Guía / Doc.:", _txtGuia);
        AddRow(body, 10, "Chofer:", _txtChofer, "Origen:", _txtOrigen);
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        TableLayoutPanel extra = new()
        {
            Dock = DockStyle.Bottom,
            Height = mode == PesajeDialogMode.AuthorizedCorrection ? 124 : 88,
            ColumnCount = 2,
            RowCount = mode == PesajeDialogMode.AuthorizedCorrection ? 3 : 2,
            Padding = new Padding(16, 4, 16, 6)
        };
        extra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        extra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        if (mode == PesajeDialogMode.AuthorizedCorrection)
        {
            extra.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            extra.Controls.Add(MkLabel("Motivo Cambio *:"), 0, 0);
            _txtMotivo.Margin = new Padding(0, 3, 0, 3);
            extra.Controls.Add(_txtMotivo, 1, 0);

            extra.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            extra.Controls.Add(MkLabel("Observación:"), 0, 1);
            _txtObservacion.Margin = new Padding(0, 3, 0, 3);
            extra.Controls.Add(_txtObservacion, 1, 1);

            extra.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            extra.Controls.Add(_chkExcepcionTol, 1, 2);
        }
        else
        {
            extra.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            extra.Controls.Add(MkLabel("Observación:"), 0, 0);
            _txtObservacion.Margin = new Padding(0, 3, 0, 3);
            extra.Controls.Add(_txtObservacion, 1, 0);

            extra.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            extra.Controls.Add(_chkExcepcionTol, 1, 1);
        }

        string okText = mode switch
        {
            PesajeDialogMode.TwoStepStart => "Registrar 1ª Medición",
            PesajeDialogMode.TwoStepComplete => "Completar y Confirmar",
            PesajeDialogMode.AuthorizedCorrection => "Guardar Corrección",
            _ => "Confirmar Pesaje"
        };

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnOk = AppTheme.CreateButton(okText, IconKind.Check, ButtonVariant.Success, (_, _) => OnSave(), null, null, 0, 34);
        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnOk);

        CancelButton = btnCancel;
        Controls.Add(body);
        Controls.Add(extra);
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

    private static void AddSpanRow(TableLayoutPanel t, int row, string label, Control c)
    {
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        t.Controls.Add(MkLabel(label), 0, row);
        c.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(c, 1, row);
        t.SetColumnSpan(c, 3);
    }

    private static void SelectComboMatch(ComboBox cmb, Func<object?, bool> predicate)
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

    private void UpdateNetPreview()
    {
        if (_mode == PesajeDialogMode.TwoStepStart)
        {
            _lblNetoCalc.Text = "Pendiente 2ª Med.";
            _lblNetoCalc.ForeColor = AppTheme.AccentAmber;
            return;
        }

        int m1 = (int)_numBruto.Value;
        int m2 = (int)_numTara.Value;
        int neto = _mode == PesajeDialogMode.TwoStepComplete
            ? Math.Abs(m1 - m2)
            : m1 - m2;

        if (neto < 0)
        {
            _lblNetoCalc.Text = "INVÁLIDO (Bruto < Tara)";
            _lblNetoCalc.ForeColor = AppTheme.AccentRed;
        }
        else
        {
            string unit = (_cmbMaterial.SelectedItem as MatOpt)?.Mat.UnidadMedida ?? "KG";
            _lblNetoCalc.Text = string.Create(UiHelpers.EsCl, $"{neto:N0} {unit}");
            _lblNetoCalc.ForeColor = AppTheme.AccentEmerald;
        }
    }

    private void OnSave()
    {
        if (_mode == PesajeDialogMode.TwoStepComplete)
        {
            if (_numTara.Value <= 0)
            {
                MessageBox.Show(this, "La segunda medición debe ser mayor que cero.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _numTara.Focus();
                return;
            }

            SegundaMedicionKg = (int)_numTara.Value;
            FechaSalida = DateOnly.FromDateTime(_dtpFecha.Value);
            HoraSalida = TimeOnly.FromDateTime(_dtpHora.Value);
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (_cmbVehiculo.SelectedItem is not VehOpt vehOpt)
        {
            MessageBox.Show(this, "Debe seleccionar un vehículo/patente registrado.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbMaterial.SelectedItem is not MatOpt matOpt)
        {
            MessageBox.Show(this, "Debe seleccionar un material activo.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_mode == PesajeDialogMode.TwoStepStart)
        {
            if (_numBruto.Value <= 0)
            {
                MessageBox.Show(this, "La primera medición debe ser mayor que cero.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            if (_numBruto.Value <= 0 || _numBruto.Value < _numTara.Value)
            {
                MessageBox.Show(this, "El peso Bruto debe ser mayor a 0 y mayor o igual que la Tara.", "Validación de Peso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (_mode == PesajeDialogMode.AuthorizedCorrection && string.IsNullOrWhiteSpace(_txtMotivo.Text))
        {
            MessageBox.Show(this, "Debe registrar el motivo auditable de la corrección.", "Motivo Obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtMotivo.Focus();
            return;
        }

        TipoCfgOpt? tipoOpt = _cmbTipoPesaje.SelectedItem as TipoCfgOpt;
        EntOpt? entOpt = _cmbEntidad.SelectedItem as EntOpt;
        OpOpt? opOpt = _cmbOperacion.SelectedItem as OpOpt;

        long parsedFolio = _existing?.Folio ?? 0;
        if (parsedFolio == 0 && long.TryParse(_txtFolioCodigo.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long fVal) && fVal > 0)
        {
            parsedFolio = fVal;
        }

        ResultRequest = new PesajeRegistroRequestDto
        {
            Id = _existing?.Id ?? 0,
            Folio = parsedFolio,
            FolioCodigo = _txtFolioCodigo.Text.Trim(),
            Planta = UiHelpers.GetSelectedPlanta(_cmbPlanta),
            TipoPesaje = tipoOpt?.Cfg.TipoBase ?? TipoPesaje.RecepcionCarga,
            TipoPesajeConfigId = tipoOpt?.Cfg.Id,
            Patente = vehOpt.Veh.PatenteOriginal,
            PesoBruto = (int)_numBruto.Value,
            PesoTara = (int)_numTara.Value,
            PrimeraMedicionKg = (int)_numBruto.Value,
            PesoEsperadoKg = _numEsperado.Value > 0 ? (int)_numEsperado.Value : null,
            UnidadMedida = matOpt.Mat.UnidadMedida,
            CodProducto = matOpt.Mat.Codigo,
            RutEntidad = entOpt?.Rut ?? string.Empty,
            NombreEntidad = entOpt?.Nombre ?? string.Empty,
            OperacionId = opOpt?.Id,
            Bascula = _txtBascula.Text.Trim(),
            GuiaDespacho = _txtGuia.Text.Trim(),
            Chofer = _txtChofer.Text.Trim(),
            Origen = _txtOrigen.Text.Trim(),
            Destino = _txtDestino.Text.Trim(),
            FechaIngreso = DateOnly.FromDateTime(_dtpFecha.Value),
            HoraIngreso = TimeOnly.FromDateTime(_dtpHora.Value),
            FechaSalida = DateOnly.FromDateTime(_dtpFecha.Value),
            HoraSalida = TimeOnly.FromDateTime(_dtpHora.Value),
            Observacion = _txtObservacion.Text.Trim(),
            AutorizacionExcepcionTolerancia = _chkExcepcionTol.Checked,
            MotivoAjuste = _txtMotivo.Text.Trim()
        };

        MotivoCambio = _txtMotivo.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
