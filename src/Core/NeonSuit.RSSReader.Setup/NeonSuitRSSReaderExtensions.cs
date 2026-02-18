using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Data.Configuration;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.RssFeedParser;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;

namespace NeonSuit.RSSReader.Setup
{
    /// <summary>
    /// Provides extension methods for registering NeonSuit RSS Reader core services,
    /// repositories, and infrastructure components into an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class NeonSuitServiceExtensions
    {
        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            string dbPath,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(dbPath, nameof(dbPath));

            // --- 0. Database Configuration (Scoped) ---
            services.AddDbContext<RssReaderDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}", sqlite =>
                {
                    sqlite.CommandTimeout(30);
                    sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                configureOptions?.Invoke(options);

#if DEBUG
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
#endif
            });

            // --- 1. Settings & Configuration (Singleton) ---
            services.AddSingleton<LogSettings>();

            // --- 2. Repositories (Scoped) ---
            services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
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

            // --- 3. Business Services (Scoped) ---
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

            // --- 4. Engines & Coordinators (Singleton/Transient) ---
            services.AddSingleton<ISyncCoordinatorService, SyncCoordinatorService>();
            services.AddTransient<IRuleEngine, RuleEngine>();

            // --- 5. Parsers & Utilities (Transient) ---
            services.AddTransient<IRssFeedParser, RssFeedParser>();

            return services;
        }

        public static IServiceCollection AddNeonSuitBackend(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<DbContextOptionsBuilder>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var dbPath = configuration.GetConnectionString("RssReaderDatabase")
                         ?? configuration["Database:Path"]
                         ?? "rssreader.db";

            return services.AddNeonSuitBackend(dbPath, configureOptions);
        }

        public static IServiceProvider UseNeonSuitDatabase(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RssReaderDbContext>();

            context.Database.Migrate();
            return serviceProvider;
        }
    }
}