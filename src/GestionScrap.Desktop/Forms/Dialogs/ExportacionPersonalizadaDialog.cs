using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo modal para configurar exportaciones personalizadas, selección específica de filas y exclusión de ceros.
/// </summary>
public sealed class ExportacionPersonalizadaDialog : Form
{
    private readonly TextBox _txtTitulo = new();
    private readonly RadioButton _rbSoloSeleccion = new();
    private readonly RadioButton _rbTodoFiltrado = new();
    private readonly CheckBox _chkExcluirCeros = new();
    private readonly CheckedListBox _clbSecciones = new() { CheckOnClick = true };
    private readonly Button _btnSelectAll;
    private readonly Button _btnSelectNone;
    private readonly Button _btnExportar;
    private readonly Button _btnCancelar;

    public string TituloPersonalizado => _txtTitulo.Text.Trim();
    public bool SoloSeleccionActual => _rbSoloSeleccion.Checked;
    public bool ExcluirCeros => _chkExcluirCeros.Checked;
    public IReadOnlyList<string> SeccionesSeleccionadas => [.. _clbSecciones.CheckedItems.Cast<string>()];

    public ExportacionPersonalizadaDialog(
        string defaultTitle,
        int selectedRowsCount,
        IReadOnlyList<string> availableSections,
        bool defaultExcludeZero = false,
        string? selectionDescription = null)
    {
        Text = "Exportación Personalizada a Excel";
        ClientSize = new Size(580, 485);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel hdr = new() { Dock = DockStyle.Top, Height = 56, BackColor = AppTheme.HeaderDark, Padding = new Padding(16, 12, 16, 8) };
        hdr.Controls.Add(new Label
        {
            Text = "Configurar Análisis y Exportación (.xlsx)",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.AccentGold,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Size = new Size(ClientSize.Width, ClientSize.Height - 114),
            Padding = new Padding(16, 12, 16, 10)
        };

        Label lblTit = new() { Text = "Título del Informe / Reporte:", Font = AppTheme.FontBodyBold, Location = new Point(16, 12), AutoSize = true };
        _txtTitulo.Text = defaultTitle;
        _txtTitulo.SetBounds(16, 36, 548, 28);
        _txtTitulo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        AppTheme.ApplyInputStyle(_txtTitulo);

        GroupBox grpAlcance = new()
        {
            Text = "Alcance de Datos a Exportar",
            Font = AppTheme.FontBodyBold,
            Location = new Point(16, 74),
            Size = new Size(548, 90),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        string selText = !string.IsNullOrWhiteSpace(selectionDescription)
            ? $"Solo lo seleccionado ({selectionDescription})"
            : $"Solo lo seleccionado actualmente ({selectedRowsCount} registro(s))";

        _rbSoloSeleccion.Text = selText;
        _rbSoloSeleccion.Font = AppTheme.FontBody;
        _rbSoloSeleccion.Location = new Point(14, 26);
        _rbSoloSeleccion.Size = new Size(520, 24);
        _rbSoloSeleccion.Enabled = selectedRowsCount > 0;

        _rbTodoFiltrado.Text = "Todos los registros visibles del filtro actual";
        _rbTodoFiltrado.Font = AppTheme.FontBody;
        _rbTodoFiltrado.Location = new Point(14, 54);
        _rbTodoFiltrado.Size = new Size(520, 24);

        if (selectedRowsCount > 0)
        {
            _rbSoloSeleccion.Checked = true;
        }
        else
        {
            _rbTodoFiltrado.Checked = true;
        }

        grpAlcance.Controls.AddRange([_rbSoloSeleccion, _rbTodoFiltrado]);

        Label lblSec = new() { Text = "Secciones / Análisis a Incluir en el Libro:", Font = AppTheme.FontBodyBold, Location = new Point(16, 174), AutoSize = true };

        TableLayoutPanel secRow = new()
        {
            Location = new Point(16, 198),
            Size = new Size(548, 134),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        secRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        secRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        secRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _clbSecciones.Font = AppTheme.FontBody;
        _clbSecciones.Dock = DockStyle.Fill;
        _clbSecciones.Margin = new Padding(0, 0, 10, 0);
        foreach (string s in availableSections)
        {
            _clbSecciones.Items.Add(s, true);
        }

        _btnSelectAll = AppTheme.CreateButton("Todas", IconKind.Check, ButtonVariant.Secondary,
            (_, _) => SetAllSections(true), "Marcar todas las secciones", null, 118, 32);
        _btnSelectAll.Margin = new Padding(0, 0, 0, 8);

        _btnSelectNone = AppTheme.CreateButton("Ninguna", IconKind.Close, ButtonVariant.Secondary,
            (_, _) => SetAllSections(false), "Desmarcar todas las secciones", null, 118, 32);
        _btnSelectNone.Margin = Padding.Empty;

        FlowLayoutPanel secBtns = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        secBtns.Controls.AddRange([_btnSelectAll, _btnSelectNone]);

        secRow.Controls.Add(_clbSecciones, 0, 0);
        secRow.Controls.Add(secBtns, 1, 0);

        _chkExcluirCeros.Text = "Excluir totales o filas con valor en cero (0 Kg)";
        _chkExcluirCeros.Checked = defaultExcludeZero;
        _chkExcluirCeros.Font = AppTheme.FontBodyBold;
        _chkExcluirCeros.Location = new Point(16, 342);
        _chkExcluirCeros.AutoSize = true;

        _btnExportar = AppTheme.CreateButton("Generar Excel", IconKind.Excel, ButtonVariant.Success, null,
            "Continuar y guardar archivo Excel personalizado", null, 0, 34);
        _btnExportar.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtTitulo.Text))
            {
                MessageBox.Show(this, "Ingrese un título válido para el reporte.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_clbSecciones.CheckedItems.Count == 0)
            {
                MessageBox.Show(this, "Seleccione al menos una sección para exportar.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary,
            (_, _) => { DialogResult = DialogResult.Cancel; Close(); }, "Cancelar exportación", null, 0, 34);

        Panel footer = AppTheme.CreateDialogFooter(_btnCancelar, _btnExportar);
        body.Controls.AddRange([lblTit, _txtTitulo, grpAlcance, lblSec, secRow, _chkExcluirCeros]);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(hdr);

        AcceptButton = _btnExportar;
        CancelButton = _btnCancelar;
    }

    private void SetAllSections(bool isChecked)
    {
        for (int i = 0; i < _clbSecciones.Items.Count; i++)
        {
            _clbSecciones.SetItemChecked(i, isChecked);
        }
    }
}
