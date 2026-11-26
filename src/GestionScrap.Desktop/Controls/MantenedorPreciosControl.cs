using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Tab 6: Mantenedor CRUD de precios pactados por proveedor y producto con validaciones reales de actualización/inserción.
/// </summary>
public sealed class MantenedorPreciosControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 130 };
    private readonly TextBox _txtFiltro = new() { Width = 220 };
    private readonly Button _btnRefrescar;

    private readonly TextBox _txtCodProd = new() { Width = 100 };
    private readonly Button _btnSelProd;
    private readonly TextBox _txtRut = new() { Width = 120 };
    private readonly Button _btnSelCliente;
    private readonly TextBox _txtRazonSocial = new() { Width = 260 };
    private readonly NumericUpDown _numPrecio = new();

    private readonly Button _btnInsertar;
    private readonly Button _btnActualizar;
    private readonly Button _btnEliminar;
    private readonly Button _btnLimpiar;

    private readonly DoubleBufferedDataGridView _dgv = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private IReadOnlyList<PrecioProveedor> _allItems = [];

    public MantenedorPreciosControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        Panel topCard = UiHelpers.BuildFilterCard(58);
        topCard.BorderStyle = BorderStyle.FixedSingle;
        UiHelpers.PopulatePlantaCombo(_cmbPlanta, includeTalcahuano: false);
        _cmbPlanta.SelectedIndexChanged += async (_, _) => await ActivateAsync();

        AppTheme.ApplyInputStyle(_txtFiltro);
        _txtFiltro.PlaceholderText = "Filtrar por RUT, Razón Social o Producto...";
        _txtFiltro.TextChanged += (_, _) => ApplyFilter();

        _btnRefrescar = AppTheme.CreateButton("Actualizar Lista", IconKind.Refresh, ButtonVariant.Secondary,
            async (_, _) => await ActivateAsync(), "Recarga las tarifas pactadas desde SQLite", Tip, 150, 34);

        FlowLayoutPanel topFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        topFlow.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 6, 0) },
            _cmbPlanta,
            new Label { Text = "Buscar:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(16, 9, 6, 0) },
            _txtFiltro,
            _btnRefrescar
        ]);
        topCard.Controls.Add(topFlow);

        Panel editorCard = new()
        {
            Dock = DockStyle.Top,
            Height = 132,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(12),
            Margin = new Padding(0, 6, 0, 6),
            BorderStyle = BorderStyle.FixedSingle
        };

        AppTheme.ApplyInputStyle(_txtCodProd);
        _txtCodProd.PlaceholderText = "Ej: S0400";
        _txtCodProd.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OpenProductoSelector();
            }
        };

        _btnSelProd = AppTheme.CreateButton("Producto", IconKind.Layers, ButtonVariant.Secondary,
            (_, _) => OpenProductoSelector(), "Seleccionar código de material del catálogo", Tip, 110, 30);

        AppTheme.ApplyInputStyle(_txtRut);
        _txtRut.PlaceholderText = "Ej: 76123456-K";
        _txtRut.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OpenClienteSelector();
            }
        };

        _btnSelCliente = AppTheme.CreateButton("Cliente", IconKind.User, ButtonVariant.Secondary,
            (_, _) => OpenClienteSelector(), "Seleccionar proveedor desde maestro de clientes", Tip, 105, 30);

        AppTheme.ApplyInputStyle(_txtRazonSocial);
        _txtRazonSocial.PlaceholderText = "Razón Social del Proveedor";

        _numPrecio.Width = 120;
        _numPrecio.Font = AppTheme.FontMono;
        _numPrecio.Maximum = 99_999_999;
        _numPrecio.Minimum = 0;
        _numPrecio.ThousandsSeparator = true;

        _btnInsertar = AppTheme.CreateButton("Insertar Nuevo", IconKind.Plus, ButtonVariant.Success,
            async (_, _) => await GuardarAsync(permitirActualizar: false), "Registra una nueva tarifa pactada (rechaza si ya existe)", Tip, 145, 34);
        _btnActualizar = AppTheme.CreateButton("Actualizar", IconKind.Save, ButtonVariant.Primary,
            async (_, _) => await GuardarAsync(permitirActualizar: true), "Actualiza el precio de una combinación proveedor/producto existente", Tip, 125, 34);
        _btnEliminar = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger,
            async (_, _) => await EliminarAsync(), "Elimina la tarifa seleccionada de la planta actual", Tip, 115, 34);
        _btnLimpiar = AppTheme.CreateButton("Limpiar", IconKind.Refresh, ButtonVariant.Secondary,
            (_, _) => ClearForm(), "Limpia los campos de edición", Tip, 105, 34);

        TableLayoutPanel edRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        edRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        edRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        FlowLayoutPanel edR1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        edR1.Controls.AddRange(
        [
            new Label { Text = "Cód. Producto:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 0) },
            _txtCodProd,
            _btnSelProd,
            new Label { Text = "RUT Proveedor:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(12, 7, 4, 0) },
            _txtRut,
            _btnSelCliente,
            new Label { Text = "Razón Social:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(12, 7, 4, 0) },
            _txtRazonSocial
        ]);

        FlowLayoutPanel edR2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        edR2.Controls.AddRange(
        [
            new Label { Text = "Precio Pactado ($/Kg):", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 6, 0) },
            _numPrecio,
            _btnInsertar,
            _btnActualizar,
            _btnEliminar,
            _btnLimpiar
        ]);

        edRows.Controls.Add(edR1, 0, 0);
        edRows.Controls.Add(edR2, 0, 1);
        editorCard.Controls.Add(edRows);

        Panel gridCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        _dgv.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(PrecioProveedor.Indice), "N0", 45),
            UiHelpers.AddTextCol("Planta", nameof(PrecioProveedor.Planta), 65),
            UiHelpers.AddTextCol("RUT", nameof(PrecioProveedor.Rut), 85),
            UiHelpers.AddTextCol("Razón Social", nameof(PrecioProveedor.RazonSocial), 150),
            UiHelpers.AddTextCol("Cód. Producto", nameof(PrecioProveedor.CodigoProducto), 75),
            UiHelpers.AddCurrencyCol("Precio $/Kg", nameof(PrecioProveedor.Precio), 80),
            UiHelpers.AddDateCol("Actualización", nameof(PrecioProveedor.FechaActualizacion), 80)
        ]);
        _dgv.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgv.Rows[e.RowIndex].DataBoundItem is PrecioProveedor item)
            {
                PopulateForm(item);
            }
        };
        gridCard.Controls.Add(_dgv);

        Controls.Add(gridCard);
        Controls.Add(editorCard);
        Controls.Add(topCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canEdit = _auth.HasPermission(PermisoSistema.AdministrarMantenedorPrecios);
        _btnInsertar.Enabled = canEdit;
        _btnActualizar.Enabled = canEdit;
        _btnEliminar.Enabled = canEdit;
        _btnRefrescar.Enabled = _auth.HasPermission(PermisoSistema.ConsultarInformes);
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            _allItems = await _app.GetPreciosProveedorAsync(planta, ct);
            ApplyFilter();
            NotifyStatus($"Mantenedor de precios ({planta.ToShortTitle()}): {_allItems.Count} tarifas pactadas.");
        }, NotifyStatus, "Cargando tarifas por proveedor...");
    }

    private void ApplyFilter()
    {
        string term = _txtFiltro.Text.Trim();
        IReadOnlyList<PrecioProveedor> filtered = string.IsNullOrWhiteSpace(term)
            ? _allItems
            : [.. _allItems.Where(p =>
                p.Rut.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.RazonSocial.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.CodigoProducto.Contains(term, StringComparison.OrdinalIgnoreCase))];

        _dgv.DataSource = null;
        _dgv.DataSource = filtered;
    }

    private void PopulateForm(PrecioProveedor item)
    {
        _txtCodProd.Text = item.CodigoProducto;
        _txtRut.Text = item.Rut;
        _txtRazonSocial.Text = item.RazonSocial;
        _numPrecio.Value = Math.Min(_numPrecio.Maximum, Math.Max(0, item.Precio));
    }

    private void ClearForm()
    {
        _txtCodProd.Clear();
        _txtRut.Clear();
        _txtRazonSocial.Clear();
        _numPrecio.Value = 0;
    }

    private void OpenProductoSelector()
    {
        using ProductoSelectorDialog dlg = new(_app);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedProducto is not null)
        {
            _txtCodProd.Text = dlg.SelectedProducto.Codigo;
        }
    }

    private void OpenClienteSelector()
    {
        using ClienteSelectorDialog dlg = new(_app, UiHelpers.GetSelectedPlanta(_cmbPlanta));
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedCliente is not null)
        {
            _txtRut.Text = dlg.SelectedCliente.RutFormateado;
            _txtRazonSocial.Text = dlg.SelectedCliente.RazonSocial;
        }
    }

    private async Task GuardarAsync(bool permitirActualizar)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            await _app.GuardarPrecioProveedorAsync(
                planta,
                _txtRut.Text,
                _txtRazonSocial.Text,
                _txtCodProd.Text,
                _numPrecio.Value,
                permitirActualizar);

            await ActivateAsync();
            NotifyStatus($"Tarifa {(permitirActualizar ? "actualizada" : "insertada")} para {_txtRut.Text} / {_txtCodProd.Text}.");
        }, NotifyStatus, "Guardando tarifa de proveedor...");
    }

    private async Task EliminarAsync()
    {
        if (string.IsNullOrWhiteSpace(_txtRut.Text) || string.IsNullOrWhiteSpace(_txtCodProd.Text))
        {
            MessageBox.Show(this, "Seleccione primero la tarifa que desea eliminar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            this,
            $"¿Confirma eliminar el precio pactado para RUT '{_txtRut.Text}' y producto '{_txtCodProd.Text}'?",
            "Confirmar Eliminación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Planta planta = UiHelpers.GetSelectedPlanta(_cmbPlanta);
            await _app.EliminarPrecioProveedorAsync(planta, _txtRut.Text, _txtCodProd.Text);
            ClearForm();
            await ActivateAsync();
            NotifyStatus("Tarifa eliminada correctamente.");
        }, NotifyStatus, "Eliminando tarifa...");
    }
}
