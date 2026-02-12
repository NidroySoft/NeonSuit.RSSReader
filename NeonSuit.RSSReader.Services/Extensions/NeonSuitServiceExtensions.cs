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
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddNeonSuitBackend(this IServiceCollection services, string dbPath)
        {
            // --- 0. Infrastructure and Context ---
            // Configure DbContext with SQLite
            services.AddDbContext<RssReaderDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}")
                       .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking),
                ServiceLifetime.Singleton); // Singleton: one shared connection manager per application

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
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddNeonSuitBackendWithConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Get connection string from configuration
            var dbPath = configuration.GetConnectionString("RssReaderDatabase")
                ?? configuration["Database:Path"]
                ?? "rssreader.db";

            // Configure DbContext
            services.AddDbContext<RssReaderDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}")
                       .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            // Register repositories and services (same as above)
            return services.AddNeonSuitBackend(dbPath);
        }

        /// <summary>
        /// Adds support for SQLite migrations and ensures database is created.
        /// </summary>
        public static IServiceCollection AddNeonSuitDatabaseMigrations(this IServiceCollection services)
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RssReaderDbContext>();

            // Apply any pending migrations
            context.Database.Migrate();

            return services;
        }
    }
}