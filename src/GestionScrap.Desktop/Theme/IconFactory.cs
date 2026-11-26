using System.Collections.Concurrent;
using System.Drawing.Drawing2D;

namespace GestionScrap.Desktop.Theme;

public enum IconKind
{
    Dashboard,
    Layers,
    ShoppingCart,
    User,
    Store,
    Document,
    DollarSign,
    Plus,
    Database,
    Shield,
    Search,
    Excel,
    Save,
    Trash,
    Play,
    Refresh,
    Check,
    Close,
    Edit,
    Key,
    Backup,
    Import,
    Settings,
    Lock,
    Logout
}

public static class IconFactory
{
    private static readonly ConcurrentDictionary<(IconKind Kind, int Size, int Argb), Bitmap> Cache = new();

    public static Bitmap GetIcon(IconKind kind, Color color, int size = 16)
    {
        return Cache.GetOrAdd((kind, size, color.ToArgb()), key => Render(key.Kind, Color.FromArgb(key.Argb), key.Size));
    }

    private static PointF P(float x, float y) => new(x, y);

    private static Bitmap Render(IconKind kind, Color color, int size)
    {
        Bitmap bmp = new(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float u = size / 16f;
        using Pen pen = new(color, Math.Max(1.4f, 1.6f * u))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using SolidBrush brush = new(color);

        switch (kind)
        {
            case IconKind.Dashboard:
                g.DrawRectangle(pen, 2 * u, 2 * u, 5 * u, 5 * u);
                g.DrawRectangle(pen, 9 * u, 2 * u, 5 * u, 5 * u);
                g.DrawRectangle(pen, 2 * u, 9 * u, 5 * u, 5 * u);
                g.DrawRectangle(pen, 9 * u, 9 * u, 5 * u, 5 * u);
                break;

            case IconKind.Layers:
                g.DrawPolygon(pen, new[] { P(8 * u, 2 * u), P(14 * u, 5.5f * u), P(8 * u, 9 * u), P(2 * u, 5.5f * u) });
                g.DrawLines(pen, new[] { P(2 * u, 9 * u), P(8 * u, 12.5f * u), P(14 * u, 9 * u) });
                g.DrawLines(pen, new[] { P(2 * u, 11.5f * u), P(8 * u, 14.8f * u), P(14 * u, 11.5f * u) });
                break;

            case IconKind.ShoppingCart:
                g.DrawLines(pen, new[] { P(1.5f * u, 3 * u), P(4 * u, 3 * u), P(5.5f * u, 10.5f * u), P(13 * u, 10.5f * u), P(14.5f * u, 5 * u), P(4.5f * u, 5 * u) });
                g.FillEllipse(brush, 5.2f * u, 12 * u, 2.2f * u, 2.2f * u);
                g.FillEllipse(brush, 11 * u, 12 * u, 2.2f * u, 2.2f * u);
                break;

            case IconKind.User:
                g.DrawEllipse(pen, 5 * u, 2 * u, 6 * u, 6 * u);
                g.DrawArc(pen, 2.5f * u, 9 * u, 11 * u, 8 * u, 190, 160);
                break;

            case IconKind.Store:
                g.DrawRectangle(pen, 2.5f * u, 6.5f * u, 11 * u, 7.5f * u);
                g.DrawPolygon(pen, new[] { P(1.5f * u, 6.5f * u), P(3 * u, 2.5f * u), P(13 * u, 2.5f * u), P(14.5f * u, 6.5f * u) });
                g.DrawRectangle(pen, 6.5f * u, 10 * u, 3 * u, 4 * u);
                break;

            case IconKind.Document:
                g.DrawPolygon(pen, new[] { P(3 * u, 1.8f * u), P(10 * u, 1.8f * u), P(13 * u, 4.8f * u), P(13 * u, 14.2f * u), P(3 * u, 14.2f * u) });
                g.DrawLine(pen, 5.5f * u, 7.5f * u, 10.5f * u, 7.5f * u);
                g.DrawLine(pen, 5.5f * u, 10.5f * u, 10.5f * u, 10.5f * u);
                break;

            case IconKind.DollarSign:
                g.DrawLine(pen, 8 * u, 1.8f * u, 8 * u, 14.2f * u);
                g.DrawArc(pen, 4.5f * u, 3.5f * u, 7 * u, 4.5f * u, 30, 240);
                g.DrawArc(pen, 4.5f * u, 8 * u, 7 * u, 4.5f * u, 210, 240);
                break;

            case IconKind.Plus:
                g.DrawEllipse(pen, 2 * u, 2 * u, 12 * u, 12 * u);
                g.DrawLine(pen, 8 * u, 4.8f * u, 8 * u, 11.2f * u);
                g.DrawLine(pen, 4.8f * u, 8 * u, 11.2f * u, 8 * u);
                break;

            case IconKind.Database:
                g.DrawEllipse(pen, 2.5f * u, 2 * u, 11 * u, 4 * u);
                g.DrawLine(pen, 2.5f * u, 4 * u, 2.5f * u, 12 * u);
                g.DrawLine(pen, 13.5f * u, 4 * u, 13.5f * u, 12 * u);
                g.DrawArc(pen, 2.5f * u, 6 * u, 11 * u, 4 * u, 0, 180);
                g.DrawArc(pen, 2.5f * u, 10 * u, 11 * u, 4 * u, 0, 180);
                break;

            case IconKind.Shield:
                g.DrawPolygon(pen, new[] { P(8 * u, 1.8f * u), P(13.5f * u, 4 * u), P(13 * u, 9.5f * u), P(8 * u, 14.2f * u), P(3 * u, 9.5f * u), P(2.5f * u, 4 * u) });
                g.DrawLines(pen, new[] { P(5.5f * u, 8.2f * u), P(7.3f * u, 10 * u), P(10.8f * u, 6.2f * u) });
                break;

            case IconKind.Search:
                g.DrawEllipse(pen, 2.2f * u, 2.2f * u, 8.5f * u, 8.5f * u);
                g.DrawLine(pen, 9.5f * u, 9.5f * u, 13.8f * u, 13.8f * u);
                break;

            case IconKind.Excel:
                g.DrawRectangle(pen, 2.5f * u, 2 * u, 11 * u, 12 * u);
                g.DrawLine(pen, 5.5f * u, 5 * u, 10.5f * u, 11 * u);
                g.DrawLine(pen, 10.5f * u, 5 * u, 5.5f * u, 11 * u);
                break;

            case IconKind.Save:
                g.DrawPolygon(pen, new[] { P(2.2f * u, 2.2f * u), P(11.5f * u, 2.2f * u), P(13.8f * u, 4.5f * u), P(13.8f * u, 13.8f * u), P(2.2f * u, 13.8f * u) });
                g.DrawRectangle(pen, 4.8f * u, 2.2f * u, 5.2f * u, 4 * u);
                g.DrawRectangle(pen, 4.5f * u, 9 * u, 7 * u, 4.8f * u);
                break;

            case IconKind.Trash:
                g.DrawLine(pen, 2.5f * u, 4.2f * u, 13.5f * u, 4.2f * u);
                g.DrawRectangle(pen, 6 * u, 2 * u, 4 * u, 2.2f * u);
                g.DrawPolygon(pen, new[] { P(3.8f * u, 4.2f * u), P(12.2f * u, 4.2f * u), P(11.4f * u, 14 * u), P(4.6f * u, 14 * u) });
                g.DrawLine(pen, 6.8f * u, 6.5f * u, 6.8f * u, 11.8f * u);
                g.DrawLine(pen, 9.2f * u, 6.5f * u, 9.2f * u, 11.8f * u);
                break;

            case IconKind.Play:
                g.FillPolygon(brush, new[] { P(4 * u, 2.5f * u), P(13 * u, 8 * u), P(4 * u, 13.5f * u) });
                break;

            case IconKind.Refresh:
                g.DrawArc(pen, 2.5f * u, 2.5f * u, 11 * u, 11 * u, 30, 290);
                g.FillPolygon(brush, new[] { P(12.5f * u, 2.5f * u), P(14 * u, 6 * u), P(10.5f * u, 5.5f * u) });
                break;

            case IconKind.Check:
                g.DrawLines(pen, new[] { P(2.5f * u, 8.5f * u), P(6.2f * u, 12.2f * u), P(13.5f * u, 4 * u) });
                break;

            case IconKind.Close:
                g.DrawLine(pen, 3.2f * u, 3.2f * u, 12.8f * u, 12.8f * u);
                g.DrawLine(pen, 12.8f * u, 3.2f * u, 3.2f * u, 12.8f * u);
                break;

            case IconKind.Edit:
                g.DrawPolygon(pen, new[] { P(2.5f * u, 13.5f * u), P(3.5f * u, 10 * u), P(11 * u, 2.5f * u), P(13.5f * u, 5 * u), P(6 * u, 12.5f * u) });
                break;

            case IconKind.Key:
                g.DrawEllipse(pen, 2 * u, 5 * u, 5.5f * u, 5.5f * u);
                g.DrawLine(pen, 7.5f * u, 7.8f * u, 14 * u, 7.8f * u);
                g.DrawLine(pen, 11.5f * u, 7.8f * u, 11.5f * u, 10.8f * u);
                g.DrawLine(pen, 13.5f * u, 7.8f * u, 13.5f * u, 10.8f * u);
                break;

            case IconKind.Backup:
            case IconKind.Import:
                g.DrawLine(pen, 8 * u, 2 * u, 8 * u, 10.5f * u);
                g.DrawLines(pen, new[] { P(4.8f * u, 7.5f * u), P(8 * u, 10.8f * u), P(11.2f * u, 7.5f * u) });
                g.DrawLines(pen, new[] { P(2.5f * u, 11 * u), P(2.5f * u, 13.8f * u), P(13.5f * u, 13.8f * u), P(13.5f * u, 11 * u) });
                break;

            case IconKind.Settings:
                g.DrawEllipse(pen, 5.5f * u, 5.5f * u, 5 * u, 5 * u);
                g.DrawEllipse(pen, 2.5f * u, 2.5f * u, 11 * u, 11 * u);
                break;

            case IconKind.Lock:
                g.DrawRectangle(pen, 3 * u, 7 * u, 10 * u, 7 * u);
                g.DrawArc(pen, 5 * u, 2.5f * u, 6 * u, 6 * u, 180, 180);
                break;

            case IconKind.Logout:
                g.DrawLines(pen, new[] { P(7 * u, 2.5f * u), P(2.5f * u, 2.5f * u), P(2.5f * u, 13.5f * u), P(7 * u, 13.5f * u) });
                g.DrawLine(pen, 6 * u, 8 * u, 13.5f * u, 8 * u);
                g.DrawLines(pen, new[] { P(10.5f * u, 5.2f * u), P(13.5f * u, 8 * u), P(10.5f * u, 10.8f * u) });
                break;
        }

        return bmp;
    }
}
