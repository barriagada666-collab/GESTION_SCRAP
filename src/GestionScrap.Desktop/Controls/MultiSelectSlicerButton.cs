using GestionScrap.Application.DTOs;
using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Botón de segmentación multi-selección estilo Power BI / ERP: abre un panel desplegable con
/// búsqueda interna, checkboxes, Seleccionar Todos, Deseleccionar Todos y contador visual de activos.
/// </summary>
public sealed class MultiSelectSlicerButton : IDisposable
{
    private readonly string _title;
    private readonly HashSet<string> _selectedKeys = new(StringComparer.OrdinalIgnoreCase);
    private List<SlicerOptionDto> _options = [];
    private ToolStripDropDown? _dropDown;
    private bool _disposed;

    public Button Button { get; }
    public event Action? SelectionChanged;

    public bool HasActiveSelection => _selectedKeys.Count > 0;
    public int SelectedCount => _selectedKeys.Count;

    public MultiSelectSlicerButton(string title, ToolTip? tip = null)
    {
        _title = title;
        Button = AppTheme.CreateButton(
            $"{title} ▾",
            IconKind.Layers,
            ButtonVariant.Secondary,
            (_, _) => OpenDropDown(),
            $"Segmentador multi-selección de {title} (clic para elegir uno o varios valores)",
            tip,
            0,
            30,
            AppTheme.FontButtonCompact);
        Button.Margin = new Padding(0, 2, 6, 4);
    }

    public void SetOptions(IReadOnlyList<SlicerOptionDto> options)
    {
        _options = [.. options];
        HashSet<string> valid = new(_options.Select(o => o.Key), StringComparer.OrdinalIgnoreCase);
        _selectedKeys.RemoveWhere(k => !valid.Contains(k));
        UpdateButtonAppearance();
    }

    public HashSet<string>? GetSelectedKeysOrNull() =>
        _selectedKeys.Count == 0 ? null : new HashSet<string>(_selectedKeys, StringComparer.OrdinalIgnoreCase);

    public HashSet<int>? GetSelectedIntsOrNull()
    {
        if (_selectedKeys.Count == 0)
        {
            return null;
        }

        HashSet<int> ints = [];
        foreach (string k in _selectedKeys)
        {
            if (int.TryParse(k, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v))
            {
                ints.Add(v);
            }
        }

        return ints.Count == 0 ? null : ints;
    }

    public void ClearSelection(bool raiseEvent = false)
    {
        if (_selectedKeys.Count == 0)
        {
            return;
        }

        _selectedKeys.Clear();
        UpdateButtonAppearance();
        if (raiseEvent)
        {
            SelectionChanged?.Invoke();
        }
    }

    private void UpdateButtonAppearance()
    {
        if (_selectedKeys.Count == 0)
        {
            Button.Text = $" {_title} ▾";
            Button.BackColor = Color.White;
            Button.ForeColor = AppTheme.TextPrimary;
            Button.FlatAppearance.BorderColor = AppTheme.CardBorder;
            Button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            Button.Image = IconFactory.GetIcon(IconKind.Layers, AppTheme.AccentBlue, 15);
        }
        else
        {
            Button.Text = $" {_title} ({_selectedKeys.Count}) ▾";
            Button.BackColor = AppTheme.AccentCyan;
            Button.ForeColor = Color.White;
            Button.FlatAppearance.BorderColor = AppTheme.AccentCyan;
            Button.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 116, 144);
            Button.Image = IconFactory.GetIcon(IconKind.Check, Color.White, 15);
        }

