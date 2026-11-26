using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 5: Valorización por precios teóricos pactados y precios reales indexados con congelamiento y edición individual.
/// </summary>
public sealed class InformePreciosControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 125 };
    private readonly RadioButton _rbTeoricos = new();
    private readonly RadioButton _rbReales = new();
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnCongelar;
    private readonly Button _btnEditarPrecio;
    private readonly Button _btnExportar;

    private readonly DoubleBufferedDataGridView _dgvResumen = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvDetalle = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly Label _lblTotales = new();

    private InformePreciosResultDto? _lastResult;

    public InformePreciosControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(98);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: false);
        _rbTeoricos.Text = "Precios Teóricos (Pactados)";
        _rbTeoricos.Checked = true;
        _rbTeoricos.Font = AppTheme.FontBodyBold;
        _rbTeoricos.AutoSize = true;
        _rbTeoricos.Margin = new Padding(12, 8, 6, 0);

        _rbReales.Text = "Precios Reales (Indexados)";
        _rbReales.Font = AppTheme.FontBody;
        _rbReales.AutoSize = true;
        _rbReales.Margin = new Padding(6, 8, 12, 0);
        _rbReales.CheckedChanged += (_, _) => RefreshPermissions();

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _btnConsultar = AppTheme.CreateButton("Consultar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(), "Calcula valorización teórica o real en la planta seleccionada", Tip, 120, 34);
        _btnCongelar = AppTheme.CreateButton("Congelar Teóricos", IconKind.Lock, ButtonVariant.Warning,
            async (_, _) => await CongelarAsync(), "Fija los precios teóricos consultados como precios reales indexados", Tip, 165, 34);
        _btnEditarPrecio = AppTheme.CreateButton("Editar Precio Folio", IconKind.Edit, ButtonVariant.Secondary,
            (_, _) => EditarFilaSeleccionada(), "Modifica el precio real de un folio específico (solo en modo Reales)", Tip, 165, 34);
        _btnExportar = AppTheme.CreateButton("Exportar Excel", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Exporta el detalle y resumen de precios a Excel", Tip, 145, 34);

        TableLayoutPanel filterRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) },
            _cmbPlanta,
            _rbTeoricos,
            _rbReales,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 8, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 8, 4, 0) },
            _dtpHasta
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r2.Controls.AddRange([_btnConsultar, _btnCongelar, _btnEditarPrecio, _btnExportar]);

        filterRows.Controls.Add(r1, 0, 0);
        filterRows.Controls.Add(r2, 0, 1);
        filterCard.Controls.Add(filterRows);

        Panel resumenCard = new()
        {
            Dock = DockStyle.Top,
            Height = 175,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            Margin = new Padding(0, 6, 0, 6),
            BorderStyle = BorderStyle.FixedSingle
        };
        Label lblResTitle = new()
        {
            Text = "RESUMEN PONDERADO POR FAMILIA (SUPER BASULIP · RECAM · ALTA)",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.AccentBlue,
            Dock = DockStyle.Top,
            Height = 26
        };
        _dgvResumen.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Familia", nameof(PrecioFamiliaResumenDto.Grupo), 120),
            UiHelpers.AddNumberCol("Kilos Totales", nameof(PrecioFamiliaResumenDto.TotalKilos), "N0", 85),
            UiHelpers.AddCurrencyCol("Prom. Ponderado ($/Kg)", nameof(PrecioFamiliaResumenDto.PrecioPromedioPonderado), 95),
            UiHelpers.AddCurrencyCol("Total Valorizado ($)", nameof(PrecioFamiliaResumenDto.TotalValorizado), 100)
        ]);
        resumenCard.Controls.Add(_dgvResumen);
        resumenCard.Controls.Add(lblResTitle);

        Panel detalleCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        _lblTotales.Text = "DETALLE VALORIZADO (Doble clic para editar en modo Precios Reales) · Total: 0 Kg | $0";
        _lblTotales.Font = AppTheme.FontSection;
        _lblTotales.ForeColor = AppTheme.AccentCyan;
        _lblTotales.Dock = DockStyle.Top;
        _lblTotales.Height = 28;

        _dgvDetalle.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(PrecioDetalleRowDto.Folio), "N0", 55),
            UiHelpers.AddTextCol("Cód", nameof(PrecioDetalleRowDto.CodigoProd), 55),
            UiHelpers.AddTextCol("Descripción", nameof(PrecioDetalleRowDto.Descripcion), 115),
            UiHelpers.AddTextCol("RUT", nameof(PrecioDetalleRowDto.RutCliente), 75),
            UiHelpers.AddTextCol("Proveedor", nameof(PrecioDetalleRowDto.Proveedor), 125),
            UiHelpers.AddNumberCol("Kilos", nameof(PrecioDetalleRowDto.Kilos), "N0", 60),
            UiHelpers.AddCurrencyCol("Precio $/Kg", nameof(PrecioDetalleRowDto.Precio), 70),
            UiHelpers.AddCurrencyCol("Total $", nameof(PrecioDetalleRowDto.Total), 80),
            UiHelpers.AddTextCol("Familia", nameof(PrecioDetalleRowDto.Familia), 85)
        ]);
        _dgvDetalle.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditarFilaSeleccionada();
            }
        };

        detalleCard.Controls.Add(_dgvDetalle);
        detalleCard.Controls.Add(_lblTotales);

        Controls.Add(detalleCard);
        Controls.Add(resumenCard);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        _btnConsultar.Enabled = _auth.HasPermission(PermisoSistema.ConsultarInformes);
        _btnCongelar.Enabled = _auth.HasPermission(PermisoSistema.CongelarPreciosTeoricos) && _rbTeoricos.Checked;
        _btnEditarPrecio.Enabled = _auth.HasPermission(PermisoSistema.EditarPreciosReales) && _rbReales.Checked;
        _btnExportar.Enabled = _auth.HasPermission(PermisoSistema.ExportarExcel);
    }

    public override Task ActivateAsync(CancellationToken ct = default) => ConsultarAsync(ct);

    private async Task ConsultarAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            ModoPrecio modo = _rbTeoricos.Checked ? ModoPrecio.Teoricos : ModoPrecio.Reales;
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastResult = await _app.GetInformePreciosAsync(planta, modo, desde, hasta, ct);
            _dgvResumen.DataSource = null;
            _dgvResumen.DataSource = _lastResult.ResumenFamilias;
            _dgvDetalle.DataSource = null;
            _dgvDetalle.DataSource = _lastResult.Detalle;

            _lblTotales.Text = string.Create(
                UiHelpers.EsCl,
                $"{planta.ToShortTitle()} ({modo}) · {_lastResult.Detalle.Count} compras · {_lastResult.TotalKilos:N0} Kg · ${_lastResult.TotalValorizado:N0}");
            RefreshPermissions();
            NotifyStatus($"Informe de precios ({modo}): {_lastResult.TotalKilos.ToString("N0", UiHelpers.EsCl)} Kg = ${_lastResult.TotalValorizado.ToString("N0", UiHelpers.EsCl)}.");
        }, NotifyStatus, "Calculando informe de precios...");
    }

    private void EditarFilaSeleccionada()
    {
        if (!_auth.HasPermission(PermisoSistema.EditarPreciosReales))
        {
            MessageBox.Show(this, "Su perfil no tiene permisos para editar precios reales.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_dgvDetalle.CurrentRow?.DataBoundItem is not PrecioDetalleRowDto row || _lastResult is null)
        {
            MessageBox.Show(this, "Seleccione una fila de compra en la grilla inferior.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using ActualizarPrecioDialog dlg = new(
            _app,
            _lastResult.Planta,
            _lastResult.Modo,
            row.Folio,
            row.CodigoProd,
            row.Descripcion,
            row.Proveedor,
            row.Familia,
            row.Precio);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ = ConsultarAsync();
        }
    }

    private async Task CongelarAsync()
    {
        if (_lastResult is null || _lastResult.Detalle.Count == 0)
        {
            MessageBox.Show(this, "Consulte primero los precios teóricos del período.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int elegibles = _lastResult.Detalle.Count(d => d.Precio > 0);
        if (elegibles == 0)
        {
            MessageBox.Show(this, "No existen compras con precio teórico > 0 para congelar.", "Nada que Congelar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            this,
            $"Se congelarán {elegibles} tarifas teóricas como Precios Reales históricos para {_lastResult.Planta.ToShortTitle()}.\n\n¿Desea continuar?",
            "Confirmar Congelamiento",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            int count = await _app.CongelarPreciosTeoricosAsync(_lastResult.Planta, _lastResult.Modo, _lastResult.Detalle);
            NotifyStatus($"Congelados {count} precios reales.");
            MessageBox.Show(this, $"Se congelaron correctamente {count} precios reales indexados.", "Proceso Completado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _rbReales.Checked = true;
            await ConsultarAsync();
        }, NotifyStatus, "Congelando precios teóricos...");
    }

    private void Exportar()
    {
        if (_lastResult is null || _lastResult.Detalle.Count == 0)
        {
            MessageBox.Show(this, "No existen datos de precios para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"InformePrecios_{_lastResult.Planta}_{_lastResult.Modo}_{DateTime.Now:yyyyMMdd}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportInformePrecios(
                path,
                _lastResult.Planta,
                DateOnly.FromDateTime(_dtpDesde.Value),
                DateOnly.FromDateTime(_dtpHasta.Value),
                _lastResult.Modo,
                [.. _lastResult.Detalle.Select(d => (d.Folio, d.CodigoProd, d.Descripcion, d.Proveedor, d.Kilos, d.Precio, d.Total, d.Familia))],
                [.. _lastResult.ResumenFamilias.Select(r => (r.Grupo, r.TotalKilos, r.PrecioPromedioPonderado, r.TotalValorizado))]);

            NotifyStatus($"Excel de precios generado: {saved}");
            MessageBox.Show(this, $"Informe de precios exportado:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
