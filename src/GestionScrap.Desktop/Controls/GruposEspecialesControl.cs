using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 3: Matriz consolidada de 8 grupos estratégicos especiales (Retail, Mix, Agrícola, Pesquera, Agrosuper).
/// Separa Pesquera y Agrosuper en paneles independientes corrigiendo la colisión de grillas del legacy VBA.
/// </summary>
public sealed class GruposEspecialesControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly Button _btnConsultar;
    private readonly Label _lblGranTotal = new();
    private readonly TableLayoutPanel _cardsHost = new();

    public GruposEspecialesControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel filterCard = UiHelpers.BuildFilterCard(62);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        DateTime today = DateTime.Today;
        _dtpDesde.Value = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        _dtpHasta.Value = today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _btnConsultar = AppTheme.CreateButton("Consultar 8 Grupos", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await ConsultarAsync(), "Calcula las 8 segmentaciones de clientes estratégicos", Tip, 165, 34);

        _lblGranTotal.Text = "TOTAL SEGMENTOS: 0 Kg";
        _lblGranTotal.Font = AppTheme.FontSection;
        _lblGranTotal.ForeColor = AppTheme.AccentCyan;
        _lblGranTotal.AutoSize = true;
        _lblGranTotal.Margin = new Padding(18, 9, 0, 0);

        FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, WrapContents = false };
        flow.Controls.AddRange(
        [
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 9, 4, 0) },
            _dtpHasta,
            _btnConsultar,
            _lblGranTotal
        ]);
        filterCard.Controls.Add(flow);

        Panel scrollWrapper = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        _cardsHost.Dock = DockStyle.Top;
        _cardsHost.AutoSize = true;
        _cardsHost.ColumnCount = 2;
        _cardsHost.RowCount = 4;
        _cardsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _cardsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (int r = 0; r < 4; r++)
        {
            _cardsHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
        }

        scrollWrapper.Controls.Add(_cardsHost);
        Controls.Add(scrollWrapper);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        _btnConsultar.Enabled = _auth.HasPermission(PermisoSistema.ConsultarInformes);
    }

    public override Task ActivateAsync(CancellationToken ct = default) => ConsultarAsync(ct);

    private async Task ConsultarAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
            DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);
            IReadOnlyList<GrupoEspecialCardDto> cards = await _app.GetGruposEspecialesAsync(desde, hasta, ct);

            _cardsHost.SuspendLayout();
            try
            {
                _cardsHost.Controls.Clear();
                for (int i = 0; i < cards.Count; i++)
                {
                    GrupoEspecialCardDto cardDto = cards[i];
                    Panel card = BuildGroupCard(cardDto);
                    _cardsHost.Controls.Add(card, i % 2, i / 2);
                }
            }
            finally
            {
                _cardsHost.ResumeLayout(true);
            }

            long total = cards.Sum(c => c.TotalKilos);
            _lblGranTotal.Text = string.Create(UiHelpers.EsCl, $"TOTAL SEGMENTOS: {total:N0} Kg");
            NotifyStatus($"8 grupos estratégicos calculados: {total.ToString("N0", UiHelpers.EsCl)} Kg.");
        }, NotifyStatus, "Calculando grupos especiales...");
    }

    private static Panel BuildGroupCard(GrupoEspecialCardDto dto)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(5),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };

        Label lblTitle = new()
        {
            Text = $"{dto.Titulo}  ({dto.VistaReferencia})",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.AccentBlue,
            Dock = DockStyle.Top,
            Height = 26
        };

        DoubleBufferedDataGridView dgv = new()
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false
        };
        dgv.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Cliente / Sucursal", nameof(GrupoEspecialItemDto.Cliente), 140),
            UiHelpers.AddNumberCol("Kilos Netos", nameof(GrupoEspecialItemDto.Kilos), "N0", 65)
        ]);
        dgv.DataSource = dto.Items;

        Label lblFooter = new()
        {
            Text = string.Create(UiHelpers.EsCl, $"Total Grupo: {dto.TotalKilos:N0} Kg  ({dto.Items.Count} clientes)"),
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight
        };

        card.Controls.Add(dgv);
        card.Controls.Add(lblFooter);
        card.Controls.Add(lblTitle);
        return card;
    }
}
