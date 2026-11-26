using System.ComponentModel;
using System.Reflection;
using GestionScrap.Application.Services;
using GestionScrap.Desktop.Controls;
using GestionScrap.Desktop.Forms;
using GestionScrap.Desktop.Forms.Dialogs;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Enums;
using GestionScrap.Infrastructure.Configuration;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;

namespace GestionScrap.Tests.UI;

/// <summary>
/// Pruebas automatizadas de auditoría UI WinForms en hilo STA: verifica iconos, textos, accesibilidad,
/// dimensiones visibles, manejadores Click enlazados y restricciones visuales por perfil RBAC.
/// </summary>
public sealed class WinFormsAuditTests
{
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

    private static readonly object? ClickEventKey =
        typeof(Control).GetField("EventClick", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
        ?? typeof(Control).GetField("s_clickEvent", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
        ?? typeof(Control).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.Name.Contains("click", StringComparison.OrdinalIgnoreCase))?.GetValue(null);

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
        root.CreateControl();
        root.PerformLayout();

        List<Button> buttons = [.. GetAllButtons(root)];
        Assert.NotEmpty(buttons);

        foreach (Button btn in buttons)
        {
            string id = $"{contextName} -> '{btn.Text.Trim()}'";
            Assert.False(string.IsNullOrWhiteSpace(btn.Text), $"Botón sin texto en {contextName}");
            Assert.True(btn.Image is not null, $"Botón sin icono asignado: {id}");
            Assert.False(string.IsNullOrWhiteSpace(btn.AccessibleName), $"Botón sin AccessibleName: {id}");
            Assert.True(HasClickHandler(btn), $"Botón sin evento Click funcional asociado: {id}");
            Assert.True(btn.Width > 0 && btn.Height > 0, $"Botón con tamaño inválido: {id}");
            Assert.True(btn.Left >= 0 && btn.Top >= 0, $"Botón fuera de límites (negativo): {id} ({btn.Bounds})");
        }
    }

    [Fact]
    public void AllFormsDialogsAndTabs_HaveValidButtonsIconsTextAndWiredHandlers()
    {
        RunInSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "GestionScrap_UITests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string dbPath = Path.Combine(tempDir, "ui_audit.db");
            string settingsPath = Path.Combine(tempDir, "settings.json");

            try
            {
                using SqliteScrapRepository repo = new(dbPath);
                repo.InitializeAsync().GetAwaiter().GetResult();
                SecuritySessionService auth = new(repo);
                auth.LoginAsync("admin", "Admin@2026").GetAwaiter().GetResult();

                AppSettingsStore store = new(settingsPath);
                AppSettings settings = store.Load();
                settings.SqliteDatabasePath = dbPath;

                ScrapApplicationService app = new(
                    repo,
                    auth,
                    new FirebirdSyncService(),
                    new ClosedXmlExcelExportService(),
                    new MdbMigrationService(repo));

                using (LoginForm login = new(auth))
                {
                    AssertAllButtonsValid(login, nameof(LoginForm));
                }

                using (MainForm main = new(app, auth, store, settings))
                {
                    AssertAllButtonsValid(main, nameof(MainForm));
                    Assert.Equal(10, main.NavigationButtons.Count);
                    Assert.All(main.NavigationButtons, b => Assert.True(b.Enabled));
                }

                UserControl[] tabs =
                [
                    new DashboardControl(app, auth, _ => { }),
                    new GrupoMaterialControl(app, auth),
                    new ComprasTrasladosControl(app, auth),
                    new InformeClienteControl(app, auth),
                    new GruposEspecialesControl(app, auth),
                    new InformeJcrammControl(app, auth),
                    new InformePreciosControl(app, auth),
                    new MantenedorPreciosControl(app, auth),
                    new ImportacionFirebirdControl(app, auth, store, settings),
                    new AdministracionSeguridadControl(app, auth, settings)
                ];

                foreach (UserControl tab in tabs)
                {
                    using (tab)
                    {
                        tab.Size = new Size(1280, 760);
                        AssertAllButtonsValid(tab, tab.GetType().Name);
                    }
                }

                FirebirdPlantaEndpoint ep = settings.FirebirdEndpoints[0];
                Form[] dialogs =
                [
                    new ActualizarPrecioDialog(app, Planta.Placilla, ModoPrecio.Reales, 1001, "S0400", "SCRAP", "PROV", "RECAM", 150m),
                    new CambiarPasswordDialog(auth),
                    new ClienteSelectorDialog(app, Planta.Placilla),
                    new ConfiguracionDbDialog(store, settings, app),
                    new EjecutarPaFirebirdDialog(app, ep),
                    new LimpiarTablasDialog(app),
                    new ProductoSelectorDialog(app)
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
                        // Limpieza best-effort de carpeta temporal
                    }
                }
            }
        });
    }

    [Fact]
    public void MainForm_DisablesRestrictedNavigationModulesByUserRole()
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

                // Rol Operador: no puede entrar a Seguridad y MDB (índice 9)
                auth.LoginAsync("operador", "Oper@2026").GetAwaiter().GetResult();
                using (MainForm mainOp = new(app, auth, store, settings))
                {
                    Assert.True(mainOp.NavigationButtons[0].Enabled);
                    Assert.True(mainOp.NavigationButtons[8].Enabled);
                    Assert.False(mainOp.NavigationButtons[9].Enabled);
                }

                auth.Logout();

                // Rol Auditor: sin Importar Firebird (índice 8), con Seguridad/Auditoría (índice 9)
                auth.LoginAsync("auditor", "Audit@2026").GetAwaiter().GetResult();
                using (MainForm mainAud = new(app, auth, store, settings))
                {
                    Assert.False(mainAud.NavigationButtons[8].Enabled);
                    Assert.True(mainAud.NavigationButtons[9].Enabled);
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
                        // Ignorar en limpieza temporal
                    }
                }
            }
        });
    }
}
