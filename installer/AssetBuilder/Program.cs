using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "assets");
string versionText = args.Length > 1 ? args[1] : "v4.0";

Directory.CreateDirectory(outDir);

int[] sizes = [16, 32, 48, 64, 128, 256];
List<byte[]> pngFrames = [];
foreach (int s in sizes)
{
    using Bitmap bmp = RenderAppBadge(s);
    using MemoryStream ms = new();
    bmp.Save(ms, ImageFormat.Png);
    byte[] bytes = ms.ToArray();
    pngFrames.Add(bytes);
    if (s == 256)
    {
        File.WriteAllBytes(Path.Combine(outDir, "app_logo.png"), bytes);
    }
}
WriteIco(Path.Combine(outDir, "app_icon.ico"), sizes, pngFrames);

SaveBmp24(RenderWizardBanner(164, 314, versionText), Path.Combine(outDir, "wizard_banner.bmp"));
SaveBmp24(RenderWizardBanner(328, 628, versionText), Path.Combine(outDir, "wizard_banner_2x.bmp"));
SaveBmp24(RenderWizardSmall(55, 58), Path.Combine(outDir, "wizard_small.bmp"));
SaveBmp24(RenderWizardSmall(110, 116), Path.Combine(outDir, "wizard_small_2x.bmp"));

Console.WriteLine($"Assets generados correctamente en: {outDir}");
return 0;

