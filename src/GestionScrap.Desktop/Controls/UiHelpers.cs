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

    /// <summary>
    /// Crea una tarjeta de filtros autoajustable en altura que envuelve controles y botones sin recortes en cualquier DPI.
    /// </summary>
    public static Panel BuildAutoFilterCard(params FlowLayoutPanel[] rows)
    {
        Panel card = new()
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            BackColor = AppTheme.CardSurface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 10),
            Height = 48
        };

        TableLayoutPanel stack = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = rows.Length,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (int i = 0; i < rows.Length; i++)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            FlowLayoutPanel row = rows[i];
            row.Dock = DockStyle.Fill;
            row.AutoSize = true;
            row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            row.WrapContents = true;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Margin = i == 0 ? new Padding(0, 0, 0, 4) : new Padding(0, 4, 0, 0);
            stack.Controls.Add(row, 0, i);
        }

        bool syncing = false;
        int lastAvail = -1;

        void SyncCardHeight(bool force = false)
        {
            if (syncing)
            {
                return;
            }

            int avail = Math.Max(200, card.ClientSize.Width - card.Padding.Horizontal);
            if (!force && avail == lastAvail)
            {
                return;
            }

            syncing = true;
            try
            {
                lastAvail = avail;
                stack.MaximumSize = new Size(avail, 0);

                int totalStackHeight = 0;
                foreach (FlowLayoutPanel row in rows)
                {
                    row.MaximumSize = new Size(avail, 0);
                    Size pref = row.GetPreferredSize(new Size(avail, 0));
                    totalStackHeight += pref.Height + row.Margin.Vertical;
                }

                int borderAllowance = card.BorderStyle == BorderStyle.None ? 0 : 2;
                int needed = totalStackHeight + card.Padding.Vertical + borderAllowance + 4;
                if (card.Height != needed)
                {
                    card.Height = needed;
                }
            }
            finally
            {
                syncing = false;
            }
        }

        card.Controls.Add(stack);
        card.HandleCreated += (_, _) => SyncCardHeight(force: true);
        card.SizeChanged += (_, _) => SyncCardHeight(force: false);
        return card;
    }

    public static FlowLayoutPanel CreateFilterRow(params Control[] controls)
    {
        FlowLayoutPanel row = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row.Controls.AddRange(controls);
        return row;
    }

    public static Label CreateFilterLabel(string text, int leftGap = 8) => new()
    {
        Text = text,
        Font = AppTheme.FontBodyBold,
        ForeColor = AppTheme.TextPrimary,
        AutoSize = true,
        Margin = new Padding(leftGap, 8, 4, 4)
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
            Font = AppTheme.FontSmallBold,
            ForeColor = AppTheme.TextSecondary,
            AutoEllipsis = true,
            Dock = DockStyle.Top,
            Height = 20
        };

        valueLabel = new Label
        {
            Text = "0",
            Font = AppTheme.FontKpiValue,
            ForeColor = AppTheme.TextPrimary,
            AutoEllipsis = true,
            Dock = DockStyle.Top,
            Height = 34
        };

        subtitleLabel = new Label
        {
            Text = "Sin datos",
            Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextSecondary,
            AutoEllipsis = true,
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

    private static int ComputeHeaderMinWidth(string header, int floor = 56)
    {
        int measured = TextRenderer.MeasureText(
            header,
            AppTheme.FontSmallBold,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        return Math.Clamp(measured + 22, floor, 210);
    }

    public static DataGridViewTextBoxColumn AddTextCol(string header, string dataProperty, float fillWeight = 100f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        MinimumWidth = ComputeHeaderMinWidth(header, 62),
        SortMode = DataGridViewColumnSortMode.Automatic
    };

    public static DataGridViewTextBoxColumn AddNumberCol(string header, string dataProperty, string format = "N0", float fillWeight = 65f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        MinimumWidth = ComputeHeaderMinWidth(header, 54),
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleRight,
            Format = format,
            FormatProvider = EsCl
        }
    };

    public static DataGridViewTextBoxColumn AddCurrencyCol(string header, string dataProperty, float fillWeight = 75f) =>
        AddNumberCol(header, dataProperty, "C0", fillWeight);

    public static DateTime CurrentMonthStartLocal => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public static DateTime CurrentDateLocal => DateTime.Today;

    /// <summary>
    /// Selecciona automáticamente la fila bajo el puntero al hacer clic derecho antes de desplegar un ContextMenuStrip.
    /// </summary>
    public static void AttachGridContextMenu(DataGridView dgv, ContextMenuStrip menu)
    {
        dgv.ContextMenuStrip = menu;
        dgv.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.RowIndex < dgv.Rows.Count)
            {
                if (!dgv.Rows[e.RowIndex].Selected)
                {
                    dgv.ClearSelection();
                    dgv.Rows[e.RowIndex].Selected = true;
                }

                if (e.ColumnIndex >= 0 && e.ColumnIndex < dgv.Columns.Count)
                {
                    dgv.CurrentCell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            }
        };
    }

    /// <summary>
    /// Muestra un diálogo modal limpio para solicitar un motivo auditable o texto breve.
    /// </summary>
    public static string? PromptAuditReason(IWin32Window owner, string title, string prompt, string defaultValue = "")
    {
        using Form dlg = new()
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(470, 210),
            BackColor = AppTheme.CanvasLight
        };

        Label lbl = new()
        {
            Text = prompt,
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.TextPrimary,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 0, 0, 8)
        };

        TextBox txt = new()
        {
            Multiline = true,
            Height = 76,
            Dock = DockStyle.Fill,
            Text = defaultValue,
            ScrollBars = ScrollBars.Vertical
        };
        AppTheme.ApplyInputStyle(txt);

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 14, 16, 10)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        body.Controls.Add(lbl, 0, 0);
        body.Controls.Add(txt, 0, 1);

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, null, null, null, 0, 34);
        btnCancel.DialogResult = DialogResult.Cancel;
        Button btnOk = AppTheme.CreateButton("Confirmar", IconKind.Check, ButtonVariant.Primary, (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text))
            {
                MessageBox.Show(dlg, "Debe ingresar un motivo válido para continuar.", "Campo Obligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txt.Focus();
                return;
            }

            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        }, null, null, 0, 34);

        Panel footer = AppTheme.CreateDialogFooter(btnCancel, btnOk);
        dlg.CancelButton = btnCancel;
        dlg.Controls.Add(body);
        dlg.Controls.Add(footer);

        return dlg.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }

    /// <summary>
    /// Exporta las filas seleccionadas (o todas si no hay selección) de cualquier DataGridView a Excel (.xlsx).
    /// </summary>
    public static void ExportDataGridViewToExcel(
        IWin32Window owner,
        DataGridView dgv,
        GestionScrap.Domain.Interfaces.IExcelExportService exporter,
        string reportTitle,
        string sheetName,
        bool onlySelected,
        Action<string>? notifyStatus = null)
    {
        List<DataGridViewColumn> visibleCols = [.. dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex)];
        if (visibleCols.Count == 0 || dgv.Rows.Count == 0)
        {
            MessageBox.Show(owner, "No hay filas disponibles para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<DataGridViewRow> targetRows = onlySelected && dgv.SelectedRows.Count > 0
            ? [.. dgv.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index)]
            : [.. dgv.Rows.Cast<DataGridViewRow>()];

        string? path = PromptSaveExcelPath(owner, $"{sheetName.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string[] headers = [.. visibleCols.Select(c => c.HeaderText)];
        List<object?[]> rows = [.. targetRows.Select(r => visibleCols.Select(c => r.Cells[c.Index].Value).ToArray())];

        string saved = exporter.ExportReportePersonalizado(
            path,
            new GestionScrap.Domain.Entities.ReportePersonalizadoPayload(
                reportTitle,
                $"Exportado el {DateTime.Now:dd-MM-yyyy HH:mm} · {rows.Count:N0} registro(s)",
                [("Registros Exportados", rows.Count.ToString("N0", EsCl), onlySelected ? "Selección del usuario" : "Tabla completa")],
                [new GestionScrap.Domain.Entities.ExportTableSection(sheetName, reportTitle, headers, rows)]));

        notifyStatus?.Invoke($"Exportado Excel: {saved}");
        MessageBox.Show(owner, $"Archivo Excel generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static DataGridViewTextBoxColumn AddDateCol(string header, string dataProperty, float fillWeight = 70f) => new()
    {
        HeaderText = header,
        DataPropertyName = dataProperty,
        Name = "col_" + dataProperty,
        FillWeight = fillWeight,
        MinimumWidth = Math.Max(82, ComputeHeaderMinWidth(header, 82)),
        DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Format = "dd-MM-yyyy",
            FormatProvider = EsCl
        }
    };
}
