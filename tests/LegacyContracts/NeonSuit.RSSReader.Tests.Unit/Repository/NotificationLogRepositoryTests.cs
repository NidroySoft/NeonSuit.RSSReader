using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{
    [CollectionDefinition("Database_NotificationLog")]
    public class DatabaseCollectionNotificationLog : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_NotificationLog")]
    public class NotificationLogRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly NotificationLogRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private bool _disposed;

        public NotificationLogRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();

            _dbContext = fixture.Context;
            ClearTestData().Wait();
            SeedTestData().Wait();

            _repository = new NotificationLogRepository(_dbContext, _mockLogger.Object);
        }

        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext<NotificationLogRepository>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()));
            _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()));
            _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()));
            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()));
        }

        #region Test Data Helpers

        private async Task ClearTestData()
        {
            // Desactivar FK temporalmente para poder borrar en cualquier orden
            await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NotificationLogs");
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Articles");
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Rules");
                await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('NotificationLogs', 'Articles', 'Rules')");
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            }

            _dbContext.ChangeTracker.Clear();
        }

        private async Task SeedTestData()
        {
            // 1. Crear Articles necesarios primero
            var articles = new List<Article>
    {
        new Article
        {
            Id = 100,
            FeedId = 1,  // Asumiendo que Feed 1 existe del DatabaseFixture
            Title = "Test Article 100",
            Content = "Content 100",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddDays(-1),
            Status = ArticleStatus.Unread
        },
        new Article
        {
            Id = 101,
            FeedId = 1,
            Title = "Test Article 101",
            Content = "Content 101",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddDays(-2),
            Status = ArticleStatus.Unread
        },
        new Article
        {
            Id = 102,
            FeedId = 1,
            Title = "Test Article 102",
            Content = "Content 102",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddDays(-5),
            Status = ArticleStatus.Unread
        },
        new Article
        {
            Id = 103,
            FeedId = 1,
            Title = "Test Article 103",
            Content = "Content 103",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddMinutes(-30),
            Status = ArticleStatus.Unread
        },
        new Article
        {
            Id = 104,
            FeedId = 1,
            Title = "Test Article 104",
            Content = "Content 104",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddHours(-3),
            Status = ArticleStatus.Unread
        }
    };

            // 2. Crear Rules necesarios
            var rules = new List<Rule>
    {
        new Rule
        {
            Id = 10,
            Name = "Test Rule 10",
            IsEnabled = true,
            Priority = 1,
            CreatedAt = DateTime.UtcNow
        },
        new Rule
        {
            Id = 11,
            Name = "Test Rule 11",
            IsEnabled = true,
            Priority = 2,
            CreatedAt = DateTime.UtcNow
        },
        new Rule
        {
            Id = 12,
            Name = "Test Rule 12",
            IsEnabled = true,
            Priority = 3,
            CreatedAt = DateTime.UtcNow
        },
        new Rule
        {
            Id = 13,
            Name = "Test Rule 13",
            IsEnabled = true,
            Priority = 4,
            CreatedAt = DateTime.UtcNow
        }
    };

            // 3. Guardar Articles y Rules primero
            await _dbContext.Articles.AddRangeAsync(articles);
            await _dbContext.Rules.AddRangeAsync(rules);
            await _dbContext.SaveChangesAsync();

            // 4. Ahora crear los NotificationLogs (las FK ya existen)
            var testLogs = new List<NotificationLog>
    {
        new NotificationLog
        {
            Id = 1,
            ArticleId = 100,  // ← Ahora existe
            RuleId = 10,      // ← Ahora existe
            NotificationType = NotificationType.Toast,
            Title = "Test Article 1",
            Message = "New article published",
            SentAt = DateTime.UtcNow.AddDays(-1),
            Delivered = true,
            Action = NotificationAction.None,
            Channel = "Email"
        },
        new NotificationLog
        {
            Id = 2,
            ArticleId = 100,  // ← Existe
            RuleId = 10,      // ← Existe
            NotificationType = NotificationType.Toast,
            Title = "Test Article 1",
            Message = "New article published",
            SentAt = DateTime.UtcNow.AddHours(-2),
            Delivered = true,
            Action = NotificationAction.Clicked,
            ActionAt = DateTime.UtcNow.AddHours(-1.5),
            Channel = "Email"
        },
        new NotificationLog
        {
            Id = 3,
            ArticleId = 101,  // ← Existe
            RuleId = 11,      // ← Existe
            NotificationType = NotificationType.Silent,
            Title = "Daily Summary",
            Message = "3 new articles",
            SentAt = DateTime.UtcNow.AddDays(-2),
            Delivered = false,
            Error = "Failed to send email",
            Channel = "Email"
        },
        new NotificationLog
        {
            Id = 4,
            ArticleId = 102,  // ← Existe
            RuleId = 12,      // ← Existe
            NotificationType = NotificationType.Banner,
            Title = "System Alert",
            Message = "High CPU usage detected",
            SentAt = DateTime.UtcNow.AddHours(-5),
            Delivered = true,
            Action = NotificationAction.Dismissed,
            ActionAt = DateTime.UtcNow.AddHours(-4),
            Channel = "InApp"
        },
        new NotificationLog
        {
            Id = 5,
            ArticleId = 103,  // ← Existe
            RuleId = 10,      // ← Existe
            NotificationType = NotificationType.Both,
            Title = "Test Article 2",
            Message = "New article published",
            SentAt = DateTime.UtcNow.AddMinutes(-30),
            Delivered = true,
            Action = NotificationAction.None,
            Channel = "Push"
        },
        new NotificationLog
        {
            Id = 6,
            ArticleId = 104,  // ← Existe
            RuleId = 13,      // ← Existe
            NotificationType = NotificationType.Sound,
            Title = "Alert Sound",
            Message = "New notification sound",
            SentAt = DateTime.UtcNow.AddHours(-3),
            Delivered = true,
            Action = NotificationAction.None,
            Channel = "Sound"
        }
            };

            await _dbContext.NotificationLogs.AddRangeAsync(testLogs);
            await _dbContext.SaveChangesAsync();
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region GetByArticleIdAsync

        [Fact]
        public async Task GetByArticleIdAsync_WithExistingArticle_ReturnsOrderedLogs()
        {
            // Act
            var result = await _repository.GetByArticleIdAsync(100);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeInDescendingOrder(x => x.SentAt);
            result.Should().AllSatisfy(log => log.ArticleId.Should().Be(100));
        }

        [Fact]
        public async Task GetByArticleIdAsync_WithNonExistingArticle_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetByArticleIdAsync(999);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByArticleIdAsync_WithNoTracking_DoesNotAttachEntities()
        {
            // Act
            var result = await _repository.GetByArticleIdAsync(100);

            // Assert
            var entry = _dbContext.Entry(result.First());
            entry.State.Should().Be(EntityState.Detached);
        }

        #endregion

        #region GetByRuleIdAsync

        [Fact]
        public async Task GetByRuleIdAsync_WithExistingRule_ReturnsOrderedLogs()
        {
            // Act
            var result = await _repository.GetByRuleIdAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().BeInDescendingOrder(x => x.SentAt);
            result.Should().AllSatisfy(log => log.RuleId.Should().Be(10));
        }

        [Fact]
        public async Task GetByRuleIdAsync_WithNonExistingRule_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetByRuleIdAsync(999);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region GetByDateRangeAsync

        [Fact]
        public async Task GetByDateRangeAsync_WithValidRange_ReturnsLogsInRange()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-1);
            var endDate = DateTime.UtcNow;

            // Act
            var result = await _repository.GetByDateRangeAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(4);
            result.Should().AllSatisfy(log => log.SentAt.Should().BeOnOrAfter(startDate).And.BeOnOrBefore(endDate));
        }

        [Fact]
        public async Task GetByDateRangeAsync_WithNoLogsInRange_ReturnsEmptyList()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-100);
            var endDate = DateTime.UtcNow.AddDays(-90);

            // Act
            var result = await _repository.GetByDateRangeAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByDateRangeAsync_WithReversedDates_ReturnsEmptyList()
        {
            // Arrange
            var startDate = DateTime.UtcNow;
            var endDate = DateTime.UtcNow.AddDays(-1);

            // Act
            var result = await _repository.GetByDateRangeAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region GetRecentAsync

        [Fact]
        public async Task GetRecentAsync_WithDefaultLimit_Returns50MostRecent()
        {
            // Act
            var result = await _repository.GetRecentAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(6);
            result.Should().BeInDescendingOrder(x => x.SentAt);
        }

        [Fact]
        public async Task GetRecentAsync_WithCustomLimit_ReturnsSpecifiedCount()
        {
            // Act
            var result = await _repository.GetRecentAsync(3);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().BeInDescendingOrder(x => x.SentAt);
        }

        [Fact]
        public async Task GetRecentAsync_WithZeroLimit_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetRecentAsync(0);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region InsertAsync

        [Fact]
        public async Task InsertAsync_WithValidLog_AddsToDatabaseAndReturnsId()
        {
            // Arrange
            var newLog = new NotificationLog
            {
                Id = -9999,  // ✅ ID NEGATIVO ÚNICO para evitar colisión
                ArticleId = 100,
                RuleId = 10,
                NotificationType = NotificationType.Banner,
                Title = "New Test Article",
                Message = "Test message",
                Channel = "Email"
            };

            // Act
            var result = await _repository.InsertAsync(newLog);
            ClearEntityTracking();

            // ✅ Buscar por el ID que SETEAMOS, NO por el resultado
            var insertedLog = await _dbContext.NotificationLogs
                .FirstOrDefaultAsync(x => x.Id == newLog.Id);

            // Assert
            result.Should().BePositive();
            insertedLog.Should().NotBeNull();
            insertedLog!.Id.Should().Be(newLog.Id);
            insertedLog.NotificationType.Should().Be(NotificationType.Banner);
        }

        [Fact]
        public async Task InsertAsync_WithNullLog_ThrowsArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _repository.InsertAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }
        #endregion

        #region UpdateAsync

        [Fact]
        public async Task Debug_NotificationTypeConversion()
        {
            // Arrange
            var article = new Article
            {
                Id = 999,
                FeedId = 1,
                Title = "Test",
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                PublishedDate = DateTime.UtcNow,
                Status = ArticleStatus.Unread
            };
            await _dbContext.Articles.AddAsync(article);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Crear log con Sound
            var log = new NotificationLog
            {
                ArticleId = 999,
                NotificationType = NotificationType.Sound,
                Title = "Sound Test",
                Delivered = true
            };

            await _dbContext.NotificationLogs.AddAsync(log);
            await _dbContext.SaveChangesAsync();

            // Verificar raw SQL
            var rawValue = await _dbContext.Database
                .ExecuteSqlRawAsync("SELECT NotificationType FROM NotificationLogs WHERE Id = {0}", log.Id);

            // Leer con EF Core
            ClearEntityTracking();
            var inserted = await _dbContext.NotificationLogs.FindAsync(log.Id);


            // Assert
            inserted?.NotificationType.Should().Be(NotificationType.Sound);
        }

        [Fact]
        public async Task UpdateAsync_WithExistingLog_UpdatesProperties()
        {
            // Arrange
            var log = await _dbContext.NotificationLogs.FirstAsync();
            log.Title = "Updated Title";
            log.Message = "Updated Message";
            log.NotificationType = NotificationType.Banner;

            // Act
            var result = await _repository.UpdateAsync(log);
            var updatedLog = await _dbContext.NotificationLogs.FindAsync(log.Id);

            // Assert
            result.Should().Be(1);
            updatedLog!.Title.Should().Be("Updated Title");
            updatedLog.Message.Should().Be("Updated Message");
            updatedLog.NotificationType.Should().Be(NotificationType.Banner);
        }

        [Fact]
        public async Task UpdateAsync_WithNonExistingLog_ThrowsDbUpdateConcurrencyException()
        {
            // Arrange
            var nonExistentLog = new NotificationLog
            {
                Id = 999,
                ArticleId = 1,
                NotificationType = NotificationType.Banner,
                SentAt = DateTime.UtcNow
            };

            // Act & Assert
            Func<Task> act = async () => await _repository.UpdateAsync(nonExistentLog);

            // ✅ Verificar que la excepción lanzada es DbUpdateException con inner exception de concurrencia
            await act.Should().ThrowAsync<DbUpdateException>()
                .Where(e => e.InnerException is DbUpdateConcurrencyException)
                .WithMessage("*Failed to save changes affecting entities: NotificationLog*");
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_WithExistingId_RemovesFromDatabase()
        {
            // Arrange
            var logId = 1;

            // Act
            var result = await _repository.DeleteAsync(logId);
            var deletedLog = await _dbContext.NotificationLogs.FindAsync(logId);

            // Assert
            result.Should().Be(1);
            deletedLog.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistingId_ReturnsZero()
        {
            // Act
            var result = await _repository.DeleteAsync(999);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region RecordActionAsync

        [Fact]
        public async Task RecordActionAsync_WithExistingNotification_UpdatesActionAndActionAt()
        {
            // Arrange
            var notificationId = 5;
            var action = NotificationAction.Clicked;

            // Act
            var result = await _repository.RecordActionAsync(notificationId, action);
            var updatedLog = await _dbContext.NotificationLogs.FindAsync(notificationId);

            // Assert
            result.Should().Be(1);
            updatedLog!.Action.Should().Be(action);
            updatedLog.ActionAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task RecordActionAsync_WithNonExistingNotification_ReturnsZero()
        {
            // Act
            var result = await _repository.RecordActionAsync(999, NotificationAction.Clicked);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region GetStatisticsAsync

        [Fact]
        public async Task GetStatisticsAsync_WithData_ReturnsCorrectStatistics()
        {
            // Act
            var stats = await _repository.GetStatisticsAsync();

            // Assert
            stats.TotalNotifications.Should().Be(6);
            stats.ClickedNotifications.Should().Be(1);
            stats.DismissedNotifications.Should().Be(1);
            stats.FailedDeliveries.Should().Be(1);
            stats.FirstNotificationDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromSeconds(1));
            stats.LastNotificationDate.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(-30), TimeSpan.FromSeconds(1));
            stats.ClickThroughRate.Should().BeApproximately(16.67, 0.1);
        }

        [Fact]
        public async Task GetStatisticsAsync_WithNoData_ReturnsEmptyStatistics()
        {
            // Arrange
            await ClearTestData();

            // Act
            var stats = await _repository.GetStatisticsAsync();

            // Assert
            stats.TotalNotifications.Should().Be(0);
            stats.ClickedNotifications.Should().Be(0);
            stats.DismissedNotifications.Should().Be(0);
            stats.FailedDeliveries.Should().Be(0);
            stats.ClickThroughRate.Should().Be(0);
        }

        #endregion

        #region GetPendingNotificationsAsync

        [Fact]
        public async Task GetPendingNotificationsAsync_ReturnsDeliveredAndUnactedLogs()
        {
            // Act
            var result = await _repository.GetPendingNotificationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().AllSatisfy(log =>
            {
                log.Action.Should().Be(NotificationAction.None);
                log.Delivered.Should().BeTrue();
            });
        }

        #endregion

        #region CleanupOldLogsAsync


        [Fact]
        public async Task CleanupOldLogsAsync_WithDefaultRetention_RemovesLogsOlderThan30Days()
        {
            // Arrange
            // Crear el Article primero
            var article = new Article
            {
                Id = 999,
                FeedId = 1,
                Title = "Old Article",
                Content = "Content",
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                PublishedDate = DateTime.UtcNow.AddDays(-35),
                Status = ArticleStatus.Unread
            };
            await _dbContext.Articles.AddAsync(article);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var oldLog = new NotificationLog
            {
                ArticleId = 999,  // ← Ahora existe
                NotificationType = NotificationType.Silent,
                SentAt = DateTime.UtcNow.AddDays(-31),
                Delivered = true
            };
            await _dbContext.NotificationLogs.AddAsync(oldLog);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var deletedCount = await _repository.CleanupOldLogsAsync();
            var remainingOldLog = await _dbContext.NotificationLogs.FindAsync(oldLog.Id);

            // Assert
            deletedCount.Should().BePositive();
            remainingOldLog.Should().BeNull();
        }

        [Fact]
        public async Task CleanupOldLogsAsync_WithCustomRetention_RemovesLogsOlderThanSpecifiedDays()
        {
            // Arrange
            // 1. Crear el Article primero
            var article = new Article
            {
                Id = 999,
                FeedId = 1,
                Title = "Old Article",
                Content = "Content",
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                PublishedDate = DateTime.UtcNow.AddDays(-10),
                Status = ArticleStatus.Unread
            };
            await _dbContext.Articles.AddAsync(article);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // 2. Ahora crear el NotificationLog
            var oldLog = new NotificationLog
            {
                ArticleId = 999,  // ← Ahora existe
                NotificationType = NotificationType.Toast,
                SentAt = DateTime.UtcNow.AddDays(-8),
                Delivered = true
            };
            await _dbContext.NotificationLogs.AddAsync(oldLog);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var deletedCount = await _repository.CleanupOldLogsAsync(7);
            var remainingOldLog = await _dbContext.NotificationLogs.FindAsync(oldLog.Id);

            // Assert
            deletedCount.Should().BePositive();
            remainingOldLog.Should().BeNull();
        }

        #endregion

        #region GetClickThroughRateAsync

        [Fact]
        public async Task GetClickThroughRateAsync_ReturnsCorrectPercentage()
        {
            // Act
            var ctr = await _repository.GetClickThroughRateAsync();

            // Assert
            ctr.Should().BeApproximately(16.67, 0.1);
        }

        [Fact]
        public async Task GetClickThroughRateAsync_WithNoData_ReturnsZero()
        {
            // Arrange
            await ClearTestData();

            // Act
            var ctr = await _repository.GetClickThroughRateAsync();

            // Assert
            ctr.Should().Be(0);
        }

        #endregion       

        #region GetTodayCountAsync

        [Fact]
        public async Task GetTodayCountAsync_ReturnsCountOfTodayLogs()
        {
            // Act
            var count = await _repository.GetTodayCountAsync();

            // Assert - Verificar que cuenta correctamente, no importa el número exacto
            // O contar manualmente cuántos deberían ser de hoy basado en el seed
            var expectedCount = await _dbContext.NotificationLogs
                .CountAsync(n => n.SentAt.Date == DateTime.UtcNow.Date);

            count.Should().Be(expectedCount);
        }

        [Fact]
        public async Task GetTodayCountAsync_WithNoTodayLogs_ReturnsZero()
        {
            // Arrange
            await ClearTestData();

            // Act
            var count = await _repository.GetTodayCountAsync();

            // Assert
            count.Should().Be(0);
        }

        #endregion

        #region GetNotificationTypeDistributionAsync

        [Fact]
        public async Task GetNotificationTypeDistributionAsync_ReturnsCorrectDistribution()
        {
            // Act
            var distribution = await _repository.GetNotificationTypeDistributionAsync();

            // Assert
            distribution.Should().HaveCount(5);

            distribution.Should().ContainKey(NotificationType.Toast);
            distribution[NotificationType.Toast].Should().Be(2);

            distribution.Should().ContainKey(NotificationType.Sound);
            distribution[NotificationType.Sound].Should().Be(1);

            distribution.Should().ContainKey(NotificationType.Both);
            distribution[NotificationType.Both].Should().Be(1);

            distribution.Should().ContainKey(NotificationType.Silent);
            distribution[NotificationType.Silent].Should().Be(1);

            distribution.Should().ContainKey(NotificationType.Banner);
            distribution[NotificationType.Banner].Should().Be(1);
        }

        [Fact]
        public async Task GetNotificationTypeDistributionAsync_WithOnlyOneType_ReturnsSingleEntry()
        {
            // Arrange
            await ClearTestData();

            // Crear el Article primero
            var article = new Article
            {
                Id = 999,
                FeedId = 1,
                Title = "Only Banner Article",
                Content = "Content",
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                PublishedDate = DateTime.UtcNow,
                Status = ArticleStatus.Unread
            };
            await _dbContext.Articles.AddAsync(article);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var singleTypeLog = new NotificationLog
            {
                ArticleId = 999,  // ← Ahora existe
                NotificationType = NotificationType.Banner,
                Title = "Only Banner",
                Delivered = true
            };
            await _dbContext.NotificationLogs.AddAsync(singleTypeLog);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var distribution = await _repository.GetNotificationTypeDistributionAsync();

            // Assert
            distribution.Should().HaveCount(1);
            distribution.Should().ContainKey(NotificationType.Banner);
            distribution[NotificationType.Banner].Should().Be(1);
        }

        [Fact]
        public async Task GetNotificationTypeDistributionAsync_WithNoData_ReturnsEmptyDictionary()
        {
            // Arrange
            await ClearTestData();

            // Act
            var distribution = await _repository.GetNotificationTypeDistributionAsync();

            // Assert
            distribution.Should().NotBeNull();
            distribution.Should().BeEmpty();
        }

        [Fact]
        public async Task GetNotificationTypeDistributionAsync_EnumValues_MatchExpectedIntegers()
        {
            // Act
            var distribution = await _repository.GetNotificationTypeDistributionAsync();
            var keys = distribution.Keys.ToList();

            // Assert
            keys.Should().Contain(NotificationType.Toast);
            ((int)NotificationType.Toast).Should().Be(0);

            keys.Should().Contain(NotificationType.Sound);
            ((int)NotificationType.Sound).Should().Be(1);

            keys.Should().Contain(NotificationType.Both);
            ((int)NotificationType.Both).Should().Be(2);

            keys.Should().Contain(NotificationType.Silent);
            ((int)NotificationType.Silent).Should().Be(3);

            keys.Should().Contain(NotificationType.Banner);
            ((int)NotificationType.Banner).Should().Be(4);
        }

        #endregion

        #region Performance Tests

        [Fact(Skip = "Performance test - run manually")]
        public async Task GetByDateRangeAsync_Performance_Under100ms()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await _repository.GetByDateRangeAsync(startDate, endDate);

            // Assert
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        }

        [Fact(Skip = "Performance test - run manually")]
        public async Task CleanupOldLogsAsync_Performance_Under200ms()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await _repository.CleanupOldLogsAsync(1);

            // Assert
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
        }

        [Fact(Skip = "Performance test - run manually")]
        public async Task GetNotificationTypeDistributionAsync_Performance_Under50ms()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await _repository.GetNotificationTypeDistributionAsync();

            // Assert
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mockLogger?.Reset();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}