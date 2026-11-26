using System.Drawing.Drawing2D;
using GestionScrap.Desktop.Theme;

namespace GestionScrap.Desktop.Forms;

/// <summary>
/// Pantalla corporativa de arranque sin dependencias externas con identidad Cambiaso (1875).
/// </summary>
public sealed class SplashForm : Form
{
    private readonly Label _lblStatus = new();
    private readonly Panel _progressFill = new();
    private int _progressPercent = 15;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(640, 340);
        BackColor = Color.White;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Icon = AppTheme.LoadAppIcon();

        Panel leftPane = new()
        {
            Dock = DockStyle.Left,
            Width = 264,
            Padding = new Padding(16)
        };
        leftPane.Paint += (_, e) =>
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using LinearGradientBrush lgb = new(leftPane.ClientRectangle, AppTheme.AccentBlue, AppTheme.HeaderDark, 125f);
            g.FillRectangle(lgb, leftPane.ClientRectangle);

            using Pen borderGold = new(AppTheme.AccentGold, 2f);
            g.DrawLine(borderGold, leftPane.Width - 1, 0, leftPane.Width - 1, leftPane.Height);
        };

        PictureBox picBadge = new()
        {
            Size = new Size(138, 138),
            Location = new Point(63, 44),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = AppTheme.LoadAppLogo()
        };

        Label lblDiv = new()
        {
            Text = "DIVISIÓN SCRAP & BÁSCULAS\nPlacilla · Santiago · Talcahuano",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(12, 210, 240, 48)
        };

        Label lblYear = new()
        {
            Text = "CAMBIASO · DESDE 1875",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = AppTheme.AccentGold,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(12, 272, 240, 26)
        };

        leftPane.Controls.AddRange([picBadge, lblDiv, lblYear]);

        Panel rightPane = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(28)
        };

        PictureBox picWordmark = new()
        {
            Size = new Size(176, 92),
            Location = new Point(28, 22),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppTheme.LoadCambiasoWordmark()
        };

        Label lblAppTitle = new()
        {
            Text = "Sistema de Gestión",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = AppTheme.HeaderDark,
            Location = new Point(28, 122),
            AutoSize = true
        };

        Label lblAppSub = new()
        {
            Text = "Pesajes, Grupos de Material y Proveedores",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(29, 154),
            AutoSize = true
        };

        Panel progressTrack = new()
        {
            Bounds = new Rectangle(28, 198, 316, 12),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        _progressFill.BackColor = AppTheme.AccentCyan;
        _progressFill.Bounds = new Rectangle(0, 0, 56, 12);
        progressTrack.Controls.Add(_progressFill);

        _lblStatus.Text = "Inicializando base SQLite WAL y seguridad...";
        _lblStatus.Font = AppTheme.FontSubtitle;
        _lblStatus.ForeColor = AppTheme.TextSecondary;
        _lblStatus.Bounds = new Rectangle(28, 222, 320, 40);

        Label lblVersion = new()
        {
            Text = "Versión 4.0 · .NET 10 LTS",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = AppTheme.AccentCyan,
            Location = new Point(185, 290),
            AutoSize = true
        };

        Label lblCopy = new()
        {
            Text = "Cambiaso Hnos. S.A.C. · 2026",
            Font = new Font("Segoe UI", 8F),
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(165, 310),
            AutoSize = true
        };

        rightPane.Controls.AddRange([picWordmark, lblAppTitle, lblAppSub, progressTrack, _lblStatus, lblVersion, lblCopy]);
        Controls.Add(rightPane);
        Controls.Add(leftPane);

        Paint += (_, e) =>
        {
            using Pen border = new(AppTheme.CardBorder, 1f);
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        };
    }

    public void UpdateProgress(int percent, string message)
    {
        _progressPercent = Math.Clamp(percent, 5, 100);
        _progressFill.Width = (int)(316 * (_progressPercent / 100.0));
        _lblStatus.Text = message;
        Refresh();
    }
}
