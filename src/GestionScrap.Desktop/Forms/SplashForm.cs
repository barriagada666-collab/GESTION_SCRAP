using System.Drawing.Drawing2D;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain;

namespace GestionScrap.Desktop.Forms;

/// <summary>
/// Pantalla corporativa de arranque sin dependencias externas con identidad Cambiaso (1875).
/// </summary>
public sealed class SplashForm : Form
{
    private readonly Label _lblStatus = new();
    private readonly Panel _progressTrack = new();
    private readonly Panel _progressFill = new();
    private int _progressPercent = 15;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(660, 356);
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
            UseMnemonic = false,
            Font = AppTheme.FontSmallBold,
            ForeColor = Color.FromArgb(226, 232, 240),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(12, 210, 240, 48)
        };

        Label lblYear = new()
        {
            Text = "CAMBIASO · DESDE 1875",
            Font = AppTheme.FontSmallBold,
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
            Font = AppTheme.FontSplashTitle,
            ForeColor = AppTheme.HeaderDark,
            Location = new Point(28, 122),
            AutoSize = true
        };

        Label lblAppSub = new()
        {
            Text = "Pesajes, Grupos de Material y Proveedores",
            Font = AppTheme.FontSubtitle,
            ForeColor = AppTheme.TextSecondary,
            Location = new Point(29, 154),
            AutoSize = true
        };

        _progressTrack.Bounds = new Rectangle(28, 198, 340, 12);
        _progressTrack.BackColor = Color.FromArgb(226, 232, 240);
        _progressFill.BackColor = AppTheme.AccentCyan;
        _progressFill.Bounds = new Rectangle(0, 0, 56, 12);
        _progressTrack.Controls.Add(_progressFill);

        _lblStatus.Text = "Inicializando base SQLite WAL y seguridad...";
        _lblStatus.Font = AppTheme.FontSubtitle;
        _lblStatus.ForeColor = AppTheme.TextSecondary;
        _lblStatus.Bounds = new Rectangle(28, 222, 340, 44);

        Label lblVersion = new()
        {
            Text = $"Versión {AppMetadata.Version} · .NET 10 LTS",
            Font = AppTheme.FontSmallBold,
            ForeColor = AppTheme.AccentCyan,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(28, 294, 340, 22)
        };

        Label lblCopy = new()
        {
            Text = "Cambiaso Hnos. S.A.C. · 2026",
            Font = AppTheme.FontCaption,
            ForeColor = AppTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(28, 316, 340, 20)
        };

        rightPane.Controls.AddRange([picWordmark, lblAppTitle, lblAppSub, _progressTrack, _lblStatus, lblVersion, lblCopy]);
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
        _progressFill.Width = (int)(_progressTrack.Width * (_progressPercent / 100.0));
        _lblStatus.Text = message;
        Refresh();
    }
}
