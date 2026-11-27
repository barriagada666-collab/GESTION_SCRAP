using GestionScrap.Application.DTOs;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Domain.Interfaces;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Módulo 2: Informe por Grupo de Material con CRUD completo en árbol jerárquico,
/// exclusión de totales en cero y exportación completa o por rama/selección.
/// </summary>
public sealed class GrupoMaterialControl : ScrapTabControlBase
{
    private readonly ScrapApplicationService _app;
    private readonly IAuthService _auth;

    private readonly ComboBox _cmbPlanta = new() { Width = 145 };
    private readonly DateTimePicker _dtpDesde = new();
    private readonly DateTimePicker _dtpHasta = new();
    private readonly CheckBox _chkSoloRecepciones = new();
    private readonly CheckBox _chkExcluirCeros = new();
    private readonly Button _btnConsultar;
    private readonly Button _btnExportar;
    private readonly Button _btnExportarSeleccion;

    private readonly Button _btnAddPadre;
    private readonly Button _btnAddSubgrupo;
    private readonly Button _btnAddProducto;
    private readonly Button _btnEditar;
    private readonly Button _btnMover;
    private readonly Button _btnSubir;
    private readonly Button _btnBajar;
    private readonly Button _btnEliminar;

    private readonly ImageList _treeIcons = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly TreeView _tree = new();

    private readonly Label _lblKilosVal;
    private readonly Label _lblKilosSub;
    private readonly Label _lblNodosVal;
    private readonly Label _lblNodosSub;
    private readonly Label _lblUnassignedVal;
    private readonly Label _lblUnassignedSub;

