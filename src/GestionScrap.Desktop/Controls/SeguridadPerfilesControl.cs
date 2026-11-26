using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 5: Administración completa de Usuarios, Perfiles RBAC configurables, Auditoría y Respaldo SQLite.
/// </summary>
public sealed class SeguridadPerfilesControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettings _settings;

    private readonly TabControl _innerTabs = new() { Dock = DockStyle.Fill };

    // SubTab 1: Usuarios
    private readonly DoubleBufferedDataGridView _dgvUsers = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly TextBox _txtUsername = new() { Width = 175 };
    private readonly TextBox _txtNombre = new() { Width = 240 };
    private readonly ComboBox _cmbPerfil = new() { Width = 175, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtPass = new() { Width = 195, UseSystemPasswordChar = true };
    private readonly CheckBox _chkActivo = new() { Text = "Cuenta Activa", Checked = true, AutoSize = true };
    private readonly CheckedListBox _clbUserOverrides = new() { CheckOnClick = true };
    private readonly Button _btnClearOverrides;
    private readonly Button _btnNuevoUser;
    private readonly Button _btnGuardarUser;
    private readonly Button _btnDesbloquearUser;
    private readonly Button _btnEliminarUser;
    private long _editingUserId;
    private string _existingHash = string.Empty;
    private string _existingSalt = string.Empty;

    // SubTab 2: Perfiles RBAC
    private readonly DoubleBufferedDataGridView _dgvPerfiles = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly TextBox _txtPerfilNombre = new() { Width = 220 };
    private readonly TextBox _txtPerfilDesc = new() { Width = 280 };
    private readonly CheckedListBox _clbPerfilPermisos = new() { CheckOnClick = true };
    private readonly Button _btnNuevoPerfil;
    private readonly Button _btnGuardarPerfil;
    private readonly Button _btnEliminarPerfil;
    private long _editingPerfilId;
    private bool _editingPerfilSystem;

    // SubTab 3: Auditoría & Mantenimiento BD
    private readonly ComboBox _cmbAccion = new() { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtUserFiltro = new() { Width = 150 };
    private readonly Button _btnFiltrarAudit;
    private readonly Button _btnExportarAudit;
    private readonly DoubleBufferedDataGridView _dgvAudit = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly Button _btnBackupSqlite;
    private readonly Button _btnPurgarDb;

    private IReadOnlyList<PerfilUsuario> _perfiles = [];
    private IReadOnlyList<AuditLogEntry> _lastLogs = [];

    private sealed record PerfilComboItem(long Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    public SeguridadPerfilesControl(ScrapApplicationService app, IAuthService auth, AppSettings settings)
    {
        _app = app;
        _auth = auth;
        _settings = settings;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);
        _innerTabs.Font = AppTheme.FontBodyBold;

        // -----------------------------------------------------------------
        // 1. GESTIÓN DE USUARIOS
        // -----------------------------------------------------------------
        TabPage tabUsers = new("Usuarios del Sistema") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };
        TableLayoutPanel usersSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        usersSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        usersSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));

        Panel pnlUGrid = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle };
        Label lblUG = new() { Text = "Cuentas Registradas (clic para editar o asignar perfil)", Font = AppTheme.FontSection, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 28 };
        _dgvUsers.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(Usuario.Id), "N0", 35),
            UiHelpers.AddTextCol("Usuario", nameof(Usuario.NombreUsuario), 85),
            UiHelpers.AddTextCol("Nombre Completo", nameof(Usuario.NombreCompleto), 125),
            UiHelpers.AddTextCol("Perfil", nameof(Usuario.NombrePerfil), 85),
            UiHelpers.AddTextCol("Activo", nameof(Usuario.Activo), 45),
            UiHelpers.AddNumberCol("Fallos", nameof(Usuario.IntentosFallidos), "N0", 45)
        ]);
        _dgvUsers.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvUsers.Rows[e.RowIndex].DataBoundItem is Usuario u)
            {
                LoadUserToEditor(u);
            }
        };
        pnlUGrid.Controls.Add(_dgvUsers);
        pnlUGrid.Controls.Add(lblUG);

        Panel pnlUEd = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(12), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle, AutoScroll = true };
        AppTheme.ApplyInputStyle(_txtUsername);
        AppTheme.ApplyInputStyle(_txtNombre);
        AppTheme.ApplyInputStyle(_txtPass);
        _txtPass.PlaceholderText = "Vacío = mantener clave actual";
        _cmbPerfil.Font = AppTheme.FontBody;

        foreach (PermisoSistema p in Enum.GetValues<PermisoSistema>())
        {
            _clbUserOverrides.Items.Add(p);
        }

        _clbUserOverrides.Font = AppTheme.FontBody;
        _clbUserOverrides.Size = new Size(380, 155);

        _btnClearOverrides = AppTheme.CreateButton("Heredar del Perfil", IconKind.Refresh, ButtonVariant.Secondary,
            (_, _) => ClearOverrideCheckboxes(), "Deja vacías las excepciones para que use exactamente los permisos de su perfil", Tip, 155, 30);
        _btnNuevoUser = AppTheme.CreateButton("Nuevo", IconKind.Plus, ButtonVariant.Secondary,
            (_, _) => ResetUserEditor(), "Prepara el editor para crear una nueva cuenta", Tip, 95, 34);
        _btnGuardarUser = AppTheme.CreateButton("Guardar Usuario", IconKind.Save, ButtonVariant.Success,
            async (_, _) => await SaveUserAsync(), "Crea o actualiza el usuario seleccionado", Tip, 145, 34);
        _btnDesbloquearUser = AppTheme.CreateButton("Desbloquear", IconKind.Lock, ButtonVariant.Warning,
            async (_, _) => await UnlockSelectedUserAsync(), "Desbloquea cuenta tras intentos fallidos", Tip, 125, 34);
        _btnEliminarUser = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger,
            async (_, _) => await DeleteUserAsync(), "Elimina el usuario seleccionado", Tip, 105, 34);

        TableLayoutPanel uEdLayout = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        uEdLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        uEdLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        void AddUserRow(int r, string lbl, Control c)
        {
            uEdLayout.Controls.Add(new Label { Text = lbl, Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 4) }, 0, r);
            c.Margin = new Padding(0, 4, 4, 4);
            uEdLayout.Controls.Add(c, 1, r);
        }

        AddUserRow(0, "Usuario:", _txtUsername);
        AddUserRow(1, "Nombre:", _txtNombre);
        AddUserRow(2, "Perfil / Rol:", _cmbPerfil);
        AddUserRow(3, "Contraseña:", _txtPass);
        AddUserRow(4, "Estado:", _chkActivo);

        FlowLayoutPanel ovTop = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        ovTop.Controls.AddRange([new Label { Text = "Excepciones por Usuario (opcional):", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, _btnClearOverrides]);
        uEdLayout.Controls.Add(ovTop, 0, 5);
        uEdLayout.SetColumnSpan(ovTop, 2);

        uEdLayout.Controls.Add(_clbUserOverrides, 0, 6);
        uEdLayout.SetColumnSpan(_clbUserOverrides, 2);

        FlowLayoutPanel uBtns = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        uBtns.Controls.AddRange([_btnNuevoUser, _btnGuardarUser, _btnDesbloquearUser, _btnEliminarUser]);
        uEdLayout.Controls.Add(uBtns, 0, 7);
        uEdLayout.SetColumnSpan(uBtns, 2);

        pnlUEd.Controls.Add(uEdLayout);
        usersSplit.Controls.Add(pnlUGrid, 0, 0);
        usersSplit.Controls.Add(pnlUEd, 1, 0);
        tabUsers.Controls.Add(usersSplit);

        // -----------------------------------------------------------------
        // 2. GESTIÓN DE PERFILES RBAC
        // -----------------------------------------------------------------
        TabPage tabPerfiles = new("Perfiles y Permisos RBAC") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };
        TableLayoutPanel perfSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        perfSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        perfSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));

        Panel pnlPGrid = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle };
        Label lblPG = new() { Text = "Perfiles de Acceso Configurables (clic para editar permisos)", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 28 };
        _dgvPerfiles.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(PerfilUsuario.Id), "N0", 35),
            UiHelpers.AddTextCol("Nombre del Perfil", nameof(PerfilUsuario.Nombre), 95),
            UiHelpers.AddTextCol("Descripción", nameof(PerfilUsuario.Descripcion), 175),
            UiHelpers.AddTextCol("Sistema", nameof(PerfilUsuario.EsSistema), 50)
        ]);
        _dgvPerfiles.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvPerfiles.Rows[e.RowIndex].DataBoundItem is PerfilUsuario pr)
            {
                LoadPerfilToEditor(pr);
            }
        };
        pnlPGrid.Controls.Add(_dgvPerfiles);
        pnlPGrid.Controls.Add(lblPG);

        Panel pnlPEd = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(12), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle, AutoScroll = true };
        AppTheme.ApplyInputStyle(_txtPerfilNombre);
        AppTheme.ApplyInputStyle(_txtPerfilDesc);
        foreach (PermisoSistema p in Enum.GetValues<PermisoSistema>())
        {
            _clbPerfilPermisos.Items.Add(p);
        }

        _clbPerfilPermisos.Font = AppTheme.FontBody;
        _clbPerfilPermisos.Size = new Size(390, 200);

        _btnNuevoPerfil = AppTheme.CreateButton("Nuevo Perfil", IconKind.Plus, ButtonVariant.Secondary,
            (_, _) => ResetPerfilEditor(), "Crear un nuevo perfil de acceso", Tip, 130, 34);
        _btnGuardarPerfil = AppTheme.CreateButton("Guardar Perfil", IconKind.Save, ButtonVariant.Success,
            async (_, _) => await SavePerfilAsync(), "Guarda el perfil y actualiza los permisos de sus usuarios", Tip, 145, 34);
        _btnEliminarPerfil = AppTheme.CreateButton("Eliminar Perfil", IconKind.Trash, ButtonVariant.Danger,
            async (_, _) => await DeletePerfilAsync(), "Elimina el perfil personalizado seleccionado", Tip, 140, 34);

        TableLayoutPanel pEdLayout = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pEdLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
        pEdLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        pEdLayout.Controls.Add(new Label { Text = "Nombre Perfil:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 4) }, 0, 0);
        pEdLayout.Controls.Add(_txtPerfilNombre, 1, 0);
        pEdLayout.Controls.Add(new Label { Text = "Descripción:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 4) }, 0, 1);
        pEdLayout.Controls.Add(_txtPerfilDesc, 1, 1);
        pEdLayout.Controls.Add(new Label { Text = "Permisos Habilitados:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 4, 4) }, 0, 2);
        pEdLayout.SetColumnSpan(pEdLayout.GetControlFromPosition(0, 2)!, 2);
        pEdLayout.Controls.Add(_clbPerfilPermisos, 0, 3);
        pEdLayout.SetColumnSpan(_clbPerfilPermisos, 2);

        FlowLayoutPanel pBtns = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        pBtns.Controls.AddRange([_btnNuevoPerfil, _btnGuardarPerfil, _btnEliminarPerfil]);
        pEdLayout.Controls.Add(pBtns, 0, 4);
        pEdLayout.SetColumnSpan(pBtns, 2);

        pnlPEd.Controls.Add(pEdLayout);
        perfSplit.Controls.Add(pnlPGrid, 0, 0);
        perfSplit.Controls.Add(pnlPEd, 1, 0);
        tabPerfiles.Controls.Add(perfSplit);

        // -----------------------------------------------------------------
        // 3. AUDITORÍA & MANTENIMIENTO SQLITE
        // -----------------------------------------------------------------
        TabPage tabAudit = new("Auditoría y Respaldo SQLite") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        Panel auditTop = UiHelpers.BuildFilterCard(58);
        auditTop.BorderStyle = BorderStyle.FixedSingle;
        _cmbAccion.Items.AddRange(
        [
            "Todas",
            "LOGIN_EXITOSO",
            "LOGIN_FALLIDO",
            "CAMBIO_CLAVE",
            "IMPORTACION_PESAJES",
            "SINCRONIZACION_CLIENTES",
            "SINCRONIZACION_PRODUCTOS",
            "MIGRACION_MDB",
            "NODO_GRUPO_CREAR",
            "NODO_GRUPO_EDITAR",
            "NODO_GRUPO_MOVER",
            "NODO_GRUPO_REORDENAR",
            "NODO_GRUPO_ELIMINAR",
            "PERFIL_CREAR",
            "PERFIL_EDITAR",
            "PERFIL_ELIMINAR",
            "CREACION_USUARIO",
            "ACTUALIZACION_USUARIO",
            "ELIMINACION_USUARIO",
            "PURGA_TABLAS"
        ]);
        _cmbAccion.SelectedIndex = 0;
        AppTheme.ApplyInputStyle(_txtUserFiltro);
        _txtUserFiltro.PlaceholderText = "Filtrar usuario...";

        _btnFiltrarAudit = AppTheme.CreateButton("Filtrar Logs", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await LoadAuditLogsAsync(), "Consulta los registros de auditoría en SQLite", Tip, 125, 32);
        _btnExportarAudit = AppTheme.CreateButton("Exportar Logs", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportAuditLogs(), "Exporta la bitácora de auditoría a Excel", Tip, 138, 32);

        FlowLayoutPanel aFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        aFlow.Controls.AddRange(
        [
            new Label { Text = "Acción:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 4, 0) },
            _cmbAccion,
            new Label { Text = "Usuario:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(10, 8, 4, 0) },
            _txtUserFiltro,
            _btnFiltrarAudit,
            _btnExportarAudit
        ]);
        auditTop.Controls.Add(aFlow);

        Panel auditGridCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _dgvAudit.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(AuditLogEntry.Id), "N0", 40),
            UiHelpers.AddTextCol("Fecha UTC", nameof(AuditLogEntry.TimestampUtc), 95),
            UiHelpers.AddTextCol("Usuario", nameof(AuditLogEntry.Usuario), 75),
            UiHelpers.AddTextCol("Acción", nameof(AuditLogEntry.Accion), 100),
            UiHelpers.AddTextCol("Entidad", nameof(AuditLogEntry.Entidad), 70),
            UiHelpers.AddTextCol("ID Reg.", nameof(AuditLogEntry.RegistroId), 60),
            UiHelpers.AddTextCol("Detalle Operativo", nameof(AuditLogEntry.Detalle), 220)
        ]);
        auditGridCard.Controls.Add(_dgvAudit);

        Panel maintCard = new() { Dock = DockStyle.Bottom, Height = 64, BackColor = AppTheme.CardSurface, Padding = new Padding(12), Margin = new Padding(0, 8, 0, 0), BorderStyle = BorderStyle.FixedSingle };
        Label lblDb = new() { Text = $"Base SQLite: {settings.SqliteDatabasePath}", Font = AppTheme.FontMono, ForeColor = AppTheme.TextSecondary, AutoSize = true, Margin = new Padding(0, 10, 14, 0) };
        _btnBackupSqlite = AppTheme.CreateButton("Respaldar SQLite (.db)", IconKind.Backup, ButtonVariant.Primary,
            async (_, _) => await BackupSqliteAsync(), "Genera copia íntegra en caliente usando SQLite Backup API", Tip, 185, 34);
        _btnPurgarDb = AppTheme.CreateButton("Purgar Pesajes", IconKind.Trash, ButtonVariant.Danger,
            (_, _) => OpenPurgeDialog(), "Elimina registros de pesajes (requiere confirmación escrita ELIMINAR)", Tip, 150, 34);

        FlowLayoutPanel maintFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        maintFlow.Controls.AddRange([lblDb, _btnBackupSqlite, _btnPurgarDb]);
        maintCard.Controls.Add(maintFlow);

        tabAudit.Controls.Add(auditGridCard);
        tabAudit.Controls.Add(maintCard);
        tabAudit.Controls.Add(auditTop);

        _innerTabs.TabPages.AddRange([tabUsers, tabPerfiles, tabAudit]);
        Controls.Add(_innerTabs);

        ResetUserEditor();
        ResetPerfilEditor();
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canAdmin = _auth.HasPermission(PermisoSistema.AdministrarSeguridadPerfiles);
        bool canAudit = _auth.HasPermission(PermisoSistema.VerAuditoria);
        bool canMaint = _auth.HasPermission(PermisoSistema.MantenimientoBaseDatos);

        _btnNuevoUser.Enabled = canAdmin;
        _btnGuardarUser.Enabled = canAdmin;
        _btnDesbloquearUser.Enabled = canAdmin;
        _btnEliminarUser.Enabled = canAdmin;
        _btnClearOverrides.Enabled = canAdmin;

        _btnNuevoPerfil.Enabled = canAdmin;
        _btnGuardarPerfil.Enabled = canAdmin;
        _btnEliminarPerfil.Enabled = canAdmin;

        _btnFiltrarAudit.Enabled = canAudit;
        _btnExportarAudit.Enabled = canAudit && _auth.HasPermission(PermisoSistema.ExportarExcel);
        _btnBackupSqlite.Enabled = canMaint;
        _btnPurgarDb.Enabled = canMaint;
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        RefreshPermissions();
        if (_auth.HasPermission(PermisoSistema.AdministrarSeguridadPerfiles))
        {
            await LoadPerfilesAsync(ct);
            await LoadUsersAsync(ct);
        }

        if (_auth.HasPermission(PermisoSistema.VerAuditoria))
        {
            await LoadAuditLogsAsync(ct);
        }
    }

    private async Task LoadPerfilesAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            _perfiles = await _app.GetPerfilesAsync(ct);
            _dgvPerfiles.DataSource = null;
            _dgvPerfiles.DataSource = _perfiles;

            long? prevId = (_cmbPerfil.SelectedItem as PerfilComboItem)?.Id;
            _cmbPerfil.Items.Clear();
            foreach (PerfilUsuario p in _perfiles)
            {
                _cmbPerfil.Items.Add(new PerfilComboItem(p.Id, p.Nombre));
            }

            if (_cmbPerfil.Items.Count > 0)
            {
                int idx = 0;
                if (prevId.HasValue)
                {
                    for (int i = 0; i < _cmbPerfil.Items.Count; i++)
                    {
                        if (_cmbPerfil.Items[i] is PerfilComboItem item && item.Id == prevId.Value)
                        {
                            idx = i;
                            break;
                        }
                    }
                }

                _cmbPerfil.SelectedIndex = idx;
            }
        }, NotifyStatus);
    }

    private async Task LoadUsersAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            IReadOnlyList<Usuario> users = await _app.GetUsuariosAsync(ct);
            _dgvUsers.DataSource = null;
            _dgvUsers.DataSource = users;
        }, NotifyStatus);
    }

    private void LoadUserToEditor(Usuario u)
    {
        _editingUserId = u.Id;
        _existingHash = u.PasswordHash;
        _existingSalt = u.PasswordSalt;
        _txtUsername.Text = u.NombreUsuario;
        _txtNombre.Text = u.NombreCompleto;
        _txtPass.Clear();
        _chkActivo.Checked = u.Activo;

        for (int i = 0; i < _cmbPerfil.Items.Count; i++)
        {
            if (_cmbPerfil.Items[i] is PerfilComboItem pci && pci.Id == u.PerfilId)
            {
                _cmbPerfil.SelectedIndex = i;
                break;
            }
        }

        for (int i = 0; i < _clbUserOverrides.Items.Count; i++)
        {
            if (_clbUserOverrides.Items[i] is PermisoSistema perm)
            {
                _clbUserOverrides.SetItemChecked(i, u.PermisosPersonalizados.Contains(perm));
            }
        }
    }

    private void ResetUserEditor()
    {
        _editingUserId = 0;
        _existingHash = string.Empty;
        _existingSalt = string.Empty;
        _txtUsername.Clear();
        _txtNombre.Clear();
        _txtPass.Clear();
        _chkActivo.Checked = true;
        if (_cmbPerfil.Items.Count > 0)
        {
            _cmbPerfil.SelectedIndex = 0;
        }

        ClearOverrideCheckboxes();
    }

    private void ClearOverrideCheckboxes()
    {
        for (int i = 0; i < _clbUserOverrides.Items.Count; i++)
        {
            _clbUserOverrides.SetItemChecked(i, false);
        }
    }

    private async Task SaveUserAsync()
    {
        if (_cmbPerfil.SelectedItem is not PerfilComboItem selPerfil)
        {
            MessageBox.Show(this, "Seleccione un perfil para el usuario.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            List<PermisoSistema> overrides = [];
            foreach (object obj in _clbUserOverrides.CheckedItems)
            {
                if (obj is PermisoSistema perm)
                {
                    overrides.Add(perm);
                }
            }

            Usuario user = new()
            {
                Id = _editingUserId,
                NombreUsuario = _txtUsername.Text.Trim(),
                NombreCompleto = _txtNombre.Text.Trim(),
                PerfilId = selPerfil.Id,
                NombrePerfil = selPerfil.Nombre,
                PasswordHash = _existingHash,
                PasswordSalt = _existingSalt,
                Activo = _chkActivo.Checked,
                PermisosPersonalizados = overrides
            };

            string? plainPass = string.IsNullOrWhiteSpace(_txtPass.Text) ? null : _txtPass.Text;
            await _app.SaveUsuarioAsync(user, plainPass);
            ResetUserEditor();
            await LoadUsersAsync();
            NotifyStatus($"Usuario '{user.NombreUsuario}' guardado (Perfil: {selPerfil.Nombre}).");
        }, NotifyStatus, "Guardando usuario...");
    }

    private async Task UnlockSelectedUserAsync()
    {
        if (_dgvUsers.CurrentRow?.DataBoundItem is not Usuario selected)
        {
            MessageBox.Show(this, "Seleccione primero un usuario de la grilla.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Usuario unlocked = new()
            {
                Id = selected.Id,
                NombreUsuario = selected.NombreUsuario,
                NombreCompleto = selected.NombreCompleto,
                PerfilId = selected.PerfilId,
                NombrePerfil = selected.NombrePerfil,
                PasswordHash = selected.PasswordHash,
                PasswordSalt = selected.PasswordSalt,
                Activo = true,
                IntentosFallidos = 0,
                BloqueadoHastaUtc = null,
                PermisosPersonalizados = selected.PermisosPersonalizados,
                CreadoEnUtc = selected.CreadoEnUtc
            };
            await _app.SaveUsuarioAsync(unlocked, null);
            await LoadUsersAsync();
            NotifyStatus($"Usuario '{selected.NombreUsuario}' desbloqueado.");
        }, NotifyStatus);
    }

    private async Task DeleteUserAsync()
    {
        if (_dgvUsers.CurrentRow?.DataBoundItem is not Usuario selected)
        {
            MessageBox.Show(this, "Seleccione el usuario que desea eliminar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            this,
            $"¿Confirma eliminar la cuenta '{selected.NombreUsuario}'?",
            "Eliminar Usuario",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.DeleteUsuarioAsync(selected.Id);
            ResetUserEditor();
            await LoadUsersAsync();
            NotifyStatus($"Usuario '{selected.NombreUsuario}' eliminado.");
        }, NotifyStatus);
    }

    private void LoadPerfilToEditor(PerfilUsuario p)
    {
        _editingPerfilId = p.Id;
        _editingPerfilSystem = p.EsSistema;
        _txtPerfilNombre.Text = p.Nombre;
        _txtPerfilDesc.Text = p.Descripcion;
        for (int i = 0; i < _clbPerfilPermisos.Items.Count; i++)
        {
            if (_clbPerfilPermisos.Items[i] is PermisoSistema perm)
            {
                _clbPerfilPermisos.SetItemChecked(i, p.Permisos.Contains(perm));
            }
        }
    }

    private void ResetPerfilEditor()
    {
        _editingPerfilId = 0;
        _editingPerfilSystem = false;
        _txtPerfilNombre.Clear();
        _txtPerfilDesc.Clear();
        for (int i = 0; i < _clbPerfilPermisos.Items.Count; i++)
        {
            _clbPerfilPermisos.SetItemChecked(i, i < 4);
        }
    }

    private async Task SavePerfilAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            List<PermisoSistema> perms = [];
            foreach (object obj in _clbPerfilPermisos.CheckedItems)
            {
                if (obj is PermisoSistema p)
                {
                    perms.Add(p);
                }
            }

            PerfilUsuario perfil = new()
            {
                Id = _editingPerfilId,
                Nombre = _txtPerfilNombre.Text.Trim(),
                Descripcion = _txtPerfilDesc.Text.Trim(),
                EsSistema = _editingPerfilSystem,
                Permisos = perms
            };

            await _app.SavePerfilAsync(perfil);
            ResetPerfilEditor();
            await LoadPerfilesAsync();
            await LoadUsersAsync();
            NotifyStatus($"Perfil '{perfil.Nombre}' guardado correctamente.");
        }, NotifyStatus, "Guardando perfil...");
    }

    private async Task DeletePerfilAsync()
    {
        if (_dgvPerfiles.CurrentRow?.DataBoundItem is not PerfilUsuario selected)
        {
            MessageBox.Show(this, "Seleccione el perfil a eliminar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            this,
            $"¿Confirma eliminar el perfil '{selected.Nombre}'?",
            "Eliminar Perfil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.DeletePerfilAsync(selected.Id);
            ResetPerfilEditor();
            await LoadPerfilesAsync();
            NotifyStatus($"Perfil '{selected.Nombre}' eliminado.");
        }, NotifyStatus);
    }

    private async Task LoadAuditLogsAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            string? accion = _cmbAccion.SelectedItem?.ToString();
            if (string.Equals(accion, "Todas", StringComparison.Ordinal))
            {
                accion = null;
            }

            string? usr = string.IsNullOrWhiteSpace(_txtUserFiltro.Text) ? null : _txtUserFiltro.Text.Trim();
            _lastLogs = await _app.GetAuditLogsAsync(500, accion, usr, ct);
            _dgvAudit.DataSource = null;
            _dgvAudit.DataSource = _lastLogs;
            NotifyStatus($"Auditoría cargada: {_lastLogs.Count} eventos.");
        }, NotifyStatus);
    }

    private void ExportAuditLogs()
    {
        if (_lastLogs.Count == 0)
        {
            MessageBox.Show(this, "No hay registros de auditoría para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"Auditoria_Cambiaso_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportAuditLogs(path, _lastLogs);
            NotifyStatus($"Auditoría exportada: {saved}");
            MessageBox.Show(this, $"Bitácora exportada:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task BackupSqliteAsync()
    {
        using SaveFileDialog sfd = new()
        {
            Title = "Respaldar Base de Datos SQLite",
            Filter = "Base SQLite (*.db)|*.db",
            DefaultExt = "db",
            AddExtension = true,
            FileName = $"gestion_scrap_respaldo_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            string saved = await _app.BackupDatabaseAsync(sfd.FileName);
            NotifyStatus($"Respaldo SQLite guardado: {saved}");
            MessageBox.Show(this, $"Respaldo generado correctamente:\n{saved}", "Respaldo Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, NotifyStatus, "Generando respaldo SQLite...");
    }

    private void OpenPurgeDialog()
    {
        using LimpiarTablasDialog dlg = new(_app);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            NotifyStatus($"Purga completada: {dlg.PurgedCount} pesajes eliminados.");
        }
    }
}
