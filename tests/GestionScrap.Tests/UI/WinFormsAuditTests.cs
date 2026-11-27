using System.ComponentModel;
using System.Reflection;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Forms;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Desktop.Theme;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Configuration;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.UI;

/// <summary>
/// Pruebas automatizadas STA para validar UI/UX, ajuste de texto en una línea sin recortes ni solapes,
/// encabezados legibles en grillas, branding embebido y permisos dinámicos de perfiles en MainForm.
/// </summary>
public sealed class WinFormsAuditTests
{
    private static readonly object? ClickEventKey =
        typeof(Control).GetField("s_clickEvent", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
        ?? typeof(Control).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.Name.Contains("click", StringComparison.OrdinalIgnoreCase))?.GetValue(null);

    private static void RunInSta(Action action)
    {
        Exception? captured = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static IEnumerable<T> GetAllControls<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T typed)
            {
                yield return typed;
            }

            foreach (T nested in GetAllControls<T>(c))
            {
                yield return nested;
            }
        }
    }

    private static bool HasClickHandler(Button btn)
    {
        if (btn.DialogResult != DialogResult.None)
        {
            return true;
        }

        PropertyInfo? eventsProp = typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);
        if (eventsProp?.GetValue(btn) is not EventHandlerList list || ClickEventKey is null)
        {
            return false;
        }

        return list[ClickEventKey] is not null;
    }

    private static void ForceLayoutRecursive(Control container)
    {
        container.CreateControl();
        container.PerformLayout();

        foreach (Control child in container.Controls)
        {
            if (child is TabControl tc)
            {
                tc.CreateControl();
                tc.PerformLayout();
                int pageW = Math.Max(200, tc.ClientSize.Width - 8);
                int pageH = Math.Max(200, tc.ClientSize.Height - 28);
                TabPage? original = tc.SelectedTab;
                foreach (TabPage page in tc.TabPages)
                {
                    tc.SelectedTab = page;
                    page.CreateControl();
                    page.Size = new Size(pageW, pageH);
                    ForceLayoutRecursive(page);
                }

                if (original is not null)
                {
                    tc.SelectedTab = original;
                }
            }
            else
            {
                ForceLayoutRecursive(child);
            }
        }

        container.PerformLayout();
    }

    private static void AssertAllButtonsAndLayoutValid(Control root, string contextName)
    {
        if (root is UserControl uc)
        {
            using Form host = new()
            {
                ClientSize = new Size(1280, 760),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000),
                ShowInTaskbar = false
            };
            host.Controls.Add(uc);
            host.CreateControl();
            uc.Bounds = host.ClientRectangle;
            ForceLayoutRecursive(host);
            ValidateButtonsAndGridsCore(uc, contextName);
            host.Controls.Remove(uc);
            return;
        }

        root.CreateControl();
        ForceLayoutRecursive(root);
        ValidateButtonsAndGridsCore(root, contextName);
    }

    private static void ValidateButtonsAndGridsCore(Control root, string contextName)
    {
        List<Button> buttons = [.. GetAllControls<Button>(root)];
        Assert.NotEmpty(buttons);

        // Validar cada botón individual: texto completo en 1 línea, icono, evento Click, sin recorte en padre
        foreach (Button btn in buttons)
        {
            string id = $"{contextName} -> '{btn.Text.Trim()}'";
            Assert.False(string.IsNullOrWhiteSpace(btn.Text), $"Botón sin texto en {contextName}");
            Assert.True(btn.Image is not null, $"Botón sin icono asignado: {id}");
            Assert.False(string.IsNullOrWhiteSpace(btn.AccessibleName), $"Botón sin AccessibleName: {id}");
            Assert.True(HasClickHandler(btn), $"Botón sin evento Click funcional: {id}");
            Assert.True(btn.Width > 0 && btn.Height > 0, $"Botón con tamaño inválido: {id}");
            Assert.True(btn.Left >= 0 && btn.Top >= 0, $"Botón fuera de área visible: {id} ({btn.Bounds})");

            // Verificar que el texto completo cabe en una sola línea sin saltos ni recortes
            Size measured = TextRenderer.MeasureText(
                btn.Text,
                btn.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int minInnerWidth = measured.Width + (btn.Image?.Width ?? 0) + btn.Padding.Horizontal + 6;
            Assert.True(
                btn.ClientSize.Width >= minInnerWidth,
                $"Texto de botón demasiado apretado o con salto de línea en {id}: ClientWidth={btn.ClientSize.Width}, Requerido>={minInnerWidth}");
            Assert.True(
                btn.ClientSize.Height >= measured.Height + 6,
                $"Altura insuficiente para texto de botón en {id}: ClientHeight={btn.ClientSize.Height}, TextHeight={measured.Height}");

            // Verificar que el botón no queda recortado dentro de su contenedor padre (ni colapsado en altura)
            Control? parent = btn.Parent;
            if (parent is not null && (parent is not ScrollableControl sc || !sc.AutoScroll))
            {
                Assert.True(
                    parent.ClientSize.Height >= btn.Height - 2,
                    $"Contenedor padre demasiado bajo (colapsado) en {id}: parent.ClientHeight={parent.ClientSize.Height} < btn.Height={btn.Height}");
                Assert.True(
                    btn.Right <= parent.ClientSize.Width + 2,
                    $"Botón recortado a la derecha en {id}: btn.Right={btn.Right} > parent.ClientWidth={parent.ClientSize.Width}");
                Assert.True(
                    btn.Bottom <= parent.ClientSize.Height + 2,
                    $"Botón recortado abajo en {id}: btn.Bottom={btn.Bottom} > parent.ClientHeight={parent.ClientSize.Height}");
            }
        }

        // En diálogos/formularios de tamaño fijo verificar que ningún TextBox/ComboBox/GroupBox desborde el ancho de su padre
        if (root is Form dlgForm && dlgForm.FormBorderStyle == FormBorderStyle.FixedDialog)
        {
            foreach (Control ctrl in GetAllControls<Control>(dlgForm))
            {
                if (ctrl is TextBox or ComboBox or GroupBox or CheckedListBox && ctrl.Parent is Control p && p.ClientSize.Width > 0)
                {
                    Assert.True(
                        ctrl.Right <= p.ClientSize.Width + 2,
                        $"Control '{ctrl.GetType().Name}' desborda a la derecha en {contextName}: Right={ctrl.Right} > ParentClientWidth={p.ClientSize.Width}");
                }
            }
        }

        // Verificar cero superposición entre botones hermanos dentro del mismo contenedor
        foreach (IGrouping<Control?, Button> group in buttons.GroupBy(b => b.Parent))
        {
            List<Button> siblings = [.. group];
            for (int i = 0; i < siblings.Count; i++)
            {
                for (int j = i + 1; j < siblings.Count; j++)
                {
                    Button a = siblings[i];
                    Button b = siblings[j];
                    Assert.False(
                        a.Bounds.IntersectsWith(b.Bounds),
                        $"Superposición detectada en {contextName}: '{a.Text.Trim()}' ({a.Bounds}) intersecta '{b.Text.Trim()}' ({b.Bounds})");
                }
            }
        }

        // Validar contraste y selección de encabezados en todas las grillas DataGridView
        foreach (DataGridView dgv in GetAllControls<DataGridView>(root))
        {
            Assert.False(dgv.EnableHeadersVisualStyles, $"DataGridView en {contextName} debe desactivar estilos visuales de cabecera para conservar paleta corporativa.");
            Assert.Equal(dgv.ColumnHeadersDefaultCellStyle.BackColor, dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor);
            Assert.Equal(dgv.ColumnHeadersDefaultCellStyle.ForeColor, dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor);
        }
    }

    [Fact]
    public void EmbeddedCambiasoAssets_LoadWithZeroExternalDependencies()
    {
        using Image logo = AppTheme.LoadAppLogo();
        using Image emblem = AppTheme.LoadCambiasoEmblem();
        using Image wordmark = AppTheme.LoadCambiasoWordmark();
        using Icon? icon = AppTheme.LoadAppIcon();

        Assert.True(logo.Width >= 48);
        Assert.True(emblem.Width >= 48);
        Assert.True(wordmark.Width >= 100);
        Assert.NotNull(icon);
    }

    [Fact]
    public void AllFormsTabsAndDialogs_HaveValidButtonsIconsAndClickHandlers()
    {
        RunInSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_UI_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "ui.db");
            string settingsPath = Path.Combine(tempDir, "settings.json");

            try
            {
                using SqliteScrapRepository repo = new(dbPath);
                repo.InitializeAsync().GetAwaiter().GetResult();
                SecuritySessionService auth = new(repo);
                auth.LoginAsync("barriagada", "Cambias0").GetAwaiter().GetResult();

                AppSettingsStore store = new(settingsPath);
                AppSettings settings = store.Load();
                settings.SqliteDatabasePath = dbPath;

                ScrapApplicationService app = new(
                    repo,
                    auth,
                    new FirebirdSyncService(),
                    new ClosedXmlExcelExportService(),
                    new MdbMigrationService(repo));

                using (SplashForm splash = new())
                {
                    splash.CreateControl();
                    splash.UpdateProgress(80, "Verificando UI...");
                }

                using (LoginForm login = new(auth))
                {
                    AssertAllButtonsAndLayoutValid(login, nameof(LoginForm));
                }

                using (MainForm main = new(app, auth, store, settings))
                {
                    main.WindowState = FormWindowState.Normal;
                    main.Size = new Size(1380, 840);
                    AssertAllButtonsAndLayoutValid(main, nameof(MainForm));
                    Assert.Equal(5, main.NavigationButtons.Count);
                    Assert.All(main.NavigationButtons, b => Assert.True(b.Enabled));

                    // Verificar también en tamaño mínimo soportado (1180x720)
                    main.Size = new Size(1180, 720);
                    main.PerformLayout();
                    AssertAllButtonsAndLayoutValid(main, $"{nameof(MainForm)}_MinSize");
                }

                UserControl[] tabs =
                [
                    new InformeResumenControl(app, auth, _ => { }),
                    new GrupoMaterialControl(app, auth),
                    new AnalisisProveedoresControl(app, auth),
                    new ImportadorControl(app, auth, store, settings),
                    new SeguridadPerfilesControl(app, auth, settings)
                ];

                foreach (UserControl tab in tabs)
                {
                    using (tab)
                    {
                        tab.Size = new Size(1280, 760);
                        AssertAllButtonsAndLayoutValid(tab, tab.GetType().Name);
                    }
                }

                MaterialGroupNode[] nodes = [.. repo.GetMaterialNodesAsync().GetAwaiter().GetResult()];
                Producto[] prods = [.. repo.GetProductosAsync().GetAwaiter().GetResult()];

                Form[] dialogs =
                [
                    new CambiarPasswordDialog(auth),
                    new ConfiguracionDbDialog(store, settings, app),
                    new LimpiarTablasDialog(app),
                    new MaterialNodeEditorDialog(NodoGrupoTipo.CarpetaGrupo, null, "Raíz Principal", prods),
                    new MaterialNodeEditorDialog(NodoGrupoTipo.ProductoAsignado, nodes[0].Id, nodes[0].Nombre, prods),
                    new MoverNodoDialog(nodes[^1], nodes),
                    new ExportacionPersonalizadaDialog("Informe Personalizado", 2, ["KPIs y Estadísticas", "Segmentación"])
                ];

                foreach (Form dlg in dialogs)
                {
                    using (dlg)
                    {
                        AssertAllButtonsAndLayoutValid(dlg, dlg.GetType().Name);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // Limpieza temporal best-effort
                    }
                }
            }
        });
    }

    [Fact]
    public void MainForm_EnforcesDynamicProfileRestrictions()
    {
        RunInSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_UIRoles_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "ui_roles.db");
            string settingsPath = Path.Combine(tempDir, "settings.json");

            try
            {
                using SqliteScrapRepository repo = new(dbPath);
                repo.InitializeAsync().GetAwaiter().GetResult();
                SecuritySessionService auth = new(repo);
                AppSettingsStore store = new(settingsPath);
                AppSettings settings = store.Load();
                ScrapApplicationService app = new(
                    repo,
                    auth,
                    new FirebirdSyncService(),
                    new ClosedXmlExcelExportService(),
                    new MdbMigrationService(repo));

                // Crear usuario con perfil Analista (ID 3: sin ImportarDatos ni AdministrarSeguridadPerfiles)
                auth.LoginAsync("barriagada", "Cambias0").GetAwaiter().GetResult();
                app.SaveUsuarioAsync(new Usuario
                {
                    NombreUsuario = "analista1",
                    NombreCompleto = "Analista Pruebas",
                    PerfilId = 3,
                    NombrePerfil = "Analista",
                    Activo = true
                }, "Analista2026").GetAwaiter().GetResult();
                auth.Logout();

                auth.LoginAsync("analista1", "Analista2026").GetAwaiter().GetResult();
                using MainForm mainAnalista = new(app, auth, store, settings);
                Assert.True(mainAnalista.NavigationButtons[0].Enabled);  // Informe Resumen
                Assert.True(mainAnalista.NavigationButtons[1].Enabled);  // Grupos de Material
                Assert.True(mainAnalista.NavigationButtons[2].Enabled);  // Análisis Proveedores
                Assert.False(mainAnalista.NavigationButtons[3].Enabled); // Importador BD
                Assert.False(mainAnalista.NavigationButtons[4].Enabled); // Seguridad y Perfiles
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // Ignorar limpieza temporal
                    }
                }
            }
        });
    }
}
