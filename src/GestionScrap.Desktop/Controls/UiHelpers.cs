using System.Globalization;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Utilidades visuales, filtros compartidos y ejecución asíncrona homogénea entre los módulos.
/// </summary>
public static class UiHelpers
{
    public static readonly CultureInfo EsCl = CultureInfo.GetCultureInfo("es-CL");

    private sealed record NullablePlantaOpt(Planta? Planta, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record NullableTipoOpt(TipoPesaje? Tipo, string Label)
    {
        public override string ToString() => Label;
    }

    public static Panel BuildFilterCard(int height = 64) => new()
    {
        Dock = DockStyle.Top,
        Height = height,
        BackColor = AppTheme.CardSurface,
        Padding = new Padding(12, 8, 12, 8),
        Margin = new Padding(0, 0, 0, 10)
    };

    public static void PopulatePlantaCombo(ComboBox cmb, bool includeTalcahuano = true)
    {
        cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        cmb.Font = AppTheme.FontBody;
        cmb.Items.Clear();
        cmb.Items.Add(new PlantaComboItem(Planta.Placilla, "Placilla"));
        cmb.Items.Add(new PlantaComboItem(Planta.Santiago, "Santiago"));
        if (includeTalcahuano)
        {
            cmb.Items.Add(new PlantaComboItem(Planta.Talcahuano, "Talcahuano"));
        }

        cmb.SelectedIndex = 0;
    }

    public static Planta GetSelectedPlanta(ComboBox cmb) =>
        cmb.SelectedItem is PlantaComboItem item ? item.Planta : Planta.Placilla;

    public static void SelectPlanta(ComboBox cmb, Planta planta)
    {
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            if (cmb.Items[i] is PlantaComboItem item && item.Planta == planta)
            {
                cmb.SelectedIndex = i;
                return;
            }
        }
    }

    public static void PopulatePlantaFilterCombo(ComboBox cmb)
    {
        cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        cmb.Font = AppTheme.FontBody;
        cmb.Items.Clear();
        cmb.Items.Add(new NullablePlantaOpt(null, "Todas las Plantas"));
        cmb.Items.Add(new NullablePlantaOpt(Planta.Santiago, "Santiago (Placilla 2)"));
        cmb.Items.Add(new NullablePlantaOpt(Planta.Placilla, "Placilla"));
        cmb.Items.Add(new NullablePlantaOpt(Planta.Talcahuano, "Talcahuano"));
        cmb.SelectedIndex = 0;
    }

    public static Planta? GetSelectedNullablePlanta(ComboBox cmb) =>
        (cmb.SelectedItem as NullablePlantaOpt)?.Planta;

    public static void PopulateTipoFilterCombo(ComboBox cmb, bool defaultRecepcion = false)
    {
        cmb.DropDownStyle = ComboBoxStyle.DropDownList;
        cmb.Font = AppTheme.FontBody;
        cmb.Items.Clear();
        if (defaultRecepcion)
        {
            cmb.Items.Add(new NullableTipoOpt(TipoPesaje.RecepcionCarga, "Solo Recepción / Compras"));
            cmb.Items.Add(new NullableTipoOpt(null, "Todos los Movimientos"));
        }
        else
        {
            cmb.Items.Add(new NullableTipoOpt(null, "Todos los Tipos"));
            cmb.Items.Add(new NullableTipoOpt(TipoPesaje.RecepcionCarga, "Recepción / Compras"));
        }

        cmb.Items.Add(new NullableTipoOpt(TipoPesaje.DespachoCarga, "Despacho de Carga"));
        cmb.Items.Add(new NullableTipoOpt(TipoPesaje.MovimientoInterno, "Movimiento Interno"));
        cmb.SelectedIndex = 0;
    }

    public static TipoPesaje? GetSelectedNullableTipo(ComboBox cmb) =>
        (cmb.SelectedItem as NullableTipoOpt)?.Tipo;

    public static void FormatVarianceCell(DataGridViewCellFormattingEventArgs e)
    {
        if (e.Value is not double d || e.CellStyle is null)
        {
            return;
        }

        e.CellStyle.Font = AppTheme.FontBodyBold;
        if (d > 0.05)
        {
            e.CellStyle.ForeColor = AppTheme.AccentEmerald;
        }
        else if (d < -0.05)
        {
            e.CellStyle.ForeColor = AppTheme.AccentAmber;
        }
        else
        {
            e.CellStyle.ForeColor = AppTheme.TextSecondary;
        }
    }

    public static Panel CreateKpiCard(string title, Color accentColor, out Label valueLabel, out Label subtitleLabel)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Margin = new Padding(4, 3, 4, 3),
            Padding = new Padding(12, 8, 12, 8),
            BorderStyle = BorderStyle.FixedSingle
        };

        Panel bar = new()
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = accentColor
        };

        Label lblTitle = new()
        {
            Text = title,
            Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
            ForeColor = AppTheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 19
        };

        valueLabel = new Label
        {
            Text = "0",
            Font = AppTheme.FontKpiValue,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 31
        };

        subtitleLabel = new Label
        {
            Text = "Sin datos",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Dock = DockStyle.Fill
        };

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0) };
        body.Controls.Add(subtitleLabel);
        body.Controls.Add(valueLabel);
        body.Controls.Add(lblTitle);

        card.Controls.Add(body);
        card.Controls.Add(bar);
        return card;
    }

    public static string? PromptSaveExcelPath(IWin32Window owner, string defaultFileName)
    {
        using SaveFileDialog sfd = new()
        {
            Title = "Guardar informe Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = defaultFileName
        };

        return sfd.ShowDialog(owner) == DialogResult.OK ? sfd.FileName : null;
    }

    public static async Task RunSafeAsync(
        Control host,
        Func<Task> action,
        Action<string>? notifyStatus = null,
        string? runningStatus = null)
    {
        Cursor? prevCursor = host.Cursor;
        try
        {
            host.UseWaitCursor = true;
            if (!string.IsNullOrWhiteSpace(runningStatus))
            {
                notifyStatus?.Invoke(runningStatus);
            }

            await action();
        }
        catch (UnauthorizedAccessException ex)
        {
            notifyStatus?.Invoke("Operación restringida por perfil de seguridad.");
            MessageBox.Show(host, ex.Message, "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            notifyStatus?.Invoke("Operación cancelada.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            notifyStatus?.Invoke($"Error: {ex.Message}");
            MessageBox.Show(host, ex.Message, "Validación de Proceso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            notifyStatus?.Invoke($"Error inesperado: {ex.Message}");
            MessageBox.Show(host, $"Ocurrió un error al ejecutar la operación:\n\n{ex.Message}", "Error del Sistema", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            host.UseWaitCursor = false;
            host.Cursor = prevCursor;
        }
    }

    public static DataGridViewTextBoxColumn AddTextCol(string header, string dataProperty, float fillWeight = 100f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        SortMode = DataGridViewColumnSortMode.Automatic
    };

    public static DataGridViewTextBoxColumn AddNumberCol(string header, string dataProperty, string format = "N0", float fillWeight = 65f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleRight,
            Format = format,
            FormatProvider = EsCl
        }
    };

    public static DataGridViewTextBoxColumn AddCurrencyCol(string header, string dataProperty, float fillWeight = 75f) =>
        AddNumberCol(header, dataProperty, "C0", fillWeight);

    public static DataGridViewTextBoxColumn AddDateCol(string header, string dataProperty, float fillWeight = 70f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Format = "dd-MM-yyyy",
            FormatProvider = EsCl
        }
    };
}
