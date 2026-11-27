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
        ClientSize = new Size(520, 445);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel hdr = new() { Dock = DockStyle.Top, Height = 54, BackColor = AppTheme.HeaderDark, Padding = new Padding(16, 12, 16, 8) };
        hdr.Controls.Add(new Label
        {
            Text = "Configurar Análisis y Exportación (.xlsx)",
            Font = AppTheme.FontTitle,
            ForeColor = AppTheme.AccentGold,
            Dock = DockStyle.Fill
        });

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(16) };

        Label lblTit = new() { Text = "Título del Informe / Reporte:", Font = AppTheme.FontBodyBold, Location = new Point(16, 12), AutoSize = true };
        _txtTitulo.Text = defaultTitle;
        _txtTitulo.SetBounds(16, 34, 488, 28);
        AppTheme.ApplyInputStyle(_txtTitulo);

        GroupBox grpAlcance = new()
        {
            Text = "Alcance de Datos a Exportar",
            Font = AppTheme.FontBodyBold,
            Location = new Point(16, 70),
            Size = new Size(488, 86)
        };

        string selText = !string.IsNullOrWhiteSpace(selectionDescription)
            ? $"Solo lo seleccionado ({selectionDescription})"
            : $"Solo lo seleccionado actualmente ({selectedRowsCount} registro(s))";

        _rbSoloSeleccion.Text = selText;
        _rbSoloSeleccion.Font = AppTheme.FontBody;
        _rbSoloSeleccion.Location = new Point(14, 24);
        _rbSoloSeleccion.Size = new Size(460, 24);
        _rbSoloSeleccion.Enabled = selectedRowsCount > 0;

        _rbTodoFiltrado.Text = "Todos los registros visibles del filtro actual";
        _rbTodoFiltrado.Font = AppTheme.FontBody;
        _rbTodoFiltrado.Location = new Point(14, 52);
        _rbTodoFiltrado.Size = new Size(460, 24);

        if (selectedRowsCount > 0)
        {
            _rbSoloSeleccion.Checked = true;
        }
        else
        {
            _rbTodoFiltrado.Checked = true;
        }

        grpAlcance.Controls.AddRange([_rbSoloSeleccion, _rbTodoFiltrado]);

        Label lblSec = new() { Text = "Secciones / Análisis a Incluir en el Libro:", Font = AppTheme.FontBodyBold, Location = new Point(16, 164), AutoSize = true };
        _clbSecciones.Font = AppTheme.FontBody;
        _clbSecciones.Location = new Point(16, 186);
        _clbSecciones.Size = new Size(368, 126);
        foreach (string s in availableSections)
        {
            _clbSecciones.Items.Add(s, true);
        }

        _btnSelectAll = AppTheme.CreateButton("Todas", IconKind.Check, ButtonVariant.Secondary,
            (_, _) => SetAllSections(true), "Marcar todas las secciones", null, 110, 30);
        _btnSelectAll.Location = new Point(394, 186);

        _btnSelectNone = AppTheme.CreateButton("Ninguna", IconKind.Close, ButtonVariant.Secondary,
            (_, _) => SetAllSections(false), "Desmarcar todas las secciones", null, 110, 30);
        _btnSelectNone.Location = new Point(394, 224);

        _chkExcluirCeros.Text = "Excluir totales o filas con valor en cero (0 Kg)";
        _chkExcluirCeros.Checked = defaultExcludeZero;
        _chkExcluirCeros.Font = AppTheme.FontBodyBold;
        _chkExcluirCeros.Location = new Point(16, 320);
        _chkExcluirCeros.AutoSize = true;

        Panel footer = new() { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(14, 10, 14, 10) };
        _btnExportar = AppTheme.CreateButton("Generar Excel", IconKind.Excel, ButtonVariant.Success, null, "Continuar y guardar archivo Excel personalizado", null, 155, 34);
        _btnExportar.Location = new Point(220, 10);
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
            (_, _) => { DialogResult = DialogResult.Cancel; Close(); }, "Cancelar exportación", null, 115, 34);
        _btnCancelar.Location = new Point(385, 10);

        footer.Controls.AddRange([_btnExportar, _btnCancelar]);
        body.Controls.AddRange([lblTit, _txtTitulo, grpAlcance, lblSec, _clbSecciones, _btnSelectAll, _btnSelectNone, _chkExcluirCeros]);

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
