using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo unificado CRM (Clientes y Proveedores por RUT sin duplicados + Ficha 360°),
/// Catálogo Maestro de Materiales (filtros combinados + historial) y Flota de Vehículos/Patentes,
/// con CRUD completo, activación/desactivación, protección histórica y menús contextuales.
/// </summary>
public sealed class CrmMaestrosControl : ScrapTabControlBase
{
    private readonly EnterpriseApplicationService _enterprise;
    private readonly ScrapApplicationService _scrap;
    private readonly IAuthService _auth;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Subtab 1: CRM Clientes & Proveedores
    private readonly ComboBox _cmbCrmRol = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbCrmEstado = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtCrmBuscar = new() { Width = 180 };
    private readonly Button _btnCrmBuscar;
    private readonly Button _btnCrmNuevo;
    private readonly Button _btnCrmEditar;
    private readonly Button _btnCrmDoc;
    private readonly Button _btnCrmToggle;
    private readonly Button _btnCrmEliminar;
    private readonly Button _btnCrmExportar;
    private readonly DoubleBufferedDataGridView _dgvCrm = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly Label _lblCrmFicha = new();
    private readonly DoubleBufferedDataGridView _dgvCrmPesajes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvCrmDocs = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    // Subtab 2: Catálogo de Materiales
    private readonly TextBox _txtMatBuscar = new() { Width = 155 };
    private readonly ComboBox _cmbMatGrupo = new() { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbMatUnidad = new() { Width = 92, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbMatEstado = new() { Width = 115, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtMatProveedor = new() { Width = 115 };
    private readonly Button _btnMatBuscar;
    private readonly Button _btnMatNuevo;
    private readonly Button _btnMatEditar;
    private readonly Button _btnMatToggle;
    private readonly Button _btnMatEliminar;
    private readonly Button _btnMatExportar;
    private readonly DoubleBufferedDataGridView _dgvMateriales = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly Label _lblMatHistorial = new();
    private readonly DoubleBufferedDataGridView _dgvMatPesajes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    // Subtab 3: Vehículos y Patentes (Flota)
    private readonly TextBox _txtVehBuscar = new() { Width = 175 };
    private readonly ComboBox _cmbVehEstado = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnVehBuscar;
    private readonly Button _btnVehNuevo;
    private readonly Button _btnVehEditar;
    private readonly Button _btnVehToggle;
    private readonly Button _btnVehEliminar;
    private readonly Button _btnVehExportar;
    private readonly DoubleBufferedDataGridView _dgvVehiculos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly Label _lblVehHistorial = new();
    private readonly DoubleBufferedDataGridView _dgvVehPesajes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private readonly List<ContextMenuStrip> _menus = [];

    private sealed record RolOpt(RolEntidadComercial? Rol, string Label) { public override string ToString() => Label; }
    private sealed record ActiveOpt(bool? SoloActivos, string Label) { public override string ToString() => Label; }

    public CrmMaestrosControl(
        EnterpriseApplicationService enterprise,
        ScrapApplicationService scrap,
        IAuthService auth)
    {
        _enterprise = enterprise;
        _scrap = scrap;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);
        Size = new Size(1280, 760);

        // Botones CRM
        _btnCrmBuscar = AppTheme.CreateButton("Buscar", IconKind.Search, ButtonVariant.Primary, async (_, _) => await LoadCrmAsync(), "Filtrar entidades CRM", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmNuevo = AppTheme.CreateButton("Nueva Entidad", IconKind.Plus, ButtonVariant.Success, async (_, _) => await EditCrmAsync(null), "Crear nuevo cliente, proveedor o entidad mixta", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditSelectedCrmAsync(), "Editar ficha comercial y tributaria", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmDoc = AppTheme.CreateButton("+ Documento", IconKind.Document, ButtonVariant.Gold, async (_, _) => await AddCrmDocumentAsync(), "Asociar documento comercial a la entidad", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmToggle = AppTheme.CreateButton("Activar / Desactivar", IconKind.Check, ButtonVariant.Warning, async (_, _) => await ToggleSelectedCrmAsync(), "Cambiar estado comercial sin perder historial", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmEliminar = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await DeleteSelectedCrmAsync(), "Eliminar solo si no posee pesajes ni operaciones", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnCrmExportar = AppTheme.CreateButton("Exportar", IconKind.Excel, ButtonVariant.Secondary, (_, _) => ExportGrid(_dgvCrm, "CRM Clientes y Proveedores", "CRM_Entidades", true), "Exportar selección a Excel", Tip, 0, 30, AppTheme.FontButtonCompact);

        // Botones Materiales
        _btnMatBuscar = AppTheme.CreateButton("Filtrar", IconKind.Search, ButtonVariant.Primary, async (_, _) => await LoadMaterialesAsync(), "Aplicar filtros combinados al catálogo de materiales", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMatNuevo = AppTheme.CreateButton("Nuevo Material", IconKind.Plus, ButtonVariant.Success, async (_, _) => await EditMaterialAsync(null), "Crear nuevo material maestro", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMatEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditSelectedMaterialAsync(), "Editar material seleccionado", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMatToggle = AppTheme.CreateButton("Activar / Desactivar", IconKind.Check, ButtonVariant.Warning, async (_, _) => await ToggleSelectedMaterialAsync(), "Activar o desactivar material", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMatEliminar = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await DeleteSelectedMaterialAsync(), "Eliminar solo si no tiene historial", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnMatExportar = AppTheme.CreateButton("Exportar", IconKind.Excel, ButtonVariant.Secondary, (_, _) => ExportGrid(_dgvMateriales, "Catálogo de Materiales", "Materiales", true), "Exportar catálogo", Tip, 0, 30, AppTheme.FontButtonCompact);

        // Botones Vehículos
        _btnVehBuscar = AppTheme.CreateButton("Buscar", IconKind.Search, ButtonVariant.Primary, async (_, _) => await LoadVehiculosAsync(), "Buscar patente exacta o parcial", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnVehNuevo = AppTheme.CreateButton("Nuevo Vehículo", IconKind.Plus, ButtonVariant.Success, async (_, _) => await EditVehiculoAsync(null), "Registrar nueva patente normalizada", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnVehEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditSelectedVehiculoAsync(), "Editar ficha del vehículo", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnVehToggle = AppTheme.CreateButton("Activar / Desactivar", IconKind.Check, ButtonVariant.Warning, async (_, _) => await ToggleSelectedVehiculoAsync(), "Activar o desactivar vehículo", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnVehEliminar = AppTheme.CreateButton("Eliminar", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await DeleteSelectedVehiculoAsync(), "Eliminar solo si no tiene pesajes históricos", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnVehExportar = AppTheme.CreateButton("Exportar", IconKind.Excel, ButtonVariant.Secondary, (_, _) => ExportGrid(_dgvVehiculos, "Flota de Vehículos y Patentes", "Vehiculos", true), "Exportar flota", Tip, 0, 30, AppTheme.FontButtonCompact);

        _tabs.Font = AppTheme.FontBodyBold;
        _tabs.TabPages.Add(BuildCrmTab());
        _tabs.TabPages.Add(BuildMaterialesTab());
        _tabs.TabPages.Add(BuildVehiculosTab());

        Controls.Add(_tabs);
        RefreshPermissions();
    }

    private static void PopulateActiveCombo(ComboBox cmb)
    {
        cmb.Font = AppTheme.FontBody;
        cmb.Margin = new Padding(0, 3, 6, 3);
        cmb.Items.Clear();
        cmb.Items.AddRange([new ActiveOpt(null, "Todos"), new ActiveOpt(true, "Solo Activos"), new ActiveOpt(false, "Inactivos")]);
        cmb.SelectedIndex = 0;
    }

    private TabPage BuildCrmTab()
    {
        TabPage tp = new("CRM Clientes y Proveedores (RUT Único)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        _cmbCrmRol.Font = AppTheme.FontBody;
        _cmbCrmRol.Margin = new Padding(0, 3, 6, 3);
        _cmbCrmRol.Items.AddRange(
        [
            new RolOpt(null, "Todos los Roles"),
            new RolOpt(RolEntidadComercial.Cliente, "Clientes"),
            new RolOpt(RolEntidadComercial.Proveedor, "Proveedores"),
            new RolOpt(RolEntidadComercial.Ambos, "Cliente y Proveedor")
        ]);
        _cmbCrmRol.SelectedIndex = 0;
        PopulateActiveCombo(_cmbCrmEstado);

        AppTheme.ApplyInputStyle(_txtCrmBuscar);
        _txtCrmBuscar.Margin = new Padding(0, 3, 6, 3);
        _txtCrmBuscar.PlaceholderText = "RUT, Razón Social, Ciudad...";

        Panel filter = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(
                UiHelpers.CreateFilterLabel("Rol:", 0), _cmbCrmRol,
                UiHelpers.CreateFilterLabel("Estado:"), _cmbCrmEstado,
                UiHelpers.CreateFilterLabel("Buscar:"), _txtCrmBuscar,
                _btnCrmBuscar),
            UiHelpers.CreateFilterRow(_btnCrmNuevo, _btnCrmEditar, _btnCrmDoc, _btnCrmToggle, _btnCrmEliminar, _btnCrmExportar));

        AppTheme.StyleDataGridView(_dgvCrm);
        _dgvCrm.MultiSelect = true;
        _dgvCrm.Columns.AddRange(
        [
            UiHelpers.AddTextCol("RUT", nameof(Cliente.RutFormateado), 82),
            UiHelpers.AddTextCol("Razón Social", nameof(Cliente.RazonSocial), 155),
            UiHelpers.AddTextCol("Rol CRM", nameof(Cliente.RolDescripcion), 100),
            UiHelpers.AddTextCol("Giro", nameof(Cliente.Giro), 105),
            UiHelpers.AddTextCol("Ciudad", nameof(Cliente.Ciudad), 82),
            UiHelpers.AddTextCol("Contacto", nameof(Cliente.ContactoNombre), 95),
            UiHelpers.AddTextCol("Teléfono", nameof(Cliente.Telefono), 82),
            UiHelpers.AddTextCol("Email", nameof(Cliente.Email), 105),
            UiHelpers.AddTextCol("Estado", nameof(Cliente.EstadoDescripcion), 64)
        ]);
        _dgvCrm.SelectionChanged += async (_, _) => await LoadSelectedCrmFichaAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Nueva Entidad CRM", null, async (_, _) => await EditCrmAsync(null));
        menu.Items.Add("Editar Ficha CRM", null, async (_, _) => await EditSelectedCrmAsync());
        menu.Items.Add("Asociar Documento Comercial", null, async (_, _) => await AddCrmDocumentAsync());
        menu.Items.Add("Activar / Desactivar", null, async (_, _) => await ToggleSelectedCrmAsync());
        menu.Items.Add("Eliminar (si no tiene historial)", null, async (_, _) => await DeleteSelectedCrmAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar Selección a Excel", null, (_, _) => ExportGrid(_dgvCrm, "CRM Entidades", "CRM_Entidades", true));
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvCrm, menu);

        AppTheme.StyleDataGridView(_dgvCrmPesajes);
        _dgvCrmPesajes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Tipo", nameof(Pesaje.TipoPesajeDisplay), 105),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 68),
            UiHelpers.AddTextCol("Material", nameof(Pesaje.NomProducto), 130),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 72),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 84)
        ]);

        AppTheme.StyleDataGridView(_dgvCrmDocs);
        _dgvCrmDocs.Columns.AddRange(
        [
            UiHelpers.AddDateCol("Fecha", nameof(DocumentoAsociado.FechaDocumento), 74),
            UiHelpers.AddTextCol("Tipo Documento", nameof(DocumentoAsociado.TipoDocumento), 115),
            UiHelpers.AddTextCol("Número", nameof(DocumentoAsociado.NumeroDocumento), 88),
            UiHelpers.AddTextCol("Observaciones", nameof(DocumentoAsociado.Observaciones), 150)
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 160;
                split.Panel2MinSize = 140;
                split.SplitterDistance = (int)(split.Height * 0.56);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvCrm);

        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _lblCrmFicha.Text = "Ficha 360° CRM: seleccione una entidad para ver pesajes, operaciones, vehículos y documentos.";
        _lblCrmFicha.Font = AppTheme.FontSmallBold;
        _lblCrmFicha.ForeColor = AppTheme.AccentBlue;
        _lblCrmFicha.AutoEllipsis = true;
        _lblCrmFicha.UseMnemonic = false;
        _lblCrmFicha.Dock = DockStyle.Top;
        _lblCrmFicha.Height = 24;

        TableLayoutPanel fichaSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        fichaSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        fichaSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        fichaSplit.Controls.Add(_dgvCrmPesajes, 0, 0);
        fichaSplit.Controls.Add(_dgvCrmDocs, 1, 0);

        botCard.Controls.Add(fichaSplit);
        botCard.Controls.Add(_lblCrmFicha);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(filter);
        return tp;
    }

    private TabPage BuildMaterialesTab()
    {
        TabPage tp = new("Catálogo de Materiales") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        AppTheme.ApplyInputStyle(_txtMatBuscar);
        _txtMatBuscar.Margin = new Padding(0, 3, 6, 3);
        _txtMatBuscar.PlaceholderText = "Código o nombre...";
        AppTheme.ApplyInputStyle(_txtMatProveedor);
        _txtMatProveedor.Margin = new Padding(0, 3, 6, 3);
        _txtMatProveedor.PlaceholderText = "RUT Prov.";

        _cmbMatGrupo.Font = AppTheme.FontBody;
        _cmbMatGrupo.Margin = new Padding(0, 3, 6, 3);
        _cmbMatGrupo.Items.Add("Todos los Grupos");
        _cmbMatGrupo.SelectedIndex = 0;

        _cmbMatUnidad.Font = AppTheme.FontBody;
        _cmbMatUnidad.Margin = new Padding(0, 3, 6, 3);
        _cmbMatUnidad.Items.AddRange(["Todas", "KG", "TON"]);
        _cmbMatUnidad.SelectedIndex = 0;

        PopulateActiveCombo(_cmbMatEstado);

        Panel filter = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(
                UiHelpers.CreateFilterLabel("Buscar:", 0), _txtMatBuscar,
                UiHelpers.CreateFilterLabel("Grupo:"), _cmbMatGrupo,
                UiHelpers.CreateFilterLabel("Unidad:"), _cmbMatUnidad,
                UiHelpers.CreateFilterLabel("Estado:"), _cmbMatEstado,
                UiHelpers.CreateFilterLabel("Prov.:"), _txtMatProveedor,
                _btnMatBuscar),
            UiHelpers.CreateFilterRow(_btnMatNuevo, _btnMatEditar, _btnMatToggle, _btnMatEliminar, _btnMatExportar));

        AppTheme.StyleDataGridView(_dgvMateriales);
        _dgvMateriales.MultiSelect = true;
        _dgvMateriales.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(Producto.Codigo), 86),
            UiHelpers.AddTextCol("Nombre Material", nameof(Producto.NombreVisible), 160),
            UiHelpers.AddTextCol("Grupo de Material", nameof(Producto.Grupo), 125),
            UiHelpers.AddTextCol("Unidad", nameof(Producto.UnidadMedida), 54),
            UiHelpers.AddNumberCol("Tol. %", nameof(Producto.ToleranciaDiferenciaPct), "N1", 52),
            UiHelpers.AddTextCol("Estado", nameof(Producto.EstadoDescripcion), 66),
            UiHelpers.AddTextCol("Info. Adicional", nameof(Producto.InformacionAdicional), 135)
        ]);
        _dgvMateriales.SelectionChanged += async (_, _) => await LoadSelectedMaterialHistoryAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Nuevo Material", null, async (_, _) => await EditMaterialAsync(null));
        menu.Items.Add("Editar Material", null, async (_, _) => await EditSelectedMaterialAsync());
        menu.Items.Add("Activar / Desactivar", null, async (_, _) => await ToggleSelectedMaterialAsync());
        menu.Items.Add("Eliminar (si no tiene historial)", null, async (_, _) => await DeleteSelectedMaterialAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar Selección a Excel", null, (_, _) => ExportGrid(_dgvMateriales, "Catálogo Materiales", "Materiales", true));
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvMateriales, menu);

        AppTheme.StyleDataGridView(_dgvMatPesajes);
        _dgvMatPesajes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 68),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 70),
            UiHelpers.AddTextCol("Entidad", nameof(Pesaje.NomCliente), 140),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 72),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 86)
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 160;
                split.Panel2MinSize = 130;
                split.SplitterDistance = (int)(split.Height * 0.58);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvMateriales);

        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _lblMatHistorial.Text = "Historial del Material: seleccione un registro para ver stock derivado y pesajes recientes.";
        _lblMatHistorial.Font = AppTheme.FontSmallBold;
        _lblMatHistorial.ForeColor = AppTheme.AccentBlue;
        _lblMatHistorial.AutoEllipsis = true;
        _lblMatHistorial.UseMnemonic = false;
        _lblMatHistorial.Dock = DockStyle.Top;
        _lblMatHistorial.Height = 24;
        botCard.Controls.Add(_dgvMatPesajes);
        botCard.Controls.Add(_lblMatHistorial);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(filter);
        return tp;
    }

    private TabPage BuildVehiculosTab()
    {
        TabPage tp = new("Vehículos y Patentes (Flota)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        AppTheme.ApplyInputStyle(_txtVehBuscar);
        _txtVehBuscar.Margin = new Padding(0, 3, 6, 3);
        _txtVehBuscar.PlaceholderText = "Patente exacta/parcial, marca...";
        PopulateActiveCombo(_cmbVehEstado);

        Panel filter = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(
                UiHelpers.CreateFilterLabel("Buscar:", 0), _txtVehBuscar,
                UiHelpers.CreateFilterLabel("Estado:"), _cmbVehEstado,
                _btnVehBuscar, _btnVehNuevo, _btnVehEditar, _btnVehToggle, _btnVehEliminar, _btnVehExportar));

        AppTheme.StyleDataGridView(_dgvVehiculos);
        _dgvVehiculos.MultiSelect = true;
        _dgvVehiculos.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Patente", nameof(Vehiculo.PatenteOriginal), 82),
            UiHelpers.AddTextCol("Clave Norm.", nameof(Vehiculo.PatenteNormalizada), 78),
            UiHelpers.AddTextCol("Tipo Vehículo", nameof(Vehiculo.TipoVehiculo), 96),
            UiHelpers.AddTextCol("Marca", nameof(Vehiculo.Marca), 80),
            UiHelpers.AddTextCol("Modelo", nameof(Vehiculo.Modelo), 80),
            UiHelpers.AddNumberCol("Capacidad Kg", nameof(Vehiculo.CapacidadCargaKg), "N0", 78),
            UiHelpers.AddTextCol("Entidad Asociada", nameof(Vehiculo.NombreEntidadAsociada), 140),
            UiHelpers.AddTextCol("Estado", nameof(Vehiculo.EstadoDescripcion), 66)
        ]);
        _dgvVehiculos.SelectionChanged += async (_, _) => await LoadSelectedVehiculoHistoryAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Nuevo Vehículo / Patente", null, async (_, _) => await EditVehiculoAsync(null));
        menu.Items.Add("Editar Vehículo", null, async (_, _) => await EditSelectedVehiculoAsync());
        menu.Items.Add("Activar / Desactivar", null, async (_, _) => await ToggleSelectedVehiculoAsync());
        menu.Items.Add("Eliminar (si no tiene historial)", null, async (_, _) => await DeleteSelectedVehiculoAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar Selección a Excel", null, (_, _) => ExportGrid(_dgvVehiculos, "Flota Vehículos", "Vehiculos", true));
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvVehiculos, menu);

