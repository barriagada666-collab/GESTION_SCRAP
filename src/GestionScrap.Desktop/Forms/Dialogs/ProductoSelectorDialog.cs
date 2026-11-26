using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class ProductoSelectorDialog : Form
{
    private readonly ScrapApplicationService _app;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtBuscar = new();
    private readonly DoubleBufferedDataGridView _grid = new();
    private readonly Label _lblCount = new();
    private IReadOnlyList<Producto> _allProductos = [];

    public Producto? SelectedProducto { get; private set; }

    public ProductoSelectorDialog(ScrapApplicationService app)
    {
        _app = app;
        Text = "Catálogo de Materiales y Productos";
        Size = new Size(720, 490);
        MinimumSize = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel top = new() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(14), BackColor = Color.White };
        Label lbl = new() { Text = "Filtrar material:", Font = AppTheme.FontBodyBold, Location = new Point(14, 21), AutoSize = true };
        _txtBuscar.SetBounds(130, 17, 560, 28);
        _txtBuscar.PlaceholderText = "Código (ej. 5300000004), descripción o grupo...";
        AppTheme.ApplyInputStyle(_txtBuscar);
        _txtBuscar.TextChanged += (_, _) => ApplyFilter();
        top.Controls.AddRange([lbl, _txtBuscar]);

        _grid.Dock = DockStyle.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Codigo", HeaderText = "Código", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Descripcion", HeaderText = "Descripción Producto", FillWeight = 56 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Grupo", HeaderText = "Familia / Grupo", FillWeight = 22 });
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ConfirmSelection();
            }
        };

        Panel footer = new() { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(14, 10, 14, 10), BackColor = Color.White };
        _lblCount.Location = new Point(14, 18);
        _lblCount.AutoSize = true;
        _lblCount.Font = AppTheme.FontBody;
        _lblCount.ForeColor = AppTheme.TextSecondary;

        Button btnCancelar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar catálogo", _toolTip, 115, 34);
        btnCancelar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancelar.Location = new Point(452, 10);

        Button btnElegir = AppTheme.CreateButton("Seleccionar", IconKind.Check, ButtonVariant.Success, (_, _) => ConfirmSelection(),
            "Usar producto seleccionado", _toolTip, 130, 34);
        btnElegir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnElegir.Location = new Point(575, 10);

        footer.Controls.AddRange([_lblCount, btnCancelar, btnElegir]);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(footer);

        AcceptButton = btnElegir;
        CancelButton = btnCancelar;
        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _allProductos = await _app.GetProductosAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string term = _txtBuscar.Text.Trim();
        List<Producto> filtered =
        [
            .. _allProductos.Where(p =>
                string.IsNullOrEmpty(term) ||
                p.Codigo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Descripcion.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Grupo.Contains(term, StringComparison.OrdinalIgnoreCase))
        ];

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (Producto p in filtered)
        {
            int idx = _grid.Rows.Add(p.Codigo, p.Descripcion, p.Grupo);
            _grid.Rows[idx].Tag = p;
        }
        _grid.ResumeLayout();
        _lblCount.Text = $"{filtered.Count} productos disponibles";
    }

    private void ConfirmSelection()
    {
        if (_grid.CurrentRow?.Tag is Producto p)
        {
            SelectedProducto = p;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show(this, "Seleccione un material de la grilla.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
