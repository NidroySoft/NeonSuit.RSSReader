// =======================================================
// Data/Database/RssReaderDbContext.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Database;
using NeonSuit.RSSReader.Core.Models;
using Serilog;

namespace NeonSuit.RSSReader.Data.Database
{
    /// <summary>
    /// Entity Framework Core database context for the RSS Reader application.
    /// Manages all entity mappings, database connections, and provides access to DbSets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context is optimized for SQLite in low-resource environments:
    /// <list type="bullet">
    /// <item><description>No-tracking by default for read performance</description></item>
    /// <item><description>Split queries to prevent cartesian explosion</description></item>
    /// <item><description>Detailed logging only in DEBUG builds</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The context is split into multiple partial class files for maintainability.    
    /// </para>
    /// </remarks>
    internal partial class RSSReaderDbContext : DbContext, IRSSReaderDbContext
    {
        private readonly ILogger _logger;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RSSReaderDbContext"/> class.
        /// </summary>
        /// <param name="options">DbContext options configured by the DI container.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
        public RSSReaderDbContext(DbContextOptions<RSSReaderDbContext> options, ILogger logger)
            : base(options)
        {
            _logger = logger?.ForContext<RSSReaderDbContext>()
                ?? throw new ArgumentNullException(nameof(logger));

            _logger.Information("EF Core DbContext initialized. Path: {DatabasePath}",
                DatabasePath ?? "unknown");
        }

        #region DbSets

        /// <summary>
        /// Gets the DbSet for <see cref="Category"/> entities.
        /// </summary>
        public DbSet<Category> Categories => Set<Category>();

        /// <summary>
        /// Gets the DbSet for <see cref="Feed"/> entities.
        /// </summary>
        public DbSet<Feed> Feeds => Set<Feed>();

        /// <summary>
        /// Gets the DbSet for <see cref="Article"/> entities.
        /// </summary>
        public DbSet<Article> Articles => Set<Article>();

        /// <summary>
        /// Gets the DbSet for <see cref="Tag"/> entities.
        /// </summary>
        public DbSet<Tag> Tags => Set<Tag>();

        /// <summary>
        /// Gets the DbSet for <see cref="Rule"/> entities.
        /// </summary>
        public DbSet<Rule> Rules => Set<Rule>();

        /// <summary>
        /// Gets the DbSet for <see cref="ArticleTag"/> join entities.
        /// </summary>
        public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();

        /// <summary>
        /// Gets the DbSet for <see cref="RuleCondition"/> entities.
        /// </summary>
        public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();

        /// <summary>
        /// Gets the DbSet for <see cref="NotificationLog"/> entities.
        /// </summary>
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

        /// <summary>
        /// Gets the DbSet for <see cref="UserPreferences"/> entities.
        /// </summary>
        public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

        /// <summary>
        /// Gets the full file-system path to the SQLite database file.
        /// </summary>
        public string? DatabasePath => Database?.GetDbConnection()?.DataSource;

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a new instance of <see cref="RSSReaderDbContext"/> with the specified database path.
        /// </summary>
        /// <param name="dbPath">File path to the SQLite database.</param>
        /// <param name="logger">Serilog logger instance.</param>
        /// <returns>A configured DbContext instance.</returns>
        /// <exception cref="ArgumentException">Thrown if dbPath is null or empty.</exception>
        public static RSSReaderDbContext Create(string dbPath, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

            var options = BuildOptions(dbPath);
            return new RSSReaderDbContext(options, logger);
        }

        /// <summary>
        /// Builds DbContext options for SQLite.
        /// </summary>
        private static DbContextOptions<RSSReaderDbContext> BuildOptions(string dbPath)
        {
            var builder = new DbContextOptionsBuilder<RSSReaderDbContext>();
            ConfigureSqliteOptions(builder, dbPath);
            return builder.Options;
        }

        /// <summary>
        /// Configures SQLite-specific options including timeouts and migration assembly.
        /// </summary>
        private static void ConfigureSqliteOptions(DbContextOptionsBuilder builder, string dbPath)
        {
            builder.UseSqlite(
                $"Data Source={dbPath};Cache=Shared;",
                sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                    sqliteOptions.MigrationsAssembly(typeof(RSSReaderDbContext).Assembly.FullName);
                });
        }

        #endregion

        #region OnConfiguring

        /// <summary>
        /// Configures additional DbContext options not set by the DI container.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                _logger?.Debug("DbContext not pre-configured, relying on constructor options");
            }

#if DEBUG
            // Enable detailed error information and SQL logging in development
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif

            base.OnConfiguring(optionsBuilder);
        }

        #endregion

        #region OnModelCreating

        /// <summary>
        /// Configures the entity mappings, relationships, indexes, and filters.
        /// Delegates to partial methods for better organization.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            try
            {
                // Configuration is split across multiple partial methods
                ConfigureTableNames(modelBuilder);
                ConfigureEntities(modelBuilder);
                ConfigureIndexes(modelBuilder);
                ConfigureRelationships(modelBuilder);
                ConfigureValueConversions(modelBuilder);
                ConfigureQueryFilters(modelBuilder);

                _logger.Debug("EF Core model configuration completed");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to configure EF Core model");
                throw;
            }
        }

        #endregion
    }
}