static Bitmap RenderAppBadge(int size)
{
    Bitmap bmp = new(size, size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    float pad = size * 0.04f;
    float r = size * 0.22f;
    RectangleF rect = new(pad, pad, size - pad * 2, size - pad * 2);

    using (GraphicsPath path = RoundedRect(rect, r))
    using (LinearGradientBrush bg = new(rect, Color.FromArgb(15, 23, 42), Color.FromArgb(14, 116, 144), 135f))
    using (Pen borderPen = new(Color.FromArgb(56, 189, 248), Math.Max(1f, size * 0.03f)))
    {
        g.FillPath(bg, path);
        g.DrawPath(borderPen, path);
    }

    float cx = size * 0.5f;
    float cy = size * 0.46f;
    float stroke = Math.Max(1.5f, size * 0.06f);

    using Pen cyanPen = new(Color.FromArgb(34, 211, 238), stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    using SolidBrush amberBrush = new(Color.FromArgb(245, 158, 11));
    using SolidBrush whiteBrush = new(Color.FromArgb(241, 245, 249));
    using SolidBrush greenBrush = new(Color.FromArgb(16, 185, 129));
    using Pen darkPen = new(Color.FromArgb(15, 23, 42), Math.Max(1.2f, size * 0.045f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

    float baseW = size * 0.62f;
    float baseY = size * 0.72f;
    g.DrawLine(cyanPen, cx - baseW / 2f, baseY, cx + baseW / 2f, baseY);
    g.DrawLine(cyanPen, cx, cy - size * 0.14f, cx, baseY);

    float boxW = size * 0.44f;
    float boxH = size * 0.24f;
    RectangleF boxRect = new(cx - boxW / 2f, baseY - boxH - stroke * 0.6f, boxW, boxH);
    using (GraphicsPath boxPath = RoundedRect(boxRect, size * 0.06f))
    {
        g.FillPath(amberBrush, boxPath);
    }

    float dialR = size * 0.14f;
    RectangleF dialRect = new(cx - dialR, size * 0.16f, dialR * 2f, dialR * 2f);
    g.FillEllipse(whiteBrush, dialRect);
    g.DrawLine(darkPen, cx, size * 0.16f + dialR, cx + dialR * 0.52f, size * 0.16f + dialR * 0.48f);

    float barW = size * 0.34f;
    RectangleF barRect = new(cx - barW / 2f, size * 0.81f, barW, Math.Max(1.5f, size * 0.055f));
    g.FillRectangle(greenBrush, barRect);

    return bmp;
}

static Bitmap RenderWizardBanner(int width, int height, string ver)
{
    float scale = width / 164f;
    Bitmap bmp = new(width, height, PixelFormat.Format24bppRgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

    Rectangle full = new(0, 0, width, height);
    using (LinearGradientBrush bg = new(full, Color.FromArgb(12, 20, 38), Color.FromArgb(17, 46, 72), 90f))
    {
        g.FillRectangle(bg, full);
    }

    using (Pen gridPen = new(Color.FromArgb(26, 56, 86), 1f * scale))
    {
        for (int y = 0; y < height; y += (int)(22 * scale))
        {
            g.DrawLine(gridPen, 0, y, width, y);
        }
    }

    using (SolidBrush topBrush = new(Color.FromArgb(6, 182, 212)))
    {
        g.FillRectangle(topBrush, 0, 0, width, 5f * scale);
    }

    int badgeSize = (int)(86 * scale);
    using (Bitmap badge = RenderAppBadge(badgeSize))
    {
        g.DrawImage(badge, (width - badgeSize) / 2, (int)(50 * scale));
    }

    using (Pen divPen = new(Color.FromArgb(245, 158, 11), 1.5f * scale))
    {
        g.DrawLine(divPen, 20 * scale, 162 * scale, width - 20 * scale, 162 * scale);
    }

    using Font titleFont = new("Segoe UI", 12.5f * scale, FontStyle.Bold, GraphicsUnit.Point);
    using Font subFont = new("Segoe UI", 8.5f * scale, FontStyle.Regular, GraphicsUnit.Point);
    using Font badgeFont = new("Segoe UI", 8.5f * scale, FontStyle.Bold, GraphicsUnit.Point);
    using StringFormat sf = new() { Alignment = StringAlignment.Center };
    using SolidBrush white = new(Color.White);
    using SolidBrush cyan = new(Color.FromArgb(103, 232, 249));
    using SolidBrush muted = new(Color.FromArgb(148, 163, 184));
    using SolidBrush pillBrush = new(Color.FromArgb(15, 118, 110));
    using Pen pillPen = new(Color.FromArgb(45, 212, 191), 1f * scale);

    g.DrawString("GESTIÓN DE", titleFont, white, new RectangleF(8 * scale, 176 * scale, width - 16 * scale, 24 * scale), sf);
    g.DrawString("SCRAP", titleFont, cyan, new RectangleF(8 * scale, 198 * scale, width - 16 * scale, 24 * scale), sf);
    g.DrawString("Pesaje e Informes", subFont, muted, new RectangleF(8 * scale, 228 * scale, width - 16 * scale, 20 * scale), sf);

    RectangleF pillRect = new(20 * scale, 260 * scale, width - 40 * scale, 26 * scale);
    using (GraphicsPath pillPath = RoundedRect(pillRect, 10 * scale))
    {
        g.FillPath(pillBrush, pillPath);
        g.DrawPath(pillPen, pillPath);
    }
    g.DrawString($"{ver}  \u00B7  .NET 10", badgeFont, white, new RectangleF(pillRect.X, pillRect.Y + 4.5f * scale, pillRect.Width, pillRect.Height), sf);

    return bmp;
}

static Bitmap RenderWizardSmall(int width, int height)
{
    Bitmap bmp = new(width, height, PixelFormat.Format24bppRgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.FromArgb(245, 247, 250));
    int s = Math.Min(width, height) - 4;
    using Bitmap badge = RenderAppBadge(s);
    g.DrawImage(badge, (width - s) / 2, (height - s) / 2);
    return bmp;
}

static void SaveBmp24(Bitmap bmp, string path)
{
    using (bmp)
    {
        bmp.Save(path, ImageFormat.Bmp);
    }
}

static void WriteIco(string path, int[] icoSizes, List<byte[]> frames)
{
    using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
    using BinaryWriter bw = new(fs);
    bw.Write((short)0);
    bw.Write((short)1);
    bw.Write((short)icoSizes.Length);
    int offset = 6 + 16 * icoSizes.Length;
    for (int i = 0; i < icoSizes.Length; i++)
    {
        byte dim = icoSizes[i] >= 256 ? (byte)0 : (byte)icoSizes[i];
        bw.Write(dim);
        bw.Write(dim);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(frames[i].Length);
        bw.Write(offset);
        offset += frames[i].Length;
    }
    foreach (byte[] frame in frames)
    {
        bw.Write(frame);
    }
}

static GraphicsPath RoundedRect(RectangleF bounds, float radius)
{
    float d = radius * 2f;
    GraphicsPath path = new();
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}