        AppTheme.EnsureButtonFitsText(Button, 0, 30);
    }

    private sealed record CheckItem(string Key, string Display)
    {
        public override string ToString() => Display;
    }

    private void OpenDropDown()
    {
        _dropDown?.Close();
        _dropDown?.Dispose();

        HashSet<string> working = new(_selectedKeys, StringComparer.OrdinalIgnoreCase);

        Panel host = new()
        {
            Size = new Size(340, 355),
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };

        Label lblHeader = new()
        {
            Text = $"Filtrar por {_title} (selección múltiple)",
            Font = AppTheme.FontBodyBold,
            ForeColor = AppTheme.SidebarDark,
            Dock = DockStyle.Top,
            Height = 24
        };

        TextBox txtSearch = new()
        {
            Dock = DockStyle.Top,
            PlaceholderText = $"Buscar en {_title.ToLowerInvariant()}..."
        };
        AppTheme.ApplyInputStyle(txtSearch);

        CheckedListBox clb = new()
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            Font = AppTheme.FontSmall,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };

        bool suppressItemCheck = false;
        void PopulateList(string query)
        {
            suppressItemCheck = true;
            try
            {
                clb.BeginUpdate();
                clb.Items.Clear();
                string q = query.Trim();
                IEnumerable<SlicerOptionDto> filtered = string.IsNullOrEmpty(q)
                    ? _options
                    : _options.Where(o => o.Label.Contains(q, StringComparison.OrdinalIgnoreCase) || o.Key.Contains(q, StringComparison.OrdinalIgnoreCase));

                foreach (SlicerOptionDto opt in filtered)
                {
                    string display = opt.Count > 0 ? $"{opt.Label}  [{opt.Count:N0}]" : opt.Label;
                    int idx = clb.Items.Add(new CheckItem(opt.Key, display));
                    if (working.Contains(opt.Key))
                    {
                        clb.SetItemChecked(idx, true);
                    }
                }
            }
            finally
            {
                clb.EndUpdate();
                suppressItemCheck = false;
            }
        }

        clb.ItemCheck += (_, e) =>
        {
            if (suppressItemCheck || clb.Items[e.Index] is not CheckItem item)
            {
                return;
            }

            if (e.NewValue == CheckState.Checked)
            {
                working.Add(item.Key);
            }
            else
            {
                working.Remove(item.Key);
            }
        };

        txtSearch.TextChanged += (_, _) => PopulateList(txtSearch.Text);
        PopulateList(string.Empty);

        Button btnTodos = AppTheme.CreateButton("Todos", IconKind.Check, ButtonVariant.Secondary, (_, _) =>
        {
            suppressItemCheck = true;
            try
            {
                for (int i = 0; i < clb.Items.Count; i++)
                {
                    if (clb.Items[i] is CheckItem item)
                    {
                        working.Add(item.Key);
                        clb.SetItemChecked(i, true);
                    }
                }
            }
            finally
            {
                suppressItemCheck = false;
            }
        }, "Seleccionar todos los valores visibles", null, 0, 30, AppTheme.FontButtonCompact);

        Button btnNinguno = AppTheme.CreateButton("Ninguno", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            suppressItemCheck = true;
            try
            {
                working.Clear();
                for (int i = 0; i < clb.Items.Count; i++)
                {
                    clb.SetItemChecked(i, false);
                }
            }
            finally
            {
                suppressItemCheck = false;
            }
        }, "Limpiar toda la selección de esta categoría", null, 0, 30, AppTheme.FontButtonCompact);

        Button btnAplicar = AppTheme.CreateButton("Aplicar", IconKind.Search, ButtonVariant.Primary, (_, _) =>
        {
            _selectedKeys.Clear();
            foreach (string k in working)
            {
                _selectedKeys.Add(k);
            }

            UpdateButtonAppearance();
            _dropDown?.Close();
            SelectionChanged?.Invoke();
        }, "Aplicar selección al informe", null, 0, 30, AppTheme.FontButtonCompact);

        FlowLayoutPanel bar = new()
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        bar.Controls.AddRange([btnTodos, btnNinguno, btnAplicar]);

        Panel spacer = new() { Dock = DockStyle.Top, Height = 6 };
        host.Controls.Add(clb);
        host.Controls.Add(spacer);
        host.Controls.Add(txtSearch);
        host.Controls.Add(lblHeader);
        host.Controls.Add(bar);

        ToolStripControlHost ctrlHost = new(host)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false,
            Size = host.Size
        };

        _dropDown = new ToolStripDropDown
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoClose = true
        };
        _dropDown.Items.Add(ctrlHost);
        _dropDown.Show(Button, new Point(0, Button.Height + 2));
        txtSearch.Focus();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dropDown?.Dispose();
    }
}
