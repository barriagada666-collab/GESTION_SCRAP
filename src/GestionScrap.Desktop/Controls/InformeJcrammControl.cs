using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 4: Despachos a cliente JCRAMM con control persistente de último folio cerrado y exportación + avance atómico.
/// </summary>
public sealed class InformeJcrammControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly NumericUpDown _numUltimoFolio = new();
    private readonly Label _lblFolioInfo = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnExportarCerrar;

    private readonly Label _lblResumenTotal = new();
    private readonly Label _lblDetalleTotal = new();
    private readonly DoubleBufferedDataGridView _dgvResumen = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvDetalle = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private JcrammReportDto? _lastReport;

    public InformeJcrammControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(66);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        _numUltimoFolio.Width = 115;
        _numUltimoFolio.Font = AppTheme.FontMono;
        _numUltimoFolio.Maximum = 999_999_999;
        _numUltimoFolio.Minimum = 0;

        _btnConsultar = AppTheme.CreateButton("Buscar Pendientes", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(useCustomOverride: true),
            "Lista los pesajes posteriores al folio de corte indicado", Tip, 165, 34);
        _btnExportarCerrar = AppTheme.CreateButton("Exportar y Cerrar Folio", IconKind.Excel, ButtonVariant.Success,
            async (_, _) => await ExportarYCerrarAsync(),
            "Exporta a Excel y actualiza el último folio cerrado en la base central", Tip, 200, 34);

        _lblFolioInfo.Text = "Último folio registrado: -";
        _lblFolioInfo.Font = AppTheme.FontBodyBold;
        _lblFolioInfo.ForeColor = AppTheme.AccentBlue;
        _lblFolioInfo.AutoSize = true;
        _lblFolioInfo.Margin = new Padding(16, 10, 0, 0);

        FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, WrapContents = false };
        flow.Controls.AddRange(
        [
            new Label { Text = "Folio Corte (>):", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 10, 6, 0) },
            _numUltimoFolio,
            _btnConsultar,
            _btnExportarCerrar,
            _lblFolioInfo
        ]);
        filterCard.Controls.Add(flow);

        TableLayoutPanel split = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));

        Panel pnlResumen = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        _lblResumenTotal.Text = "RESUMEN PRODUCTOS: 0 Kg";
        _lblResumenTotal.Font = AppTheme.FontSection;
        _lblResumenTotal.ForeColor = AppTheme.AccentCyan;
        _lblResumenTotal.Dock = DockStyle.Top;
        _lblResumenTotal.Height = 28;
        _dgvResumen.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Producto", nameof(JcrammResumenRowDto.Producto), 130),
            UiHelpers.AddNumberCol("Kilos", nameof(JcrammResumenRowDto.Kilos), "N0", 70)
        ]);
        pnlResumen.Controls.Add(_dgvResumen);
        pnlResumen.Controls.Add(_lblResumenTotal);

        Panel pnlDetalle = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        _lblDetalleTotal.Text = "DETALLE DESPACHOS PENDIENTES: 0 Kg";
        _lblDetalleTotal.Font = AppTheme.FontSection;
        _lblDetalleTotal.ForeColor = AppTheme.AccentEmerald;
        _lblDetalleTotal.Dock = DockStyle.Top;
        _lblDetalleTotal.Height = 28;
        _dgvDetalle.Columns.AddRange(
        [
            UiHelpers.AddDateCol("Fecha", nameof(JcrammDetalleRowDto.Fecha), 70),
            UiHelpers.AddNumberCol("Folio", nameof(JcrammDetalleRowDto.Folio), "N0", 55),
            UiHelpers.AddTextCol("Código", nameof(JcrammDetalleRowDto.CodProducto), 55),
            UiHelpers.AddTextCol("Descripción", nameof(JcrammDetalleRowDto.Descripcion), 115),
            UiHelpers.AddTextCol("Patente", nameof(JcrammDetalleRowDto.Patente), 65),
            UiHelpers.AddTextCol("Proveedor", nameof(JcrammDetalleRowDto.Proveedor), 120),
            UiHelpers.AddTextCol("Ciudad", nameof(JcrammDetalleRowDto.Ciudad), 75),
            UiHelpers.AddNumberCol("Kilos", nameof(JcrammDetalleRowDto.Kilos), "N0", 65)
        ]);
        pnlDetalle.Controls.Add(_dgvDetalle);
        pnlDetalle.Controls.Add(_lblDetalleTotal);

        split.Controls.Add(pnlResumen, 0, 0);
        split.Controls.Add(pnlDetalle, 1, 0);

        Controls.Add(split);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        _btnConsultar.Enabled = _auth.HasPermission(PermisoSistema.ConsultarInformes);
        _btnExportarCerrar.Enabled = _auth.HasPermission(PermisoSistema.CerrarFolioJcramm)
            && _auth.HasPermission(PermisoSistema.ExportarExcel);
    }

    public override Task ActivateAsync(CancellationToken ct = default) => ConsultarAsync(useCustomOverride: false, ct);

    private async Task ConsultarAsync(bool useCustomOverride, CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            long? overrideFolio = useCustomOverride ? (long)_numUltimoFolio.Value : null;
            _lastReport = await _app.GetInformeJcrammAsync(overrideFolio, ct);

            if (!useCustomOverride)
            {
                _numUltimoFolio.Value = Math.Min(_numUltimoFolio.Maximum, Math.Max(0, _lastReport.UltimoFolio.Folio));
            }

            _lblFolioInfo.Text = string.Create(
                UiHelpers.EsCl,
                $"Último guardado: #{_lastReport.UltimoFolio.Folio} ({_lastReport.UltimoFolio.Fecha:dd/MM/yyyy})");

            _dgvResumen.DataSource = null;
            _dgvResumen.DataSource = _lastReport.Resumen;
            _dgvDetalle.DataSource = null;
            _dgvDetalle.DataSource = _lastReport.Detalle;

            _lblResumenTotal.Text = string.Create(UiHelpers.EsCl, $"RESUMEN ({_lastReport.Resumen.Count}): {_lastReport.TotalKilosResumen:N0} Kg");
            _lblDetalleTotal.Text = string.Create(UiHelpers.EsCl, $"DETALLE ({_lastReport.Detalle.Count} pesajes): {_lastReport.TotalKilosDetalle:N0} Kg");

            NotifyStatus($"JCRAMM: {_lastReport.Detalle.Count} pesajes pendientes desde folio > {(overrideFolio ?? _lastReport.UltimoFolio.Folio)}.");
        }, NotifyStatus, "Consultando despachos JCRAMM...");
    }

    private async Task ExportarYCerrarAsync()
    {
        if (_lastReport is null || _lastReport.Detalle.Count == 0)
        {
            MessageBox.Show(this, "No existen pesajes pendientes para exportar y cerrar.", "Sin Pendientes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        long nuevoCorte = _lastReport.Detalle[^1].Folio;
        DialogResult confirm = MessageBox.Show(
            this,
            $"Se exportarán {_lastReport.Detalle.Count} despachos ({_lastReport.TotalKilosDetalle.ToString("N0", UiHelpers.EsCl)} Kg) y el nuevo folio de cierre pasará a #{nuevoCorte}.\n\n¿Desea continuar?",
            "Confirmar Cierre JCRAMM",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"Informe_JCRAMM_Hasta_{nuevoCorte}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.CerrarYExportarJcrammAsync(path, _lastReport);
            NotifyStatus($"JCRAMM exportado y cerrado en folio #{nuevoCorte}.");
            MessageBox.Show(this, $"Informe JCRAMM generado y folio cerrado en #{nuevoCorte}:\n{path}", "Cierre Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await ConsultarAsync(useCustomOverride: false);
        }, NotifyStatus, "Exportando y cerrando folio JCRAMM...");
    }
}
