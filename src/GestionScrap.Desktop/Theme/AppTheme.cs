using System.Reflection;

namespace GestionScrap.Desktop.Theme;

public enum ButtonVariant
{
    Primary,
    Success,
    Secondary,
    Warning,
    Danger,
    Gold,
    GhostDark
}

/// <summary>
/// Sistema visual corporativo Cambiaso (Azul Petróleo + Cian Técnico + Dorado 1875)
/// con dimensionamiento seguro de botones High-DPI y recursos embebidos sin dependencias externas.
/// </summary>
public static class AppTheme
{
    public static readonly Color HeaderDark = Color.FromArgb(11, 37, 58);        // #0B253A
    public static readonly Color SidebarDark = Color.FromArgb(11, 60, 93);       // #0B3C5D
    public static readonly Color CanvasLight = Color.FromArgb(241, 245, 249);    // #F1F5F9
    public static readonly Color CardSurface = Color.White;
    public static readonly Color CardBorder = Color.FromArgb(203, 213, 225);     // #CBD5E1
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);       // #0F172A
    public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);    // #475569
    public static readonly Color AccentCyan = Color.FromArgb(8, 145, 178);       // #0891B2
    public static readonly Color AccentBlue = Color.FromArgb(14, 96, 138);       // #0E608A
    public static readonly Color AccentGold = Color.FromArgb(201, 164, 110);     // #C9A46E
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
    public static readonly Font FontButton = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font FontButtonCompact = new("Segoe UI", 8.5F, FontStyle.Bold);
    public static readonly Font FontSmall = new("Segoe UI", 8.5F, FontStyle.Regular);
    public static readonly Font FontSmallBold = new("Segoe UI", 8.5F, FontStyle.Bold);
    public static readonly Font FontSmallItalic = new("Segoe UI", 8.5F, FontStyle.Italic);
    public static readonly Font FontCaption = new("Segoe UI", 8F, FontStyle.Regular);
    public static readonly Font FontBrandTitle = new("Segoe UI", 11F, FontStyle.Bold);
    public static readonly Font FontHeaderModule = new("Segoe UI", 12.5F, FontStyle.Bold);
    public static readonly Font FontLoginTitle = new("Segoe UI", 14.5F, FontStyle.Bold);
    public static readonly Font FontSplashTitle = new("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font FontMono = new("Consolas", 9.5F, FontStyle.Regular);
    public static readonly Font FontMonoBold = new("Consolas", 11F, FontStyle.Bold);
    public static readonly Font FontKpiValue = new("Segoe UI", 15F, FontStyle.Bold);

    public static Button CreateButton(
        string text,
        IconKind icon,
        ButtonVariant variant,
        EventHandler? onClick = null,
        string? tooltip = null,
        ToolTip? tipControl = null,
        int width = 0,
        int height = 34,
        Font? font = null)
    {
        (Color bg, Color fg, Color border, Color hover) = variant switch
        {
            ButtonVariant.Primary => (AccentCyan, Color.White, AccentCyan, Color.FromArgb(14, 116, 144)),
            ButtonVariant.Success => (AccentEmerald, Color.White, AccentEmerald, Color.FromArgb(4, 120, 87)),
            ButtonVariant.Warning => (AccentAmber, Color.White, AccentAmber, Color.FromArgb(180, 83, 9)),
            ButtonVariant.Danger => (AccentRed, Color.White, AccentRed, Color.FromArgb(185, 28, 28)),
            ButtonVariant.Gold => (Color.FromArgb(180, 140, 82), Color.White, AccentGold, Color.FromArgb(156, 118, 64)),
            ButtonVariant.GhostDark => (Color.FromArgb(17, 52, 82), Color.FromArgb(226, 232, 240), Color.FromArgb(30, 80, 115), Color.FromArgb(24, 72, 108)),
            _ => (Color.White, TextPrimary, CardBorder, Color.FromArgb(241, 245, 249))
        };

        string cleanText = text.Trim();
        int iconSize = height <= 30 ? 15 : 16;
        Padding pad = height <= 30 ? new Padding(9, 0, 10, 0) : new Padding(10, 0, 12, 0);

        Button btn = new()
        {
            Text = " " + cleanText,
            Image = IconFactory.GetIcon(icon, fg, iconSize),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            UseCompatibleTextRendering = false,
            UseMnemonic = false,
            AutoEllipsis = false,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = font ?? FontButton,
            Cursor = Cursors.Hand,
            AutoSize = false,
            Padding = pad,
            Margin = new Padding(0, 2, 8, 4),
            AccessibleName = cleanText,
            AccessibleDescription = tooltip ?? cleanText
        };

        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = border;
        btn.FlatAppearance.MouseOverBackColor = hover;

        int baseMinW = width;
        int baseMinH = Math.Max(30, height);
        EnsureButtonFitsText(btn, baseMinW, baseMinH);

        btn.HandleCreated += (_, _) => EnsureButtonFitsText(btn, baseMinW, baseMinH);
        btn.DpiChangedAfterParent += (_, _) => EnsureButtonFitsText(btn, baseMinW, baseMinH);
        btn.TextChanged += (_, _) => EnsureButtonFitsText(btn, baseMinW, baseMinH);
        btn.FontChanged += (_, _) => EnsureButtonFitsText(btn, baseMinW, baseMinH);

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

    /// <summary>
    /// Garantiza que el texto completo del botón quepa en una sola línea sin saltos ni recortes en cualquier DPI.
    /// </summary>
    public static void EnsureButtonFitsText(Button btn, int baseMinWidth = 0, int baseMinHeight = 32)
    {
        float dpiScale = btn.DeviceDpi > 0 ? btn.DeviceDpi / 96f : 1f;
        Size measured;
        if (btn.IsHandleCreated)
        {
            using Graphics g = btn.CreateGraphics();
            measured = TextRenderer.MeasureText(
                g,
                btn.Text,
                btn.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        else
        {
            measured = TextRenderer.MeasureText(
                btn.Text,
                btn.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        int iconAllowance = btn.Image is not null ? btn.Image.Width + 8 : 0;
        int safetyBuffer = (int)Math.Ceiling(14 * dpiScale);
        int requiredWidth = measured.Width + iconAllowance + btn.Padding.Horizontal + safetyBuffer;
        int requiredHeight = Math.Max((int)Math.Ceiling(baseMinHeight * dpiScale), measured.Height + (int)Math.Ceiling(12 * dpiScale));

        int minW = (int)Math.Ceiling(baseMinWidth * dpiScale);
        int finalWidth = Math.Max(minW, requiredWidth);

        if (btn.MinimumSize.Width != finalWidth || btn.MinimumSize.Height != requiredHeight)
        {
            btn.MinimumSize = new Size(finalWidth, requiredHeight);
        }

        if (btn.Width != finalWidth || btn.Height != requiredHeight)
        {
            btn.Size = new Size(finalWidth, requiredHeight);
        }
    }

    /// <summary>
    /// Crea una barra inferior uniforme para diálogos con botones visibles, alineados a la derecha y sin superposición.
    /// </summary>
    public static Panel CreateDialogFooter(params Button[] buttonsLeftToRight)
    {
        int maxBtnH = buttonsLeftToRight.Length > 0 ? buttonsLeftToRight.Max(b => b.Height) : 34;
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = maxBtnH + 24,
            BackColor = Color.White,
            Padding = new Padding(16, 12, 16, 12)
        };
        footer.Paint += (_, e) =>
        {
            using Pen divider = new(CardBorder, 1f);
            e.Graphics.DrawLine(divider, 0, 0, footer.Width, 0);
        };

        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // En RightToLeft el primer agregado queda más a la derecha, preservando el orden visual izquierda->derecha
        for (int i = buttonsLeftToRight.Length - 1; i >= 0; i--)
        {
            Button btn = buttonsLeftToRight[i];
            btn.Margin = i == buttonsLeftToRight.Length - 1
                ? new Padding(0, 0, 0, 0)
                : new Padding(0, 0, 10, 0);
            flow.Controls.Add(btn);
        }

        void SyncHeight()
        {
            int h = buttonsLeftToRight.Length > 0 ? buttonsLeftToRight.Max(b => b.Height) : 34;
            int needed = h + footer.Padding.Vertical;
            if (footer.Height != needed)
            {
                footer.Height = needed;
            }
        }

        footer.HandleCreated += (_, _) => SyncHeight();
        footer.DpiChangedAfterParent += (_, _) => SyncHeight();
        footer.Controls.Add(flow);
        return footer;
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
        dgv.ColumnHeadersHeight = 36;
        dgv.RowTemplate.Height = 29;

        dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderDark,
            ForeColor = Color.White,
            SelectionBackColor = HeaderDark,
            SelectionForeColor = Color.White,
            Font = FontSmallBold,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False,
            Padding = new Padding(6, 0, 6, 0)
        };

        dgv.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardSurface,
            ForeColor = TextPrimary,
            Font = FontBody,
            SelectionBackColor = GridSelRow,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 2, 8, 2)
        };

        dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridAltRow,
            ForeColor = TextPrimary,
            Font = FontBody,
            SelectionBackColor = GridSelRow,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 2, 8, 2)
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
        dtp.Width = 122;
    }

    private static Bitmap? LoadEmbeddedImage(string resourceName)
    {
        Assembly asm = typeof(AppTheme).Assembly;
        using Stream? s = asm.GetManifestResourceStream(resourceName);
        if (s is null)
        {
            return null;
        }

        using MemoryStream copy = new();
        s.CopyTo(copy);
        copy.Position = 0;
        using Image loaded = Image.FromStream(copy);
        return new Bitmap(loaded);
    }

    public static Image LoadAppLogo() =>
        LoadEmbeddedImage("GestionScrap.Desktop.Assets.app_logo.png")
        ?? IconFactory.GetIcon(IconKind.Layers, AccentGold, 48);

    public static Image LoadCambiasoEmblem() =>
        LoadEmbeddedImage("GestionScrap.Desktop.Assets.cambiaso_emblem.png")
        ?? LoadAppLogo();

    public static Image LoadCambiasoWordmark() =>
        LoadEmbeddedImage("GestionScrap.Desktop.Assets.cambiaso_wordmark.png")
        ?? LoadAppLogo();

    public static Icon? LoadAppIcon()
    {
        Assembly asm = typeof(AppTheme).Assembly;
        using Stream? s = asm.GetManifestResourceStream("GestionScrap.Desktop.Assets.app_icon.ico");
        if (s is not null)
        {
            return new Icon(s);
        }

        string diskPath = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");
        return File.Exists(diskPath) ? new Icon(diskPath) : null;
    }
}
