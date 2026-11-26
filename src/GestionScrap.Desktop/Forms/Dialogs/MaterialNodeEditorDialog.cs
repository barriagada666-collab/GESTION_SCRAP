using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo CRUD para crear o editar Nodos Padre / Subgrupos o Nodos Hijo de Producto en el árbol de Grupos de Material.
/// </summary>
public sealed class MaterialNodeEditorDialog : Form
{
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtNombre = new();
    private readonly ComboBox _cmbColor = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbProducto = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NodoGrupoTipo _tipo;
    private readonly long? _parentId;
    private readonly long _editingId;
    private readonly int _orden;

    private static readonly (string Nombre, string Hex)[] PaletteOptions =
    [
        ("Cian Técnico (#0891B2)", "#0891B2"),
        ("Azul Petróleo (#0B3C5D)", "#0B3C5D"),
        ("Dorado Cambiaso (#C9A46E)", "#C9A46E"),
        ("Verde Esmeralda (#059669)", "#059669"),
        ("Azul Cobalto (#2563EB)", "#2563EB"),
        ("Violeta (#7C3AED)", "#7C3AED"),
        ("Ámbar (#D97706)", "#D97706"),
        ("Pizarra (#475569)", "#475569")
    ];

    private sealed record ColorItem(string Nombre, string Hex)
    {
        public override string ToString() => Nombre;
    }

    private sealed record ProductItem(string Codigo, string Descripcion)
    {
        public override string ToString() => $"{Codigo} · {Descripcion}";
    }

    public MaterialGroupNode? ResultNode { get; private set; }

    public MaterialNodeEditorDialog(
        NodoGrupoTipo tipo,
        long? parentId,
        string parentDisplayName,
        IReadOnlyList<Producto> productosDisponibles,
        MaterialGroupNode? existing = null)
    {
        _tipo = tipo;
        _parentId = parentId;
        _editingId = existing?.Id ?? 0;
        _orden = existing?.Orden ?? 0;

        Text = existing is null
            ? (tipo == NodoGrupoTipo.CarpetaGrupo ? "Nuevo Grupo / Subgrupo de Material" : "Agregar Producto Hijo")
            : $"Editar Nodo · {existing.Nombre}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(490, tipo == NodoGrupoTipo.ProductoAsignado ? 295 : 235);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Label lblParent = new()
        {
            Text = $"Ubicación Padre: {parentDisplayName}",
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.AccentBlue,
            Location = new Point(18, 14),
            Size = new Size(450, 22)
        };

        int y = 44;
        if (tipo == NodoGrupoTipo.ProductoAsignado)
        {
            Label lblProd = new() { Text = "Producto del Catálogo (DBPRODUCTOS):", Font = AppTheme.FontBodyBold, Location = new Point(18, y), AutoSize = true };
            _cmbProducto.SetBounds(18, y + 24, 452, 28);
            _cmbProducto.Font = AppTheme.FontBody;
            foreach (Producto p in productosDisponibles)
            {
                _cmbProducto.Items.Add(new ProductItem(p.Codigo, p.Descripcion));
            }

            if (existing is not null && !string.IsNullOrWhiteSpace(existing.CodigoProducto))
            {
                bool found = false;
                for (int i = 0; i < _cmbProducto.Items.Count; i++)
                {
                    if (_cmbProducto.Items[i] is ProductItem pi && string.Equals(pi.Codigo, existing.CodigoProducto, StringComparison.OrdinalIgnoreCase))
                    {
                        _cmbProducto.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _cmbProducto.Items.Insert(0, new ProductItem(existing.CodigoProducto, existing.Nombre));
                    _cmbProducto.SelectedIndex = 0;
                }
            }
            else if (_cmbProducto.Items.Count > 0)
            {
                _cmbProducto.SelectedIndex = 0;
            }

            _cmbProducto.SelectedIndexChanged += (_, _) =>
            {
                if (_cmbProducto.SelectedItem is ProductItem pi)
                {
                    _txtNombre.Text = pi.ToString();
                }
            };

            Controls.AddRange([lblProd, _cmbProducto]);
            y += 62;
        }

        Label lblNom = new()
        {
            Text = tipo == NodoGrupoTipo.CarpetaGrupo ? "Nombre del Grupo o Subgrupo:" : "Etiqueta Visible del Nodo Hijo:",
            Font = AppTheme.FontBodyBold,
            Location = new Point(18, y),
            AutoSize = true
        };
        _txtNombre.SetBounds(18, y + 24, 452, 28);
        AppTheme.ApplyInputStyle(_txtNombre);
        _txtNombre.Text = existing?.Nombre ?? (_cmbProducto.SelectedItem is ProductItem sel ? sel.ToString() : string.Empty);

        y += 62;
        Label lblCol = new() { Text = "Color Identificador:", Font = AppTheme.FontBodyBold, Location = new Point(18, y), AutoSize = true };
        _cmbColor.SetBounds(18, y + 24, 240, 28);
        _cmbColor.Font = AppTheme.FontBody;
        foreach ((string name, string hex) in PaletteOptions)
        {
            _cmbColor.Items.Add(new ColorItem(name, hex));
        }

        _cmbColor.SelectedIndex = 0;
        if (existing is not null)
        {
            for (int i = 0; i < _cmbColor.Items.Count; i++)
            {
                if (_cmbColor.Items[i] is ColorItem ci && string.Equals(ci.Hex, existing.ColorHex, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbColor.SelectedIndex = i;
                    break;
                }
            }
        }

        int btnY = ClientSize.Height - 52;
        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar sin guardar cambios", _toolTip, 115, 34);
        btnCancel.Location = new Point(225, btnY);

        Button btnSave = AppTheme.CreateButton("Guardar Nodo", IconKind.Save, ButtonVariant.Success, (_, _) => SaveAndClose(),
            "Guarda el nodo dentro de la jerarquía de materiales", _toolTip, 136, 34);
        btnSave.Location = new Point(346, btnY);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
        Controls.AddRange([lblParent, lblNom, _txtNombre, lblCol, _cmbColor, btnCancel, btnSave]);
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show(this, "Debe ingresar un nombre válido para el nodo.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNombre.Focus();
            return;
        }

        string code = _tipo == NodoGrupoTipo.ProductoAsignado && _cmbProducto.SelectedItem is ProductItem pi
            ? pi.Codigo
            : string.Empty;

        if (_tipo == NodoGrupoTipo.ProductoAsignado && string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show(this, "No hay producto seleccionado para asignar al nodo hijo.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string hex = (_cmbColor.SelectedItem as ColorItem)?.Hex ?? "#0891B2";
        ResultNode = new MaterialGroupNode
        {
            Id = _editingId,
            ParentId = _parentId,
            Nombre = _txtNombre.Text.Trim(),
            TipoNodo = _tipo,
            CodigoProducto = code,
            ColorHex = hex,
            Orden = _orden
        };

        DialogResult = DialogResult.OK;
        Close();
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
