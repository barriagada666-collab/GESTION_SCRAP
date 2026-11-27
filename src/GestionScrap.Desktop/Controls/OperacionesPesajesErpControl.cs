using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo integral de Operaciones ERP, Pesajes Industriales (1 y 2 mediciones),
/// Inventario (Libro Mayor Inmutable) y Tipos de Pesaje Configurables, con CRUD y menús contextuales.
/// </summary>
public sealed class OperacionesPesajesErpControl : ScrapTabControlBase
{
    private readonly EnterpriseApplicationService _enterprise;
    private readonly ScrapApplicationService _scrap;
    private readonly IAuthService _auth;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    // Subtab 1: Pesajes Industriales
    private readonly ComboBox _cmbPesPlanta = new() { Width = 130 };
    private readonly ComboBox _cmbPesEstado = new() { Width = 148, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpPesDesde = new();
    private readonly DateTimePicker _dtpPesHasta = new();
    private readonly TextBox _txtPesBuscar = new() { Width = 132 };
    private readonly Button _btnPesBuscar;
    private readonly Button _btnPesNuevo1Paso;
    private readonly Button _btnPesIniciar2Pasos;
    private readonly Button _btnPesCompletar2da;
    private readonly Button _btnPesAutorizar;
    private readonly Button _btnPesCorregir;
    private readonly Button _btnPesAnular;
    private readonly Button _btnPesExportar;
    private readonly DoubleBufferedDataGridView _dgvPesajes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvPesHistorial = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    // Subtab 2: Órdenes y Operaciones ERP
    private readonly ComboBox _cmbOpTipo = new() { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbOpEstado = new() { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnOpBuscar;
    private readonly Button _btnOpNueva;
    private readonly Button _btnOpEditar;
    private readonly Button _btnOpPesaje;
    private readonly Button _btnOpCompletar;
    private readonly Button _btnOpAnular;
    private readonly Button _btnOpDocumento;
    private readonly Button _btnOpExportar;
    private readonly DoubleBufferedDataGridView _dgvOperaciones = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly Label _lblOpTrazabilidad = new();
    private readonly DoubleBufferedDataGridView _dgvOpPesajes = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DoubleBufferedDataGridView _dgvOpMovs = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    // Subtab 3: Inventario y Movimientos
    private readonly Button _btnInvActualizar;
    private readonly Button _btnInvAjuste;
    private readonly Button _btnInvExportarStock;
    private readonly Button _btnInvExportarMovs;
    private readonly DoubleBufferedDataGridView _dgvStock = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DoubleBufferedDataGridView _dgvMovimientos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };

    // Subtab 4: Tipos de Pesaje Configurables
    private readonly Button _btnTipoNuevo;
    private readonly Button _btnTipoEditar;
    private readonly Button _btnTipoToggle;
    private readonly Button _btnTipoActualizar;
    private readonly DoubleBufferedDataGridView _dgvTipos = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

    private readonly List<ContextMenuStrip> _menus = [];

    private sealed record NullableEstadoPesajeOpt(EstadoPesaje? Estado, string Label) { public override string ToString() => Label; }
    private sealed record NullableTipoOpOpt(TipoOperacionComercial? Tipo, string Label) { public override string ToString() => Label; }
    private sealed record NullableEstadoOpOpt(EstadoOperacion? Estado, string Label) { public override string ToString() => Label; }

    public OperacionesPesajesErpControl(
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

        // Botones Subtab 1: Pesajes
        _btnPesBuscar = AppTheme.CreateButton("Buscar", IconKind.Search, ButtonVariant.Primary, async (_, _) => await LoadPesajesAsync(), "Filtrar pesajes", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesNuevo1Paso = AppTheme.CreateButton("Nuevo Pesaje", IconKind.Plus, ButtonVariant.Success, async (_, _) => await CreatePesajeAsync(PesajeDialogMode.SingleStepCreate), "Registrar pesaje completo (Bruto y Tara)", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesIniciar2Pasos = AppTheme.CreateButton("1ª Medición", IconKind.Play, ButtonVariant.Gold, async (_, _) => await CreatePesajeAsync(PesajeDialogMode.TwoStepStart), "Iniciar pesaje de entrada (pendiente de 2ª medición)", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesCompletar2da = AppTheme.CreateButton("Completar 2ª", IconKind.Check, ButtonVariant.Primary, async (_, _) => await CompleteSecondMeasurementAsync(), "Registrar 2ª medición y calcular neto definitivo", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesAutorizar = AppTheme.CreateButton("Autorizar", IconKind.Shield, ButtonVariant.Success, async (_, _) => await AuthorizePendingPesajeAsync(), "Autorizar pesaje con diferencia sobre tolerancia", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesCorregir = AppTheme.CreateButton("Corregir", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditAuthorizedPesajeAsync(), "Corrección autorizada con registro histórico", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesAnular = AppTheme.CreateButton("Anular", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await AnnulPesajeAsync(), "Anular pesaje y revertir inventario (sin borrado físico)", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnPesExportar = AppTheme.CreateButton("Exportar", IconKind.Excel, ButtonVariant.Secondary, (_, _) => ExportGrid(_dgvPesajes, "Pesajes Industriales", "Pesajes", true), "Exportar selección a Excel", Tip, 0, 30, AppTheme.FontButtonCompact);

        // Botones Subtab 2: Operaciones ERP
        _btnOpBuscar = AppTheme.CreateButton("Filtrar", IconKind.Search, ButtonVariant.Primary, async (_, _) => await LoadOperacionesAsync(), "Filtrar operaciones ERP", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpNueva = AppTheme.CreateButton("Nueva Operación", IconKind.Plus, ButtonVariant.Success, async (_, _) => await SaveOperacionAsync(null), "Crear orden de compra, venta, recepción o despacho", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditSelectedOperacionAsync(), "Editar operación seleccionada", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpPesaje = AppTheme.CreateButton("+ Pesaje Orden", IconKind.Play, ButtonVariant.Gold, async (_, _) => await CreatePesajeForSelectedOperacionAsync(), "Registrar pesaje vinculado a esta operación", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpCompletar = AppTheme.CreateButton("Completar", IconKind.Check, ButtonVariant.Primary, async (_, _) => await ChangeOperacionEstadoAsync(EstadoOperacion.Completada), "Marcar operación como Completada", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpAnular = AppTheme.CreateButton("Anular", IconKind.Trash, ButtonVariant.Danger, async (_, _) => await ChangeOperacionEstadoAsync(EstadoOperacion.Anulada), "Anular operación con motivo", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpDocumento = AppTheme.CreateButton("+ Documento", IconKind.Document, ButtonVariant.Secondary, async (_, _) => await AddDocumentToOperacionAsync(), "Asociar documento comercial a la operación", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnOpExportar = AppTheme.CreateButton("Exportar", IconKind.Excel, ButtonVariant.Secondary, (_, _) => ExportGrid(_dgvOperaciones, "Operaciones ERP", "Operaciones", true), "Exportar operaciones", Tip, 0, 30, AppTheme.FontButtonCompact);

        // Botones Subtab 3: Inventario
        _btnInvActualizar = AppTheme.CreateButton("Actualizar Stock", IconKind.Refresh, ButtonVariant.Primary, async (_, _) => await LoadInventarioAsync(), "Recalcular existencias desde el libro mayor", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnInvAjuste = AppTheme.CreateButton("Ajuste Autorizado", IconKind.Edit, ButtonVariant.Warning, async (_, _) => await RegisterAdjustmentAsync(), "Registrar ajuste positivo o negativo auditado", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnInvExportarStock = AppTheme.CreateButton("Exportar Stock", IconKind.Excel, ButtonVariant.Success, (_, _) => ExportGrid(_dgvStock, "Existencias de Inventario", "Existencias", false), "Exportar existencias a Excel", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnInvExportarMovs = AppTheme.CreateButton("Exportar Movimientos", IconKind.Save, ButtonVariant.Gold, (_, _) => ExportGrid(_dgvMovimientos, "Movimientos de Inventario", "MovimientosInv", true), "Exportar libro mayor de movimientos", Tip, 0, 30, AppTheme.FontButtonCompact);

        // Botones Subtab 4: Tipos de Pesaje
        _btnTipoNuevo = AppTheme.CreateButton("Nuevo Tipo", IconKind.Plus, ButtonVariant.Success, async (_, _) => await EditTipoPesajeAsync(null), "Crear nuevo tipo de pesaje configurable", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnTipoEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary, async (_, _) => await EditSelectedTipoPesajeAsync(), "Editar tipo de pesaje seleccionado", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnTipoToggle = AppTheme.CreateButton("Activar / Desactivar", IconKind.Check, ButtonVariant.Warning, async (_, _) => await ToggleSelectedTipoPesajeAsync(), "Cambiar estado activo/inactivo", Tip, 0, 30, AppTheme.FontButtonCompact);
        _btnTipoActualizar = AppTheme.CreateButton("Actualizar", IconKind.Refresh, ButtonVariant.Primary, async (_, _) => await LoadTiposPesajeAsync(), "Recargar catálogo de tipos", Tip, 0, 30, AppTheme.FontButtonCompact);

        _tabs.Font = AppTheme.FontBodyBold;
        _tabs.TabPages.Add(BuildPesajesTab());
        _tabs.TabPages.Add(BuildOperacionesTab());
        _tabs.TabPages.Add(BuildInventarioTab());
        _tabs.TabPages.Add(BuildTiposPesajeTab());

        Controls.Add(_tabs);
        RefreshPermissions();
    }

    private TabPage BuildPesajesTab()
    {
        TabPage tp = new("Pesajes Industriales (1 y 2 Mediciones)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        UiHelpers.PopulatePlantaFilterCombo(_cmbPesPlanta);
        _cmbPesPlanta.Margin = new Padding(0, 3, 6, 3);
        _cmbPesEstado.Font = AppTheme.FontBody;
        _cmbPesEstado.Margin = new Padding(0, 3, 6, 3);
        _cmbPesEstado.Items.AddRange(
        [
            new NullableEstadoPesajeOpt(null, "Todos los Estados"),
            new NullableEstadoPesajeOpt(EstadoPesaje.PendienteSegundaMedicion, "Pendiente 2ª Medición"),
            new NullableEstadoPesajeOpt(EstadoPesaje.Confirmado, "Confirmado"),
            new NullableEstadoPesajeOpt(EstadoPesaje.PendienteAutorizacion, "Pendiente Autorización"),
            new NullableEstadoPesajeOpt(EstadoPesaje.Anulado, "Anulado")
        ]);
        _cmbPesEstado.SelectedIndex = 0;

        _dtpPesDesde.Value = UiHelpers.CurrentMonthStartLocal;
        _dtpPesHasta.Value = UiHelpers.CurrentDateLocal;
        AppTheme.ApplyDatePickerStyle(_dtpPesDesde);
        AppTheme.ApplyDatePickerStyle(_dtpPesHasta);
        _dtpPesDesde.Margin = new Padding(0, 3, 6, 3);
        _dtpPesHasta.Margin = new Padding(0, 3, 6, 3);

        AppTheme.ApplyInputStyle(_txtPesBuscar);
        _txtPesBuscar.Margin = new Padding(0, 3, 6, 3);
        _txtPesBuscar.PlaceholderText = "Folio, Patente o RUT...";

        Panel filter = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(
                UiHelpers.CreateFilterLabel("Planta:", 0), _cmbPesPlanta,
                UiHelpers.CreateFilterLabel("Estado:"), _cmbPesEstado,
                UiHelpers.CreateFilterLabel("Desde:"), _dtpPesDesde,
                UiHelpers.CreateFilterLabel("Hasta:"), _dtpPesHasta,
                UiHelpers.CreateFilterLabel("Buscar:"), _txtPesBuscar,
                _btnPesBuscar),
            UiHelpers.CreateFilterRow(
                _btnPesNuevo1Paso, _btnPesIniciar2Pasos, _btnPesCompletar2da, _btnPesAutorizar,
                _btnPesCorregir, _btnPesAnular, _btnPesExportar));

        AppTheme.StyleDataGridView(_dgvPesajes);
        _dgvPesajes.MultiSelect = true;
        _dgvPesajes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddTextCol("Planta", nameof(Pesaje.Planta), 66),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Tipo Pesaje", nameof(Pesaje.TipoPesajeDisplay), 110),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 92),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 68),
            UiHelpers.AddTextCol("Entidad / Cliente", nameof(Pesaje.NomCliente), 130),
            UiHelpers.AddTextCol("Material", nameof(Pesaje.NomProducto), 125),
            UiHelpers.AddNumberCol("Bruto", nameof(Pesaje.PesoBruto), "N0", 62),
            UiHelpers.AddNumberCol("Tara", nameof(Pesaje.PesoTara), "N0", 60),
            UiHelpers.AddNumberCol("Neto", nameof(Pesaje.PesoNeto), "N0", 64),
            UiHelpers.AddTextCol("Un.", nameof(Pesaje.UnidadMedida), 40),
            UiHelpers.AddTextCol("Operador", nameof(Pesaje.Operador), 72)
        ]);
        _dgvPesajes.SelectionChanged += async (_, _) => await LoadSelectedPesajeHistoryAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Nuevo Pesaje Completo (1 paso)", null, async (_, _) => await CreatePesajeAsync(PesajeDialogMode.SingleStepCreate));
        menu.Items.Add("Iniciar 1ª Medición (2 pasos)", null, async (_, _) => await CreatePesajeAsync(PesajeDialogMode.TwoStepStart));
        menu.Items.Add("Completar 2ª Medición", null, async (_, _) => await CompleteSecondMeasurementAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Autorizar Pesaje Pendiente", null, async (_, _) => await AuthorizePendingPesajeAsync());
        menu.Items.Add("Corregir Pesaje (Autorizado)", null, async (_, _) => await EditAuthorizedPesajeAsync());
        menu.Items.Add("Anular Pesaje (con reverso inventario)", null, async (_, _) => await AnnulPesajeAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar Selección a Excel", null, (_, _) => ExportGrid(_dgvPesajes, "Pesajes Industriales", "Pesajes", true));
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvPesajes, menu);

        AppTheme.StyleDataGridView(_dgvPesHistorial);
        _dgvPesHistorial.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Fecha / Hora", nameof(PesajeHistorialCambio.FechaHoraLocal), 98),
            UiHelpers.AddTextCol("Usuario", nameof(PesajeHistorialCambio.Usuario), 80),
            UiHelpers.AddTextCol("Acción", nameof(PesajeHistorialCambio.Accion), 125),
            UiHelpers.AddNumberCol("Neto Ant.", nameof(PesajeHistorialCambio.PesoNetoAnterior), "N0", 68),
            UiHelpers.AddNumberCol("Neto Nuevo", nameof(PesajeHistorialCambio.PesoNetoNuevo), "N0", 68),
            UiHelpers.AddTextCol("Estado Ant.", nameof(PesajeHistorialCambio.EstadoAnterior), 82),
            UiHelpers.AddTextCol("Estado Nuevo", nameof(PesajeHistorialCambio.EstadoNuevo), 86),
            UiHelpers.AddTextCol("Motivo / Auditoría", nameof(PesajeHistorialCambio.Motivo), 190)
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 300)
            {
                split.Panel1MinSize = 160;
                split.Panel2MinSize = 110;
                split.SplitterDistance = (int)(split.Height * 0.68);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvPesajes);
        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblHist = new() { Text = "Historial Inmutable de Cambios / Autorizaciones del Pesaje Seleccionado", Font = AppTheme.FontSmallBold, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 22 };
        botCard.Controls.Add(_dgvPesHistorial);
        botCard.Controls.Add(lblHist);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(filter);
        return tp;
    }

    private TabPage BuildOperacionesTab()
    {
        TabPage tp = new("Órdenes y Operaciones ERP") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        _cmbOpTipo.Font = AppTheme.FontBody;
        _cmbOpTipo.Margin = new Padding(0, 3, 6, 3);
        _cmbOpTipo.Items.Add(new NullableTipoOpOpt(null, "Todos los Tipos"));
        foreach (TipoOperacionComercial t in Enum.GetValues<TipoOperacionComercial>())
        {
            _cmbOpTipo.Items.Add(new NullableTipoOpOpt(t, t.ToDisplayName()));
        }

        _cmbOpTipo.SelectedIndex = 0;

        _cmbOpEstado.Font = AppTheme.FontBody;
        _cmbOpEstado.Margin = new Padding(0, 3, 6, 3);
        _cmbOpEstado.Items.Add(new NullableEstadoOpOpt(null, "Todos los Estados"));
        foreach (EstadoOperacion e in Enum.GetValues<EstadoOperacion>())
        {
            _cmbOpEstado.Items.Add(new NullableEstadoOpOpt(e, e.ToDisplayName()));
        }

        _cmbOpEstado.SelectedIndex = 0;

        Panel filter = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(
                UiHelpers.CreateFilterLabel("Tipo:", 0), _cmbOpTipo,
                UiHelpers.CreateFilterLabel("Estado:"), _cmbOpEstado,
                _btnOpBuscar, _btnOpNueva, _btnOpEditar, _btnOpPesaje, _btnOpCompletar, _btnOpAnular, _btnOpDocumento, _btnOpExportar));

        AppTheme.StyleDataGridView(_dgvOperaciones);
        _dgvOperaciones.MultiSelect = true;
        _dgvOperaciones.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(OperacionComercial.Codigo), 86),
            UiHelpers.AddDateCol("Fecha", nameof(OperacionComercial.Fecha), 72),
            UiHelpers.AddTextCol("Tipo Operación", nameof(OperacionComercial.TipoDescripcion), 115),
            UiHelpers.AddTextCol("Estado", nameof(OperacionComercial.EstadoDescripcion), 82),
            UiHelpers.AddTextCol("Proveedor", nameof(OperacionComercial.NombreProveedor), 120),
            UiHelpers.AddTextCol("Cliente", nameof(OperacionComercial.NombreCliente), 120),
            UiHelpers.AddTextCol("Material", nameof(OperacionComercial.NombreProducto), 120),
            UiHelpers.AddNumberCol("Esperado", nameof(OperacionComercial.CantidadEsperadaKg), "N0", 72),
            UiHelpers.AddNumberCol("Real Conf.", nameof(OperacionComercial.CantidadRealConfirmadaKg), "N0", 72),
            UiHelpers.AddNumberCol("Dif.", nameof(OperacionComercial.DiferenciaKg), "N0", 62),
            UiHelpers.AddTextCol("Patente", nameof(OperacionComercial.Patente), 68),
            UiHelpers.AddTextCol("Documento", nameof(OperacionComercial.DocumentoAsociado), 80)
        ]);
        _dgvOperaciones.SelectionChanged += async (_, _) => await LoadSelectedOperacionTraceabilityAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Nueva Operación ERP", null, async (_, _) => await SaveOperacionAsync(null));
        menu.Items.Add("Editar Operación", null, async (_, _) => await EditSelectedOperacionAsync());
        menu.Items.Add("Registrar Pesaje Vinculado", null, async (_, _) => await CreatePesajeForSelectedOperacionAsync());
        menu.Items.Add("Asociar Documento", null, async (_, _) => await AddDocumentToOperacionAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Marcar Completada", null, async (_, _) => await ChangeOperacionEstadoAsync(EstadoOperacion.Completada));
        menu.Items.Add("Anular Operación", null, async (_, _) => await ChangeOperacionEstadoAsync(EstadoOperacion.Anulada));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exportar Selección a Excel", null, (_, _) => ExportGrid(_dgvOperaciones, "Operaciones ERP", "Operaciones", true));
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvOperaciones, menu);

        AppTheme.StyleDataGridView(_dgvOpPesajes);
        _dgvOpPesajes.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Folio", nameof(Pesaje.FolioDisplay), 64),
            UiHelpers.AddDateCol("Fecha", nameof(Pesaje.FechaIngreso), 72),
            UiHelpers.AddTextCol("Patente", nameof(Pesaje.Patente), 70),
            UiHelpers.AddTextCol("Estado", nameof(Pesaje.EstadoDisplay), 90),
            UiHelpers.AddNumberCol("Neto Kg", nameof(Pesaje.PesoNeto), "N0", 72)
        ]);

        AppTheme.StyleDataGridView(_dgvOpMovs);
        _dgvOpMovs.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Fecha/Hora", nameof(InventarioMovimiento.FechaHoraLocal), 98),
            UiHelpers.AddTextCol("Tipo Mov.", nameof(InventarioMovimiento.TipoDescripcion), 96),
            UiHelpers.AddTextCol("Folio", nameof(InventarioMovimiento.FolioPesaje), 62),
            UiHelpers.AddNumberCol("Cantidad Kg", nameof(InventarioMovimiento.CantidadKg), "N0", 75),
            UiHelpers.AddTextCol("Usuario", nameof(InventarioMovimiento.Usuario), 72)
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 150;
                split.Panel2MinSize = 140;
                split.SplitterDistance = (int)(split.Height * 0.56);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        topCard.Controls.Add(_dgvOperaciones);

        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        _lblOpTrazabilidad.Text = "Trazabilidad Punta a Punta: Seleccione una operación para ver pesajes y movimientos de inventario asociados.";
        _lblOpTrazabilidad.Font = AppTheme.FontSmallBold;
        _lblOpTrazabilidad.ForeColor = AppTheme.AccentBlue;
        _lblOpTrazabilidad.AutoEllipsis = true;
        _lblOpTrazabilidad.UseMnemonic = false;
        _lblOpTrazabilidad.Dock = DockStyle.Top;
        _lblOpTrazabilidad.Height = 24;

        TableLayoutPanel traceGrid = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        traceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        traceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        traceGrid.Controls.Add(_dgvOpPesajes, 0, 0);
        traceGrid.Controls.Add(_dgvOpMovs, 1, 0);

        botCard.Controls.Add(traceGrid);
        botCard.Controls.Add(_lblOpTrazabilidad);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(filter);
        return tp;
    }

    private TabPage BuildInventarioTab()
    {
        TabPage tp = new("Inventario y Movimientos (Libro Mayor)") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };

        Panel bar = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(_btnInvActualizar, _btnInvAjuste, _btnInvExportarStock, _btnInvExportarMovs));

        AppTheme.StyleDataGridView(_dgvStock);
        _dgvStock.MultiSelect = true;
        _dgvStock.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(InventarioExistencia.CodProducto), 76),
            UiHelpers.AddTextCol("Material", nameof(InventarioExistencia.NombreProducto), 155),
            UiHelpers.AddTextCol("Grupo", nameof(InventarioExistencia.GrupoMaterial), 115),
            UiHelpers.AddTextCol("Unid.", nameof(InventarioExistencia.UnidadMedida), 45),
            UiHelpers.AddNumberCol("Entradas", nameof(InventarioExistencia.EntradasKg), "N0", 80),
            UiHelpers.AddNumberCol("Salidas", nameof(InventarioExistencia.SalidasKg), "N0", 80),
            UiHelpers.AddNumberCol("Ajustes", nameof(InventarioExistencia.AjustesKg), "N0", 72),
            UiHelpers.AddNumberCol("Stock Actual", nameof(InventarioExistencia.StockActualKg), "N0", 86),
            UiHelpers.AddNumberCol("Movs.", nameof(InventarioExistencia.CantidadMovimientos), "N0", 54)
        ]);
        _dgvStock.SelectionChanged += async (_, _) => await LoadMovimientosForSelectedMaterialAsync();

        ContextMenuStrip stockMenu = new();
        stockMenu.Items.Add("Registrar Ajuste Autorizado para este material", null, async (_, _) => await RegisterAdjustmentAsync());
        stockMenu.Items.Add("Exportar Existencias a Excel", null, (_, _) => ExportGrid(_dgvStock, "Existencias de Inventario", "Existencias", true));
        _menus.Add(stockMenu);
        UiHelpers.AttachGridContextMenu(_dgvStock, stockMenu);

        AppTheme.StyleDataGridView(_dgvMovimientos);
        _dgvMovimientos.MultiSelect = true;
        _dgvMovimientos.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Fecha / Hora", nameof(InventarioMovimiento.FechaHoraLocal), 98),
            UiHelpers.AddTextCol("Tipo Movimiento", nameof(InventarioMovimiento.TipoDescripcion), 108),
            UiHelpers.AddTextCol("Código", nameof(InventarioMovimiento.CodProducto), 74),
            UiHelpers.AddTextCol("Material", nameof(InventarioMovimiento.NombreProducto), 135),
            UiHelpers.AddNumberCol("Cantidad (+/-)", nameof(InventarioMovimiento.CantidadKg), "N0", 78),
            UiHelpers.AddTextCol("Folio Pesaje", nameof(InventarioMovimiento.FolioPesaje), 70),
            UiHelpers.AddTextCol("Operación", nameof(InventarioMovimiento.CodigoOperacion), 80),
            UiHelpers.AddTextCol("Documento", nameof(InventarioMovimiento.DocumentoReferencia), 80),
            UiHelpers.AddTextCol("Usuario", nameof(InventarioMovimiento.Usuario), 72),
            UiHelpers.AddTextCol("Observaciones", nameof(InventarioMovimiento.Observaciones), 155)
        ]);

        ContextMenuStrip movMenu = new();
        movMenu.Items.Add("Exportar Movimientos Seleccionados", null, (_, _) => ExportGrid(_dgvMovimientos, "Libro Mayor Inventario", "MovimientosInv", true));
        movMenu.Items.Add("Ver Todos los Movimientos", null, async (_, _) => await LoadMovimientosAsync(null));
        _menus.Add(movMenu);
        UiHelpers.AttachGridContextMenu(_dgvMovimientos, movMenu);

        SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > 320)
            {
                split.Panel1MinSize = 140;
                split.Panel2MinSize = 150;
                split.SplitterDistance = (int)(split.Height * 0.45);
            }
        };

        Panel topCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblStock = new() { Text = "Existencias Derivadas Exclusivamente de Movimientos Confirmados (sin edición directa de stock)", Font = AppTheme.FontSmallBold, ForeColor = AppTheme.SidebarDark, Dock = DockStyle.Top, Height = 22 };
        topCard.Controls.Add(_dgvStock);
        topCard.Controls.Add(lblStock);

        Panel botCard = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        Label lblMovs = new() { Text = "Libro Mayor de Movimientos Inmutables de Inventario (Entradas, Salidas, Ajustes y Reversos)", Font = AppTheme.FontSmallBold, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 22 };
        botCard.Controls.Add(_dgvMovimientos);
        botCard.Controls.Add(lblMovs);

        split.Panel1.Controls.Add(topCard);
        split.Panel2.Controls.Add(botCard);
        tp.Controls.Add(split);
        tp.Controls.Add(bar);
        return tp;
    }

    private TabPage BuildTiposPesajeTab()
    {
        TabPage tp = new("Tipos de Pesaje Configurables") { BackColor = AppTheme.CanvasLight, Padding = new Padding(8) };
        Panel bar = UiHelpers.BuildAutoFilterCard(
            UiHelpers.CreateFilterRow(_btnTipoNuevo, _btnTipoEditar, _btnTipoToggle, _btnTipoActualizar));

        AppTheme.StyleDataGridView(_dgvTipos);
        _dgvTipos.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Código", nameof(TipoPesajeConfig.Codigo), 95),
            UiHelpers.AddTextCol("Nombre del Tipo de Pesaje", nameof(TipoPesajeConfig.Nombre), 165),
            UiHelpers.AddTextCol("Modo Medición", nameof(TipoPesajeConfig.ModoMedicion), 135),
            UiHelpers.AddTextCol("Impacto Inventario", nameof(TipoPesajeConfig.ImpactoInventario), 100),
            UiHelpers.AddNumberCol("Orden", nameof(TipoPesajeConfig.Orden), "N0", 52),
            UiHelpers.AddTextCol("Estado", nameof(TipoPesajeConfig.EstadoDescripcion), 70)
        ]);

        ContextMenuStrip menu = new();
        menu.Items.Add("Nuevo Tipo de Pesaje", null, async (_, _) => await EditTipoPesajeAsync(null));
        menu.Items.Add("Editar Tipo Seleccionado", null, async (_, _) => await EditSelectedTipoPesajeAsync());
        menu.Items.Add("Activar / Desactivar", null, async (_, _) => await ToggleSelectedTipoPesajeAsync());
        _menus.Add(menu);
        UiHelpers.AttachGridContextMenu(_dgvTipos, menu);

        Panel card = new() { Dock = DockStyle.Fill, BackColor = AppTheme.CardSurface, Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        card.Controls.Add(_dgvTipos);
        tp.Controls.Add(card);
        tp.Controls.Add(bar);
        return tp;
    }

    public override void RefreshPermissions()
    {
        bool canOp = _auth.HasPermission(PermisoSistema.GestionarPesajesOperaciones);
        bool canAuth = _auth.HasPermission(PermisoSistema.AutorizarAnularPesajesInventario);
        bool canExp = _auth.HasPermission(PermisoSistema.ExportarExcel);

        _btnPesNuevo1Paso.Enabled = canOp;
        _btnPesIniciar2Pasos.Enabled = canOp;
        _btnPesCompletar2da.Enabled = canOp;
        _btnPesAutorizar.Enabled = canAuth;
        _btnPesCorregir.Enabled = canAuth;
        _btnPesAnular.Enabled = canAuth;
        _btnPesExportar.Enabled = canExp;

        _btnOpNueva.Enabled = canOp;
        _btnOpEditar.Enabled = canOp;
        _btnOpPesaje.Enabled = canOp;
        _btnOpCompletar.Enabled = canOp;
        _btnOpAnular.Enabled = canAuth;
        _btnOpDocumento.Enabled = _auth.HasPermission(PermisoSistema.AdministrarCrmEntidades);
        _btnOpExportar.Enabled = canExp;

        _btnInvAjuste.Enabled = canAuth;
        _btnInvExportarStock.Enabled = canExp;
        _btnInvExportarMovs.Enabled = canExp;

        _btnTipoNuevo.Enabled = canOp;
        _btnTipoEditar.Enabled = canOp;
        _btnTipoToggle.Enabled = canOp;
    }

    public override async Task ActivateAsync(CancellationToken ct = default)
    {
        await LoadPesajesAsync(ct);
        await LoadOperacionesAsync(ct);
        await LoadInventarioAsync(ct);
        await LoadTiposPesajeAsync(ct);
    }

    private async Task LoadPesajesAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            EstadoPesaje? est = (_cmbPesEstado.SelectedItem as NullableEstadoPesajeOpt)?.Estado;
            string search = _txtPesBuscar.Text.Trim();
            EnterpriseReportFilterDto filter = new()
            {
                Planta = UiHelpers.GetSelectedNullablePlanta(_cmbPesPlanta),
                Desde = DateOnly.FromDateTime(_dtpPesDesde.Value),
                Hasta = DateOnly.FromDateTime(_dtpPesHasta.Value),
                Estados = est.HasValue ? new HashSet<EstadoPesaje> { est.Value } : null,
                ExcluirAnulados = false
            };

            EnterpriseReportResultDto rep = await _enterprise.GetEnterpriseReportAsync(filter, ct);
            IReadOnlyList<Pesaje> rows = string.IsNullOrWhiteSpace(search)
                ? rep.DetallePesajes
                : [.. rep.DetallePesajes.Where(p =>
                    p.FolioDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Patente.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.RutCliente.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.NomCliente.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.NomProducto.Contains(search, StringComparison.OrdinalIgnoreCase))];

            _dgvPesajes.DataSource = null;
            _dgvPesajes.DataSource = rows;
            NotifyStatus($"Pesajes industriales: {rows.Count:N0} registro(s).");
        }, NotifyStatus);
    }

    private async Task LoadSelectedPesajeHistoryAsync()
    {
        if (_dgvPesajes.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            _dgvPesHistorial.DataSource = null;
            return;
        }

        IReadOnlyList<PesajeHistorialCambio> hist = await _enterprise.GetPesajeHistorialAsync(p.Id);
        _dgvPesHistorial.DataSource = null;
        _dgvPesHistorial.DataSource = hist;
    }

    private async Task CreatePesajeAsync(PesajeDialogMode mode, long? presetOpId = null)
    {
        IReadOnlyList<TipoPesajeConfig> tipos = await _enterprise.GetTiposPesajeConfigAsync(soloActivos: true);
        IReadOnlyList<Vehiculo> vehs = await _enterprise.GetVehiculosAsync(soloActivos: true);
        IReadOnlyList<Producto> mats = await _enterprise.GetMaterialesAsync(soloActivos: true);
        IReadOnlyList<Cliente> ents = await _enterprise.GetEntidadesComercialesAsync(soloActivos: true);
        IReadOnlyList<OperacionComercial> ops = await _enterprise.GetOperacionesAsync();

        if (vehs.Count == 0 || mats.Count == 0)
        {
            MessageBox.Show(this, "Debe existir al menos un vehículo activo y un material activo antes de registrar pesajes.", "Catálogos Requeridos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using PesajeEditorDialog dlg = new(mode, tipos, vehs, mats, ents, ops, _auth.HasPermission(PermisoSistema.AutorizarAnularPesajesInventario), null, presetOpId);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultRequest is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Pesaje saved = mode == PesajeDialogMode.TwoStepStart
                ? await _enterprise.IniciarPesajeDosMedicionesAsync(dlg.ResultRequest)
                : await _enterprise.RegistrarPesajeCompletoAsync(dlg.ResultRequest);

            await ActivateAsync();
            NotifyStatus($"Pesaje Folio {saved.FolioDisplay} registrado ({saved.EstadoDisplay}, Neto {saved.PesoNeto:N0} {saved.UnidadMedida}).");
        }, NotifyStatus, "Guardando pesaje industrial...");
    }

    private async Task CompleteSecondMeasurementAsync()
    {
        if (_dgvPesajes.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            MessageBox.Show(this, "Seleccione un pesaje en estado 'Pendiente 2ª Medición'.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (p.Estado != EstadoPesaje.PendienteSegundaMedicion)
        {
            MessageBox.Show(this, $"El pesaje Folio {p.FolioDisplay} tiene estado '{p.EstadoDisplay}' y no admite 2ª medición.", "Estado Inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        IReadOnlyList<TipoPesajeConfig> tipos = await _enterprise.GetTiposPesajeConfigAsync();
        IReadOnlyList<Vehiculo> vehs = await _enterprise.GetVehiculosAsync();
        IReadOnlyList<Producto> mats = await _enterprise.GetMaterialesAsync();
        IReadOnlyList<Cliente> ents = await _enterprise.GetEntidadesComercialesAsync();
        IReadOnlyList<OperacionComercial> ops = await _enterprise.GetOperacionesAsync();

        using PesajeEditorDialog dlg = new(PesajeDialogMode.TwoStepComplete, tipos, vehs, mats, ents, ops, _auth.HasPermission(PermisoSistema.AutorizarAnularPesajesInventario), p);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            Pesaje saved = await _enterprise.CompletarSegundaMedicionAsync(
                p.Id, dlg.SegundaMedicionKg, dlg.FechaSalida, dlg.HoraSalida, dlg.ObservacionTexto, dlg.AutorizacionExcepcion);
            await ActivateAsync();
            NotifyStatus($"2ª medición completada para Folio {saved.FolioDisplay}: Neto {saved.PesoNeto:N0} {saved.UnidadMedida} ({saved.EstadoDisplay}).");
        }, NotifyStatus, "Calculando peso neto y actualizando inventario...");
    }

    private async Task AuthorizePendingPesajeAsync()
    {
        if (_dgvPesajes.CurrentRow?.DataBoundItem is not Pesaje p || p.Estado != EstadoPesaje.PendienteAutorizacion)
        {
            MessageBox.Show(this, "Seleccione un pesaje en estado 'Pendiente Autorización'.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? motivo = UiHelpers.PromptAuditReason(this, $"Autorizar Pesaje Folio {p.FolioDisplay}", "Ingrese justificación de autorización para confirmar el pesaje y actualizar el inventario:");
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.AutorizarPesajePendienteAsync(p.Id, motivo);
            await ActivateAsync();
        }, NotifyStatus, "Autorizando pesaje...");
    }

    private async Task EditAuthorizedPesajeAsync()
    {
        if (_dgvPesajes.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            MessageBox.Show(this, "Seleccione un pesaje para corregir.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        IReadOnlyList<TipoPesajeConfig> tipos = await _enterprise.GetTiposPesajeConfigAsync();
        IReadOnlyList<Vehiculo> vehs = await _enterprise.GetVehiculosAsync();
        IReadOnlyList<Producto> mats = await _enterprise.GetMaterialesAsync();
        IReadOnlyList<Cliente> ents = await _enterprise.GetEntidadesComercialesAsync();
        IReadOnlyList<OperacionComercial> ops = await _enterprise.GetOperacionesAsync();

        using PesajeEditorDialog dlg = new(PesajeDialogMode.AuthorizedCorrection, tipos, vehs, mats, ents, ops, _auth.HasPermission(PermisoSistema.AutorizarAnularPesajesInventario), p);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultRequest is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.ModificarPesajeAutorizadoAsync(dlg.ResultRequest, dlg.MotivoCambio);
            await ActivateAsync();
        }, NotifyStatus, "Aplicando corrección autorizada...");
    }

    private async Task AnnulPesajeAsync()
    {
        if (_dgvPesajes.CurrentRow?.DataBoundItem is not Pesaje p)
        {
            MessageBox.Show(this, "Seleccione el pesaje que desea anular.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? motivo = UiHelpers.PromptAuditReason(this, $"Anular Pesaje Folio {p.FolioDisplay}", "Indique motivo obligatorio de anulación (generará reverso en inventario y preservará el historial):");
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.AnularPesajeAsync(p.Id, motivo);
            await ActivateAsync();
        }, NotifyStatus, "Anulando pesaje...");
    }

    // ---- Operaciones ERP ----

    private async Task LoadOperacionesAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            TipoOperacionComercial? tipo = (_cmbOpTipo.SelectedItem as NullableTipoOpOpt)?.Tipo;
            EstadoOperacion? est = (_cmbOpEstado.SelectedItem as NullableEstadoOpOpt)?.Estado;
            IReadOnlyList<OperacionComercial> ops = await _enterprise.GetOperacionesAsync(tipo, est, ct: ct);
            _dgvOperaciones.DataSource = null;
            _dgvOperaciones.DataSource = ops;
        }, NotifyStatus);
    }

    private async Task LoadSelectedOperacionTraceabilityAsync()
    {
        if (_dgvOperaciones.CurrentRow?.DataBoundItem is not OperacionComercial op)
        {
            _dgvOpPesajes.DataSource = null;
            _dgvOpMovs.DataSource = null;
            return;
        }

        OperacionTrazabilidadDto tr = await _enterprise.GetOperacionTrazabilidadAsync(op.Id);
        _lblOpTrazabilidad.Text = $"Trazabilidad {op.Codigo} ({op.TipoDescripcion}) · Esperado: {op.CantidadEsperadaKg:N0} {op.UnidadMedida} · Real Confirmado: {tr.TotalPesadoConfirmadoKg:N0} · Dif: {tr.DiferenciaVsEsperadoKg:+#,##0;-#,##0;0} · Pesajes: {tr.PesajesVinculados.Count} · Movs Inv: {tr.MovimientosInventario.Count} · Docs: {tr.DocumentosAsociados.Count}";
        _dgvOpPesajes.DataSource = null;
        _dgvOpPesajes.DataSource = tr.PesajesVinculados;
        _dgvOpMovs.DataSource = null;
        _dgvOpMovs.DataSource = tr.MovimientosInventario;
    }

    private async Task SaveOperacionAsync(OperacionComercial? existing)
    {
        IReadOnlyList<Cliente> ents = await _enterprise.GetEntidadesComercialesAsync(soloActivos: true);
        IReadOnlyList<Producto> mats = await _enterprise.GetMaterialesAsync(soloActivos: true);
        IReadOnlyList<Vehiculo> vehs = await _enterprise.GetVehiculosAsync(soloActivos: true);

        using OperacionComercialDialog dlg = new(ents, mats, vehs, existing);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultOperacion is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveOperacionAsync(dlg.ResultOperacion);
            await LoadOperacionesAsync();
        }, NotifyStatus, "Guardando operación ERP...");
    }

    private Task EditSelectedOperacionAsync()
    {
        if (_dgvOperaciones.CurrentRow?.DataBoundItem is not OperacionComercial op)
        {
            MessageBox.Show(this, "Seleccione una operación ERP para editar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return SaveOperacionAsync(op);
    }

    private Task CreatePesajeForSelectedOperacionAsync()
    {
        if (_dgvOperaciones.CurrentRow?.DataBoundItem is not OperacionComercial op)
        {
            MessageBox.Show(this, "Seleccione una operación ERP para vincular el pesaje.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return CreatePesajeAsync(PesajeDialogMode.SingleStepCreate, op.Id);
    }

    private async Task ChangeOperacionEstadoAsync(EstadoOperacion newState)
    {
        if (_dgvOperaciones.CurrentRow?.DataBoundItem is not OperacionComercial op)
        {
            MessageBox.Show(this, "Seleccione una operación ERP.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string motivo = newState == EstadoOperacion.Anulada
            ? (UiHelpers.PromptAuditReason(this, $"Anular Operación {op.Codigo}", "Indique el motivo de anulación:") ?? string.Empty)
            : "Cierre operacional confirmado.";

        if (newState == EstadoOperacion.Anulada && string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.UpdateOperacionEstadoAsync(op.Id, newState, motivo);
            await LoadOperacionesAsync();
        }, NotifyStatus);
    }

    private async Task AddDocumentToOperacionAsync()
    {
        if (_dgvOperaciones.CurrentRow?.DataBoundItem is not OperacionComercial op)
        {
            MessageBox.Show(this, "Seleccione una operación ERP.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string rutRef = !string.IsNullOrWhiteSpace(op.RutProveedor) ? op.RutProveedor : op.RutCliente;
        using DocumentoAsociadoDialog dlg = new(rutRef, op.Codigo, operacionId: op.Id);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultDocumento is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveDocumentoAsync(dlg.ResultDocumento);
            await LoadSelectedOperacionTraceabilityAsync();
        }, NotifyStatus);
    }

    // ---- Inventario ----

    private async Task LoadInventarioAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            IReadOnlyList<InventarioExistencia> stock = await _enterprise.GetExistenciasInventarioAsync(ct);
            _dgvStock.DataSource = null;
            _dgvStock.DataSource = stock;
            await LoadMovimientosAsync(null, ct);
        }, NotifyStatus);
    }

    private async Task LoadMovimientosForSelectedMaterialAsync()
    {
        string? cod = (_dgvStock.CurrentRow?.DataBoundItem as InventarioExistencia)?.CodProducto;
        await LoadMovimientosAsync(cod);
    }

    private async Task LoadMovimientosAsync(string? codProducto, CancellationToken ct = default)
    {
        IReadOnlyList<InventarioMovimiento> movs = await _enterprise.GetMovimientosInventarioAsync(codProducto, limit: 500, ct: ct);
        _dgvMovimientos.DataSource = null;
        _dgvMovimientos.DataSource = movs;
    }

    private async Task RegisterAdjustmentAsync()
    {
        IReadOnlyList<Producto> mats = await _enterprise.GetMaterialesAsync();
        string? preset = (_dgvStock.CurrentRow?.DataBoundItem as InventarioExistencia)?.CodProducto;

        using AjusteInventarioDialog dlg = new(mats, preset);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.RegistrarAjusteInventarioAsync(dlg.SelectedCodProducto, dlg.SignedCantidadKg, dlg.DocumentoReferencia, dlg.Motivo);
            await LoadInventarioAsync();
        }, NotifyStatus, "Registrando ajuste autorizado...");
    }

    // ---- Tipos de Pesaje ----

    private async Task LoadTiposPesajeAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            IReadOnlyList<TipoPesajeConfig> tipos = await _enterprise.GetTiposPesajeConfigAsync(ct: ct);
            _dgvTipos.DataSource = null;
            _dgvTipos.DataSource = tipos;
        }, NotifyStatus);
    }

    private async Task EditTipoPesajeAsync(TipoPesajeConfig? existing)
    {
        using TipoPesajeConfigDialog dlg = new(existing);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultConfig is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SaveTipoPesajeConfigAsync(dlg.ResultConfig);
            await LoadTiposPesajeAsync();
        }, NotifyStatus);
    }

    private Task EditSelectedTipoPesajeAsync()
    {
        if (_dgvTipos.CurrentRow?.DataBoundItem is not TipoPesajeConfig cfg)
        {
            MessageBox.Show(this, "Seleccione un tipo de pesaje.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        return EditTipoPesajeAsync(cfg);
    }

    private async Task ToggleSelectedTipoPesajeAsync()
    {
        if (_dgvTipos.CurrentRow?.DataBoundItem is not TipoPesajeConfig cfg)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _enterprise.SetTipoPesajeConfigActivoAsync(cfg.Id, !cfg.Activo);
            await LoadTiposPesajeAsync();
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
