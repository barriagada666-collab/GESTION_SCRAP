using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;

namespace GestionScrap.Desktop.Forms;

/// <summary>
/// Ventana principal de escritorio con barra de navegación lateral, control RBAC por módulo y carga diferida.
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

    private readonly Button _btnQuickPa;
    private readonly Button _btnQuickPurge;
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

        Text = "Gestión de Scrap · Plataforma .NET 10";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1380, 840);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();
        KeyPreview = true;

        // Sidebar izquierdo
        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = 236;
        _sidebar.BackColor = AppTheme.SidebarDark;

        Panel brandPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = AppTheme.HeaderDark,
            Padding = new Padding(12, 12, 12, 10)
        };
        PictureBox picBrand = new()
        {
            Size = new Size(44, 44),
            Location = new Point(12, 16),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppTheme.LoadAppLogo()
        };
        Label lblBrandTitle = new()
        {
            Text = "GESTIÓN SCRAP",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(64, 15),
            AutoSize = true
        };
        Label lblBrandVer = new()
        {
            Text = "v4.0 · .NET 10 · SQLite",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(65, 39),
            AutoSize = true
        };
        brandPanel.Controls.AddRange([picBrand, lblBrandTitle, lblBrandVer]);

        Panel navContainer = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8, 10, 8, 10)
        };

        RegisterNavItem(navContainer, 0, "Dashboard", "Resumen ejecutivo, KPIs y alertas de tarifas", IconKind.Dashboard,
            () => _auth.HasPermission(PermisoSistema.VerDashboard), "Atajo Ctrl+1");
        RegisterNavItem(navContainer, 1, "Grupo de Material", "Consolidado de kilos por familia y planta", IconKind.Layers,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+2");
        RegisterNavItem(navContainer, 2, "Compras y Traslados", "Comparativa de compras vs movimientos internos", IconKind.ShoppingCart,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+3");
        RegisterNavItem(navContainer, 3, "Informe por Cliente", "Historial detallado por RUT o Folio", IconKind.User,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+4");
        RegisterNavItem(navContainer, 4, "Grupos Especiales", "8 segmentos Retail, Mix, Agrícola, Pesquera y Agrosuper", IconKind.Store,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+5");
        RegisterNavItem(navContainer, 5, "Informe JCRAMM", "Despachos pendientes y cierre persistente de folio", IconKind.Document,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+6");
        RegisterNavItem(navContainer, 6, "Informe de Precios", "Precios teóricos, precios reales indexados y congelamiento", IconKind.DollarSign,
            () => _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+7");
        RegisterNavItem(navContainer, 7, "Mantenedor Precios", "Tarifas pactadas por proveedor y código", IconKind.Plus,
            () => _auth.HasPermission(PermisoSistema.AdministrarMantenedorPrecios) || _auth.HasPermission(PermisoSistema.ConsultarInformes), "Atajo Ctrl+8");
        RegisterNavItem(navContainer, 8, "Importar Firebird", "Sincronización TCP/IP desde básculas de planta", IconKind.Database,
            () => _auth.HasPermission(PermisoSistema.ImportarFirebird), "Atajo Ctrl+9");
        RegisterNavItem(navContainer, 9, "Seguridad y MDB", "Usuarios RBAC, auditoría, migrador MDB y respaldo SQLite", IconKind.Shield,
            () => _auth.HasPermission(PermisoSistema.AdministrarSeguridad)
                  || _auth.HasPermission(PermisoSistema.VerAuditoria)
                  || _auth.HasPermission(PermisoSistema.MigrarDesdeMdb)
                  || _auth.HasPermission(PermisoSistema.PurgarTablasPesaje),
            "Atajo Ctrl+0");

        _sidebar.Controls.Add(navContainer);
        _sidebar.Controls.Add(brandPanel);

        // Header superior
        _header.Dock = DockStyle.Top;
        _header.Height = 60;
        _header.BackColor = AppTheme.HeaderDark;
        _header.Padding = new Padding(16, 8, 14, 8);

        Panel headerTitleBox = new() { Dock = DockStyle.Fill };
        _lblHeaderModule.Text = "Dashboard";
        _lblHeaderModule.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
        _lblHeaderModule.ForeColor = Color.White;
        _lblHeaderModule.Location = new Point(0, 2);
        _lblHeaderModule.AutoSize = true;

        _lblHeaderSub.Text = "Resumen ejecutivo";
        _lblHeaderSub.Font = new Font("Segoe UI", 8.5F);
        _lblHeaderSub.ForeColor = Color.FromArgb(148, 163, 184);
        _lblHeaderSub.Location = new Point(1, 27);
        _lblHeaderSub.AutoSize = true;
        headerTitleBox.Controls.AddRange([_lblHeaderModule, _lblHeaderSub]);

        _btnQuickPa = AppTheme.CreateButton("PA Firebird", IconKind.Play, ButtonVariant.GhostDark,
            (_, _) => OpenPaFirebirdDialog(), "Ejecutar procedimientos almacenados SAP en Firebird", _toolTip, 118, 32);
        _btnQuickPurge = AppTheme.CreateButton("Purgar", IconKind.Trash, ButtonVariant.GhostDark,
            (_, _) => OpenPurgeDialog(), "Limpiar tablas de pesajes (requiere Administrador)", _toolTip, 96, 32);
        _btnQuickConfig = AppTheme.CreateButton("Config. BD", IconKind.Settings, ButtonVariant.GhostDark,
            (_, _) => OpenConfigDbDialog(), "Configuración de rutas SQLite y servidores Firebird", _toolTip, 112, 32);
        _btnQuickPassword = AppTheme.CreateButton("Mi Clave", IconKind.Key, ButtonVariant.GhostDark,
            (_, _) => OpenChangePasswordDialog(), "Cambiar contraseña de la sesión actual", _toolTip, 108, 32);
        _btnLogout = AppTheme.CreateButton("Salir", IconKind.Logout, ButtonVariant.Danger,
            (_, _) => RequestLogout(), "Cerrar sesión actual y volver a la pantalla de acceso", _toolTip, 86, 32);

        FlowLayoutPanel headerActions = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        headerActions.Controls.AddRange([_btnQuickPa, _btnQuickPurge, _btnQuickConfig, _btnQuickPassword, _btnLogout]);
        _header.Controls.Add(headerTitleBox);
        _header.Controls.Add(headerActions);

        // Área central de contenido
        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = AppTheme.CanvasLight;

        // Barra de estado inferior
        StatusStrip status = new()
        {
            BackColor = AppTheme.HeaderDark,
            ForeColor = Color.White,
            SizingGrip = false
        };
        _lblUserStatus.ForeColor = Color.FromArgb(125, 211, 252);
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
            Text = "  " + title,
            Image = IconFactory.GetIcon(icon, Color.FromArgb(203, 213, 225), 16),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Dock = DockStyle.Top,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SidebarDark,
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = AppTheme.FontBodyBold,
            Padding = new Padding(12, 0, 8, 0),
            Cursor = Cursors.Hand,
            AccessibleName = title,
            AccessibleDescription = tooltip,
            Tag = index
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 48, 78);
        btn.Click += async (_, _) => await NavigateToAsync(index);
        _toolTip.SetToolTip(btn, $"{subtitle} ({tooltip})");

        NavItem item = new(index, title, subtitle, icon, isAllowed, btn);
        _navItems.Add(item);

        // Orden inverso por DockStyle.Top: agregamos en container y luego reordenamos por ChildIndex
        container.Controls.Add(btn);
        container.Controls.SetChildIndex(btn, 0);
    }

    private void ApplySessionToChrome()
    {
        Usuario? user = _auth.CurrentUser;
        _lblUserStatus.Text = user is null
            ? "Sin sesión activa"
            : $"Usuario: {user.NombreCompleto} ({user.NombreUsuario} · {user.Rol})";
        _lblDbStatus.Text = $"SQLite: {Path.GetFileName(_settings.SqliteDatabasePath)}";
        _lblOpStatus.Text = "Sistema listo.";

        _btnQuickPa.Enabled = _auth.HasPermission(PermisoSistema.EjecutarProcedimientosFirebird);
        _btnQuickPurge.Enabled = _auth.HasPermission(PermisoSistema.PurgarTablasPesaje);
        _btnQuickConfig.Enabled = _auth.HasPermission(PermisoSistema.AdministrarSeguridad) || _auth.HasPermission(PermisoSistema.ImportarFirebird);

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
                item.Button.Image = IconFactory.GetIcon(item.Icon, isActive ? Color.White : Color.FromArgb(203, 213, 225), 16);
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
        0 => new DashboardControl(_app, _auth, NavigateTo),
        1 => new GrupoMaterialControl(_app, _auth),
        2 => new ComprasTrasladosControl(_app, _auth),
        3 => new InformeClienteControl(_app, _auth),
        4 => new GruposEspecialesControl(_app, _auth),
        5 => new InformeJcrammControl(_app, _auth),
        6 => new InformePreciosControl(_app, _auth),
        7 => new MantenedorPreciosControl(_app, _auth),
        8 => new ImportacionFirebirdControl(_app, _auth, _settingsStore, _settings, RefreshDashboardIfLoadedAsync),
        9 => new AdministracionSeguridadControl(_app, _auth, _settings),
        _ => new DashboardControl(_app, _auth, NavigateTo)
    };

    private async Task RefreshDashboardIfLoadedAsync()
    {
        if (_tabs.TryGetValue(0, out ScrapTabControlBase? dash) && dash.Visible)
        {
            await dash.ActivateAsync();
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
            Keys.D6 => 5,
            Keys.D7 => 6,
            Keys.D8 => 7,
            Keys.D9 => 8,
            Keys.D0 => 9,
            _ => null
        };

        if (targetIndex.HasValue)
        {
            e.SuppressKeyPress = true;
            NavigateTo(targetIndex.Value);
        }
    }

    private void OpenPaFirebirdDialog()
    {
        FirebirdPlantaEndpoint ep = _settings.FirebirdEndpoints.FirstOrDefault()
            ?? AppSettingsStore.CreateDefaultEndpoints()[0];
        using EjecutarPaFirebirdDialog dlg = new(_app, ep);
        dlg.ShowDialog(this);
    }

    private void OpenPurgeDialog()
    {
        using LimpiarTablasDialog dlg = new(_app);
        if (dlg.ShowDialog(this) == DialogResult.OK && _tabs.TryGetValue(_activeTabIndex, out ScrapTabControlBase? active))
        {
            _ = active.ActivateAsync();
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
