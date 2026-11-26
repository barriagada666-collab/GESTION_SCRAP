using GestionScrap.Domain.Enums;

namespace GestionScrap.Desktop.Controls;

/// <summary>
/// Clase base para las vistas modulares de pestaña con soporte unificado de ToolTip, permisos y notificaciones de estado.
/// </summary>
public abstract class ScrapTabControlBase : UserControl
{
    protected ToolTip Tip { get; } = new()
    {
        AutoPopDelay = 6000,
        InitialDelay = 350,
        ReshowDelay = 200
    };

    public event Action<string>? StatusChanged;

    public virtual Task ActivateAsync(CancellationToken ct = default) => Task.CompletedTask;

    public virtual void RefreshPermissions()
    {
    }

    protected void NotifyStatus(string message) => StatusChanged?.Invoke(message);

    protected static Button? FindButtonByTextFragment(Control root, string textFragment)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Button btn && btn.Text.Contains(textFragment, StringComparison.OrdinalIgnoreCase))
            {
                return btn;
            }

            if (c.HasChildren)
            {
                Button? nested = FindButtonByTextFragment(c, textFragment);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Tip.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed record PlantaComboItem(Planta Planta, string Display)
{
    public override string ToString() => Display;
}
