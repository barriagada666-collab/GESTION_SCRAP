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
/// Pruebas automatizadas STA para validar UI/UX, branding embebido sin dependencias externas,
/// botones completos con iconos/acciones y permisos dinámicos de perfiles en MainForm.
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

    private static IEnumerable<Button> GetAllButtons(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Button b)
            {
                yield return b;
            }

            foreach (Button nested in GetAllButtons(c))
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

    private static void AssertAllButtonsValid(Control root, string contextName)
    {
        if (root is UserControl uc)
        {
            using Form host = new() { ClientSize = new Size(1280, 760) };
            host.Controls.Add(uc);
            host.CreateControl();
            host.PerformLayout();
            ValidateButtonsCore(uc, contextName);
            host.Controls.Remove(uc);
            return;
        }

        root.CreateControl();
        root.PerformLayout();
        ValidateButtonsCore(root, contextName);
    }

    private static void ValidateButtonsCore(Control root, string contextName)
    {
        List<Button> buttons = [.. GetAllButtons(root)];
        Assert.NotEmpty(buttons);

        foreach (Button btn in buttons)
        {
            string id = $"{contextName} -> '{btn.Text.Trim()}'";
            Assert.False(string.IsNullOrWhiteSpace(btn.Text), $"Botón sin texto en {contextName}");
            Assert.True(btn.Image is not null, $"Botón sin icono asignado: {id}");
            Assert.False(string.IsNullOrWhiteSpace(btn.AccessibleName), $"Botón sin AccessibleName: {id}");
            Assert.True(HasClickHandler(btn), $"Botón sin evento Click funcional: {id}");
            Assert.True(btn.Width > 0 && btn.Height > 0, $"Botón con tamaño inválido: {id}");
            Assert.True(btn.Left >= 0 && btn.Top >= 0, $"Botón fuera de área visible: {id} ({btn.Bounds})");
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
                    AssertAllButtonsValid(login, nameof(LoginForm));
                }

                using (MainForm main = new(app, auth, store, settings))
                {
                    AssertAllButtonsValid(main, nameof(MainForm));
                    Assert.Equal(5, main.NavigationButtons.Count);
                    Assert.All(main.NavigationButtons, b => Assert.True(b.Enabled));
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
                        AssertAllButtonsValid(tab, tab.GetType().Name);
                    }
                }

                MaterialGroupNode[] nodes = [.. repo.GetMaterialNodesAsync().GetAwaiter().GetResult()];
                Producto[] prods = [.. repo.GetProductosAsync().GetAwaiter().GetResult()];

                Form[] dialogs =
                [
                    new CambiarPasswordDialog(auth),
                    new ConfiguracionDbDialog(store, settings, app),
                    new LimpiarTablasDialog(app),
                    new MaterialNodeEditorDialog(NodoGrupoTipo.ProductoAsignado, nodes[0].Id, nodes[0].Nombre, prods),
                    new MoverNodoDialog(nodes[^1], nodes)
                ];

                foreach (Form dlg in dialogs)
                {
                    using (dlg)
                    {
                        AssertAllButtonsValid(dlg, dlg.GetType().Name);
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