        AppTheme.StyleDataGridView(_dgvVehPesajes);
        _dgvVehPesajes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 68),
            UiHelpers.AddTextCol("Tipo", nameof(Pesaje.TipoPesajeDisplay), 105),
            UiHelpers.AddTextCol("Entidad", nameof(Pesaje.NomCliente), 135),
            UiHelpers.AddTextCol("Material", nameof(Pesaje.NomProducto), 130),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 72),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 86)
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 160;
                split.Panel2MinSize = 130;
                split.SplitterDistance = (int)(split.Height * 0.58);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvVehiculos);

        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _lblVehHistorial.Text = "Historial del Vehículo: seleccione una patente para ver todos sus pesajes registrados.";
        _lblVehHistorial.Font = AppTheme.FontSmallBold;
        _lblVehHistorial.ForeColor = AppTheme.AccentBlue;
        _lblVehHistorial.AutoEllipsis = true;
        _lblVehHistorial.UseMnemonic = false;
        _lblVehHistorial.Dock = DockStyle.Top;
        _lblVehHistorial.Height = 24;
        botCard.Controls.Add(_dgvVehPesajes);
        botCard.Controls.Add(_lblVehHistorial);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(filter);
        return tp;
    }

    public override void RefreshPermissions()
    {
        bool canCrm = _auth.HasPermission(PermisoSistema.AdministrarCrmEntidades);
        bool canMatVeh = _auth.HasPermission(PermisoSistema.AdministrarMaterialesVehiculos);
        bool canExp = _auth.HasPermission(PermisoSistema.ExportarExcel);

        _btnCrmNuevo.Enabled = canCrm;
        _btnCrmEditar.Enabled = canCrm;
        _btnCrmDoc.Enabled = canCrm;
        _btnCrmToggle.Enabled = canCrm;
        _btnCrmEliminar.Enabled = canCrm;
        _btnCrmExportar.Enabled = canExp;

        _btnMatNuevo.Enabled = canMatVeh;
        _btnMatEditar.Enabled = canMatVeh;
        _btnMatToggle.Enabled = canMatVeh;
        _btnMatEliminar.Enabled = canMatVeh;
        _btnMatExportar.Enabled = canExp;

        _btnVehNuevo.Enabled = canMatVeh;
        _btnVehEditar.Enabled = canMatVeh;
        _btnVehToggle.Enabled = canMatVeh;
        _btnVehEliminar.Enabled = canMatVeh;
        _btnVehExportar.Enabled = canExp;
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await LoadCrmAsync(ct);
        await LoadMaterialesAsync(ct);
        await LoadVehiculosAsync(ct);
    }

    // ---- CRM Handlers ----

    private async Task LoadCrmAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            RolEntidadComercial? rol = (_cmbCrmRol.SelectedItem as RolOpt)?.Rol;
            bool? act = (_cmbCrmEstado.SelectedItem as ActiveOpt)?.SoloActivos;
            IReadOnlyList<Cliente> list = await _enterprise.GetEntidadesComercialesAsync(rol, act, _txtCrmBuscar.Text, ct);
            _dgvCrm.DataSource = null;
            _dgvCrm.DataSource = list;
            NotifyStatus($"CRM Entidades: {list.Count:N0} registro(s).");
        }, NotifyStatus);
    }

    private async Task LoadSelectedCrmFichaAsync()
    {
        if (_dgvCrm.CurrentRow?.DataBoundItem is not Cliente c)
        {
            _dgvCrmPesajes.DataSource = null;
            _dgvCrmDocs.DataSource = null;
            return;
        }

        EntidadComercialFichaDto f = await _enterprise.GetEntidadFicha360Async(c.RutFormateado);
        _lblCrmFicha.Text = $"Ficha 360°: {c.RazonSocial} ({c.RutFormateado}) · Rol: {c.RolDescripcion} · Pesajes Conf.: {f.TotalPesajesConfirmados:N0} ({f.TotalKilosNetos:N0} Kg) · Órdenes ERP: {f.TotalOperaciones} · Vehículos: {f.VehiculosAsociados.Count} · Docs: {f.Documentos.Count}";
        _dgvCrmPesajes.DataSource = null;
        _dgvCrmPesajes.DataSource = f.PesajesRecientes;
        _dgvCrmDocs.DataSource = null;
        _dgvCrmDocs.DataSource = f.Documentos;
    }

    private async Task EditCrmAsync(Cliente? existing)
    {
        using EntidadComercialDialog dlg = new(existing);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultEntity is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveEntidadComercialAsync(dlg.ResultEntity);
            await LoadCrmAsync();
        }, NotifyStatus, "Guardando entidad CRM...");
    }

    private Task EditSelectedCrmAsync()
    {
        if (_dgvCrm.CurrentRow?.DataBoundItem is not Cliente c)
        {
            MessageBox.Show(this, "Seleccione una entidad CRM para editar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return EditCrmAsync(c);
    }

    private async Task AddCrmDocumentAsync()
    {
        if (_dgvCrm.CurrentRow?.DataBoundItem is not Cliente c)
        {
            MessageBox.Show(this, "Seleccione una entidad CRM para asociar un documento.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using DocumentoAsociadoDialog dlg = new(c.RutFormateado, c.RazonSocial);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultDocumento is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveDocumentoAsync(dlg.ResultDocumento);
            await LoadSelectedCrmFichaAsync();
        }, NotifyStatus);
    }

    private async Task ToggleSelectedCrmAsync()
    {
        if (_dgvCrm.CurrentRow?.DataBoundItem is not Cliente c)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SetEntidadActivoAsync(c.RutFormateado, !c.Activo);
            await LoadCrmAsync();
        }, NotifyStatus);
    }

    private async Task DeleteSelectedCrmAsync()
    {
        if (_dgvCrm.CurrentRow?.DataBoundItem is not Cliente c)
        {
            return;
        }

        if (MessageBox.Show(this, $"¿Intentar eliminar la entidad '{c.RazonSocial}' ({c.RutFormateado})?\n\nNota: Si posee pesajes u operaciones históricas el sistema protegerá la integridad y le sugerirá desactivarla.", "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.DeleteEntidadSafelyAsync(c.RutFormateado);
            await LoadCrmAsync();
        }, NotifyStatus);
    }

    // ---- Materiales Handlers ----

    private async Task LoadMaterialesAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            IReadOnlyList<Producto> all = await _enterprise.GetMaterialesAsync(ct: ct);
            string prevGroup = _cmbMatGrupo.SelectedItem?.ToString() ?? "Todos los Grupos";
            _cmbMatGrupo.Items.Clear();
            _cmbMatGrupo.Items.Add("Todos los Grupos");
            foreach (string g in all.Select(m => m.Grupo).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                _cmbMatGrupo.Items.Add(g);
            }

            int gIdx = _cmbMatGrupo.Items.IndexOf(prevGroup);
            _cmbMatGrupo.SelectedIndex = gIdx >= 0 ? gIdx : 0;

            string? grpFilter = _cmbMatGrupo.SelectedIndex > 0 ? _cmbMatGrupo.SelectedItem?.ToString() : null;
            string? unitFilter = _cmbMatUnidad.SelectedIndex > 0 ? _cmbMatUnidad.SelectedItem?.ToString() : null;
            bool? actFilter = (_cmbMatEstado.SelectedItem as ActiveOpt)?.SoloActivos;

            IReadOnlyList<Producto> filtered = await _enterprise.GetMaterialesAsync(actFilter, _txtMatBuscar.Text, grpFilter, unitFilter, _txtMatProveedor.Text, ct);
            _dgvMateriales.DataSource = null;
            _dgvMateriales.DataSource = filtered;
        }, NotifyStatus);
    }

    private async Task LoadSelectedMaterialHistoryAsync()
    {
        if (_dgvMateriales.CurrentRow?.DataBoundItem is not Producto m)
        {
            _dgvMatPesajes.DataSource = null;
            return;
        }

        MaterialHistorialDto hist = await _enterprise.GetMaterialHistorialAsync(m.Codigo);
        _lblMatHistorial.Text = $"Material {m.Codigo} · {m.NombreVisible} | Stock Derivado: {hist.StockActualKg:N0} {m.UnidadMedida} (Entradas: {hist.EntradasKg:N0} · Salidas: {hist.SalidasKg:N0}) | Pesajes Históricos: {hist.TotalPesajesConfirmados:N0} ({hist.TotalKilosPesados:N0} Kg)";
        _dgvMatPesajes.DataSource = null;
        _dgvMatPesajes.DataSource = hist.PesajesRecientes;
    }

    private async Task EditMaterialAsync(Producto? existing)
    {
        IReadOnlyList<Producto> all = await _enterprise.GetMaterialesAsync();
        IEnumerable<string> groups = BusinessRules.DefaultParentGroups.Select(g => g.Nombre).Concat(all.Select(p => p.Grupo));

        using MaterialEditorDialog dlg = new(groups, existing);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultMaterial is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveMaterialAsync(dlg.ResultMaterial);
            await LoadMaterialesAsync();
        }, NotifyStatus, "Guardando material...");
    }

    private Task EditSelectedMaterialAsync()
    {
        if (_dgvMateriales.CurrentRow?.DataBoundItem is not Producto m)
        {
            MessageBox.Show(this, "Seleccione un material para editar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return EditMaterialAsync(m);
    }

    private async Task ToggleSelectedMaterialAsync()
    {
        if (_dgvMateriales.CurrentRow?.DataBoundItem is not Producto m)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SetMaterialActivoAsync(m.Codigo, !m.Activo);
            await LoadMaterialesAsync();
        }, NotifyStatus);
    }

    private async Task DeleteSelectedMaterialAsync()
    {
        if (_dgvMateriales.CurrentRow?.DataBoundItem is not Producto m)
        {
            return;
        }

        if (MessageBox.Show(this, $"¿Intentar eliminar el material '{m.Codigo} · {m.NombreVisible}'?", "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.DeleteMaterialSafelyAsync(m.Codigo);
            await LoadMaterialesAsync();
        }, NotifyStatus);
    }

    // ---- Vehículos Handlers ----

    private async Task LoadVehiculosAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            bool? act = (_cmbVehEstado.SelectedItem as ActiveOpt)?.SoloActivos;
            IReadOnlyList<Vehiculo> list = await _enterprise.GetVehiculosAsync(act, _txtVehBuscar.Text, ct);
            _dgvVehiculos.DataSource = null;
            _dgvVehiculos.DataSource = list;
        }, NotifyStatus);
    }

    private async Task LoadSelectedVehiculoHistoryAsync()
    {
        if (_dgvVehiculos.CurrentRow?.DataBoundItem is not Vehiculo v)
        {
            _dgvVehPesajes.DataSource = null;
            return;
        }

        VehiculoHistorialDto hist = await _enterprise.GetVehiculoHistorialAsync(v.PatenteNormalizada);
        _lblVehHistorial.Text = $"Patente {v.PatenteOriginal} ({v.TipoVehiculo}) · Pesajes Confirmados: {hist.TotalPesajesConfirmados:N0} · Total Neto: {hist.TotalKilosNetos:N0} Kg · Último: {(hist.UltimoPesaje.HasValue ? hist.UltimoPesaje.Value.ToString("dd-MM-yyyy", UiHelpers.EsCl) : "Sin registros")}";
        _dgvVehPesajes.DataSource = null;
        _dgvVehPesajes.DataSource = hist.PesajesRecientes;
    }

    private async Task EditVehiculoAsync(Vehiculo? existing)
    {
        IReadOnlyList<Cliente> ents = await _enterprise.GetEntidadesComercialesAsync();
        using VehiculoEditorDialog dlg = new(ents, existing);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultVehiculo is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveVehiculoAsync(dlg.ResultVehiculo);
            await LoadVehiculosAsync();
        }, NotifyStatus, "Guardando vehículo...");
    }

    private Task EditSelectedVehiculoAsync()
    {
        if (_dgvVehiculos.CurrentRow?.DataBoundItem is not Vehiculo v)
        {
            MessageBox.Show(this, "Seleccione un vehículo para editar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return EditVehiculoAsync(v);
    }

    private async Task ToggleSelectedVehiculoAsync()
    {
        if (_dgvVehiculos.CurrentRow?.DataBoundItem is not Vehiculo v)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SetVehiculoActivoAsync(v.Id, !v.Activo);
            await LoadVehiculosAsync();
        }, NotifyStatus);
    }

    private async Task DeleteSelectedVehiculoAsync()
    {
        if (_dgvVehiculos.CurrentRow?.DataBoundItem is not Vehiculo v)
        {
            return;
        }

        if (MessageBox.Show(this, $"¿Intentar eliminar el vehículo '{v.PatenteOriginal}'?", "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.DeleteVehiculoSafelyAsync(v.Id);
            await LoadVehiculosAsync();
        }, NotifyStatus);
    }

    private void ExportGrid(DataGridView dgv, string title, string sheet, bool onlySelected)
    {
        if (_auth.HasPermission(PermisoSistema.ExportarExcel))
        {
            UiHelpers.ExportDataGridViewToExcel(this, dgv, _scrap.ExcelExporter, title, sheet, onlySelected, NotifyStatus);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (ContextMenuStrip m in _menus)
            {
                m.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
