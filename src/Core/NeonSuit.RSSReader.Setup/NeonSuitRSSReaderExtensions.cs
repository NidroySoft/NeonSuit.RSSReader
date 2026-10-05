// =======================================================
// Setup/NeonSuitServiceExtensions.cs
// =======================================================

using AutoMapper;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeonSuit.RSSReader.Core.Interfaces.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.RssFeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Profiles;
using NeonSuit.RSSReader.Data.Configuration;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.RssFeedParser;

namespace NeonSuit.RSSReader.Setup
{
    /// <summary>
    /// Provides extension methods for registering NeonSuit RSS Reader core services,
    /// repositories, infrastructure components, and AutoMapper profiles into an 
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class centralizes all dependency injection configuration for the backend services.
    /// It follows Clean Architecture principles with clear separation of:
    /// <list type="bullet">
    /// <item><description>Database context and EF Core configuration</description></item>
    /// <item><description>Repository layer (data access)</description></item>
    /// <item><description>Service layer (business logic)</description></item>
    /// <item><description>Engines and coordinators (background processes)</description></item>
    /// <item><description>Utilities and parsers</description></item>
    /// <item><description>AutoMapper profiles (entity-DTO mappings)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Usage in Program.cs or Startup.cs:
    /// <code>
    /// services.AddNeonSuitBackend("Data Source=rssreader.db");
    /// // or with configuration
    /// services.AddNeonSuitBackend(configuration);
    /// 
    /// // Ensure database is created/migrated
    /// app.Services.UseNeonSuitDatabase();
    /// </code>
    /// </para>
    /// </remarks>
    public static class NeonSuitServiceExtensions
    {
        /// <summary>
        /// Registers all NeonSuit RSS Reader backend services with explicit database path.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="dbPath">File path to the SQLite database.</param>
        /// <param name="configureOptions">Optional additional configuration for DbContext options.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if services is null.</exception>
        /// <exception cref="ArgumentException">Thrown if dbPath is null or whitespace.</exception>
        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            string dbPath,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(dbPath, nameof(dbPath));

            // Accept either a file path or a SQLite connection string; quote paths safely.
            var connection = dbPath.Contains('=')
                ? new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(dbPath)
                : new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath };
            if (connection.DataSource != ":memory:" && !string.IsNullOrWhiteSpace(connection.DataSource))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(connection.DataSource));
                if (directory != null) Directory.CreateDirectory(directory);
            }

            // =========================================================================
            // AUTOMAPPER REGISTRATION
            // =========================================================================
            // Scans the Core.Profiles assembly and registers all profiles automatically
            services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps(typeof(ArticleProfile).Assembly);
                // Additional global AutoMapper configurations can be added here
                cfg.AllowNullCollections = true;
                cfg.AllowNullDestinationValues = true;
            });

            // =========================================================================
            // DATABASE CONTEXT (Scoped)
            // =========================================================================
            // Configured for SQLite with optimizations for low-resource environments
            services.AddDbContext<RSSReaderDbContext>(options =>
            {
                options.UseSqlite(connection.ToString(), sqlite =>
                {
                    // Timeout for long-running queries
                    sqlite.CommandTimeout(30);

                    // Prevents cartesian explosion on multiple includes
                    sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

                // Default to no-tracking for read performance
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                // Apply any additional custom configuration
                configureOptions?.Invoke(options);

#if DEBUG
                // Detailed error and SQL logging in development
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
#endif
            });

            services.AddScoped<IRSSReaderDbContext>(sp => sp.GetRequiredService<RSSReaderDbContext>());

            // =========================================================================
            // CONFIGURATION & SETTINGS (Singleton)
            // =========================================================================
            services.AddSingleton<LogSettings>();
            services.AddSingleton<BackendEvents>();
            services.AddSingleton<IBackendEvents>(sp => sp.GetRequiredService<BackendEvents>());
            services.AddLogging();
            services.TryAddSingleton<Serilog.ILogger>(_ => Serilog.Log.Logger);

            // =========================================================================
            // REPOSITORY LAYER (Scoped)
            // =========================================================================
            // Generic repository for basic CRUD operations
            services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

            // Specific repositories for each entity
            services.AddScoped<IArticleRepository, ArticleRepository>();
            services.AddScoped<IArticleTagRepository, ArticleTagRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IFeedRepository, FeedRepository>();
            services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
            services.AddScoped<IRuleRepository, RuleRepository>();
            services.AddScoped<IRuleConditionRepository, RuleConditionRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
            services.AddScoped<IDatabaseCleanupRepository, DatabaseCleanupRepository>();
            services.AddScoped<ISyncRepository, SyncRepository>();

            // =========================================================================
            // SERVICE LAYER (Scoped)
            // =========================================================================
            // Business logic services that orchestrate repositories and apply domain rules
            services.AddScoped<IArticleService, ArticleService>();
            services.AddScoped<IArticleTagService, ArticleTagService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IFeedService, FeedService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IRuleService, RuleService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IOpmlService, OpmlService>();
            services.AddScoped<IDatabaseCleanupService, DatabaseCleanupService>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<IModuleService, ModuleService>();

            // =========================================================================
            // ENGINES & COORDINATORS
            // =========================================================================
            // Background processes and rule engines
            services.AddSingleton<ISyncCoordinatorService, SyncCoordinatorService>();
            services.AddTransient<IRuleEngine, RuleEngine>();
            services.AddScoped<SyncTaskExecutor>();

            // =========================================================================
            // UTILITIES & PARSERS (Transient)
            // =========================================================================
            // Lightweight, stateless services
            services.AddTransient<IRssFeedParser, RssFeedParser>();

            return services;
        }

        /// <summary>
        /// Registers all NeonSuit RSS Reader backend services using IConfiguration for database settings.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configuration">Configuration instance containing connection string or database path.</param>
        /// <param name="configureOptions">Optional additional configuration for DbContext options.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <remarks>
        /// Looks for connection string in this order:
        /// <list type="number">
        /// <item><description>Configuration.GetConnectionString("RssReaderDatabase")</description></item>
        /// <item><description>Configuration["Database:Path"]</description></item>
        /// <item><description>Defaults to "rssreader.db" in current directory</description></item>
        /// </list>
        /// </remarks>
        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Resolve database path from configuration with fallbacks
            var dbPath = configuration.GetConnectionString("RssReaderDatabase")
                         ?? configuration["Database:Path"]
                         ?? "rssreader.db";

            return services.AddNeonSuitBackend(dbPath, configureOptions);
        }

        /// <summary>Asynchronously initializes the SQLite schema and applies pending migrations.</summary>
        /// <param name="serviceProvider">Backend service provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task UseNeonSuitDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>().InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Initializes the schema synchronously; prefer the async overload at application startup.</summary>
        /// <param name="serviceProvider">Backend service provider.</param>
        public static IServiceProvider UseNeonSuitDatabase(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();

            // Apply any pending migrations
            context.InitializeAsync().GetAwaiter().GetResult();

            return serviceProvider;
        }
    }
}