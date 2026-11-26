namespace GestionScrap.Desktop.Theme;

public enum ButtonVariant
{
    Primary,
    Success,
    Secondary,
    Warning,
    Danger,
    GhostDark
}

/// <summary>
/// Sistema visual unificado para formularios, controles y botones del sistema.
/// </summary>
public static class AppTheme
{
    public static readonly Color HeaderDark = Color.FromArgb(15, 23, 42);        // #0F172A
    public static readonly Color SidebarDark = Color.FromArgb(22, 33, 54);       // #162136
    public static readonly Color CanvasLight = Color.FromArgb(241, 245, 249);    // #F1F5F9
    public static readonly Color CardSurface = Color.White;
    public static readonly Color CardBorder = Color.FromArgb(203, 213, 225);     // #CBD5E1
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);       // #0F172A
    public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);    // #475569
    public static readonly Color AccentCyan = Color.FromArgb(8, 145, 178);       // #0891B2
    public static readonly Color AccentBlue = Color.FromArgb(37, 99, 235);       // #2563EB
    public static readonly Color AccentEmerald = Color.FromArgb(5, 150, 105);    // #059669
    public static readonly Color AccentAmber = Color.FromArgb(217, 119, 6);      // #D97706
    public static readonly Color AccentRed = Color.FromArgb(220, 38, 38);        // #DC2626
    public static readonly Color GridAltRow = Color.FromArgb(248, 250, 252);     // #F8FAFC
    public static readonly Color GridSelRow = Color.FromArgb(224, 242, 254);     // #E0F2FE

    public static readonly Font FontTitle = new("Segoe UI", 13F, FontStyle.Bold);
    public static readonly Font FontSubtitle = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font FontSection = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font FontBody = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font FontBodyBold = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font FontMono = new("Consolas", 9.5F, FontStyle.Regular);
    public static readonly Font FontKpiValue = new("Segoe UI", 15.5F, FontStyle.Bold);

    public static Button CreateButton(
        string text,
        IconKind icon,
        ButtonVariant variant,
        EventHandler? onClick = null,
        string? tooltip = null,
        ToolTip? tipControl = null,
        int width = 0,
        int height = 34)
    {
        (Color bg, Color fg, Color border, Color hover) = variant switch
        {
            ButtonVariant.Primary => (AccentCyan, Color.White, AccentCyan, Color.FromArgb(14, 116, 144)),
            ButtonVariant.Success => (AccentEmerald, Color.White, AccentEmerald, Color.FromArgb(4, 120, 87)),
            ButtonVariant.Warning => (AccentAmber, Color.White, AccentAmber, Color.FromArgb(180, 83, 9)),
            ButtonVariant.Danger => (AccentRed, Color.White, AccentRed, Color.FromArgb(185, 28, 28)),
            ButtonVariant.GhostDark => (Color.FromArgb(30, 41, 59), Color.FromArgb(226, 232, 240), Color.FromArgb(51, 65, 85), Color.FromArgb(51, 65, 85)),
            _ => (Color.White, TextPrimary, CardBorder, Color.FromArgb(241, 245, 249))
        };

        Button btn = new()
        {
            Text = "  " + text,
            Image = IconFactory.GetIcon(icon, fg, 16),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = FontBodyBold,
            Cursor = Cursors.Hand,
            Height = height,
            AutoSize = width <= 0,
            MinimumSize = new Size(width > 0 ? width : 110, height),
            Padding = new Padding(10, 0, 12, 0),
            AccessibleName = text,
            AccessibleDescription = tooltip ?? text
        };

        if (width > 0)
        {
            btn.Width = width;
        }

        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = border;
        btn.FlatAppearance.MouseOverBackColor = hover;

        if (onClick is not null)
        {
            btn.Click += onClick;
        }

        if (!string.IsNullOrWhiteSpace(tooltip) && tipControl is not null)
        {
            tipControl.SetToolTip(btn, tooltip);
        }

        return btn;
    }

    public static Panel CreateCardPanel(Padding? padding = null) => new()
    {
        BackColor = CardSurface,
        Padding = padding ?? new Padding(14),
        Margin = new Padding(0, 0, 0, 10)
    };

    public static void StyleDataGridView(DataGridView dgv)
    {
        dgv.BackgroundColor = CardSurface;
        dgv.BorderStyle = BorderStyle.FixedSingle;
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        dgv.GridColor = Color.FromArgb(226, 232, 240);
        dgv.EnableHeadersVisualStyles = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.ReadOnly = true;
        dgv.MultiSelect = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersHeight = 34;
        dgv.RowTemplate.Height = 28;

        dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderDark,
            ForeColor = Color.White,
            Font = FontBodyBold,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0)
        };

        dgv.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardSurface,
            ForeColor = TextPrimary,
            Font = FontBody,
            SelectionBackColor = GridSelRow,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 2, 6, 2)
        };

        dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridAltRow,
            ForeColor = TextPrimary,
            Font = FontBody,
            SelectionBackColor = GridSelRow,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 2, 6, 2)
        };
    }

    public static void ApplyInputStyle(TextBox tb)
    {
        tb.BorderStyle = BorderStyle.FixedSingle;
        tb.Font = FontBody;
        tb.BackColor = Color.White;
        tb.ForeColor = TextPrimary;
    }

    public static void ApplyDatePickerStyle(DateTimePicker dtp)
    {
        dtp.Format = DateTimePickerFormat.Short;
        dtp.Font = FontBody;
        dtp.Width = 115;
    }

    public static Image? LoadAppLogo()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "app_logo.png");
        if (!File.Exists(path))
        {
            string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "installer", "assets", "app_logo.png"));
            if (File.Exists(devPath))
            {
                path = devPath;
            }
        }

        if (File.Exists(path))
        {
            return Image.FromFile(path);
        }

        return IconFactory.GetIcon(IconKind.Layers, AccentCyan, 48);
    }

    public static Icon? LoadAppIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");
        if (!File.Exists(path))
        {
            string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "installer", "assets", "app_icon.ico"));
            if (File.Exists(devPath))
            {
                path = devPath;
            }
        }

        return File.Exists(path) ? new Icon(path) : null;
    }
}
