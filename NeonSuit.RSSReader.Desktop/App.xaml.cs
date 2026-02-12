using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Desktop.ViewModels;
using NeonSuit.RSSReader.Desktop.Views;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.FeedParser;
using System;
using System.IO;
using System.Windows;

namespace NeonSuit.RSSReader.Desktop
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 1. GESTIÓN DE RUTAS Y DIRECTORIOS (Nivel Pro)
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string neonFolder = Path.Combine(appDataPath, "NeonSuit");
            string dbPath = Path.Combine(neonFolder, "NeonSuit.db3");

            // Aseguramos que la carpeta exista antes de que SQLite intente tocarla
            if (!Directory.Exists(neonFolder))
            {
                Directory.CreateDirectory(neonFolder);
            }

            // 2. INFRAESTRUCTURA Y LOGGING
            services.AddLogging(configure =>
            {
                configure.AddDebug();
                configure.AddConsole();
            });

            // 3. BASE DE DATOS Y REPOSITORIOS
            // Usamos Singleton para el DbContext para mantener una única conexión asíncrona
            //services.AddSingleton(new RssReaderDbContext(dbPath ));

            services.AddSingleton<ArticleRepository>();
            services.AddSingleton<FeedRepository>();
            services.AddSingleton<CategoryRepository>();
            services.AddSingleton<RuleRepository>();
            services.AddSingleton<UserPreferencesRepository>();

            // 4. CAPA DE SERVICIOS (Lógica de Negocio)
            services.AddSingleton<IArticleService, ArticleService>();
            services.AddSingleton<IFeedService, FeedService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IRuleService, RuleService>();
            services.AddSingleton<ICategoryService, CategoryService>();
            services.AddSingleton<RssFeedParser>();        

            // 5. CAPA DE PRESENTACIÓN (UI)
            // Registramos el MainViewModel y la MainWindow
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>(s => new MainWindow(s.GetRequiredService<MainViewModel>()));
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Inicializamos la base de datos de forma asíncrona antes de mostrar la UI
                // Esto evita que el primer query del MainViewModel choque con una DB sin tablas
                var dbContext = ServiceProvider.GetRequiredService<RssReaderDbContext>();
                await dbContext.InitializeAsync();

                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                // Si algo falla al arrancar, lo registramos. ¡Profesionalidad ante todo!
                var logger = ServiceProvider.GetService<ILogger<App>>();
                logger?.LogCritical(ex, "La aplicación NeonSuit no pudo iniciar.");

                MessageBox.Show($"Asere, hubo un facho al arrancar: {ex.Message}",
                                "Error Crítico NeonSuit", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}