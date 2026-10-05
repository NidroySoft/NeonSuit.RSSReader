using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    /// <summary>
    /// Integration tests for <see cref="NotificationService"/>.
    /// Validates end-to-end notification workflows with real database operations.
    /// </summary>
    [Collection("Integration Tests")]
    public class NotificationServiceIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _dbFixture;
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        private readonly ServiceFactory _factory;

        private NotificationService _notificationService = null!;
        private INotificationLogRepository _notificationLogRepository = null!;
        private RssReaderDbContext _dbContext = null!;

        public NotificationServiceIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
        {
            _dbFixture = dbFixture;
            _output = output;
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger()
                .ForContext<NotificationServiceIntegrationTests>();

            _factory = new ServiceFactory(_dbFixture);
        }

        public async Task InitializeAsync()
        {
            _dbContext = _dbFixture.CreateNewDbContext();
            _notificationLogRepository = new NotificationLogRepository(_dbContext, _logger);
            _notificationService = new NotificationService(_notificationLogRepository, _logger);

            await SetupTestDataAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task SetupTestDataAsync()
        {
            // Crear un artículo de prueba
            var article = new Article
            {
                Id = 1,
                Title = "Breaking: Important RSS Update",
                Summary = "This is a test summary for notification testing.",
                Categories = "Technology,News",
                PublishedDate = DateTime.UtcNow.AddMinutes(-10),
                AddedDate = DateTime.UtcNow.AddMinutes(-10)
            };
            _dbContext.Articles.Add(article);

            await _dbContext.SaveChangesAsync();
            _output.WriteLine("Test article created with ID: {0}", article.Id);
        }

        [Fact]
        public async Task SendNotificationAsync_WithValidArticle_ShouldCreateAndPersistLog()
        {
            // Arrange
            var article = await _dbContext.Articles.FirstAsync();
            var rule = new Rule { Id = 1, Name = "Tech Alerts" };

            // Act
            var notification = await _notificationService.SendNotificationAsync(
                article,
                rule,
                NotificationType.Both,
                NotificationPriority.High);

            // Assert
            notification.Should().NotBeNull();
            notification!.ArticleId.Should().Be(article.Id);
            notification.RuleId.Should().Be(rule.Id);
            notification.NotificationType.Should().Be(NotificationType.Both);
            notification.Priority.Should().Be(NotificationPriority.High);
            notification.Title.Should().Contain("Tech Alerts");
            notification.Message.Should().Contain("Important RSS Update");
            notification.Delivered.Should().BeTrue();
            notification.SentAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));

            // Verificar persistencia en BD
            var logFromDb = await _dbContext.NotificationLogs
                .FirstOrDefaultAsync(l => l.Id == notification.Id);

            logFromDb.Should().NotBeNull();
            logFromDb!.ArticleId.Should().Be(article.Id);
        }

        [Fact]
        public async Task SendNotificationAsync_WhenRecentNotificationExists_ShouldSuppressDuplicate()
        {
            // Arrange
            var article = await _dbContext.Articles.FirstAsync();

            // Crear notificación reciente (hace 2 minutos)
            var recentLog = new NotificationLog
            {
                ArticleId = article.Id,
                NotificationType = NotificationType.Toast,
                Priority = NotificationPriority.Normal,
                Title = "Test",
                Message = "Recent",
                Delivered = true,
                SentAt = DateTime.UtcNow.AddMinutes(-2)
            };
            _dbContext.NotificationLogs.Add(recentLog);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _notificationService.SendNotificationAsync(article);

            // Assert
            result.Should().BeNull("Debería suprimir notificación duplicada reciente");
        }

        [Fact]
        public async Task RecordNotificationActionAsync_WithValidId_ShouldUpdateAction()
        {
            // Arrange
            var article = await _dbContext.Articles.FirstAsync();
            var notification = await _notificationService.SendNotificationAsync(article);

            // Act
            var success = await _notificationService.RecordNotificationActionAsync(
                notification!.Id,
                NotificationAction.Clicked);

            // Assert
            success.Should().BeTrue();

            var updatedLog = await _dbContext.NotificationLogs.FindAsync(notification.Id);
            updatedLog.Should().NotBeNull();
            updatedLog!.Action.Should().Be(NotificationAction.Clicked);
            updatedLog.ActionAt.Should().NotBeNull();
            updatedLog.ActionAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task GetRecentNotificationsAsync_ShouldReturnLatestLogs()
        {
            // Arrange - Crear 3 artículos distintos para evitar supresión por duplicado
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var art = new Article
                {
                    Title = $"Recent Test Article {i}",
                    Summary = $"Test summary {i}",
                    Categories = "Test"
                };
                _dbContext.Articles.Add(art);
                await _dbContext.SaveChangesAsync();  // genera Id real y guarda
                articles.Add(art);
            }

            // Crear 3 notificaciones con diferentes artículos (en orden inverso para probar "latest")
            var logs = new List<NotificationLog>();

            // Primera (más vieja)
            var n1 = await _notificationService.SendNotificationAsync(articles[0]);
            logs.Add(n1!);
            await Task.Delay(100);  // pequeño delay para orden temporal

            // Segunda
            var n2 = await _notificationService.SendNotificationAsync(articles[1]);
            logs.Add(n2!);
            await Task.Delay(100);

            // Tercera (más reciente)
            var n3 = await _notificationService.SendNotificationAsync(articles[2]);
            logs.Add(n3!);

            // Act - Pedir los últimos 3 (deben venir en orden descendente por SentAt)
            var recent = await _notificationService.GetRecentNotificationsAsync(limit: 3);

            // Assert
            recent.Should().HaveCount(3, "Deben devolverse exactamente 3 notificaciones recientes");

            // Verificar orden descendente (la más reciente primero)
            recent.Should().BeInDescendingOrder(n => n.SentAt);

            // Verificar que son las correctas (por ArticleId o Title)
            recent.Select(n => n.ArticleId).Should().ContainInOrder(
                articles[2].Id,  // más reciente
                articles[1].Id,
                articles[0].Id   // más vieja
            );

            // Opcional: verificar propiedades básicas
            recent[0].Title.Should().Contain("Recent Test Article 3");
            recent[0].Delivered.Should().BeTrue();
        }

        [Fact]
        public async Task CleanupOldNotificationsAsync_ShouldRemoveOldLogs()
        {
            // Arrange
            var article = await _dbContext.Articles.FirstAsync();

            // Crear notificación vieja
            var oldLog = new NotificationLog
            {
                ArticleId = article.Id,
                Title = "Old",
                Message = "Old notification",
                SentAt = DateTime.UtcNow.AddDays(-45)
            };
            _dbContext.NotificationLogs.Add(oldLog);

            // Crear notificación reciente
            await _notificationService.SendNotificationAsync(article);

            await _dbContext.SaveChangesAsync();

            // Act
            var deleted = await _notificationService.CleanupOldNotificationsAsync(retentionDays: 30);

            // Assert
            deleted.Should().Be(1);

            var remaining = await _dbContext.NotificationLogs.CountAsync();
            remaining.Should().Be(1); // solo la reciente
        }


        [Fact]
        public async Task MarkAllAsReadAsync_ShouldDismissAllPending()
        {
            // Arrange - Crear 3 artículos distintos para evitar supresión por duplicado
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var art = new Article
                {
                    Title = $"Pending Test Article {i}",
                    Summary = "Test summary",
                    Categories = "Test"
                };
                _dbContext.Articles.Add(art);
                await _dbContext.SaveChangesAsync();
                articles.Add(art);
            }

            // Crear 2 notificaciones pendientes (sin acción)
            await _notificationService.SendNotificationAsync(articles[0]);
            await _notificationService.SendNotificationAsync(articles[1]);

            // Crear una tercera que ya esté interactuada (para verificar que no se marca de nuevo)
            var interactedNotif = await _notificationService.SendNotificationAsync(articles[2]);
            await _notificationService.RecordNotificationActionAsync(interactedNotif!.Id, NotificationAction.Clicked);

            // Act
            var marked = await _notificationService.MarkAllAsReadAsync();

            // Assert
            marked.Should().Be(2, "Deben marcarse como leídas las 2 pendientes");

            var pendingAfter = await _notificationService.GetPendingNotificationsAsync();
            pendingAfter.Should().BeEmpty("No deben quedar notificaciones pendientes");

            // Verificar que la interactuada no se tocó
            var interactedLog = await _dbContext.NotificationLogs.FindAsync(interactedNotif.Id);
            interactedLog!.Action.Should().Be(NotificationAction.Clicked, "La ya interactuada no debe cambiar");
        }

        [Fact]
        public async Task GetNotificationStatisticsAsync_ShouldReturnAccurateStats()
        {
            // Arrange - Crear 4 artículos distintos (EF genera Ids)
            var articles = new List<Article>();
            for (int i = 0; i < 4; i++)
            {
                var art = new Article
                {
                    Title = $"Test Article {i + 1}",
                    Summary = "Test summary",
                    Categories = "Test"
                };
                _dbContext.Articles.Add(art);
                await _dbContext.SaveChangesAsync();  // genera Id real
                articles.Add(art);
            }

            // 1. Normal
            await _notificationService.SendNotificationAsync(articles[0], priority: NotificationPriority.Normal);

            // 2. Clickeada
            var clickedNotif = await _notificationService.SendNotificationAsync(articles[1]);
            await _notificationService.RecordNotificationActionAsync(clickedNotif!.Id, NotificationAction.Clicked);

            // 3. Descartada
            var dismissedNotif = await _notificationService.SendNotificationAsync(articles[2]);
            await _notificationService.RecordNotificationActionAsync(dismissedNotif!.Id, NotificationAction.Dismissed);

            // 4. Fallida manual
            var failedLog = new NotificationLog
            {
                ArticleId = articles[3].Id,
                Title = "Delivery Failed",
                Message = "Test failure",
                Delivered = false,
                Error = "Connection timeout",
                SentAt = DateTime.UtcNow.AddMinutes(-10)
            };
            _dbContext.NotificationLogs.Add(failedLog);
            await _dbContext.SaveChangesAsync();

            // Act
            var stats = await _notificationService.GetNotificationStatisticsAsync();

            // Assert
            stats.TotalNotifications.Should().Be(4);
            stats.ClickedNotifications.Should().Be(1);
            stats.DismissedNotifications.Should().Be(1);
            stats.FailedDeliveries.Should().Be(1);
            stats.ClickThroughRate.Should().BeApproximately(25.0, 1.0);
            stats.ClickThroughRateFormatted.Should().Be("25.00%");

            stats.FirstNotificationDate.Should().NotBeNull();
            stats.LastNotificationDate.Should().NotBeNull();
        }

        [Fact]
        public async Task GetPendingNotificationsAsync_ShouldReturnOnlyUninteractedNotifications()
        {
            // Arrange - Crear 3 artículos distintos para evitar supresión
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var art = new Article
                {
                    Title = $"Pending Test Article {i}",
                    Summary = "Test summary",
                    Categories = "Test"
                };
                _dbContext.Articles.Add(art);
                await _dbContext.SaveChangesAsync();
                articles.Add(art);
            }

            // 1. Notificación pendiente (sin acción)
            await _notificationService.SendNotificationAsync(articles[0]);

            // 2. Clickeada (ya no pendiente)
            var clicked = await _notificationService.SendNotificationAsync(articles[1]);
            await _notificationService.RecordNotificationActionAsync(clicked!.Id, NotificationAction.Clicked);

            // 3. Descartada (ya no pendiente)
            var dismissed = await _notificationService.SendNotificationAsync(articles[2]);
            await _notificationService.RecordNotificationActionAsync(dismissed!.Id, NotificationAction.Dismissed);

            // Act
            var pending = await _notificationService.GetPendingNotificationsAsync();

            // Assert
            pending.Should().HaveCount(1, "Solo la primera debe estar pendiente");
            pending[0].Action.Should().Be(NotificationAction.None, "Debe tener acción None");
            pending[0].Delivered.Should().BeTrue("Debe estar entregada");
            pending[0].ArticleId.Should().Be(articles[0].Id, "Debe corresponder al artículo pendiente");

            // Opcional: verificar que las otras NO están pendientes
            var all = await _notificationLogRepository.GetRecentAsync(10);
            all.Count(n => n.Action != NotificationAction.None).Should().Be(2);
        }
    }
}