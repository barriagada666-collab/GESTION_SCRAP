using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 0: Resumen consolidado de kilos por grupo de material across Placilla, Santiago y Talcahuano.
/// </summary>
public sealed class GrupoMaterialControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly RadioButton _rbRango = new();
    private readonly RadioButton _rbSemanal = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnExportar;
    private readonly Label _lblGranTotal = new();

    private readonly DoubleBufferedDataGridView _dgvPlacilla = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvSantiago = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvTalcahuano = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly Label _lblTotPlacilla = new();
    private readonly Label _lblTotSantiago = new();
    private readonly Label _lblTotTalcahuano = new();

    private IReadOnlyList<GrupoKilosItem> _lastPlacilla = [];
    private IReadOnlyList<GrupoKilosItem> _lastSantiago = [];
    private IReadOnlyList<GrupoKilosItem> _lastTalcahuano = [];

    public GrupoMaterialControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(62);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(today.Year, today.Month, 1);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _rbRango.Text = "Rango Fechas";
        _rbRango.Checked = true;
        _rbRango.Font = AppTheme.FontBodyBold;
        _rbRango.AutoSize = true;
        _rbRango.Margin = new Padding(10, 9, 4, 0);

        _rbSemanal.Text = "Semanal (7 días)";
        _rbSemanal.Font = AppTheme.FontBody;
        _rbSemanal.AutoSize = true;
        _rbSemanal.Margin = new Padding(4, 9, 12, 0);

        _btnConsultar = AppTheme.CreateButton("Consultar", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(), "Calcula totales por familia de material en las 3 plantas", Tip, 125, 34);
        _btnExportar = AppTheme.CreateButton("Exportar Excel", IconKind.Excel, ButtonVariant.Success,
            (_, _) => Exportar(), "Exporta las 3 plantas a un archivo Excel consolidado (.xlsx)", Tip, 145, 34);

        _lblGranTotal.Text = "TOTAL NACIONAL: 0 Kg";
        _lblGranTotal.Font = AppTheme.FontSection;
        _lblGranTotal.ForeColor = AppTheme.AccentCyan;
        _lblGranTotal.AutoSize = true;
        _lblGranTotal.Margin = new Padding(18, 9, 0, 0);

        flow.Controls.AddRange(
        [
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 9, 4, 0) },
            _dtpHasta,
            _rbRango,
            _rbSemanal,
            _btnConsultar,
            _btnExportar,
            _lblGranTotal
        ]);
        filterCard.Controls.Add(flow);

        TableLayoutPanel gridLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        gridLayout.Controls.Add(BuildPlantCard("PLANTA PLACILLA", AppTheme.AccentCyan, _dgvPlacilla, _lblTotPlacilla), 0, 0);
        gridLayout.Controls.Add(BuildPlantCard("PLANTA SANTIAGO", AppTheme.AccentBlue, _dgvSantiago, _lblTotSantiago), 1, 0);
        gridLayout.Controls.Add(BuildPlantCard("PLANTA TALCAHUANO", AppTheme.AccentEmerald, _dgvTalcahuano, _lblTotTalcahuano), 2, 0);

        Controls.Add(gridLayout);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    private static Panel BuildPlantCard(string title, Color accent, DoubleBufferedDataGridView dgv, Label footerLabel)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };

        Label lblHeader = new()
        {
            Text = title,
            Font = AppTheme.FontSection,
            ForeColor = accent,
            Dock = DockStyle.Top,
            Height = 28
        };

        dgv.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Grupo Material", nameof(GrupoKilosItem.Grupo), 120),
            UiHelpers.AddNumberCol("Kilos Netos", nameof(GrupoKilosItem.Kilos), "N0", 80)
        ]);

        footerLabel.Text = "Total Planta: 0 Kg";
        footerLabel.Font = AppTheme.FontBodyBold;
        footerLabel.ForeColor = AppTheme.TextPrimary;
        footerLabel.Dock = DockStyle.Bottom;
        footerLabel.Height = 28;
        footerLabel.TextAlign = ContentAlignment.MiddleRight;

        card.Controls.Add(dgv);
        card.Controls.Add(footerLabel);
        card.Controls.Add(lblHeader);
        return card;
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
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);
            (_lastPlacilla, _lastSantiago, _lastTalcahuano) =
                await _app.GetGrupoMaterialAsync(desde, hasta, _rbSemanal.Checked, ct);

            BindPlant(_dgvPlacilla, _lblTotPlacilla, "Total Placilla", _lastPlacilla);
            BindPlant(_dgvSantiago, _lblTotSantiago, "Total Santiago", _lastSantiago);
            BindPlant(_dgvTalcahuano, _lblTotTalcahuano, "Total Talcahuano", _lastTalcahuano);

            long nacional = _lastPlacilla.Sum(x => x.Kilos) + _lastSantiago.Sum(x => x.Kilos) + _lastTalcahuano.Sum(x => x.Kilos);
            _lblGranTotal.Text = string.Create(UiHelpers.EsCl, $"TOTAL NACIONAL: {nacional:N0} Kg");
            NotifyStatus($"Grupos de material consultados ({desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}): {nacional.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Consultando grupos de material...");
    }

    private static void BindPlant(DoubleBufferedDataGridView dgv, Label footer, string prefix, IReadOnlyList<GrupoKilosItem> rows)
    {
        dgv.DataSource = null;
        dgv.DataSource = rows;
        long total = rows.Sum(r => r.Kilos);
        footer.Text = string.Create(UiHelpers.EsCl, $"{prefix}: {total:N0} Kg");
    }

    private void Exportar()
    {
        if (_lastPlacilla.Count == 0 && _lastSantiago.Count == 0 && _lastTalcahuano.Count == 0)
        {
            MessageBox.Show(this, "Consulte primero los datos antes de exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"GrupoMaterial_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);
            string saved = _app.ExcelExporter.ExportGrupoMaterial(
                path,
                desde,
                hasta,
                [.. _lastPlacilla.Select(x => (x.Grupo, x.Kilos))],
                [.. _lastSantiago.Select(x => (x.Grupo, x.Kilos))],
                [.. _lastTalcahuano.Select(x => (x.Grupo, x.Kilos))]);

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
