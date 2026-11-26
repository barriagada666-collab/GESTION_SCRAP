using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 1: Informe comparativo de Compras vs Traslados Internos por planta.
/// </summary>
public sealed class ComprasTrasladosControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 140 };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnExportar;

    private readonly Label _lblComprasVal;
    private readonly Label _lblComprasSub;
    private readonly Label _lblTrasladosVal;
    private readonly Label _lblTrasladosSub;
    private readonly Label _lblGeneralVal;
    private readonly Label _lblGeneralSub;

    private readonly Label _lblGridComprasTitle = new();
    private readonly Label _lblGridTrasladosTitle = new();
    private readonly DoubleBufferedDataGridView _dgvCompras = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvTraslados = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private ComprasTrasladosResultDto? _lastResult;

    public ComprasTrasladosControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(62);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: true);
        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(today.Year, today.Month, 1);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _btnConsultar = AppTheme.CreateButton("Consultar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(), "Consulta compras y traslados internos por planta", Tip, 125, 34);
        _btnExportar = AppTheme.CreateButton("Exportar Excel", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Genera un libro Excel con pestañas separadas de Compras y Traslados", Tip, 145, 34);

        FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, WrapContents = false };
        flow.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 6, 0) },
            _cmbPlanta,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(14, 9, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 9, 4, 0) },
            _dtpHasta,
            _btnConsultar,
            _btnExportar
        ]);
        filterCard.Controls.Add(flow);

        TableLayoutPanel kpis = new()
        {
            Dock = DockStyle.Top,
            Height = 96,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 6)
        };
        for (int i = 0; i < 3; i++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        }

        kpis.Controls.Add(UiHelpers.CreateKpiCard("TOTAL COMPRAS", AppTheme.AccentCyan, out _lblComprasVal, out _lblComprasSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("TRASLADOS INTERNOS", AppTheme.AccentAmber, out _lblTrasladosVal, out _lblTrasladosSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("TOTAL GENERAL", AppTheme.AccentEmerald, out _lblGeneralVal, out _lblGeneralSub), 2, 0);

        TableLayoutPanel grids = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        _dgvCompras.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(ComprasRowDto.Codigo), 60),
            UiHelpers.AddTextCol("Descripción", nameof(ComprasRowDto.Descripcion), 115),
            UiHelpers.AddTextCol("Razón Social", nameof(ComprasRowDto.RazonSocial), 130),
            UiHelpers.AddNumberCol("Kilos", nameof(ComprasRowDto.Kilos), "N0", 70)
        ]);

        _dgvTraslados.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("Folio", nameof(TrasladoRowDto.Folio), "N0", 55),
            UiHelpers.AddTextCol("Patente", nameof(TrasladoRowDto.Patente), 65),
            UiHelpers.AddTextCol("Producto", nameof(TrasladoRowDto.Producto), 110),
            UiHelpers.AddDateCol("Ingreso", nameof(TrasladoRowDto.FechaEntrada), 70),
            UiHelpers.AddDateCol("Salida", nameof(TrasladoRowDto.FechaSalida), 70),
            UiHelpers.AddNumberCol("Neto Kg", nameof(TrasladoRowDto.PesoNeto), "N0", 65)
        ]);

        grids.Controls.Add(WrapGridCard(_lblGridComprasTitle, "DETALLE COMPRAS", AppTheme.AccentCyan, _dgvCompras), 0, 0);
        grids.Controls.Add(WrapGridCard(_lblGridTrasladosTitle, "TRASLADOS INTERNOS", AppTheme.AccentAmber, _dgvTraslados), 1, 0);

        Controls.Add(grids);
        Controls.Add(kpis);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    private static Panel WrapGridCard(Label header, string defaultTitle, Color accent, DoubleBufferedDataGridView dgv)
    {
        Panel p = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        header.Text = defaultTitle;
        header.Font = AppTheme.FontSection;
        header.ForeColor = accent;
        header.Dock = DockStyle.Top;
        header.Height = 28;
        p.Controls.Add(dgv);
        p.Controls.Add(header);
        return p;
    }

    public override void RefreshPermissions()
    {
        _btnConsultar.Enabled = _auth.HasPermission(PermisoSistema.ConsultarInformes);
        _btnExportar.Enabled = _auth.HasPermission(PermisoSistema.ExportarExcel);
    }

    public override Task ActivateAsync(CancellationToken ct = default) => ConsultarAsync(ct);

    private async Task ConsultarAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

            _lastResult = await _app.GetComprasTrasladosAsync(planta, desde, hasta, ct);
            _lblGridComprasTitle.Text = _lastResult.LabelCompras;
            _lblGridTrasladosTitle.Text = _lastResult.LabelTraslados;

            _dgvCompras.DataSource = null;
            _dgvCompras.DataSource = _lastResult.Compras;
            _dgvTraslados.DataSource = null;
            _dgvTraslados.DataSource = _lastResult.Traslados;

            _lblComprasVal.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.TotalComprasKilos:N0} Kg");
            _lblComprasSub.Text = $"{_lastResult.Compras.Count} agrupaciones de compra";
            _lblTrasladosVal.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.TotalTrasladosKilos:N0} Kg");
            _lblTrasladosSub.Text = $"{_lastResult.Traslados.Count} traslados entre plantas";
            _lblGeneralVal.Text = string.Create(UiHelpers.EsCl, $"{_lastResult.TotalGeneralKilos:N0} Kg");
            _lblGeneralSub.Text = $"Planta {planta.ToShortTitle()}";

            NotifyStatus($"Compras y traslados {planta.ToShortTitle()}: {_lastResult.TotalGeneralKilos.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Consultando compras y traslados...");
    }

    private void Exportar()
    {
        if (_lastResult is null || (_lastResult.Compras.Count == 0 && _lastResult.Traslados.Count == 0))
        {
            MessageBox.Show(this, "No hay registros para exportar. Ejecute una consulta primero.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"ComprasTraslados_{_lastResult.Planta}_{DateTime.Now:yyyyMMdd}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportComprasTraslados(
                path,
                _lastResult.Planta,
                DateOnly.FromDateTime(_dtpDesde.Value),
                DateOnly.FromDateTime(_dtpHasta.Value),
                [.. _lastResult.Compras.Select(c => (c.Codigo, c.Descripcion, c.RazonSocial, c.Kilos))],
                [.. _lastResult.Traslados.Select(t => (t.Folio, t.Patente, t.Producto, t.FechaEntrada, t.FechaSalida, t.PesoBruto, t.PesoTara, t.PesoNeto))],
                _lastResult.TotalComprasKilos,
                _lastResult.TotalTrasladosKilos);

            NotifyStatus($"Excel exportado en: {saved}");
            MessageBox.Show(this, $"Archivo Excel generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
