using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Forms.Dialogs;

public sealed class ClienteSelectorDialog : Form
{
    private readonly ScrapApplicationService _app;
    private readonly ToolTip _toolTip = new();
    private readonly ComboBox _cmbPlanta = new();
    private readonly TextBox _txtBuscar = new();
    private readonly DoubleBufferedDataGridView _grid = new();
    private readonly Label _lblCount = new();
    private IReadOnlyList<Cliente> _allClientes = [];

    public Cliente? SelectedCliente { get; private set; }

    public ClienteSelectorDialog(ScrapApplicationService app, Planta plantaInicial)
    {
        _app = app;
        Text = "Selector de Clientes y Proveedores";
        Size = new Size(760, 520);
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Panel top = new() { Dock = DockStyle.Top, Height = 92, Padding = new Padding(14), BackColor = Color.White };

        Label lblPlanta = new() { Text = "Planta:", Font = AppTheme.FontBodyBold, Location = new Point(14, 16), AutoSize = true };
        _cmbPlanta.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPlanta.Font = AppTheme.FontBody;
        _cmbPlanta.SetBounds(72, 12, 160, 28);
        _cmbPlanta.Items.AddRange(["Todas", "Placilla", "Santiago", "Talcahuano"]);
        _cmbPlanta.SelectedIndex = plantaInicial switch
        {
            Planta.Placilla => 1,
            Planta.Santiago => 2,
            Planta.Talcahuano => 3,
            _ => 0
        };
        _cmbPlanta.SelectedIndexChanged += (_, _) => ApplyFilter();

        Label lblSearch = new() { Text = "Buscar (RUT / Razón Social):", Font = AppTheme.FontBodyBold, Location = new Point(250, 16), AutoSize = true };
        _txtBuscar.SetBounds(14, 50, 620, 28);
        _txtBuscar.PlaceholderText = "Escriba RUT o parte del nombre del proveedor...";
        AppTheme.ApplyInputStyle(_txtBuscar);
        _txtBuscar.TextChanged += (_, _) => ApplyFilter();

        Button btnRefrescar = AppTheme.CreateButton("Actualizar", IconKind.Refresh, ButtonVariant.Secondary, async (_, _) => await LoadDataAsync(), "Recargar catálogo de clientes", _toolTip, 105, 28);
        btnRefrescar.Location = new Point(642, 50);

        top.Controls.AddRange([lblPlanta, _cmbPlanta, lblSearch, _txtBuscar, btnRefrescar]);

        _grid.Dock = DockStyle.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rut", HeaderText = "RUT", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RazonSocial", HeaderText = "Razón Social / Proveedor", FillWeight = 48 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ciudad", HeaderText = "Ciudad", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Planta", HeaderText = "Planta", FillWeight = 14 });
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

        Button btnCerrar = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cerrar sin seleccionar", _toolTip, 115, 34);
        btnCerrar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCerrar.Location = new Point(492, 10);

        Button btnElegir = AppTheme.CreateButton("Seleccionar", IconKind.Check, ButtonVariant.Primary, (_, _) => ConfirmSelection(),
            "Usar cliente seleccionado", _toolTip, 130, 34);
        btnElegir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnElegir.Location = new Point(615, 10);

        footer.Controls.AddRange([_lblCount, btnCerrar, btnElegir]);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(footer);

        AcceptButton = btnElegir;
        CancelButton = btnCerrar;
        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _allClientes = await _app.GetClientesAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Planta? pFilter = _cmbPlanta.SelectedIndex switch
        {
            1 => Planta.Placilla,
            2 => Planta.Santiago,
            3 => Planta.Talcahuano,
            _ => null
        };

        string term = _txtBuscar.Text.Trim();
        List<Cliente> filtered =
        [
            .. _allClientes.Where(c =>
            {
                bool matchPlanta = !pFilter.HasValue || !c.PlantaAsociada.HasValue || c.PlantaAsociada == pFilter;
                bool matchText = string.IsNullOrEmpty(term) ||
                                 c.Rut.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                 c.RazonSocial.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                 c.Ciudad.Contains(term, StringComparison.OrdinalIgnoreCase);
                return matchPlanta && matchText;
            })
        ];

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (Cliente c in filtered)
        {
            int idx = _grid.Rows.Add(c.RutFormateado, c.RazonSocial, c.Ciudad, c.PlantaAsociada?.ToShortTitle() ?? "Todas");
            _grid.Rows[idx].Tag = c;
        }
        _grid.ResumeLayout();
        _lblCount.Text = $"{filtered.Count} clientes encontrados (doble clic para seleccionar)";
    }

    private void ConfirmSelection()
    {
        if (_grid.CurrentRow?.Tag is Cliente c)
        {
            SelectedCliente = c;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show(this, "Seleccione un cliente de la lista.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
