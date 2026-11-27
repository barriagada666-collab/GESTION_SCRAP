using GestionScrap.Application.Services;
using GestionScrap.Desktop.Forms;
using GestionScrap.Domain;
using GestionScrap.Domain.Entities;
using GestionScrap.Domain.Interfaces;
using GestionScrap.Infrastructure.Configuration;
using GestionScrap.Infrastructure.Diagnostics;
using GestionScrap.Infrastructure.Export;
using GestionScrap.Infrastructure.Firebird;
using GestionScrap.Infrastructure.Migration;
using GestionScrap.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GestionScrap.Desktop;

internal static class Program
{
    private static readonly FileAppLogger AppLogger = new();

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
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Log(
                DiagnosticLogLevel.Error,
                "TaskScheduler",
                "Excepción asíncrona no observada capturada globalmente.",
                context: $"Runtime={Environment.Version}",
                exception: e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();
        AppLogger.Log(DiagnosticLogLevel.Info, "Startup", $"Iniciando {AppMetadata.ProductName} {AppMetadata.ShortVersion} (.NET {Environment.Version}).");

        ServiceProvider? provider = null;
        using (SplashForm splash = new())
        {
            splash.Show();
            splash.UpdateProgress(25, "Cargando configuración local y endpoints...");
            System.Windows.Forms.Application.DoEvents();

            AppSettingsStore settingsStore = new();
            AppSettings settings = settingsStore.Load();

            ServiceCollection services = new();
            services.AddSingleton<IAppLogger>(AppLogger);
            services.AddSingleton(settingsStore);
            services.AddSingleton(settings);
            services.AddSingleton(_ => new SqliteScrapRepository(settings.SqliteDatabasePath));
            services.AddSingleton<IScrapRepository>(sp => sp.GetRequiredService<SqliteScrapRepository>());
            services.AddSingleton<IEnterpriseRepository>(sp => sp.GetRequiredService<SqliteScrapRepository>());
            services.AddSingleton<IAuthService, SecuritySessionService>();
            services.AddSingleton<IFirebirdSyncService, FirebirdSyncService>();
            services.AddSingleton<IExcelExportService, ClosedXmlExcelExportService>();
            services.AddSingleton<IMdbMigrationService, MdbMigrationService>();
            services.AddSingleton<ScrapApplicationService>();
            services.AddSingleton<EnterpriseApplicationService>();

            provider = services.BuildServiceProvider();

            splash.UpdateProgress(65, "Inicializando SQLite WAL, migraciones, perfiles RBAC y jerarquía...");
            System.Windows.Forms.Application.DoEvents();
            IScrapRepository repo = provider.GetRequiredService<IScrapRepository>();
            repo.InitializeAsync().GetAwaiter().GetResult();

            DatabaseIntegrityReport integrity = repo.VerifyIntegrityAsync().GetAwaiter().GetResult();
            AppLogger.Log(
                integrity.IsHealthy ? DiagnosticLogLevel.Info : DiagnosticLogLevel.Warning,
                "SQLiteStartup",
                integrity.Summary,
                context: settings.SqliteDatabasePath,
                verification: integrity.IsHealthy ? "OK" : "REVISAR");

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
            EnterpriseApplicationService enterpriseService = provider.GetRequiredService<EnterpriseApplicationService>();

            while (true)
            {
                using LoginForm login = new(auth);
                if (login.ShowDialog() != DialogResult.OK)
                {
                    break;
                }

                bool logoutRequested;
                using (MainForm main = new(appService, enterpriseService, auth, settingsStore, settings))
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
        AppLogger.Log(
            DiagnosticLogLevel.Critical,
            $"UnhandledException.{source}",
            $"Error no controlado en origen {source}.",
            context: $"OS={Environment.OSVersion} | x64={Environment.Is64BitProcess}",
            exception: ex);

        MessageBox.Show(
            $"Se produjo un error inesperado en la aplicación ({source}):\n\n{ex.Message}\n\nDetalle registrado en:\n{AppLogger.CurrentLogFilePath}",
            "Error de Aplicación",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
