using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeonSuit.RSSReader.Core.Interfaces.Database;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NeonSuit.RSSReader.Data.Database
{
    public class RssReaderDbContext : DbContext, IRssReaderDbContext
    {
        private readonly ILogger _logger;
        private bool _isDisposed;

        #region DbSets

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Feed> Feeds => Set<Feed>();
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<Core.Models.Rule> Rules => Set<Core.Models.Rule>();
        public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
        public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
        public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

        public string? DatabasePath => Database?.GetDbConnection()?.DataSource;

        #endregion

        #region Constructors

        public RssReaderDbContext(DbContextOptions<RssReaderDbContext> options, ILogger logger)
            : base(options)
        {
            _logger = logger?.ForContext<RssReaderDbContext>()
                ?? throw new ArgumentNullException(nameof(logger));

            _logger.Information("EF Core DbContext initialized. Path: {DatabasePath}",
                DatabasePath ?? "unknown");
        }

        public static RssReaderDbContext Create(string dbPath, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

            var options = BuildOptions(dbPath);
            return new RssReaderDbContext(options, logger);
        }

        private static DbContextOptions<RssReaderDbContext> BuildOptions(string dbPath)
        {
            var builder = new DbContextOptionsBuilder<RssReaderDbContext>();
            ConfigureSqliteOptions(builder, dbPath);
            return builder.Options;
        }

        #endregion

        #region Configuration

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                _logger?.Debug("DbContext not pre-configured, relying on constructor options");
            }

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif

            base.OnConfiguring(optionsBuilder);
        }

        private static void ConfigureSqliteOptions(DbContextOptionsBuilder builder, string dbPath)
        {
            builder.UseSqlite(
                $"Data Source={dbPath};Cache=Shared;",
                sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                    sqliteOptions.MigrationsAssembly(typeof(RssReaderDbContext).Assembly.FullName);
                });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            try
            {
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

        private static void ConfigureTableNames(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArticleTag>().ToTable("ArticleTags");
            modelBuilder.Entity<RuleCondition>().ToTable("RuleConditions");
            modelBuilder.Entity<NotificationLog>().ToTable("NotificationLogs");
        }

        private static void ConfigureEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RuleCondition>()
                .Property(rc => rc.Order)
                .IsRequired();

            modelBuilder.Entity<Article>()
                .Property(a => a.Title)
                .HasMaxLength(500)
                .IsRequired();

            modelBuilder.Entity<Article>()
                .Property(a => a.Author)
                .HasMaxLength(250);

            modelBuilder.Entity<Article>()
                .Property(a => a.ContentHash)
                .HasMaxLength(64);

            modelBuilder.Entity<Feed>()
                .Property(f => f.Url)
                .HasMaxLength(1000)
                .IsRequired();

            modelBuilder.Entity<Feed>()
                .Property(f => f.Title)
                .HasMaxLength(255);

            modelBuilder.Entity<Category>()
                .Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasIndex(t => t.Name)
                    .IsUnique();

                entity.Property(t => t.Name)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(t => t.Color)
                    .HasMaxLength(9)
                    .IsRequired();
            });
        }

        private static void ConfigureIndexes(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Article>()
                .HasIndex(a => new { a.FeedId, a.Status })
                .HasDatabaseName("IX_Article_FeedId_Status");

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.PublishedDate)
                .HasDatabaseName("IX_Article_PublishedDate")
                .IsDescending();

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.ContentHash)
                .HasDatabaseName("IX_Article_ContentHash")
                .HasFilter("[ContentHash] IS NOT NULL AND [ContentHash] != ''");

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.IsNotified)
                .HasDatabaseName("IX_Article_IsNotified")
                .HasFilter("[IsNotified] = 0");

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.ProcessedByRules)
                .HasDatabaseName("IX_Article_ProcessedByRules")
                .HasFilter("[ProcessedByRules] = 0");

            modelBuilder.Entity<Feed>()
                .HasIndex(f => f.CategoryId)
                .HasDatabaseName("IX_Feed_CategoryId");

            modelBuilder.Entity<Feed>()
                .HasIndex(f => f.NextUpdateSchedule)
                .HasDatabaseName("IX_Feed_NextUpdateSchedule")
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<Feed>()
                .HasIndex(f => f.FailureCount)
                .HasDatabaseName("IX_Feed_FailureCount");

            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.Name, c.ParentCategoryId })
                .HasDatabaseName("IX_Category_Name_Parent")
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.ParentCategoryId)
                .HasDatabaseName("IX_Category_ParentId");

            modelBuilder.Entity<Core.Models.Rule>()
                .HasIndex(r => new { r.IsEnabled, r.Priority })
                .HasDatabaseName("IX_Rule_IsEnabled_Priority");

            modelBuilder.Entity<Core.Models.Rule>()
                .HasIndex(r => r.LastMatchDate)
                .HasDatabaseName("IX_Rule_LastMatchDate")
                .IsDescending()
                .HasFilter("[LastMatchDate] IS NOT NULL");

            modelBuilder.Entity<NotificationLog>()
                .HasIndex(nl => nl.ArticleId)
                .HasDatabaseName("IX_NotificationLog_ArticleId");

            modelBuilder.Entity<NotificationLog>()
                .HasIndex(nl => nl.SentAt)
                .HasDatabaseName("IX_NotificationLog_SentAt")
                .IsDescending();

            modelBuilder.Entity<NotificationLog>()
                .HasIndex(nl => nl.RuleId)
                .HasDatabaseName("IX_NotificationLog_RuleId")
                .HasFilter("[RuleId] IS NOT NULL");

            modelBuilder.Entity<ArticleTag>()
                .HasIndex(at => at.TagId)
                .HasDatabaseName("IX_ArticleTag_TagId");

            modelBuilder.Entity<RuleCondition>()
                .HasIndex(rc => rc.RuleId)
                .HasDatabaseName("IX_RuleCondition_RuleId");

            modelBuilder.Entity<RuleCondition>()
                .HasIndex(rc => new { rc.RuleId, rc.GroupId, rc.Order })
                .HasDatabaseName("IX_RuleCondition_GroupId");
        }

        private static void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Feed>()
                .HasMany(f => f.Articles)
                .WithOne(a => a.Feed)
                .HasForeignKey(a => a.FeedId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Category>()
                .HasMany(c => c.Feeds)
                .WithOne(f => f.Category)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>()
                .HasMany(c => c.Subcategories)
                .WithOne(c => c.ParentCategory)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Article>()
                .HasMany(a => a.Tags)
                .WithMany(t => t.Articles)
                .UsingEntity<ArticleTag>(
                    j => j.HasOne(at => at.Tag)
                          .WithMany(t => t.ArticleTags)
                          .HasForeignKey(at => at.TagId)
                          .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne(at => at.Article)
                          .WithMany(a => a.ArticleTags)
                          .HasForeignKey(at => at.ArticleId)
                          .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasKey(at => new { at.ArticleId, at.TagId }));

            modelBuilder.Entity<Core.Models.Rule>()
                .HasMany(r => r.Conditions)
                .WithOne(rc => rc.Rule)
                .HasForeignKey(rc => rc.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureValueConversions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Article>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<NotificationLog>()
                .Property(n => n.NotificationType)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<RuleCondition>()
                .Property(rc => rc.Field)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<RuleCondition>()
                .Property(rc => rc.Operator)
                .HasConversion<string>()
                .HasMaxLength(20);
        }

        private static void ConfigureQueryFilters(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Feed>()
                .HasQueryFilter(f => f.IsActive);
        }

        #endregion

        #region Database Management

        public async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default)
        {
            _logger.Information("Ensuring database is created...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Database.EnsureCreatedAsync(cancellationToken);
                await ApplySqliteOptimizationsAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Information("Database ensured in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to ensure database creation");
                throw new InvalidOperationException("Failed to create database", ex);
            }
        }

        private async Task ApplySqliteOptimizationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA cache_size = -10000;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA mmap_size = 268435456;", cancellationToken);

                _logger.Debug("SQLite optimizations applied");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Some SQLite optimizations failed, continuing");
            }
        }

        public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
        {
            var pendingMigrations = await Database.GetPendingMigrationsAsync(cancellationToken);

            if (pendingMigrations.Any())
            {
                _logger.Information("Applying {Count} pending migrations...", pendingMigrations.Count());
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await Database.MigrateAsync(cancellationToken);
                    await ApplySqliteOptimizationsAsync(cancellationToken);

                    stopwatch.Stop();
                    _logger.Information("Migrations applied in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to apply migrations");
                    throw new InvalidOperationException("Failed to apply migrations", ex);
                }
            }
        }

        public async Task BackupAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                throw new ArgumentException("Backup path cannot be null or empty", nameof(backupPath));

            if (backupPath.Contains('\0') || backupPath.Contains("';") || backupPath.Contains("--"))
                throw new ArgumentException("Invalid characters in backup path", nameof(backupPath));

            var currentDbPath = DatabasePath;
            if (string.IsNullOrEmpty(currentDbPath))
                throw new InvalidOperationException("Database path not available for backup");

            var fullPath = Path.GetFullPath(backupPath);
            var backupDir = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            _logger.Information("Starting database backup to: {BackupPath}", fullPath);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var safePath = backupPath.Replace("'", "''");
                var sql = FormattableStringFactory.Create($"VACUUM INTO '{safePath}';");
                await Database.ExecuteSqlAsync(sql, cancellationToken);

                stopwatch.Stop();
                var backupSize = new FileInfo(fullPath).Length;

                _logger.Information("Database backup completed in {ElapsedMs}ms. Size: {BackupSize:N0} bytes",
                    stopwatch.ElapsedMilliseconds, backupSize);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Backup operation failed");

                try
                {
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }
                catch { }

                throw new InvalidOperationException($"Failed to create backup: {ex.Message}", ex);
            }
        }

        public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            _logger.Warning("Resetting database (all data will be lost)...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Database.EnsureDeletedAsync(cancellationToken);
                await EnsureDatabaseCreatedAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Warning("Database reset completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database reset failed");
                throw new InvalidOperationException("Failed to reset database", ex);
            }
        }

        public async Task VacuumAsync(CancellationToken cancellationToken = default)
        {
            _logger.Information("Running database vacuum...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);

                stopwatch.Stop();
                _logger.Information("Database vacuum completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Vacuum operation failed");
                throw new InvalidOperationException("Failed to vacuum database", ex);
            }
        }

        public async Task<IRssReaderDbContext.DatabaseStats> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var dbPath = DatabasePath;
                var stats = new IRssReaderDbContext.DatabaseStats
                {
                    TotalSize = string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)
                        ? 0
                        : new FileInfo(dbPath).Length,
                    ArticleCount = await Articles.CountAsync(cancellationToken),
                    FeedCount = await Feeds.CountAsync(cancellationToken),
                    RuleCount = await Rules.CountAsync(cancellationToken),
                    NotificationCount = await NotificationLogs.CountAsync(cancellationToken)
                };

                _logger.Debug("Database statistics gathered");
                return stats;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve database statistics");
                throw;
            }
        }

        #endregion

        #region Transaction Management

        public async Task<IDbContextTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            if (isolationLevel != IsolationLevel.ReadCommitted && isolationLevel != IsolationLevel.Serializable)
            {
                isolationLevel = IsolationLevel.ReadCommitted;
            }

            return await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            if (Database.CurrentTransaction != null)
            {
                return await action();
            }

            await using var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);

            try
            {
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        #endregion

        #region Raw SQL Operations

        public async Task<List<T>> ExecuteRawQueryAsync<T>(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters) where T : class
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sql));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Set<T>()
                    .FromSqlRaw(sql, parameters)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Debug("Raw query executed in {ElapsedMs}ms. Rows: {RowCount}",
                    stopwatch.ElapsedMilliseconds, result.Count);

                return result;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("is not part of the model"))
            {
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} is not a mapped entity. Use only entity types.", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Raw query failed");
                throw;
            }
        }

        public async Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL command cannot be null or empty", nameof(sql));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var rowsAffected = await Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

                stopwatch.Stop();
                _logger.Debug("SQL command executed in {ElapsedMs}ms. Rows affected: {RowsAffected}",
                    stopwatch.ElapsedMilliseconds, rowsAffected);

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SQL command failed");
                throw;
            }
        }

        #endregion

        #region Legacy Interface Implementation

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseCreatedAsync(cancellationToken);
        }

        public Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Enhanced EF Core Methods

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.Error(ex, "Failed to save changes");

                var entityEntries = ex.Entries.Select(e => e.Entity.GetType().Name).Distinct();
                throw new DbUpdateException(
                    $"Failed to save changes affecting entities: {string.Join(", ", entityEntries)}",
                    ex);
            }
        }

        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Database.CanConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database connection test failed");
                return false;
            }
        }

        public async Task<List<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var pending = await Database.GetPendingMigrationsAsync(cancellationToken);
                return pending.ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get pending migrations");
                throw;
            }
        }

        #endregion

        #region Dispose Pattern

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _logger?.Debug("Disposing DbContext");
                base.Dispose();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _logger?.Debug("Disposing DbContext asynchronously");

                try
                {
                    await base.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Error disposing DbContext");
                    throw;
                }
                finally
                {
                    _isDisposed = true;
                    GC.SuppressFinalize(this);
                }
            }
        }

        #endregion

        #region Performance Monitoring

        public DatabasePerformanceStats GetPerformanceStats()
        {
            return new DatabasePerformanceStats
            {
                TrackedEntities = ChangeTracker.Entries().Count(),
                HasPendingChanges = ChangeTracker.HasChanges(),
                ContextId = ContextId.InstanceId.ToString()
            };
        }

        public class DatabasePerformanceStats
        {
            public int TrackedEntities { get; set; }
            public bool HasPendingChanges { get; set; }
            public string ContextId { get; set; } = string.Empty;
        }

        #endregion
    }
}