    private readonly DoubleBufferedDataGridView _dgvJerarquia = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, MultiSelect = true };
    private readonly DashboardChartControl _chartProductos = new() { Dock = DockStyle.Fill };

    private IReadOnlyList<MaterialGroupNode> _nodes = [];
    private GrupoMaterialReportResultDto? _lastReport;
    private long? _selectedNodeId;

    public GrupoMaterialControl(ScrapApplicationService app, IAuthService auth)
    {
        _app = app;
        _auth = auth;

        Dock = DockStyle.Fill;
        BackColor = AppTheme.CanvasLight;
        Padding = new Padding(12);

        _treeIcons.Images.Add("folder", IconFactory.GetIcon(IconKind.Layers, AppTheme.AccentGold, 16));
        _treeIcons.Images.Add("product", IconFactory.GetIcon(IconKind.Document, AppTheme.AccentCyan, 16));

        Panel filterCard = UiHelpers.BuildFilterCard(90);
        filterCard.BorderStyle = BorderStyle.FixedSingle;

        UiHelpers.PopulatePlantaFilterCombo(_cmbPlanta);

        _dtpDesde.Value = new DateTime(2024, 1, 1);
        _dtpHasta.Value = DateTime.Today;
        AppTheme.ApplyDatePickerStyle(_dtpDesde);
        AppTheme.ApplyDatePickerStyle(_dtpHasta);

        _chkSoloRecepciones.Text = "Solo Recepciones";
        _chkSoloRecepciones.Font = AppTheme.FontBody;
        _chkSoloRecepciones.AutoSize = true;
        _chkSoloRecepciones.Margin = new Padding(8, 7, 4, 0);
        _chkSoloRecepciones.CheckedChanged += async (_, _) => await LoadReportOnlyAsync();

        _chkExcluirCeros.Text = "Excluir totales en 0";
        _chkExcluirCeros.Font = AppTheme.FontBodyBold;
        _chkExcluirCeros.AutoSize = true;
        _chkExcluirCeros.Margin = new Padding(8, 7, 6, 0);
        _chkExcluirCeros.CheckedChanged += async (_, _) => await LoadReportOnlyAsync();

        _btnConsultar = AppTheme.CreateButton("Calcular Informe", IconKind.Search, ButtonVariant.Primary,
            async (_, _) => await RefreshTreeAndReportAsync(), "Recalcula acumulados por jerarquía de grupos", Tip, 145, 30);
        _btnExportar = AppTheme.CreateButton("Exportar Jerarquía", IconKind.Excel, ButtonVariant.Success,
            (_, _) => ExportarDirecto(), "Exporta la estructura completa padre/hijo y kilos a Excel", Tip, 152, 30);
        _btnExportarSeleccion = AppTheme.CreateButton("Exportar Selección...", IconKind.Save, ButtonVariant.Gold,
            (_, _) => ExportarPersonalizado(), "Exportar solo la rama o filas seleccionadas a Excel", Tip, 168, 30);

        TableLayoutPanel filterRows = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        filterRows.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r1.Controls.AddRange(
        [
            new Label { Text = "Planta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 7, 4, 0) },
            _cmbPlanta,
            new Label { Text = "Desde:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(8, 7, 4, 0) },
            _dtpDesde,
            new Label { Text = "Hasta:", Font = AppTheme.FontBodyBold, AutoSize = true, Margin = new Padding(6, 7, 4, 0) },
            _dtpHasta,
            _chkSoloRecepciones,
            _chkExcluirCeros
        ]);

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        r2.Controls.AddRange([_btnConsultar, _btnExportar, _btnExportarSeleccion]);

        filterRows.Controls.Add(r1, 0, 0);
        filterRows.Controls.Add(r2, 0, 1);
        filterCard.Controls.Add(filterRows);

        Size = new Size(1280, 760);

        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6
        };
        split.HandleCreated += (_, _) =>
        {
            if (split.Width > 860)
            {
                split.Panel1MinSize = 320;
                split.Panel2MinSize = 460;
                split.SplitterDistance = 395;
            }
        };

        Panel pnlLeft = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        Label lblTreeTitle = new()
        {
            Text = "Estructura Editable de Grupos (Nodos Padre / Hijo)",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.SidebarDark,
            Dock = DockStyle.Top,
            Height = 26
        };

        FlowLayoutPanel crudBar = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(0, 4, 0, 4),
            WrapContents = true
        };

        _btnAddPadre = AppTheme.CreateButton("Grupo Padre", IconKind.Plus, ButtonVariant.Success,
            async (_, _) => await AddNodeAsync(NodoGrupoTipo.CarpetaGrupo, atRoot: true),
            "Agregar nuevo Grupo Padre en el nivel raíz", Tip, 118, 30);
        _btnAddSubgrupo = AppTheme.CreateButton("Subgrupo", IconKind.Layers, ButtonVariant.Primary,
            async (_, _) => await AddNodeAsync(NodoGrupoTipo.CarpetaGrupo, atRoot: false),
            "Agregar carpeta Subgrupo como nodo hijo del seleccionado", Tip, 106, 30);
        _btnAddProducto = AppTheme.CreateButton("Producto", IconKind.Import, ButtonVariant.Gold,
            async (_, _) => await AddNodeAsync(NodoGrupoTipo.ProductoAsignado, atRoot: false),
            "Asignar un código de producto como nodo hijo del grupo elegido", Tip, 102, 30);
        _btnEditar = AppTheme.CreateButton("Editar", IconKind.Edit, ButtonVariant.Secondary,
            async (_, _) => await EditSelectedNodeAsync(),
            "Renombrar o editar el nodo seleccionado", Tip, 86, 30);
        _btnMover = AppTheme.CreateButton("Mover", IconKind.Move, ButtonVariant.Secondary,
            async (_, _) => await MoveSelectedNodeAsync(),
            "Mover el nodo o subrama hacia otro grupo padre", Tip, 90, 30);
        _btnSubir = AppTheme.CreateButton("Subir", IconKind.ArrowUp, ButtonVariant.Secondary,
            async (_, _) => await ReorderSelectedAsync(moveUp: true),
            "Mover nodo hacia arriba dentro del mismo grupo", Tip, 80, 30);
        _btnBajar = AppTheme.CreateButton("Bajar", IconKind.ArrowDown, ButtonVariant.Secondary,
            async (_, _) => await ReorderSelectedAsync(moveUp: false),
            "Mover nodo hacia abajo dentro del mismo grupo", Tip, 80, 30);
        _btnEliminar = AppTheme.CreateButton("Quitar", IconKind.Trash, ButtonVariant.Danger,
            async (_, _) => await DeleteSelectedNodeAsync(),
            "Quitar el nodo seleccionado (subgrupos o productos)", Tip, 88, 30);

        crudBar.Controls.AddRange([_btnAddPadre, _btnAddSubgrupo, _btnAddProducto, _btnEditar, _btnMover, _btnSubir, _btnBajar, _btnEliminar]);

        _tree.Dock = DockStyle.Fill;
        _tree.Font = AppTheme.FontBody;
        _tree.ImageList = _treeIcons;
        _tree.HideSelection = false;
        _tree.ShowLines = true;
        _tree.ShowPlusMinus = true;
        _tree.ShowRootLines = true;
        _tree.AllowDrop = true;
        _tree.AfterSelect += async (_, _) =>
        {
            _selectedNodeId = _tree.SelectedNode?.Tag is MaterialGroupNode mn ? mn.Id : null;
            await LoadReportOnlyAsync();
        };
        _tree.ItemDrag += (_, e) =>
        {
            if (e.Item is TreeNode tn && _auth.HasPermission(PermisoSistema.AdministrarGrupoMaterial))
            {
                _tree.DoDragDrop(tn, DragDropEffects.Move);
            }
        };
        _tree.DragEnter += (_, e) =>
        {
            if (e.Data is not null && e.Data.GetDataPresent(typeof(TreeNode)))
            {
                e.Effect = DragDropEffects.Move;
            }
        };
        _tree.DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode dragged || dragged.Tag is not MaterialGroupNode srcNode)
            {
                return;
            }

            Point pt = _tree.PointToClient(new Point(e.X, e.Y));
            TreeNode? targetUi = _tree.GetNodeAt(pt);
            long? targetParentId = null;
            if (targetUi?.Tag is MaterialGroupNode destNode)
            {
                targetParentId = destNode.TipoNodo == NodoGrupoTipo.CarpetaGrupo ? destNode.Id : destNode.ParentId;
            }

            if (srcNode.Id == targetParentId)
            {
                return;
            }

            await UiHelpers.RunSafeAsync(this, async () =>
            {
                await _app.MoveMaterialNodeAsync(srcNode.Id, targetParentId);
                await RefreshTreeAndReportAsync();
            }, NotifyStatus, "Moviendo nodo...");
        };

        Label lblDragHint = new()
        {
            Text = "Tip: arrastre un nodo o use Shift/Ctrl en la tabla para exportar ramas/filas seleccionadas.",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Italic),
            ForeColor = AppTheme.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 22
        };

        pnlLeft.Controls.Add(_tree);
        pnlLeft.Controls.Add(lblDragHint);
        pnlLeft.Controls.Add(crudBar);
        pnlLeft.Controls.Add(lblTreeTitle);

        Panel pnlRight = new() { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 0) };

        TableLayoutPanel kpis = new() { Dock = DockStyle.Top, Height = 92, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        kpis.Controls.Add(UiHelpers.CreateKpiCard("KILOS DEL ÁRBOL", AppTheme.AccentCyan, out _lblKilosVal, out _lblKilosSub), 0, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("GRUPOS Y NODOS", AppTheme.AccentGold, out _lblNodosVal, out _lblNodosSub), 1, 0);
        kpis.Controls.Add(UiHelpers.CreateKpiCard("PRODUCTOS SIN ASIGNAR", AppTheme.AccentAmber, out _lblUnassignedVal, out _lblUnassignedSub), 2, 0);

        Panel chartHost = new()
        {
            Dock = DockStyle.Bottom,
            Height = 205,
            Padding = new Padding(0, 6, 0, 0)
        };
        chartHost.Controls.Add(_chartProductos);

        Panel gridCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardSurface,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        Label lblGridTitle = new() { Text = "Consolidado Jerárquico (multi-selección habilitada)", Font = AppTheme.FontSection, ForeColor = AppTheme.AccentBlue, Dock = DockStyle.Top, Height = 26 };

        _dgvJerarquia.Columns.AddRange(
        [
            UiHelpers.AddTextCol("Grupo / Nodo Material", nameof(MaterialTreeFlatRowDto.NombreConSangria), 175),
            UiHelpers.AddTextCol("Código", nameof(MaterialTreeFlatRowDto.CodigoProducto), 70),
            UiHelpers.AddNumberCol("Viajes", nameof(MaterialTreeFlatRowDto.Viajes), "N0", 50),
            UiHelpers.AddNumberCol("Kilos Netos", nameof(MaterialTreeFlatRowDto.TotalKilos), "N0", 75),
            UiHelpers.AddNumberCol("% Total", nameof(MaterialTreeFlatRowDto.Porcentaje), "N2", 55),
            UiHelpers.AddNumberCol("Prom. Kg/Viaje", nameof(MaterialTreeFlatRowDto.PromedioKgViaje), "N0", 65)
        ]);
        _dgvJerarquia.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _dgvJerarquia.Rows[e.RowIndex].DataBoundItem is MaterialTreeFlatRowDto row)
            {
                if (row.Nivel == 0)
                {
                    e.CellStyle!.Font = AppTheme.FontBodyBold;
                    e.CellStyle.BackColor = Color.FromArgb(241, 245, 249);
                }
            }
        };

        gridCard.Controls.Add(_dgvJerarquia);
        gridCard.Controls.Add(lblGridTitle);

        pnlRight.Controls.Add(gridCard);
        pnlRight.Controls.Add(chartHost);
        pnlRight.Controls.Add(kpis);

        split.Panel1.Controls.Add(pnlLeft);
        split.Panel2.Controls.Add(pnlRight);

        Controls.Add(split);
        Controls.Add(filterCard);
        RefreshPermissions();
    }

    public override void RefreshPermissions()
    {
        bool canRead = _auth.HasPermission(PermisoSistema.VerGrupoMaterial);
        bool canEdit = _auth.HasPermission(PermisoSistema.AdministrarGrupoMaterial);
        bool canExport = _auth.HasPermission(PermisoSistema.ExportarExcel);

        _btnConsultar.Enabled = canRead;
        _btnExportar.Enabled = canExport;
        _btnExportarSeleccion.Enabled = canExport;

        _btnAddPadre.Enabled = canEdit;
        _btnAddSubgrupo.Enabled = canEdit;
        _btnAddProducto.Enabled = canEdit;
        _btnEditar.Enabled = canEdit;
        _btnMover.Enabled = canEdit;
        _btnSubir.Enabled = canEdit;
        _btnBajar.Enabled = canEdit;
        _btnEliminar.Enabled = canEdit;
    }

    public override Task ActivateAsync(CancellationToken ct = default) => RefreshTreeAndReportAsync(ct);

    private async Task RefreshTreeAndReportAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () =>
        {
            _nodes = await _app.GetMaterialGroupTreeAsync(ct);
            PopulateTree(_nodes, _selectedNodeId);
            await LoadReportCoreAsync(ct);
        }, NotifyStatus, "Cargando jerarquía de grupos de material...");
    }

    private async Task LoadReportOnlyAsync(CancellationToken ct = default)
    {
        await UiHelpers.RunSafeAsync(this, async () => await LoadReportCoreAsync(ct), NotifyStatus);
    }

    private async Task LoadReportCoreAsync(CancellationToken ct)
    {
        Planta? planta = UiHelpers.GetSelectedNullablePlanta(_cmbPlanta);
        DateOnly desde = DateOnly.FromDateTime(_dtpDesde.Value);
        DateOnly hasta = DateOnly.FromDateTime(_dtpHasta.Value);

        _lastReport = await _app.GetGrupoMaterialReportAsync(
            planta, desde, hasta, _chkSoloRecepciones.Checked, _selectedNodeId, _chkExcluirCeros.Checked, ct);

        _lblKilosVal.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalKilos:N0} Kg");
        _lblKilosSub.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.TotalViajes:N0} entregas acumuladas");

        _lblNodosVal.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.GruposPadreCount} grupos");
        _lblNodosSub.Text = string.Create(UiHelpers.EsCl, $"{_lastReport.NodosTotalesCount} nodos totales · {_lastReport.Filas.Count} visibles");

        _lblUnassignedVal.Text = _lastReport.ProductosSinClasificarCount.ToString(UiHelpers.EsCl);
        _lblUnassignedSub.Text = _lastReport.ProductosSinClasificarCount == 0
            ? "Catálogo 100% asignado"
            : "Códigos sin asignar en árbol";

        _dgvJerarquia.DataSource = null;
        _dgvJerarquia.DataSource = _lastReport.Filas;

        string chartTitle = _selectedNodeId.HasValue
            ? $"Top Productos en '{_nodes.FirstOrDefault(n => n.Id == _selectedNodeId)?.Nombre}'"
            : "Top Productos del Período";
        _chartProductos.SetBarData(chartTitle, _lastReport.DetalleProductosSeleccionados);

        NotifyStatus($"Grupos de Material: {_lastReport.Filas.Count} filas visibles · {_lastReport.TotalKilos.ToString("N0", UiHelpers.EsCl)} Kg.");
    }

    private void PopulateTree(IReadOnlyList<MaterialGroupNode> nodes, long? selectId)
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            Dictionary<long, List<MaterialGroupNode>> byParent = nodes
                .GroupBy(n => n.ParentId ?? 0L)
                .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Orden).ThenBy(n => n.Nombre, StringComparer.OrdinalIgnoreCase).ToList());

            TreeNode? toSelect = null;

            void AddChildren(TreeNodeCollection targetCol, long parentKey)
            {
                if (!byParent.TryGetValue(parentKey, out List<MaterialGroupNode>? kids))
                {
                    return;
                }

                foreach (MaterialGroupNode n in kids)
                {
                    string imgKey = n.TipoNodo == NodoGrupoTipo.CarpetaGrupo ? "folder" : "product";
                    TreeNode tn = new(n.Nombre)
                    {
                        Tag = n,
                        ImageKey = imgKey,
                        SelectedImageKey = imgKey
                    };
                    if (n.TipoNodo == NodoGrupoTipo.CarpetaGrupo)
                    {
                        tn.NodeFont = AppTheme.FontBodyBold;
                    }

                    targetCol.Add(tn);
                    if (selectId.HasValue && n.Id == selectId.Value)
                    {
                        toSelect = tn;
                    }

                    AddChildren(tn.Nodes, n.Id);
                }
            }

            AddChildren(_tree.Nodes, 0L);

            foreach (TreeNode root in _tree.Nodes)
            {
                root.Expand();
            }

            if (toSelect is not null)
            {
                _tree.SelectedNode = toSelect;
                toSelect.EnsureVisible();
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    private MaterialGroupNode? GetSelectedNode() => _tree.SelectedNode?.Tag as MaterialGroupNode;

    private async Task AddNodeAsync(NodoGrupoTipo tipo, bool atRoot)
    {
        MaterialGroupNode? sel = GetSelectedNode();
        long? parentId = null;
        string parentLabel = "Raíz Principal";

        if (!atRoot)
        {
            if (sel is null)
            {
                MessageBox.Show(this, "Seleccione primero un Grupo Padre o Subgrupo en el árbol.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MaterialGroupNode? folder = sel.TipoNodo == NodoGrupoTipo.CarpetaGrupo
                ? sel
                : _nodes.FirstOrDefault(n => n.Id == sel.ParentId);

            parentId = folder?.Id;
            parentLabel = folder?.Nombre ?? "Raíz Principal";
        }

        IReadOnlyList<Producto> prods = tipo == NodoGrupoTipo.ProductoAsignado
            ? await _app.GetUnassignedProductsAsync()
            : [];

        if (tipo == NodoGrupoTipo.ProductoAsignado && prods.Count == 0)
        {
            prods = await _app.GetProductosAsync();
        }

        using MaterialNodeEditorDialog dlg = new(tipo, parentId, parentLabel, prods);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultNode is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            MaterialGroupNode saved = await _app.SaveMaterialNodeAsync(dlg.ResultNode);
            _selectedNodeId = saved.Id;
            await RefreshTreeAndReportAsync();
        }, NotifyStatus, "Guardando nuevo nodo...");
    }

    private async Task EditSelectedNodeAsync()
    {
        MaterialGroupNode? sel = GetSelectedNode();
        if (sel is null)
        {
            MessageBox.Show(this, "Seleccione un nodo en el árbol para editar.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string parentName = sel.ParentId.HasValue
            ? (_nodes.FirstOrDefault(n => n.Id == sel.ParentId)?.Nombre ?? "Grupo Padre")
            : "Raíz Principal";
        IReadOnlyList<Producto> prods = sel.TipoNodo == NodoGrupoTipo.ProductoAsignado ? await _app.GetProductosAsync() : [];

        using MaterialNodeEditorDialog dlg = new(sel.TipoNodo, sel.ParentId, parentName, prods, sel);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ResultNode is null)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.SaveMaterialNodeAsync(dlg.ResultNode);
            _selectedNodeId = sel.Id;
            await RefreshTreeAndReportAsync();
        }, NotifyStatus, "Actualizando nodo...");
    }

    private async Task MoveSelectedNodeAsync()
    {
        MaterialGroupNode? sel = GetSelectedNode();
        if (sel is null)
        {
            MessageBox.Show(this, "Seleccione un nodo en el árbol para moverlo a otro grupo.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using MoverNodoDialog dlg = new(sel, _nodes);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.MoveMaterialNodeAsync(sel.Id, dlg.SelectedParentId);
            _selectedNodeId = sel.Id;
            await RefreshTreeAndReportAsync();
        }, NotifyStatus, "Moviendo nodo...");
    }

    private async Task ReorderSelectedAsync(bool moveUp)
    {
        MaterialGroupNode? sel = GetSelectedNode();
        if (sel is null)
        {
            MessageBox.Show(this, "Seleccione un nodo para cambiar su posición.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.ReorderMaterialNodeAsync(sel.Id, moveUp);
            _selectedNodeId = sel.Id;
            await RefreshTreeAndReportAsync();
        }, NotifyStatus);
    }

    private async Task DeleteSelectedNodeAsync()
    {
        MaterialGroupNode? sel = GetSelectedNode();
        if (sel is null)
        {
            MessageBox.Show(this, "Seleccione el nodo que desea quitar del árbol.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool hasKids = _nodes.Any(n => n.ParentId == sel.Id);
        bool deleteSubtree = true;

        if (hasKids)
        {
            DialogResult res = MessageBox.Show(
                this,
                $"El grupo '{sel.Nombre}' contiene nodos hijos.\n\n• SÍ: Eliminar el grupo junto con TODOS sus hijos.\n• NO: Eliminar solo el grupo y subir sus hijos al nivel superior.\n• CANCELAR: Abortar.",
                "Quitar Grupo Padre",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Cancel)
            {
                return;
            }

            deleteSubtree = res == DialogResult.Yes;
        }
        else
        {
            DialogResult res = MessageBox.Show(
                this,
                $"¿Confirma quitar el nodo '{sel.Nombre}' del árbol?",
                "Confirmar Eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res != DialogResult.Yes)
            {
                return;
            }
        }

        await UiHelpers.RunSafeAsync(this, async () =>
        {
            await _app.DeleteMaterialNodeAsync(sel.Id, deleteSubtree);
            _selectedNodeId = null;
            await RefreshTreeAndReportAsync();
        }, NotifyStatus, "Eliminando nodo...");
    }

    private void ExportarDirecto()
    {
        if (_lastReport is null || _lastReport.Filas.Count == 0)
        {
            MessageBox.Show(this, "Calcule primero el informe de grupos antes de exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"GruposMaterial_Cambiaso_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string saved = _app.ExcelExporter.ExportGrupoMaterialJerarquico(
                path,
                _lastReport.PeriodoLabel,
                [.. _lastReport.Filas.Select(f => (f.Nivel, f.Nombre, f.TipoNodo == NodoGrupoTipo.CarpetaGrupo ? "Grupo" : "Producto", f.CodigoProducto, f.Viajes, f.TotalKilos, f.Porcentaje))],
                _lastReport.TotalKilos);

            NotifyStatus($"Jerarquía exportada: {saved}");
            MessageBox.Show(this, $"Excel jerárquico generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportarPersonalizado()
    {
        if (_lastReport is null || _lastReport.Filas.Count == 0)
        {
            MessageBox.Show(this, "No hay datos en el informe actual para exportar.", "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<MaterialTreeFlatRowDto> gridSel = [.. _dgvJerarquia.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as MaterialTreeFlatRowDto).OfType<MaterialTreeFlatRowDto>()];
        List<MaterialTreeFlatRowDto> branchSel = [];
        MaterialGroupNode? treeSel = GetSelectedNode();
        if (treeSel is not null)
        {
            HashSet<long> subtreeIds = CollectSubtreeNodeIds(treeSel.Id);
            branchSel = [.. _lastReport.Filas.Where(f => subtreeIds.Contains(f.NodeId))];
        }

        List<MaterialTreeFlatRowDto> effectiveSelection = gridSel.Count > 0 ? gridSel : branchSel;
        string selDesc = gridSel.Count > 0
            ? $"{gridSel.Count} fila(s) de la tabla"
            : (treeSel is not null ? $"Rama '{treeSel.Nombre}' ({branchSel.Count} nodos)" : string.Empty);

        string[] sections =
        [
            "Resumen KPIs del Árbol",
            "Consolidado Grupos Padre",
            "Jerarquía Padre / Hijo",
            "Detalle Productos del Nodo"
        ];

        using ExportacionPersonalizadaDialog dlg = new(
            "Cambiaso · Informe Personalizado por Grupos de Material",
            effectiveSelection.Count,
            sections,
            _chkExcluirCeros.Checked,
            string.IsNullOrWhiteSpace(selDesc) ? null : selDesc);

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string? path = UiHelpers.PromptSaveExcelPath(this, $"GruposMaterial_Personalizado_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            bool onlySel = dlg.SoloSeleccionActual;
            bool exZero = dlg.ExcluirCeros;
            IReadOnlyList<string> inc = dlg.SeccionesSeleccionadas;

            IEnumerable<MaterialTreeFlatRowDto> targetRows = onlySel && effectiveSelection.Count > 0
                ? effectiveSelection
                : _lastReport.Filas;
            if (exZero) targetRows = targetRows.Where(r => r.TotalKilos > 0);
            List<MaterialTreeFlatRowDto> rowsList = [.. targetRows];

            List<(string Indicador, string Valor, string Detalle)> kpis = [];
            if (inc.Contains("Resumen KPIs del Árbol"))
            {
                kpis.Add(("Kilos Exportados", $"{rowsList.Where(r => r.Nivel == 0 || onlySel).Sum(r => r.TotalKilos):N0} Kg", _lastReport.PeriodoLabel));
                kpis.Add(("Grupos Padre / Nodos Totales", $"{_lastReport.GruposPadreCount} / {_lastReport.NodosTotalesCount}", $"{rowsList.Count} filas exportadas"));
                kpis.Add(("Productos sin Asignar", _lastReport.ProductosSinClasificarCount.ToString(UiHelpers.EsCl), "Catálogo operativo"));
            }

            List<ExportTableSection> exportSections = [];
            if (inc.Contains("Consolidado Grupos Padre"))
            {
                IEnumerable<GrupoKilosItem> roots = exZero ? _lastReport.ConsolidadoPadre.Where(c => c.Kilos > 0) : _lastReport.ConsolidadoPadre;
                exportSections.Add(new ExportTableSection(
                    "Consolidado Padre",
                    "Resumen por Grupo Padre",
                    ["Grupo Padre", "Viajes", "Kilos Netos", "% Participación"],
                    [.. roots.Select(c => new object?[] { c.Grupo, c.Viajes, c.Kilos, c.Porcentaje })]));
            }

            if (inc.Contains("Jerarquía Padre / Hijo"))
            {
                exportSections.Add(new ExportTableSection(
                    "Jerarquía",
                    onlySel ? "Nodos / Subrama Seleccionada" : "Jerarquía Completa",
                    ["Grupo / Nodo Material", "Tipo", "Código Prod.", "Viajes", "Kilos Netos", "% Total", "Prom. Kg/Viaje"],
                    [.. rowsList.Select(f => new object?[]
                    {
                        f.NombreConSangria,
                        f.TipoNodo == NodoGrupoTipo.CarpetaGrupo ? "Grupo" : "Producto",
                        f.CodigoProducto,
                        f.Viajes,
                        f.TotalKilos,
                        f.Porcentaje,
                        f.PromedioKgViaje
                    })]));
            }

            if (inc.Contains("Detalle Productos del Nodo"))
            {
                IEnumerable<BarChartItemDto> prods = exZero
                    ? _lastReport.DetalleProductosSeleccionados.Where(p => p.Value > 0)
                    : _lastReport.DetalleProductosSeleccionados;
                exportSections.Add(new ExportTableSection(
                    "Productos del Nodo",
                    treeSel is not null ? $"Productos en {treeSel.Nombre}" : "Top Productos",
                    ["Producto", "Kilos Netos"],
                    [.. prods.Select(p => new object?[] { p.Label, p.Value })]));
            }

            string saved = _app.ExcelExporter.ExportReportePersonalizado(
                path,
                new ReportePersonalizadoPayload(dlg.TituloPersonalizado, _lastReport.PeriodoLabel, kpis, exportSections));

            NotifyStatus($"Exportación personalizada generada: {saved}");
            MessageBox.Show(this, $"Excel de grupos generado correctamente:\n{saved}", "Exportación Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error al Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private HashSet<long> CollectSubtreeNodeIds(long rootId)
    {
        HashSet<long> ids = [];
        Dictionary<long, List<MaterialGroupNode>> byParent = _nodes
            .GroupBy(n => n.ParentId ?? 0L)
            .ToDictionary(g => g.Key, g => g.ToList());

        void Walk(long id)
        {
            if (!ids.Add(id))
            {
                return;
            }

            if (byParent.TryGetValue(id, out List<MaterialGroupNode>? kids))
            {
                foreach (MaterialGroupNode k in kids)
                {
                    Walk(k.Id);
                }
            }
        }

        Walk(rootId);
        return ids;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _treeIcons.Dispose();
        }

        base.Dispose(disposing);
    }
}
