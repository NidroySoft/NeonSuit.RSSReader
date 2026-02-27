using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Desktop.ViewModels;
using NeonSuit.RSSReader.Setup;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace NeonSuit.RSSReader.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        private readonly ILogger _logger;
        private bool _isShuttingDown = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            // Configurar manejador de excepciones no controladas
            SetupExceptionHandling();

            try
            {
                // Configurar y construir el host
                _host = CreateHostBuilder().Build();

                // Obtener logger después de construir el host
                _logger = _host.Services.GetRequiredService<ILogger>();
                _logger.Information("=== NeonSuit RSS Reader Iniciando ===");
            }
            catch (Exception ex)
            {
                // Si falla la construcción, mostrar error y salir
                MessageBox.Show($"Error crítico al iniciar la aplicación: {ex.Message}",
                    "Error Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        /// <summary>
        /// Creates and configures the host builder.
        /// </summary>
        private static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Cargar configuración desde múltiples fuentes
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                                        optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();

                    // Configuración específica para el usuario
                    var appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "NeonSuit", "RSSReader");

                    if (!Directory.Exists(appDataPath))
                        Directory.CreateDirectory(appDataPath);

                    var userConfigPath = Path.Combine(appDataPath, "appsettings.user.json");
                    if (File.Exists(userConfigPath))
                    {
                        config.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
                    }
                })
                .UseSerilog((context, config) =>
                {
                    // Configuración de Serilog
                    var appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "NeonSuit", "RSSReader", "Logs");

                    if (!Directory.Exists(appDataPath))
                        Directory.CreateDirectory(appDataPath);

                    config.ReadFrom.Configuration(context.Configuration)
                          .Enrich.FromLogContext()
                          .Enrich.WithThreadId()
                          .Enrich.WithProcessId()
                          .Enrich.WithMachineName()
                          .WriteTo.Console()
                          .WriteTo.Debug()
                          .WriteTo.File(
                              path: Path.Combine(appDataPath, "log-.txt"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 30,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                          .WriteTo.Sink<SerilogSink>(); // Sink personalizado para UI
                })
                .ConfigureServices((context, services) =>
                {
                    // =================================================================
                    // 1. BACKEND COMPLETO (PROYECTO PEGAMENTO)
                    // =================================================================
                    var dbPath = context.Configuration.GetConnectionString("RssReaderDatabase")
                                 ?? Path.Combine(
                                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                     "NeonSuit", "RSSReader", "rssreader.db");

                    // Asegurar directorio de base de datos
                    var dbDirectory = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
                        Directory.CreateDirectory(dbDirectory);

                    services.AddNeonSuitBackend(dbPath);

                    // =================================================================
                    // 2. CONFIGURACIÓN DE LA APLICACIÓN
                    // =================================================================
                    services.Configure<AppSettings>(context.Configuration.GetSection("AppSettings"));
                    services.AddSingleton<AppState>();

                    // =================================================================
                    // 3. SERVICIOS DE UI
                    // =================================================================
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IToastNotificationService, ToastNotificationService>();
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<ISettingsService, UISettingsService>();

                    // Servicio para actualizaciones
                    services.AddSingleton<IUpdateService, UpdateService>();

                    // =================================================================
                    // 4. VIEWMODELS
                    // =================================================================
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<FeedListViewModel>();
                    services.AddTransient<ArticleListViewModel>();
                    services.AddTransient<ArticleReaderViewModel>();
                    services.AddTransient<CategoryManagerViewModel>();
                    services.AddTransient<TagManagerViewModel>();
                    services.AddTransient<RuleManagerViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<ModuleManagerViewModel>();
                    services.AddTransient<NotificationCenterViewModel>();
                    services.AddTransient<StatisticsViewModel>();
                    services.AddTransient<BackupRestoreViewModel>();

                    // ViewModels para diálogos
                    services.AddTransient<AddFeedDialogViewModel>();
                    services.AddTransient<EditCategoryDialogViewModel>();
                    services.AddTransient<EditTagDialogViewModel>();
                    services.AddTransient<CreateRuleDialogViewModel>();
                    services.AddTransient<ImportOpmlDialogViewModel>();

                    // =================================================================
                    // 5. VENTANAS
                    // =================================================================
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddTransient<ModuleManagerWindow>();
                    services.AddTransient<AboutWindow>();
                });

        /// <summary>
        /// Configura el manejo de excepciones no controladas.
        /// </summary>
        private void SetupExceptionHandling()
        {
            // Excepciones en hilos de UI
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Excepciones en hilos de background
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

            // Excepciones en tareas
            TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        }

        /// <summary>
        /// Handles the Startup event of the application.
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Iniciar el host
                await _host.StartAsync();

                var logger = _host.Services.GetRequiredService<ILogger>();
                logger.Information("Host iniciado correctamente");

                // Inicializar base de datos
                await InitializeDatabaseAsync();

                // Verificar actualizaciones (en background)
                _ = CheckForUpdatesAsync();

                // Crear y mostrar ventana principal
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

                mainWindow.DataContext = mainViewModel;
                mainWindow.Show();

                // Iniciar servicios de sincronización
                await StartBackgroundServicesAsync();

                // Cargar módulos si existen
                await LoadModulesAsync();

                logger.Information("Aplicación iniciada correctamente");
            }
            catch (Exception ex)
            {
                var logger = _host.Services.GetService<ILogger>();
                logger?.Error(ex, "Error fatal durante el inicio de la aplicación");

                MessageBox.Show($"Error al iniciar la aplicación: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// Initializes the database and applies migrations.
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            var logger = _host.Services.GetRequiredService<ILogger>();
            logger.Information("Inicializando base de datos...");

            try
            {
                await Task.Run(() => _host.Services.UseNeonSuitDatabase());
                logger.Information("Base de datos inicializada correctamente");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error al inicializar la base de datos");
                throw;
            }
        }

        /// <summary>
        /// Starts background services like sync coordinator.
        /// </summary>
        private async Task StartBackgroundServicesAsync()
        {
            var logger = _host.Services.GetRequiredService<ILogger>();
            logger.Information("Iniciando servicios de background...");

            try
            {
                // Iniciar sincronización automática
                var syncService = _host.Services.GetRequiredService<ISyncCoordinatorService>();
                await syncService.StartAsync();

                logger.Information("Servicios de background iniciados");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error al iniciar servicios de background");
                // No relanzamos la excepción para que la app pueda continuar
            }
        }

        /// <summary>
        /// Loads external modules from the Modules directory.
        /// </summary>
        private async Task LoadModulesAsync()
        {
            var logger = _host.Services.GetRequiredService<ILogger>();
            logger.Information("Cargando módulos externos...");

            try
            {
                var moduleService = _host.Services.GetRequiredService<IModuleService>();
                var modulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");

                if (Directory.Exists(modulesPath))
                {
                    await moduleService.LoadModulesFromDirectoryAsync(modulesPath);
                    logger.Information("Módulos cargados correctamente");
                }
                else
                {
                    logger.Debug("Directorio de módulos no encontrado: {Path}", modulesPath);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error al cargar módulos");
            }
        }

        /// <summary>
        /// Checks for application updates in the background.
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateService = _host.Services.GetService<IUpdateService>();
                if (updateService != null)
                {
                    var hasUpdate = await updateService.CheckForUpdatesAsync();
                    if (hasUpdate)
                    {
                        // Notificar al usuario (el ViewModel se encargará de mostrar)
                        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
                        mainViewModel.ShowUpdateAvailableNotification();
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = _host.Services.GetRequiredService<ILogger>();
                logger.Warning(ex, "Error al verificar actualizaciones");
            }
        }

        /// <summary>
        /// Handles the Exit event of the application.
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            var logger = _host.Services.GetService<ILogger>();
            logger?.Information("Iniciando cierre de aplicación...");

            try
            {
                // Detener servicios de background
                var syncService = _host.Services.GetService<ISyncCoordinatorService>();
                if (syncService != null)
                {
                    await syncService.StopAsync();
                }

                // Guardar estado de la aplicación
                var appState = _host.Services.GetService<AppState>();
                appState?.SaveState();

                logger?.Information("Servicios detenidos correctamente");
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Error durante el cierre de la aplicación");
            }
            finally
            {
                await _host.StopAsync();
                _host.Dispose();

                logger?.Information("=== NeonSuit RSS Reader Finalizado ===\n");
                Log.CloseAndFlush();

                base.OnExit(e);
            }
        }

        #region Exception Handlers

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = _host.Services.GetService<ILogger>();
            logger?.Fatal(e.Exception, "Excepción no controlada en el hilo de UI");

            // Mostrar diálogo de error
            ShowFatalErrorDialog("Error en la interfaz de usuario", e.Exception);

            e.Handled = true; // Evitar que la aplicación termine
        }

        private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var logger = _host.Services.GetService<ILogger>();
            logger?.Fatal(exception, "Excepción no controlada en AppDomain. IsTerminating: {IsTerminating}", e.IsTerminating);

            if (e.IsTerminating)
            {
                // La aplicación va a terminar, mostrar mensaje
                ShowFatalErrorDialog("Error fatal en la aplicación", exception);
            }
        }

        private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var logger = _host.Services.GetService<ILogger>();
            logger?.Error(e.Exception, "Excepción no observada en Task");

            e.SetObserved(); // Marcar como observada para evitar que termine el proceso
        }

        private void ShowFatalErrorDialog(string title, Exception? exception)
        {
            var message = exception?.Message ?? "Error desconocido";
            var details = exception?.ToString() ?? "No hay detalles disponibles";

            // En producción, podrías guardar los detalles en un archivo de log
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeonSuit", "RSSReader", "Crashes",
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            File.WriteAllText(logPath, details);

            // Mostrar mensaje simple al usuario
            MessageBox.Show(
                $"{title}\n\n{message}\n\nSe ha guardado un registro del error en:\n{logPath}",
                "Error Fatal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Sink personalizado para mostrar logs en la UI.
    /// </summary>
    public class SerilogSink : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            // Aquí puedes enviar logs a la UI si es necesario
            // Por ejemplo, a un control de LogViewer
        }
    }

    /// <summary>
    /// Configuración de la aplicación.
    /// </summary>
    public class AppSettings
    {
        public bool AutoRefreshEnabled { get; set; } = true;
        public int RefreshIntervalMinutes { get; set; } = 30;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public string Theme { get; set; } = "Light";
    }

    /// <summary>
    /// Estado global de la aplicación.
    /// </summary>
    public class AppState
    {
        private readonly ILogger _logger;
        private readonly string _statePath;

        public AppState(ILogger logger)
        {
            _logger = logger;
            _statePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeonSuit", "RSSReader", "appstate.json");
        }

        public void SaveState()
        {
            try
            {
                var state = new
                {
                    LastShutdown = DateTime.UtcNow,
                    WindowPosition = MainWindowPosition,
                    WindowSize = MainWindowSize,
                    IsMaximized = IsMainWindowMaximized
                };

                var json = System.Text.Json.JsonSerializer.Serialize(state);
                File.WriteAllText(_statePath, json);

                _logger.Debug("Estado de aplicación guardado");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error al guardar estado de aplicación");
            }
        }

        public (double Left, double Top) MainWindowPosition { get; set; }
        public (double Width, double Height) MainWindowSize { get; set; }
        public bool IsMainWindowMaximized { get; set; }
    }

    #endregion
}