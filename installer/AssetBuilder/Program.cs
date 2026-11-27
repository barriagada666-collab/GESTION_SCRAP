using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "assets");
string versionText = args.Length > 1 ? args[1] : "v4.1.1";

Directory.CreateDirectory(outDir);

string sourceLogoPath = Path.Combine(outDir, "cambiaso_brand_source.png");
if (!File.Exists(sourceLogoPath))
{
    Console.Error.WriteLine($"No se encontro logo fuente en: {sourceLogoPath}");
    return 1;
}

using Bitmap fullSource = (Bitmap)Image.FromFile(sourceLogoPath);

// Recorta el monograma circular superior del logo fuente (coordenadas medidas: X=409..617, Y=15..224 en 1024x538)
int embX = (int)(fullSource.Width * 0.39);
int embY = (int)(fullSource.Height * 0.015);
int embW = (int)(fullSource.Width * 0.22);
int embH = embW;
using Bitmap croppedEmblem = new(256, 256, PixelFormat.Format32bppArgb);
using (Graphics g = Graphics.FromImage(croppedEmblem))
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.Transparent);
    g.DrawImage(fullSource, new Rectangle(8, 8, 240, 240), new Rectangle(embX, embY, embW, embH), GraphicsUnit.Pixel);
}
croppedEmblem.Save(Path.Combine(outDir, "cambiaso_emblem.png"), ImageFormat.Png);

// Genera versión wordmark optimizada para UI (512x269)
using Bitmap wordmark = new(512, 269, PixelFormat.Format32bppArgb);
using (Graphics g = Graphics.FromImage(wordmark))
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.Transparent);
    g.DrawImage(fullSource, 0, 0, 512, 269);
}
wordmark.Save(Path.Combine(outDir, "cambiaso_wordmark.png"), ImageFormat.Png);

