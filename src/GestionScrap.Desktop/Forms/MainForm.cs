using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Forms;

/// <summary>
/// Ventana principal Cambiaso con los 5 módulos activos: Informe Resumen, Grupos de Material (Árbol CRUD),
/// Análisis de Proveedores, Importador Multi-Origen y Seguridad / Perfiles.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly ToolTip _toolTip = new();

    private readonly Panel _sidebar = new();
    private readonly Panel _header = new();
    private readonly Panel _contentHost = new();
    private readonly Label _lblHeaderModule = new();
    private readonly Label _lblHeaderSub = new();

    private readonly Button _btnQuickConfig;
    private readonly Button _btnQuickPassword;
    private readonly Button _btnLogout;

    private readonly ToolStripStatusLabel _lblUserStatus = new();
    private readonly ToolStripStatusLabel _lblDbStatus = new();
    private readonly ToolStripStatusLabel _lblOpStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private readonly List<NavItem> _navItems = [];
    private readonly Dictionary<int, ScrapTabControlBase> _tabs = [];
    private int _activeTabIndex = -1;

    public bool LogoutRequested { get; private set; }
    public IReadOnlyList<Button> NavigationButtons => [.. _navItems.Select(n => n.Button)];

    private sealed record NavItem(
        int Index,
        string Title,
        string Subtitle,
        IconKind Icon,
        Func<bool> IsAllowed,
        Button Button);

    public MainForm(
        ScrapApplicationService app,
        IAuthService auth,
        AppSettingsStore settingsStore,
        AppSettings settings)
    {
        _app = app;
        _auth = auth;
        _settingsStore = settingsStore;
        _settings = settings;

        Text = $"Cambiaso · Gestión de Scrap y Pesajes {AppMetadata.ShortVersion}";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1380, 840);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();
        KeyPreview = true;

        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = 258;
        _sidebar.BackColor = AppTheme.SidebarDark;

        Panel brandPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(12, 12, 12, 10)
        };
        brandPanel.Paint += (_, e) =>
        {
            using Pen goldBar = new(AppTheme.AccentGold, 2f);
            e.Graphics.DrawLine(goldBar, 0, brandPanel.Height - 1, brandPanel.Width, brandPanel.Height - 1);
        };

        PictureBox picBrand = new()
        {
            Size = new Size(48, 48),
            Location = new Point(12, 18),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppTheme.LoadAppLogo()
        };
        Label lblBrandTitle = new()
        {
            Text = "CAMBIASO SCRAP",
            Font = AppTheme.FontBrandTitle,
            ForeColor = AppTheme.AccentGold,
            Location = new Point(68, 18),
            AutoSize = true
        };
        Label lblBrandVer = new()
        {
            Text = $"{AppMetadata.ShortVersion} · .NET 10 · SQLite",
            Font = AppTheme.FontSmall,
            ForeColor = Color.FromArgb(186, 210, 228),
            Location = new Point(69, 44),
            AutoSize = true
        };
        brandPanel.Controls.AddRange([picBrand, lblBrandTitle, lblBrandVer]);

        Panel navContainer = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8, 12, 8, 10)
        };

        RegisterNavItem(navContainer, 0, "Informe Resumen", "Resumen ejecutivo de volumen, plantas y movimientos", IconKind.Dashboard,
            () => _auth.HasPermission(PermisoSistema.VerInformeResumen), "Atajo Ctrl+1");
        RegisterNavItem(navContainer, 1, "Grupos de Material", "Árbol editable (Nodos Padre/Hijo) e informe consolidado", IconKind.Layers,
            () => _auth.HasPermission(PermisoSistema.VerGrupoMaterial), "Atajo Ctrl+2");
        RegisterNavItem(navContainer, 2, "Análisis Proveedores", "Ranking Pareto ABC, evolución, flota y detalle de entregas", IconKind.Store,
            () => _auth.HasPermission(PermisoSistema.VerAnalisisProveedores), "Atajo Ctrl+3");
        RegisterNavItem(navContainer, 3, "Importador BD", "Conexión directa Firebird WMDBSPC.GDB y migración Access", IconKind.Database,
            () => _auth.HasPermission(PermisoSistema.ImportarDatos), "Atajo Ctrl+4");
        RegisterNavItem(navContainer, 4, "Seguridad y Perfiles", "Gestión de usuarios, perfiles RBAC, auditoría y respaldo SQLite", IconKind.Shield,
            () => _auth.HasPermission(PermisoSistema.AdministrarSeguridadPerfiles)
                  || _auth.HasPermission(PermisoSistema.VerAuditoria)
                  || _auth.HasPermission(PermisoSistema.MantenimientoBaseDatos),
            "Atajo Ctrl+5");

        _sidebar.Controls.Add(navContainer);
        _sidebar.Controls.Add(brandPanel);

        _header.Dock = DockStyle.Top;
        _header.Height = 68;
        _header.BackColor = AppTheme.HeaderDark;
        _header.Padding = new Padding(16, 10, 14, 10);
        _header.Paint += (_, e) =>
        {
            using Pen goldBar = new(AppTheme.AccentGold, 2f);
            e.Graphics.DrawLine(goldBar, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        };

        TableLayoutPanel headerTitleBox = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        headerTitleBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerTitleBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTitleBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblHeaderModule.Text = "Informe Resumen";
        _lblHeaderModule.Font = AppTheme.FontHeaderModule;
        _lblHeaderModule.ForeColor = Color.White;
        _lblHeaderModule.Dock = DockStyle.Fill;
        _lblHeaderModule.AutoEllipsis = true;
        _lblHeaderModule.AutoSize = true;
        _lblHeaderModule.Margin = new Padding(0, 0, 8, 2);

        _lblHeaderSub.Text = "Resumen ejecutivo operativo";
        _lblHeaderSub.Font = AppTheme.FontSmall;
        _lblHeaderSub.ForeColor = Color.FromArgb(186, 210, 228);
        _lblHeaderSub.Dock = DockStyle.Fill;
        _lblHeaderSub.AutoEllipsis = true;
        _lblHeaderSub.AutoSize = true;
        _lblHeaderSub.Margin = new Padding(1, 0, 8, 0);
        headerTitleBox.Controls.Add(_lblHeaderModule, 0, 0);
        headerTitleBox.Controls.Add(_lblHeaderSub, 0, 1);

        _btnQuickConfig = AppTheme.CreateButton("Config. BD", IconKind.Settings, ButtonVariant.GhostDark,
            (_, _) => OpenConfigDbDialog(), "Configuración de rutas SQLite y servidores Firebird", _toolTip, 0, 34);
        _btnQuickPassword = AppTheme.CreateButton("Mi Clave", IconKind.Key, ButtonVariant.GhostDark,
            (_, _) => OpenChangePasswordDialog(), "Cambiar contraseña de la sesión actual", _toolTip, 0, 34);
        _btnLogout = AppTheme.CreateButton("Salir", IconKind.Logout, ButtonVariant.Danger,
            (_, _) => RequestLogout(), "Cerrar sesión actual y regresar al login", _toolTip, 0, 34);
        _btnLogout.Margin = new Padding(0, 2, 0, 2);

        FlowLayoutPanel headerActions = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 6, 0, 0),
            Margin = Padding.Empty
        };
        headerActions.Controls.AddRange([_btnQuickConfig, _btnQuickPassword, _btnLogout]);
        _header.Controls.Add(headerTitleBox);
        _header.Controls.Add(headerActions);

        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = AppTheme.CanvasLight;

        StatusStrip status = new()
        {
            BackColor = AppTheme.HeaderDark,
            ForeColor = Color.White,
            SizingGrip = false
        };
        _lblUserStatus.ForeColor = AppTheme.AccentGold;
        _lblUserStatus.Font = AppTheme.FontBodyBold;
        _lblDbStatus.ForeColor = Color.FromArgb(110, 231, 183);
        _lblDbStatus.Font = AppTheme.FontBody;
        _lblOpStatus.ForeColor = Color.FromArgb(226, 232, 240);
        _lblOpStatus.Font = AppTheme.FontBody;
        status.Items.AddRange([_lblUserStatus, new ToolStripSeparator(), _lblDbStatus, new ToolStripSeparator(), _lblOpStatus]);

        Controls.Add(_contentHost);
        Controls.Add(_header);
        Controls.Add(_sidebar);
        Controls.Add(status);

        KeyDown += OnFormKeyDown;
        Shown += async (_, _) => await InitializeViewAsync();

        ApplySessionToChrome();
    }

    private void RegisterNavItem(
        Panel container,
        int index,
        string title,
        string subtitle,
        IconKind icon,
        Func<bool> isAllowed,
        string tooltip)
    {
        Button btn = new()
        {
            Text = " " + title,
            Image = IconFactory.GetIcon(icon, Color.FromArgb(203, 213, 225), 16),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            UseMnemonic = false,
            Dock = DockStyle.Top,
            Height = 46,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarDark,
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = AppTheme.FontBodyBold,
            Padding = new Padding(14, 0, 10, 0),
            Cursor = Cursors.Hand,
            AccessibleName = title,
            AccessibleDescription = tooltip,
            Tag = index
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 96, 138);
        btn.Click += async (_, _) => await NavigateToAsync(index);
        _toolTip.SetToolTip(btn, $"{subtitle} ({tooltip})");

        NavItem item = new(index, title, subtitle, icon, isAllowed, btn);
        _navItems.Add(item);

        container.Controls.Add(btn);
        container.Controls.SetChildIndex(btn, 0);
    }

    private void ApplySessionToChrome()
    {
        Usuario? user = _auth.CurrentUser;
        _lblUserStatus.Text = user is null
            ? "Sin sesión activa"
            : $"Usuario: {user.NombreCompleto} ({user.NombreUsuario} · {user.NombrePerfil})";
        _lblDbStatus.Text = $"SQLite: {Path.GetFileName(_settings.SqliteDatabasePath)}";
        _lblOpStatus.Text = "Sistema listo.";

        _btnQuickConfig.Enabled = _auth.HasPermission(PermisoSistema.ImportarDatos) || _auth.HasPermission(PermisoSistema.MantenimientoBaseDatos);

        foreach (NavItem item in _navItems)
        {
            item.Button.Enabled = item.IsAllowed();
            item.Button.ForeColor = item.Button.Enabled
                ? Color.FromArgb(226, 232, 240)
                : Color.FromArgb(100, 116, 139);
        }
    }

    private async Task InitializeViewAsync()
    {
        NavItem? firstAllowed = _navItems.FirstOrDefault(n => n.IsAllowed());
        if (firstAllowed is not null)
        {
            await NavigateToAsync(firstAllowed.Index);
        }
    }

    public void NavigateTo(int index) => _ = NavigateToAsync(index);

    public async Task NavigateToAsync(int index)
    {
        NavItem? target = _navItems.FirstOrDefault(n => n.Index == index);
        if (target is null)
        {
            return;
        }

        if (!target.IsAllowed())
        {
            MessageBox.Show(this, $"Su perfil actual no tiene permisos para acceder al módulo '{target.Title}'.", "Acceso Restringido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _contentHost.SuspendLayout();
        try
        {
            if (!_tabs.TryGetValue(index, out ScrapTabControlBase? ctrl))
            {
                ctrl = CreateTabControl(index);
                ctrl.StatusChanged += msg => _lblOpStatus.Text = msg;
                _tabs[index] = ctrl;
                _contentHost.Controls.Add(ctrl);
            }

            foreach (KeyValuePair<int, ScrapTabControlBase> kv in _tabs)
            {
                kv.Value.Visible = kv.Key == index;
            }

            ctrl.RefreshPermissions();
            ctrl.BringToFront();
            _activeTabIndex = index;

            _lblHeaderModule.Text = target.Title;
            _lblHeaderSub.Text = target.Subtitle;

            foreach (NavItem item in _navItems)
            {
                bool isActive = item.Index == index;
                item.Button.BackColor = isActive ? AppTheme.AccentCyan : AppTheme.SidebarDark;
                item.Button.ForeColor = !item.Button.Enabled
                    ? Color.FromArgb(100, 116, 139)
                    : Color.White;
                item.Button.Image = IconFactory.GetIcon(item.Icon, isActive ? Color.White : AppTheme.AccentGold, 16);
            }
        }
        finally
        {
            _contentHost.ResumeLayout(true);
        }

        if (_tabs.TryGetValue(index, out ScrapTabControlBase? active))
        {
            await active.ActivateAsync();
        }
    }

    private ScrapTabControlBase CreateTabControl(int index) => index switch
    {
        0 => new InformeResumenControl(_app, _auth, NavigateTo),
        1 => new GrupoMaterialControl(_app, _auth),
        2 => new AnalisisProveedoresControl(_app, _auth),
        3 => new ImportadorControl(_app, _auth, _settingsStore, _settings, RefreshDataTabsIfLoadedAsync),
        4 => new SeguridadPerfilesControl(_app, _auth, _settings),
        _ => new InformeResumenControl(_app, _auth, NavigateTo)
    };

    private async Task RefreshDataTabsIfLoadedAsync()
    {
        foreach (int idx in new[] { 0, 1, 2 })
        {
            if (_tabs.TryGetValue(idx, out ScrapTabControlBase? tab) && tab.Visible)
            {
                await tab.ActivateAsync();
            }
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5 && _tabs.TryGetValue(_activeTabIndex, out ScrapTabControlBase? active))
        {
            e.SuppressKeyPress = true;
            _ = active.ActivateAsync();
            return;
        }

        if (!e.Control)
        {
            return;
        }

        int? targetIndex = e.KeyCode switch
        {
            Keys.D1 => 0,
            Keys.D2 => 1,
            Keys.D3 => 2,
            Keys.D4 => 3,
            Keys.D5 => 4,
            _ => null
        };

        if (targetIndex.HasValue)
        {
            e.SuppressKeyPress = true;
            NavigateTo(targetIndex.Value);
        }
    }

    private void OpenConfigDbDialog()
    {
        using ConfiguracionDbDialog dlg = new(_settingsStore, _settings, _app);
        dlg.ShowDialog(this);
    }

    private void OpenChangePasswordDialog()
    {
        using CambiarPasswordDialog dlg = new(_auth);
        dlg.ShowDialog(this);
    }

    private void RequestLogout()
    {
        DialogResult confirm = MessageBox.Show(
            this,
            "¿Desea cerrar la sesión actual y regresar al inicio de sesión?",
            "Cerrar Sesión",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            LogoutRequested = true;
            Close();
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
