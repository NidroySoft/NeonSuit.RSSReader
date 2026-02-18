using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services.FeedParser;

namespace NeonSuit.RSSReader.Services.Extensions
{
    /// <summary>
    /// Extension methods for registering NeonSuit RSS Reader backend services and repositories
    /// into a Microsoft.Extensions.DependencyInjection IServiceCollection.
    /// 
    /// This configuration is designed to be portable across multiple frontends:
    /// - Desktop (WPF, WinUI, MAUI)
    /// - Web (ASP.NET Core, Blazor)
    /// - Console or background services
    /// 
    /// Lifetimes are chosen to balance performance and correctness:
    /// - Singleton: long-lived components (DbContext, coordinators, settings).
    /// - Scoped: repositories and services that manage data (per scope in web, per manual scope in desktop).
    /// - Transient: lightweight, stateless helpers (parsers, engines).
    /// </summary>
    public static class NeonSuitServiceExtensions
    {
        /// <summary>
        /// Registers all NeonSuit backend services, repositories, and infrastructure.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="dbPath">Path to the SQLite database file.</param>
        /// <param name="configureOptions">Optional callback to configure DbContext options.</param>
        /// <returns>The updated service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
        /// <exception cref="ArgumentException">Thrown when dbPath is null or empty.</exception>
        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            string dbPath,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

            // --- 0. Infrastructure and Context ---
            // Configure DbContext with SQLite
            services.AddDbContext<RssReaderDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}", sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                    sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                // Apply custom configuration if provided
                configureOptions?.Invoke(options);

#if DEBUG
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(true);
#endif
            }, ServiceLifetime.Singleton); // Singleton: one shared connection manager per application

            // Register DbContext as a resolvable service
            services.AddSingleton(provider => provider.GetRequiredService<RssReaderDbContext>());

            // --- 1. Data Repositories ---
            // Scoped: ensures one instance per scope (per request in web, per manual scope in desktop).
            services.AddScoped<IArticleRepository, ArticleRepository>();
            services.AddScoped<IArticleTagRepository, ArticleTagRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IFeedRepository, FeedRepository>();
            services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
            services.AddScoped<IRuleRepository, RuleRepository>();
            services.AddScoped<IRuleConditionRepository, RuleConditionRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
            services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

            // --- 2. Business Services ---
            // Scoped: business logic services, tied to repository lifetimes.
            services.AddScoped<IArticleService, ArticleService>();
            services.AddScoped<IArticleTagService, ArticleTagService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IFeedService, FeedService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IRuleService, RuleService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IOpmlService, OpmlService>();
            services.AddScoped<IDatabaseCleanupService, DatabaseCleanupService>();

            // --- 3. Coordinators and Engines ---
            // Singleton: long-lived orchestrators.
            services.AddSingleton<ISyncCoordinatorService, SyncCoordinatorService>();
            // Transient: lightweight, stateless engines.
            services.AddTransient<IRuleEngine>();

            // --- 4. Parsers and Utilities ---
            // Transient: created on demand, no state retained.
            services.AddTransient<RssFeedParser>();

            return services;
        }

        /// <summary>
        /// Alternative method for ASP.NET Core or environments with configuration.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">Configuration provider.</param>
        /// <param name="configureOptions">Optional callback to configure DbContext options.</param>
        /// <returns>The updated service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Get connection string from configuration
            var dbPath = configuration.GetConnectionString("RssReaderDatabase")
                ?? configuration["Database:Path"]
                ?? "rssreader.db";

            return services.AddNeonSuitBackend(dbPath, configureOptions);
        }

        /// <summary>
        /// Ensures database is created and migrations are applied.
        /// Call this method after service registration in your application startup.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>The service provider for chaining.</returns>
        public static IServiceProvider UseNeonSuitDatabase(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RssReaderDbContext>();
            context.Database.Migrate();
            return serviceProvider;
        }
    }
}