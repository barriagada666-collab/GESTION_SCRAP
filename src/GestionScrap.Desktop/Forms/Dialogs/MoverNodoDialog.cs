using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Forms.Dialogs;

/// <summary>
/// Diálogo para mover un nodo (grupo, subgrupo o producto) hacia cualquier otro nodo padre o a raíz.
/// </summary>
public sealed class MoverNodoDialog : Form
{
    private readonly ToolTip _toolTip = new();
    private readonly TreeView _tree = new();
    public long? SelectedParentId { get; private set; }

    public MoverNodoDialog(MaterialGroupNode movingNode, IReadOnlyList<MaterialGroupNode> allNodes)
    {
        Text = $"Mover Nodo · {movingNode.Nombre}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 390);
        BackColor = AppTheme.CanvasLight;
        Icon = AppTheme.LoadAppIcon();

        Label lblInfo = new()
        {
            Text = "Seleccione el Grupo / Nodo Padre de destino:",
            Font = AppTheme.FontBodyBold,
            Location = new Point(16, 14),
            AutoSize = true
        };

        _tree.SetBounds(16, 42, 428, 282);
        _tree.Font = AppTheme.FontBody;
        _tree.HideSelection = false;
        _tree.ShowLines = true;
        _tree.ShowPlusMinus = true;

        TreeNode rootVirtual = new("Raíz Principal (Nivel Superior)") { Tag = (long?)null };
        _tree.Nodes.Add(rootVirtual);

        HashSet<long> blockedSubtree = CollectDescendantIds(allNodes, movingNode.Id);
        Dictionary<long, List<MaterialGroupNode>> byParent = allNodes
            .Where(n => n.TipoNodo == NodoGrupoTipo.CarpetaGrupo && !blockedSubtree.Contains(n.Id))
            .GroupBy(n => n.ParentId ?? 0L)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Orden).ThenBy(n => n.Nombre).ToList());

        void Populate(TreeNode uiParent, long parentKey)
        {
            if (!byParent.TryGetValue(parentKey, out List<MaterialGroupNode>? kids))
            {
                return;
            }

            foreach (MaterialGroupNode n in kids)
            {
                TreeNode tn = new(n.Nombre) { Tag = (long?)n.Id };
                uiParent.Nodes.Add(tn);
                Populate(tn, n.Id);
            }
        }

        Populate(rootVirtual, 0L);
        rootVirtual.ExpandAll();
        _tree.SelectedNode = rootVirtual;

        Button btnCancel = AppTheme.CreateButton("Cancelar", IconKind.Close, ButtonVariant.Secondary, (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, "Cancelar movimiento", _toolTip, 115, 34);
        btnCancel.Location = new Point(195, 340);

        Button btnConfirm = AppTheme.CreateButton("Mover Aquí", IconKind.Move, ButtonVariant.Primary, (_, _) =>
        {
            SelectedParentId = _tree.SelectedNode?.Tag as long?;
            DialogResult = DialogResult.OK;
            Close();
        }, "Reubicar el nodo dentro del destino seleccionado", _toolTip, 128, 34);
        btnConfirm.Location = new Point(316, 340);

        AcceptButton = btnConfirm;
        CancelButton = btnCancel;
        Controls.AddRange([lblInfo, _tree, btnCancel, btnConfirm]);
    }

    private static HashSet<long> CollectDescendantIds(IReadOnlyList<MaterialGroupNode> all, long rootId)
    {
        HashSet<long> set = [rootId];
        bool added = true;
        while (added)
        {
            added = false;
            foreach (MaterialGroupNode n in all)
            {
                if (n.ParentId.HasValue && set.Contains(n.ParentId.Value) && set.Add(n.Id))
                {
                    added = true;
                }
            }
        }

        return set;
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