int[] sizes = [16, 32, 48, 64, 128, 256];
List<byte[]> pngFrames = [];
foreach (int s in sizes)
{
    using Bitmap bmp = RenderCorporateBadge(s, croppedEmblem);
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

SaveBmp24(RenderWizardBanner(164, 314, versionText, croppedEmblem), Path.Combine(outDir, "wizard_banner.bmp"));
SaveBmp24(RenderWizardBanner(328, 628, versionText, croppedEmblem), Path.Combine(outDir, "wizard_banner_2x.bmp"));
SaveBmp24(RenderWizardSmall(55, 58, croppedEmblem), Path.Combine(outDir, "wizard_small.bmp"));
SaveBmp24(RenderWizardSmall(110, 116, croppedEmblem), Path.Combine(outDir, "wizard_small_2x.bmp"));

Console.WriteLine($"Assets corporativos Cambiaso {versionText} generados correctamente en: {outDir}");
return 0;

static Bitmap RenderCorporateBadge(int size, Bitmap emblem)
{
    Bitmap bmp = new(size, size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.Transparent);

    float pad = size * 0.04f;
    float r = size * 0.22f;
    RectangleF rect = new(pad, pad, size - pad * 2, size - pad * 2);

    // Paleta corporativa petróleo-azulado (#0B3C5D -> #0E2238) con aro dorado Cambiaso (#C9A46E)
    using (GraphicsPath path = RoundedRect(rect, r))
    using (LinearGradientBrush bg = new(rect, Color.FromArgb(14, 96, 138), Color.FromArgb(11, 37, 58), 135f))
    using (Pen goldPen = new(Color.FromArgb(201, 164, 110), Math.Max(1.2f, size * 0.045f)))
    {
        g.FillPath(bg, path);
        g.DrawPath(goldPen, path);
    }

    float ringPad = size * 0.14f;
    RectangleF ringRect = new(ringPad, ringPad, size - ringPad * 2, size - ringPad * 2);
    using (Pen cyanRing = new(Color.FromArgb(95, 56, 189, 248), Math.Max(1f, size * 0.025f)))
    {
        g.DrawEllipse(cyanRing, ringRect);
    }

    float embPad = size * 0.17f;
    g.DrawImage(emblem, new RectangleF(embPad, embPad, size - embPad * 2, size - embPad * 2));
    return bmp;
}

static Bitmap RenderWizardBanner(int width, int height, string ver, Bitmap emblem)
{
    float scale = width / 164f;
    Bitmap bmp = new(width, height, PixelFormat.Format24bppRgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

    Rectangle full = new(0, 0, width, height);
    using (LinearGradientBrush bg = new(full, Color.FromArgb(11, 60, 93), Color.FromArgb(10, 29, 48), 95f))
    {
        g.FillRectangle(bg, full);
    }

    // Líneas decorativas estilo industrial petróleo + dorado
    using (Pen gridPen = new(Color.FromArgb(26, 201, 164, 110), 1f * scale))
    {
        for (int i = -60; i < height + 80; i += 24)
        {
            g.DrawLine(gridPen, 0, i * scale, width, (i + 52) * scale);
        }
    }

    using (SolidBrush goldAccent = new(Color.FromArgb(201, 164, 110)))
    {
        g.FillRectangle(goldAccent, 0, 0, 5f * scale, height);
    }

    float badgeSize = 92f * scale;
    float bx = (width - badgeSize) / 2f;
    float by = 38f * scale;
    using (Bitmap badge = RenderCorporateBadge((int)badgeSize, emblem))
    {
        g.DrawImage(badge, bx, by, badgeSize, badgeSize);
    }

    using Font titleFont = new("Segoe UI", 11f * scale, FontStyle.Bold, GraphicsUnit.Point);
    using Font subFont = new("Segoe UI", 8.2f * scale, FontStyle.Regular, GraphicsUnit.Point);
    using Font brandFont = new("Segoe UI", 7.8f * scale, FontStyle.Bold, GraphicsUnit.Point);
    using Font badgeFont = new("Segoe UI", 8.5f * scale, FontStyle.Bold, GraphicsUnit.Point);
    using SolidBrush whiteBrush = new(Color.White);
    using SolidBrush goldBrush = new(Color.FromArgb(201, 164, 110));
    using SolidBrush mutedBrush = new(Color.FromArgb(186, 210, 228));
    using StringFormat center = new() { Alignment = StringAlignment.Center };

    g.DrawString("CAMBIASO HNOS.", brandFont, goldBrush, width / 2f, 142f * scale, center);
    g.DrawString("Gestión de\nScrap y Pesajes", titleFont, whiteBrush, width / 2f, 160f * scale, center);
    g.DrawString("Grupos · Proveedores\nFirebird · SQLite", subFont, mutedBrush, width / 2f, 208f * scale, center);

    RectangleF pill = new(24f * scale, 260f * scale, width - 48f * scale, 24f * scale);
    using (GraphicsPath pillPath = RoundedRect(pill, 11f * scale))
    using (SolidBrush pillFill = new(Color.FromArgb(14, 116, 144)))
    using (Pen pillBorder = new(Color.FromArgb(201, 164, 110), 1f * scale))
    {
        g.FillPath(pillFill, pillPath);
        g.DrawPath(pillBorder, pillPath);
    }

    g.DrawString($"{ver} · .NET 10", badgeFont, whiteBrush, width / 2f, 264f * scale, center);
    return bmp;
}

static Bitmap RenderWizardSmall(int width, int height, Bitmap emblem)
{
    Bitmap bmp = new(width, height, PixelFormat.Format24bppRgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

    Rectangle full = new(0, 0, width, height);
    using (LinearGradientBrush bg = new(full, Color.FromArgb(14, 72, 108), Color.FromArgb(11, 37, 58), 135f))
    {
        g.FillRectangle(bg, full);
    }

    int badgeSize = (int)(Math.Min(width, height) * 0.88f);
    using Bitmap badge = RenderCorporateBadge(badgeSize, emblem);
    g.DrawImage(badge, (width - badgeSize) / 2f, (height - badgeSize) / 2f, badgeSize, badgeSize);
    return bmp;
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

static void SaveBmp24(Bitmap bmp, string filePath)
{
    using (bmp)
    {
        bmp.Save(filePath, ImageFormat.Bmp);
    }
}

static void WriteIco(string filePath, int[] sizes, List<byte[]> pngFrames)
{
    using FileStream fs = File.Create(filePath);
    using BinaryWriter bw = new(fs);
    bw.Write((short)0);
    bw.Write((short)1);
    bw.Write((short)sizes.Length);

    int offset = 6 + (sizes.Length * 16);
    for (int i = 0; i < sizes.Length; i++)
    {
        byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
        bw.Write(dim);
        bw.Write(dim);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(pngFrames[i].Length);
        bw.Write(offset);
        offset += pngFrames[i].Length;
    }

    for (int i = 0; i < pngFrames.Count; i++)
    {
        bw.Write(pngFrames[i]);
    }
}
