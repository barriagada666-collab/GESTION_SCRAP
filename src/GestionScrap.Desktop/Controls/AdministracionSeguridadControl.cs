using System.Globalization;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo administrativo de Seguridad (RBAC), Auditoría Forense, Migración desde Access MDB y Respaldo SQLite.
/// </summary>
public sealed class AdministracionSeguridadControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;
    private readonly AppSettings _settings;

    private readonly TabControl _innerTabs = new() { Dock = DockStyle.Fill };

    // Sub-tab Usuarios
    private readonly DoubleBufferedDataGridView _dgvUsers = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly TextBox _txtUsername = new() { Width = 170 };
    private readonly TextBox _txtNombre = new() { Width = 240 };
    private readonly ComboBox _cmbRol = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtPass = new() { Width = 190, UseSystemPasswordChar = true };
    private readonly CheckBox _chkActivo = new() { Text = "Usuario Activo", Checked = true, AutoSize = true };
    private readonly CheckedListBox _clbPermisos = new() { CheckOnClick = true };
    private readonly Button _btnNuevoUser;
    private readonly Button _btnGuardarUser;
    private readonly Button _btnEliminarUser;
    private readonly Button _btnDesbloquearUser;
    private readonly Button _btnResetPermDefaults;
    private long _editingUserId;
    private string _existingHash = string.Empty;
    private string _existingSalt = string.Empty;

    // Sub-tab Auditoría
    private readonly ComboBox _cmbAccion = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtUserFiltro = new() { Width = 150 };
    private readonly Button _btnFiltrarAudit;
    private readonly Button _btnExportarAudit;
    private readonly DoubleBufferedDataGridView _dgvAudit = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private IReadOnlyList<AuditLogEntry> _lastLogs = [];

    // Sub-tab Migración MDB & Mantenimiento
    private readonly TextBox _txtMdbPath = new();
    private readonly Button _btnExaminarMdb;
    private readonly Button _btnMigrarMdb;
    private readonly TextBox _txtMdbLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label _lblSqliteInfo = new();
    private readonly Button _btnBackupSqlite;
    private readonly Button _btnResetDemo;
    private readonly Button _btnPurgarDb;

    public AdministracionSeguridadControl(ScrapApplicationService app, IAuthService auth, AppSettings settings)
    {
        _app = app;
        _auth = auth;
        _settings = settings;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(14);

        _innerTabs.Font = AppTheme.FontBodyBold;

        // 1. Tab Usuarios & RBAC
        TabPage tabUsers = new("Usuarios y Permisos RBAC") { BackColor = AppTheme.CanvasLight, Padding = new Padding(10) };
        TableLayoutPanel usersSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        usersSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        usersSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));

        Panel pnlUsersGrid = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle };
        Label lblUG = new() { Text = "Cuentas Registradas (clic para editar)", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 28 };
        _dgvUsers.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(Usuario.Id), "N0", 35),
            UiHelpers.AddTextCol("Usuario", nameof(Usuario.NombreUsuario), 80),
            UiHelpers.AddTextCol("Nombre Completo", nameof(Usuario.NombreCompleto), 120),
            UiHelpers.AddTextCol("Rol", nameof(Usuario.Rol), 75),
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
        pnlUsersGrid.Controls.Add(_dgvUsers);
        pnlUsersGrid.Controls.Add(lblUG);

        Panel pnlUserEd = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(12), Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle, AutoScroll = true };
        AppTheme.ApplyInputStyle(_txtUsername);
        AppTheme.ApplyInputStyle(_txtNombre);
        AppTheme.ApplyInputStyle(_txtPass);
        _txtPass.PlaceholderText = "Vacío = conservar clave actual";

        _cmbRol.DataSource = Enum.GetValues<RolUsuario>();
        _cmbRol.Font = AppTheme.FontBody;
        _cmbRol.SelectedIndexChanged += (_, _) =>
        {
            if (_editingUserId == 0)
            {
                ApplyRoleDefaultsToChecklist((RolUsuario)(_cmbRol.SelectedItem ?? RolUsuario.Operador));
            }
        };

        foreach (PermisoSistema p in Enum.GetValues<PermisoSistema>())
        {
            _clbPermisos.Items.Add(p);
        }

        _clbPermisos.Font = AppTheme.FontBody;
        _clbPermisos.Size = new Size(390, 175);

        _btnResetPermDefaults = AppTheme.CreateButton("Permisos del Rol", IconKind.Refresh, ButtonVariant.Secondary,
            (_, _) => ApplyRoleDefaultsToChecklist((RolUsuario)(_cmbRol.SelectedItem ?? RolUsuario.Operador)),
            "Carga los permisos estándar correspondientes al rol elegido", Tip, 155, 30);
        _btnNuevoUser = AppTheme.CreateButton("Nuevo", IconKind.Plus, ButtonVariant.Secondary,
            (_, _) => ResetUserEditor(), "Prepara el formulario para crear un nuevo usuario", Tip, 95, 34);
        _btnGuardarUser = AppTheme.CreateButton("Guardar", IconKind.Save, ButtonVariant.Success,
            async (_, _) => await SaveUserAsync(), "Guarda el usuario, rol y permisos personalizados", Tip, 110, 34);
        _btnEliminarUser = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger,
            async (_, _) => await DeleteUserAsync(), "Elimina el usuario seleccionado", Tip, 108, 34);
        _btnDesbloquearUser = AppTheme.CreateButton("Desbloquear", IconKind.Lock, ButtonVariant.Warning,
            async (_, _) => await UnlockSelectedUserAsync(), "Reinicia el contador de intentos fallidos y quita bloqueo temporal", Tip, 130, 34);

        TableLayoutPanel edLayout = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        edLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135F));
        edLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        void AddRow(int r, string lbl, Control ctrl)
        {
            edLayout.Controls.Add(new Label { Text = lbl, Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 4) }, 0, r);
            ctrl.Margin = new Padding(0, 4, 4, 4);
            edLayout.Controls.Add(ctrl, 1, r);
        }

        AddRow(0, "Usuario:", _txtUsername);
        AddRow(1, "Nombre Completo:", _txtNombre);
        AddRow(2, "Perfil / Rol:", _cmbRol);
        AddRow(3, "Contraseña:", _txtPass);
        AddRow(4, "Estado:", _chkActivo);

        FlowLayoutPanel permTop = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        permTop.Controls.AddRange([new Label { Text = "Permisos Granulares:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, _btnResetPermDefaults]);
        edLayout.Controls.Add(permTop, 0, 5);
        edLayout.SetColumnSpan(permTop, 2);

        edLayout.Controls.Add(_clbPermisos, 0, 6);
        edLayout.SetColumnSpan(_clbPermisos, 2);

        FlowLayoutPanel userBtns = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        userBtns.Controls.AddRange([_btnNuevoUser, _btnGuardarUser, _btnEliminarUser, _btnDesbloquearUser]);
        edLayout.Controls.Add(userBtns, 0, 7);
        edLayout.SetColumnSpan(userBtns, 2);

        pnlUserEd.Controls.Add(edLayout);
        usersSplit.Controls.Add(pnlUsersGrid, 0, 0);
        usersSplit.Controls.Add(pnlUserEd, 1, 0);
        tabUsers.Controls.Add(usersSplit);

        // 2. Tab Auditoría
        TabPage tabAudit = new("Registro de Auditoría") { BackColor = AppTheme.CanvasLight, Padding = new Padding(10) };
        Panel auditTop = UiHelpers.BuildFilterCard(58);
        auditTop.BorderStyle = BorderStyle.FixedSingle;
        _cmbAccion.Items.AddRange(
        [
            "Todas",
            "LOGIN_EXITOSO",
            "LOGIN_FALLIDO",
            "CAMBIO_CLAVE",
            "CREACION_PRECIO_PROV",
            "ACTUALIZACION_PRECIO_PROV",
            "ELIMINACION_PRECIO_PROV",
            "ACTUALIZACION_PRECIO_REAL",
            "CONGELAMIENTO_PRECIOS",
            "EXPORTACION_EXCEL",
            "IMPORTACION_PESAJES",
            "EJECUCION_PA",
            "MIGRACION_MDB",
            "PURGA_TABLAS",
            "RESTABLECER_DEMO",
            "CREACION_USUARIO",
            "ACTUALIZACION_USUARIO",
            "ELIMINACION_USUARIO"
        ]);
        _cmbAccion.SelectedIndex = 0;
        AppTheme.ApplyInputStyle(_txtUserFiltro);
        _txtUserFiltro.PlaceholderText = "Usuario (opcional)";

        _btnFiltrarAudit = AppTheme.CreateButton("Filtrar Logs", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await LoadAuditLogsAsync(), "Consulta la bitácora de auditoría en SQLite", Tip, 130, 34);
        _btnExportarAudit = AppTheme.CreateButton("Exportar Logs", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportAuditLogs(), "Exporta los registros de auditoría a Excel", Tip, 140, 34);

        FlowLayoutPanel auditFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        auditFlow.Controls.AddRange(
        [
            new Label { Text = "Acción:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 9, 6, 0) },
            _cmbAccion,
            new Label { Text = "Usuario:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(12, 9, 6, 0) },
            _txtUserFiltro,
            _btnFiltrarAudit,
            _btnExportarAudit
        ]);
        auditTop.Controls.Add(auditFlow);

        Panel auditGridCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
        _dgvAudit.Columns.AddRange(
        [
            UiHelpers.AddNumberCol("ID", nameof(AuditLogEntry.Id), "N0", 40),
            UiHelpers.AddTextCol("Fecha UTC", nameof(AuditLogEntry.TimestampUtc), 95),
            UiHelpers.AddTextCol("Usuario", nameof(AuditLogEntry.Usuario), 70),
            UiHelpers.AddTextCol("Acción", nameof(AuditLogEntry.Accion), 85),
            UiHelpers.AddTextCol("Entidad", nameof(AuditLogEntry.Entidad), 70),
            UiHelpers.AddTextCol("ID Reg.", nameof(AuditLogEntry.RegistroId), 65),
            UiHelpers.AddTextCol("Detalle Operativo", nameof(AuditLogEntry.Detalle), 220)
        ]);
        auditGridCard.Controls.Add(_dgvAudit);
        tabAudit.Controls.Add(auditGridCard);
        tabAudit.Controls.Add(auditTop);

        // 3. Tab Migración Access MDB & Respaldo SQLite
        TabPage tabMig = new("Migración Access MDB y Respaldo") { BackColor = AppTheme.CanvasLight, Padding = new Padding(10) };

        Panel cardMdb = new() { Dock = DockStyle.Top, Height = 290, BackColor = AppTheme.CardSurface, Padding = new Padding(14), BorderStyle = BorderStyle.FixedSingle };
        Label lblMdbTitle = new() { Text = "Importación Histórica desde Base Access (.mdb / .accdb)", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 28 };
        Label lblMdbSub = new() { Text = "Importa pesajes (Placilla, Santiago, Talcahuano), clientes, grupos de productos, tarifas por proveedor y folios indexados.", Font = AppTheme.FontSubtitle, ForeColor = AppTheme.TextSecondary, Dock = DockStyle.Top, Height = 26 };

        Panel mdbPickerRow = new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 4, 0, 4) };
        _txtMdbPath.SetBounds(0, 6, 480, 26);
        _txtMdbPath.Text = settings.LastMdbPath;
        AppTheme.ApplyInputStyle(_txtMdbPath);

        _btnExaminarMdb = AppTheme.CreateButton("Examinar .MDB", IconKind.Document, ButtonVariant.Secondary,
            (_, _) => BrowseMdb(), "Selecciona archivo BD_SCRAP.mdb heredado", Tip, 145, 32);
        _btnExaminarMdb.Location = new Point(490, 4);

        _btnMigrarMdb = AppTheme.CreateButton("Iniciar Migración", IconKind.Import, ButtonVariant.Success,
            async (_, _) => await RunMdbMigrationAsync(), "Migra registros Access hacia SQLite evitando duplicados", Tip, 165, 32);
        _btnMigrarMdb.Location = new Point(645, 4);
        mdbPickerRow.Controls.AddRange([_txtMdbPath, _btnExaminarMdb, _btnMigrarMdb]);

        _txtMdbLog.Dock = DockStyle.Fill;
        _txtMdbLog.Font = AppTheme.FontMono;
        _txtMdbLog.BackColor = Color.FromArgb(15, 23, 42);
        _txtMdbLog.ForeColor = Color.FromArgb(226, 232, 240);

        cardMdb.Controls.Add(_txtMdbLog);
        cardMdb.Controls.Add(mdbPickerRow);
        cardMdb.Controls.Add(lblMdbSub);
        cardMdb.Controls.Add(lblMdbTitle);

        Panel cardBackup = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(14), Margin = new Padding(0, 10, 0, 0), BorderStyle = BorderStyle.FixedSingle };
        Label lblBkTitle = new() { Text = "Administración de Base SQLite y Datos de Demostración", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentCyan, Dock = DockStyle.Top, Height = 28 };
        _lblSqliteInfo.Text = $"Archivo SQLite activo: {settings.SqliteDatabasePath}";
        _lblSqliteInfo.Font = AppTheme.FontMono;
        _lblSqliteInfo.ForeColor = AppTheme.TextSecondary;
        _lblSqliteInfo.Dock = DockStyle.Top;
        _lblSqliteInfo.Height = 28;

        FlowLayoutPanel bkFlow = new() { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 8, 0, 0) };
        _btnBackupSqlite = AppTheme.CreateButton("Respaldar SQLite (.db)", IconKind.Backup, ButtonVariant.Primary,
            async (_, _) => await BackupSqliteAsync(), "Genera una copia íntegra en caliente usando SQLite Backup API", Tip, 185, 36);
        _btnResetDemo = AppTheme.CreateButton("Recargar Datos Demo", IconKind.Refresh, ButtonVariant.Warning,
            async (_, _) => await ResetDemoAsync(), "Restaura el set inicial de datos demostrativos de prueba", Tip, 185, 36);
        _btnPurgarDb = AppTheme.CreateButton("Purgar Pesajes", IconKind.Trash, ButtonVariant.Danger,
            (_, _) => OpenPurgeDialog(), "Elimina pesajes temporales e índice real (requiere confirmación ELIMINAR)", Tip, 155, 36);
        bkFlow.Controls.AddRange([_btnBackupSqlite, _btnResetDemo, _btnPurgarDb]);

        cardBackup.Controls.Add(bkFlow);
        cardBackup.Controls.Add(_lblSqliteInfo);
        cardBackup.Controls.Add(lblBkTitle);

        tabMig.Controls.Add(cardBackup);
        tabMig.Controls.Add(cardMdb);

        _innerTabs.TabPages.AddRange([tabUsers, tabAudit, tabMig]);
        Controls.Add(_innerTabs);

        ResetUserEditor();
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canAdmin = _auth.HasPermission(PermisoSistema.AdministrarSeguridad);
        bool canAudit = _auth.HasPermission(PermisoSistema.VerAuditoria);
        bool canMdb = _auth.HasPermission(PermisoSistema.MigrarDesdeMdb);
        bool canPurge = _auth.HasPermission(PermisoSistema.PurgarTablasPesaje);

        _btnNuevoUser.Enabled = canAdmin;
        _btnGuardarUser.Enabled = canAdmin;
        _btnEliminarUser.Enabled = canAdmin;
        _btnDesbloquearUser.Enabled = canAdmin;
        _btnResetPermDefaults.Enabled = canAdmin;

        _btnFiltrarAudit.Enabled = canAudit;
        _btnExportarAudit.Enabled = canAudit && _auth.HasPermission(PermisoSistema.ExportarExcel);

        _btnExaminarMdb.Enabled = canMdb;
        _btnMigrarMdb.Enabled = canMdb;
        _btnBackupSqlite.Enabled = canAdmin;
        _btnResetDemo.Enabled = canAdmin;
        _btnPurgarDb.Enabled = canPurge;
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        RefreshPermissions();
        if (_auth.HasPermission(PermisoSistema.AdministrarSeguridad))
        {
            await LoadUsersAsync(ct);
        }

        if (_auth.HasPermission(PermisoSistema.VerAuditoria))
        {
            await LoadAuditLogsAsync(ct);
        }
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
        _cmbRol.SelectedItem = u.Rol;
        _txtPass.Clear();
        _chkActivo.Checked = u.Activo;

        IReadOnlyCollection<PermisoSistema> effective = u.PermisosPersonalizados.Count > 0
            ? u.PermisosPersonalizados
            : RolePermissionMatrix.GetDefaultsForRole(u.Rol);

        for (int i = 0; i < _clbPermisos.Items.Count; i++)
        {
            PermisoSistema perm = (PermisoSistema)_clbPermisos.Items[i];
            _clbPermisos.SetItemChecked(i, effective.Contains(perm));
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
        _cmbRol.SelectedItem = RolUsuario.Operador;
        ApplyRoleDefaultsToChecklist(RolUsuario.Operador);
    }

    private void ApplyRoleDefaultsToChecklist(RolUsuario rol)
    {
        IReadOnlyCollection<PermisoSistema> defaults = RolePermissionMatrix.GetDefaultsForRole(rol);
        for (int i = 0; i < _clbPermisos.Items.Count; i++)
        {
            PermisoSistema p = (PermisoSistema)_clbPermisos.Items[i];
            _clbPermisos.SetItemChecked(i, defaults.Contains(p));
        }
    }

    private async Task SaveUserAsync()
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            List<PermisoSistema> selectedPerms = [];
            foreach (object checkedItem in _clbPermisos.CheckedItems)
            {
                if (checkedItem is PermisoSistema perm)
                {
                    selectedPerms.Add(perm);
                }
            }

            Usuario user = new()
            {
                Id = _editingUserId,
                NombreUsuario = _txtUsername.Text.Trim(),
                NombreCompleto = _txtNombre.Text.Trim(),
                Rol = (RolUsuario)(_cmbRol.SelectedItem ?? RolUsuario.Operador),
                PasswordHash = _existingHash,
                PasswordSalt = _existingSalt,
                Activo = _chkActivo.Checked,
                PermisosPersonalizados = selectedPerms
            };

            string? plainPass = string.IsNullOrWhiteSpace(_txtPass.Text) ? null : _txtPass.Text;
            await _app.SaveUsuarioAsync(user, plainPass);
            ResetUserEditor();
            await LoadUsersAsync();
            NotifyStatus($"Usuario '{user.NombreUsuario}' guardado correctamente.");
        }, NotifyStatus, "Guardando usuario...");
    }

    private async Task UnlockSelectedUserAsync()
    {
        if (_dgvUsers.CurrentRow?.DataBoundItem is not Usuario selected)
        {
            MessageBox.Show(this, "Seleccione un usuario en la tabla para desbloquear.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Usuario unlocked = new()
            {
                Id = selected.Id,
                NombreUsuario = selected.NombreUsuario,
                NombreCompleto = selected.NombreCompleto,
                Rol = selected.Rol,
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
        }, NotifyStatus, "Desbloqueando usuario...");
    }

    private async Task DeleteUserAsync()
    {
        if (_dgvUsers.CurrentRow?.DataBoundItem is not Usuario selected)
        {
            MessageBox.Show(this, "Seleccione un usuario en la tabla para eliminar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            this,
            $"¿Confirma eliminar la cuenta '{selected.NombreUsuario}' ({selected.NombreCompleto})?",
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
        }, NotifyStatus, "Eliminando usuario...");
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
            NotifyStatus($"Bitácora cargada: {_lastLogs.Count} eventos.");
        }, NotifyStatus, "Cargando bitácora de auditoría...");
    }

    private void ExportAuditLogs()
    {
        if (_lastLogs.Count == 0)
        {
            MessageBox.Show(this, "No hay eventos de auditoría cargados para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"Auditoria_GestionScrap_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportAuditLogs(path, _lastLogs);
            NotifyStatus($"Auditoría exportada: {saved}");
            MessageBox.Show(this, $"Bitácora de auditoría exportada:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseMdb()
    {
        using OpenFileDialog ofd = new()
        {
            Title = "Seleccionar base Access heredada",
            Filter = "Base Microsoft Access (*.mdb;*.accdb)|*.mdb;*.accdb|Todos los archivos (*.*)|*.*"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _txtMdbPath.Text = ofd.FileName;
        }
    }

    private async Task RunMdbMigrationAsync()
    {
        string path = _txtMdbPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Seleccione un archivo .mdb / .accdb válido antes de iniciar.", "Archivo No Encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            _settings.LastMdbPath = path;
            _txtMdbLog.Text = $"[{DateTime.Now:HH:mm:ss}] Iniciando migración desde: {path}{Environment.NewLine}";
            var res = await _app.ImportFromAccessMdbAsync(path);

            _txtMdbLog.AppendText(string.Create(
                CultureInfo.CurrentCulture,
                $"[{DateTime.Now:HH:mm:ss}] Migración finalizada:{Environment.NewLine}" +
                $"  • Pesajes importados : {res.PesajesImported:N0}{Environment.NewLine}" +
                $"  • Clientes           : {res.ClientesImported:N0}{Environment.NewLine}" +
                $"  • Productos/Grupos   : {res.ProductosImported:N0}{Environment.NewLine}" +
                $"  • Precios proveedor  : {res.PreciosImported:N0}{Environment.NewLine}" +
                $"  • Index reales       : {res.IndexPreciosImported:N0}{Environment.NewLine}"));

            foreach (string w in res.Warnings)
            {
                _txtMdbLog.AppendText($"  [AVISO] {w}{Environment.NewLine}");
            }

            NotifyStatus($"Migración MDB completada: {res.PesajesImported} pesajes, {res.ClientesImported} clientes.");
        }, NotifyStatus, "Ejecutando migración Access MDB...");
    }

    private async Task BackupSqliteAsync()
    {
        using SaveFileDialog sfd = new()
        {
            Title = "Guardar respaldo de base de datos SQLite",
            Filter = "Base de Datos SQLite (*.db)|*.db",
            DefaultExt = "db",
            AddExtension = true,
            FileName = $"gestion_scrap_backup_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            string saved = await _app.BackupDatabaseAsync(sfd.FileName);
            NotifyStatus($"Respaldo SQLite guardado en: {saved}");
            MessageBox.Show(this, $"Respaldo generado correctamente:\n{saved}", "Respaldo Exitoso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, NotifyStatus, "Generando respaldo SQLite...");
    }

    private async Task ResetDemoAsync()
    {
        DialogResult confirm = MessageBox.Show(
            this,
            "¿Desea restaurar el conjunto de datos demostrativos en SQLite? Esta operación recargará los registros de ejemplo.",
            "Confirmar Recarga Demo",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.ResetDemoDataAsync();
            NotifyStatus("Datos demostrativos recargados.");
            MessageBox.Show(this, "Datos de demostración recargados correctamente.", "Operación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, NotifyStatus, "Recargando datos demo...");
    }

    private void OpenPurgeDialog()
    {
        using LimpiarTablasDialog dlg = new(_app);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            NotifyStatus($"Purga ejecutada: {dlg.PurgedCount} registros eliminados.");
        }
    }
}
