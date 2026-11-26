using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 2: Informe detallado de pesajes por Cliente (RUT) o por Folio individual (corregido vs VBA legacy).
/// </summary>
public sealed class InformeClienteControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 130 };
    private readonly RadioButton _rbRut = new();
    private readonly RadioButton _rbFolio = new();
    private readonly TextBox _txtCriterio = new() { Width = 140 };
    private readonly Button _btnCatalogoClientes;
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnLimpiar;
    private readonly Button _btnExportar;

    private readonly Label _lblResumen = new();
    private readonly DoubleBufferedDataGridView _dgv = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private IReadOnlyList<Pesaje> _rows = [];
    private string _selectedRazonSocial = string.Empty;

    public InformeClienteControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(96);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: true);
        _rbRut.Text = "Buscar por RUT";
        _rbRut.Checked = true;
        _rbRut.Font = AppTheme.FontBodyBold;
        _rbRut.AutoSize = true;

        _rbFolio.Text = "Buscar por Folio";
        _rbFolio.Font = AppTheme.FontBody;
        _rbFolio.AutoSize = true;

        AppTheme.ApplyInputStyle(_txtCriterio);
        _txtCriterio.PlaceholderText = "Ej: 76123456-K o Folio";
        _txtCriterio.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = ConsultarAsync();
            }
            else if (e.KeyCode is Keys.F2 or Keys.Space && _rbRut.Checked)
            {
                e.SuppressKeyPress = true;
                OpenClienteSelector();
            }
        };

        _btnCatalogoClientes = AppTheme.CreateButton("Catálogo", IconKind.User, ButtonVariant.Secondary,
            (_, _) => OpenClienteSelector(), "Abrir buscador de clientes (atajo F2 / Espacio)", Tip, 112, 32);
        _rbRut.CheckedChanged += (_, _) => _btnCatalogoClientes.Enabled = _rbRut.Checked;

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _btnConsultar = AppTheme.CreateButton("Consultar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(), "Ejecuta la búsqueda por RUT o Folio en la planta elegida", Tip, 120, 34);
        _btnLimpiar = AppTheme.CreateButton("Limpiar", IconKind.Refresh, ButtonVariant.Secondary,
            (_, _) => ClearSearch(), "Limpia criterios y resultados", Tip, 105, 34);
        _btnExportar = AppTheme.CreateButton("Exportar Ticket", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Genera el informe en Excel del cliente o folio consultado", Tip, 145, 34);

        TableLayoutPanel filterRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel row1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        row1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) },
            _cmbPlanta,
            _rbRut,
            _rbFolio,
            new Label { Text = "Criterio:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 8, 6, 0) },
            _txtCriterio,
            _btnCatalogoClientes
        ]);

        FlowLayoutPanel row2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        row2.Controls.AddRange(
        [
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 8, 4, 0) },
            _dtpHasta,
            _btnConsultar,
            _btnLimpiar,
            _btnExportar
        ]);

        filterRows.Controls.Add(row1, 0, 0);
        filterRows.Controls.Add(row2, 0, 1);
        filterCard.Controls.Add(filterRows);

        Panel gridCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(12),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblResumen.Text = "Ingrese un RUT (F2 para catálogo) o Folio y presione Consultar.";
        _lblResumen.Font = AppTheme.FontSection;
        _lblResumen.ForeColor = AppTheme.AccentCyan;
        _lblResumen.Dock = DockStyle.Top;
        _lblResumen.Height = 28;

        _dgv.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(Pesaje.Folio), "N0", 60),
            UiHelpers.AddTextCol("Tipo", nameof(Pesaje.TipoPesaje), 75),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 65),
            UiHelpers.AddTextCol("RUT", nameof(Pesaje.RutCliente), 80),
            UiHelpers.AddTextCol("Cliente / Razón Social", nameof(Pesaje.NomCliente), 130),
            UiHelpers.AddTextCol("Producto", nameof(Pesaje.NomProducto), 115),
            UiHelpers.AddDateCol("Ingreso", nameof(Pesaje.FechaIngreso), 70),
            UiHelpers.AddDateCol("Salida", nameof(Pesaje.FechaSalida), 70),
            UiHelpers.AddNumberCol("Bruto", nameof(Pesaje.PesoBruto), "N0", 60),
            UiHelpers.AddNumberCol("Tara", nameof(Pesaje.PesoTara), "N0", 60),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 65)
        ]);

        gridCard.Controls.Add(_dgv);
        gridCard.Controls.Add(_lblResumen);

        Controls.Add(gridCard);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canRead = _auth.HasPermission(PermisoSistema.ConsultarInformes);
        _btnConsultar.Enabled = canRead;
        _btnExportar.Enabled = _auth.HasPermission(PermisoSistema.ExportarExcel);
    }

    private void OpenClienteSelector()
    {
        using ClienteSelectorDialog dlg = new(_app, UiHelpers.GetSelectedPlanta(_cmbPlanta));
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedCliente is not null)
        {
            _rbRut.Checked = true;
            _txtCriterio.Text = dlg.SelectedCliente.RutFormateado;
            _selectedRazonSocial = dlg.SelectedCliente.RazonSocial;
            _ = ConsultarAsync();
        }
    }

    private void ClearSearch()
    {
        _txtCriterio.Clear();
        _selectedRazonSocial = string.Empty;
        _rows = [];
        _dgv.DataSource = null;
        _lblResumen.Text = "Criterio limpiado. Ingrese RUT o Folio para consultar.";
        NotifyStatus("Búsqueda por cliente/folio reiniciada.");
    }

    private async Task ConsultarAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_txtCriterio.Text))
        {
            MessageBox.Show(this, "Ingrese un RUT o número de Folio para consultar.", "Criterio Requerido", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtCriterio.Focus();
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _rows = await _app.GetInformeClienteAsync(planta, _rbRut.Checked, _txtCriterio.Text, desde, hasta, ct);
            _dgv.DataSource = null;
            _dgv.DataSource = _rows;

            long total = _rows.Sum(r => (long)r.PesoNeto);
            if (_rows.Count > 0 && string.IsNullOrWhiteSpace(_selectedRazonSocial))
            {
                _selectedRazonSocial = _rows[0].NomCliente;
            }

            _lblResumen.Text = string.Create(
                UiHelpers.EsCl,
                $"{planta.ToShortTitle()} · {_rows.Count} registros encontrados · Total Neto: {total:N0} Kg");
            NotifyStatus($"Informe por cliente/folio: {_rows.Count} pesajes ({total.ToString("N0", UiHelpers.EsCl)} Kg).");
        }, NotifyStatus, "Consultando pesajes...");
    }

    private void Exportar()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "No existen pesajes en la grilla actual para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"InformeCliente_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            long total = _rows.Sum(r => (long)r.PesoNeto);
            string razon = _rows[0].NomCliente;
            string rut = _rows[0].RutCliente;

            string saved = _app.ExcelExporter.ExportInformeCliente(
                path,
                planta,
                rut,
                razon,
                DateOnly.FromDateTime(_dtpDesde.Value),
                DateOnly.FromDateTime(_dtpHasta.Value),
                [.. _rows.Select(p => (p.Folio, p.Patente, p.NomProducto, p.FechaIngreso, p.FechaSalida, p.PesoBruto, p.PesoTara, p.PesoNeto))],
                total);

            NotifyStatus($"Ticket cliente exportado: {saved}");
            MessageBox.Show(this, $"Informe exportado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(this, ex.Message, "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
