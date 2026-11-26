using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GestionScrap.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => HandleFatalException("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleFatalException("AppDomain", ex);
            }
        };

        ApplicationConfiguration.Initialize();

        ServiceProvider? provider = null;
        using (SplashForm splash = new())
        {
            splash.Show();
            splash.UpdateProgress(25, "Cargando configuración local y endpoints...");
            System.Windows.Forms.Application.DoEvents();

            AppSettingsStore settingsStore = new();
            AppSettings settings = settingsStore.Load();

            ServiceCollection services = new();
            services.AddSingleton(settingsStore);
            services.AddSingleton(settings);
            services.AddSingleton<IScrapRepository>(_ => new SqliteScrapRepository(settings.SqliteDatabasePath));
            services.AddSingleton<IAuthService, SecuritySessionService>();
            services.AddSingleton<IFirebirdSyncService, FirebirdSyncService>();
            services.AddSingleton<IExcelExportService, ClosedXmlExcelExportService>();
            services.AddSingleton<IMdbMigrationService, MdbMigrationService>();
            services.AddSingleton<ScrapApplicationService>();

            provider = services.BuildServiceProvider();

            splash.UpdateProgress(65, "Inicializando SQLite WAL, perfiles RBAC y jerarquía...");
            System.Windows.Forms.Application.DoEvents();
            IScrapRepository repo = provider.GetRequiredService<IScrapRepository>();
            repo.InitializeAsync().GetAwaiter().GetResult();

            splash.UpdateProgress(100, "Sistema listo.");
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(260);
            splash.Close();
        }

        using (provider)
        {
            AppSettingsStore settingsStore = provider.GetRequiredService<AppSettingsStore>();
            AppSettings settings = provider.GetRequiredService<AppSettings>();
            IAuthService auth = provider.GetRequiredService<IAuthService>();
            ScrapApplicationService appService = provider.GetRequiredService<ScrapApplicationService>();

            while (true)
            {
                using LoginForm login = new(auth);
                if (login.ShowDialog() != DialogResult.OK)
                {
                    break;
                }

                bool logoutRequested;
                using (MainForm main = new(appService, auth, settingsStore, settings))
                {
                    System.Windows.Forms.Application.Run(main);
                    logoutRequested = main.LogoutRequested;
                }

                if (!logoutRequested)
                {
                    break;
                }

                auth.Logout();
            }
        }
    }

    private static void HandleFatalException(string source, Exception ex)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GestionScrap");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "error.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Evitar cascada de excepciones al registrar el log
        }

        MessageBox.Show(
            $"Se produjo un error inesperado en la aplicación ({source}):\n\n{ex.Message}",
            "Error de Aplicación",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
