using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for the <see cref="NotificationService"/> class.
    /// Validates notification creation, delivery, and management operations.
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly Mock<INotificationLogRepository> _mockNotificationLogRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly NotificationService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationServiceTests"/> class.
        /// </summary>
        public NotificationServiceTests()
        {
            _mockNotificationLogRepository = new Mock<INotificationLogRepository>();
            _mockLogger = new Mock<ILogger>();

            _mockLogger.Setup(x => x.ForContext<NotificationService>())
                       .Returns(_mockLogger.Object);
            _service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullNotificationLogRepository_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new NotificationService(null!, _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("notificationLogRepository");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new NotificationService(_mockNotificationLogRepository.Object, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeOfType<NotificationService>();
        }

        #endregion

        #region SendNotificationAsync Tests

        [Fact]
        public async Task SendNotificationAsync_WithNullArticle_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Act
            Func<Task> act = async () => await service.SendNotificationAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("article");
        }

        [Fact]
        public async Task SendNotificationAsync_WhenShouldNotifyReturnsFalse_ShouldReturnNull()
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Mock ShouldNotifyAsync to return false
            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>
                {
            new NotificationLog { ArticleId = 1, SentAt = DateTime.UtcNow.AddMinutes(-4) }
                });

            // Act
            var result = await service.SendNotificationAsync(article);

            // Assert
            result.Should().BeNull();
            _mockNotificationLogRepository.Verify(x => x.InsertAsync(It.IsAny<NotificationLog>()), Times.Never);

            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(10), Times.Once);
        }
        [Fact]
        public async Task SendNotificationAsync_WhenShouldNotifyReturnsTrue_ShouldCreateNotification()
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article", Summary = "Test Summary" };
            var rule = new Rule { Id = 1, Name = "Test Rule", NotificationTemplate = null };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .Callback<NotificationLog>(n => n.Id = 1)
                .ReturnsAsync(1);

            // Act
            var result = await service.SendNotificationAsync(article, rule, NotificationType.Toast, NotificationPriority.High);

            // Assert
            result.Should().NotBeNull();
            result!.ArticleId.Should().Be(1);
            result.RuleId.Should().Be(1);
            result.NotificationType.Should().Be(NotificationType.Toast);
            result.Priority.Should().Be(NotificationPriority.High);
            result.Title.Should().Be("Test Rule: Test Article");
            result.Message.Should().Be("Test Summary");
            result.Duration.Should().Be(10);
            result.Delivered.Should().BeTrue();

            _mockNotificationLogRepository.Verify(x => x.InsertAsync(It.IsAny<NotificationLog>()), Times.Once);

            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(10), Times.Once);
        }

        [Fact]
        public async Task SendNotificationAsync_Debug_CheckMessageGeneration()
        {
            // Arrange
            var article = new Article
            {
                Id = 1,
                Title = "Test Article",
                Summary = "Test Summary",
                Content = "Test Content"
            };
            var rule = new Rule
            {
                Id = 1,
                Name = "Test Rule",
                NotificationTemplate = null // Explícitamente null
            };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            NotificationLog? capturedNotification = null;
            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .Callback<NotificationLog>(n =>
                {
                    n.Id = 1;
                    capturedNotification = n;
                })
                .ReturnsAsync(1);

            // Act
            var result = await service.SendNotificationAsync(article, rule, NotificationType.Toast, NotificationPriority.High);

            // Debug
            Console.WriteLine($"Title: {result?.Title}");
            Console.WriteLine($"Message: {result?.Message}");
            Console.WriteLine($"Message length: {result?.Message?.Length}");
            Console.WriteLine($"Has rule template: {!string.IsNullOrEmpty(rule.NotificationTemplate)}");

            // También verifica la notificación capturada
            Console.WriteLine($"Captured Message: {capturedNotification?.Message}");

            // Assert
            result.Should().NotBeNull();
            result!.Message.Should().Be("Test Summary"); // Esto fallará si hay bug
        }

        [Fact]
        public async Task SendNotificationAsync_ShouldInvokeOnNotificationCreatedEvent()
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .Callback<NotificationLog>(n => n.Id = 1)
                .ReturnsAsync(1);

            NotificationLog? capturedNotification = null;
            service.OnNotificationCreated += (sender, notification) => capturedNotification = notification;

            // Act
            var result = await service.SendNotificationAsync(article);

            // Assert
            capturedNotification.Should().NotBeNull();
            capturedNotification.Should().BeEquivalentTo(result);
        }

        [Fact]
        public async Task SendNotificationAsync_WhenRepositoryThrows_ShouldLogErrorAndRethrow()
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            Func<Task> act = async () => await service.SendNotificationAsync(article);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Database error");

            _mockNotificationLogRepository.Verify(x => x.InsertAsync(It.IsAny<NotificationLog>()), Times.Exactly(2));

            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(10), Times.Once);
        }

        [Fact]
        public async Task SendNotificationAsync_WhenExceptionOccurs_ShouldCreateFailedNotificationLog()
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .ThrowsAsync(new Exception("Database error"));

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.Is<NotificationLog>(n => !n.Delivered)))
                .ReturnsAsync(1);

            // Act
            Func<Task> act = async () => await service.SendNotificationAsync(article);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _mockNotificationLogRepository.Verify(x => x.InsertAsync(It.Is<NotificationLog>(n => !n.Delivered && n.Error == "Database error")), Times.Once);
        }

        [Theory]
        [InlineData(NotificationPriority.Low, 5)]
        [InlineData(NotificationPriority.Normal, 7)]
        [InlineData(NotificationPriority.High, 10)]
        [InlineData(NotificationPriority.Critical, 15)]
        public async Task SendNotificationAsync_ShouldSetCorrectDurationBasedOnPriority(
            NotificationPriority priority, int expectedDuration)
        {
            // Arrange
            var article = new Article { Id = 1, Title = "Test Article" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            _mockNotificationLogRepository.Setup(x => x.InsertAsync(It.IsAny<NotificationLog>()))
                .Callback<NotificationLog>(n => n.Id = 1)
                .ReturnsAsync(1);

            // Act
            var result = await service.SendNotificationAsync(article, notificationType: NotificationType.Toast, priority: priority);

            // Assert
            result.Should().NotBeNull();
            result!.Duration.Should().Be(expectedDuration);
        }

        #endregion

        #region RecordNotificationActionAsync Tests

        [Fact]
        public async Task RecordNotificationActionAsync_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(1, NotificationAction.Clicked))
                .ReturnsAsync(1);

            // Act
            var result = await service.RecordNotificationActionAsync(1, NotificationAction.Clicked);

            // Assert
            result.Should().BeTrue();

            _mockNotificationLogRepository.Verify(x => x.RecordActionAsync(1, NotificationAction.Clicked), Times.Once);
        }

        [Fact]
        public async Task RecordNotificationActionAsync_WhenNoRowsUpdated_ShouldReturnFalse()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(999, NotificationAction.Clicked))
                .ReturnsAsync(0);

            // Act
            var result = await service.RecordNotificationActionAsync(999, NotificationAction.Clicked);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task RecordNotificationActionAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(1, NotificationAction.Clicked))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.RecordNotificationActionAsync(1, NotificationAction.Clicked);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockNotificationLogRepository.Verify(x => x.RecordActionAsync(1, NotificationAction.Clicked), Times.Once);
        }

        #endregion

        #region GetRecentNotificationsAsync Tests

        [Fact]
        public async Task GetRecentNotificationsAsync_WithDefaultLimit_ShouldReturn20Notifications()
        {
            // Arrange
            var expectedNotifications = Enumerable.Range(1, 20)
                .Select(i => new NotificationLog { Id = i, Title = $"Notification {i}" })
                .ToList();

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(20))
                .ReturnsAsync(expectedNotifications);

            // Act
            var result = await service.GetRecentNotificationsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedNotifications);
            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(20), Times.Once);
        }

        [Fact]
        public async Task GetRecentNotificationsAsync_WithCustomLimit_ShouldReturnSpecifiedNumberOfNotifications()
        {
            // Arrange
            var expectedNotifications = Enumerable.Range(1, 10)
                .Select(i => new NotificationLog { Id = i, Title = $"Notification {i}" })
                .ToList();

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(expectedNotifications);

            // Act
            var result = await service.GetRecentNotificationsAsync(10);

            // Assert
            result.Should().BeEquivalentTo(expectedNotifications);
            result.Should().HaveCount(10);
        }

        [Fact]
        public async Task GetRecentNotificationsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(20))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.GetRecentNotificationsAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(20), Times.Once);
        }

        #endregion

        #region GetPendingNotificationsAsync Tests

        [Fact]
        public async Task GetPendingNotificationsAsync_ShouldReturnPendingNotifications()
        {
            // Arrange
            var expectedNotifications = new List<NotificationLog>
            {
                new NotificationLog { Id = 1, Title = "Pending 1" },
                new NotificationLog { Id = 2, Title = "Pending 2" }
            };

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ReturnsAsync(expectedNotifications);

            // Act
            var result = await service.GetPendingNotificationsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedNotifications);
            _mockNotificationLogRepository.Verify(x => x.GetPendingNotificationsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetPendingNotificationsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.GetPendingNotificationsAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockNotificationLogRepository.Verify(x => x.GetPendingNotificationsAsync(), Times.Once);
        }

        #endregion

        #region MarkAllAsReadAsync Tests

        [Fact]
        public async Task MarkAllAsReadAsync_ShouldDismissAllPendingNotifications()
        {
            // Arrange
            var pendingNotifications = new List<NotificationLog>
            {
                new NotificationLog { Id = 1 },
                new NotificationLog { Id = 2 }
            };

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ReturnsAsync(pendingNotifications);

            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(It.IsAny<int>(), NotificationAction.Dismissed))
                .ReturnsAsync(1);

            // Act
            var result = await service.MarkAllAsReadAsync();

            // Assert
            result.Should().Be(2);
            _mockNotificationLogRepository.Verify(x => x.RecordActionAsync(1, NotificationAction.Dismissed), Times.Once);
            _mockNotificationLogRepository.Verify(x => x.RecordActionAsync(2, NotificationAction.Dismissed), Times.Once);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_WhenNoPendingNotifications_ShouldReturnZero()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ReturnsAsync(new List<NotificationLog>());

            // Act
            var result = await service.MarkAllAsReadAsync();

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_WhenActionFailsForSome_ShouldContinueAndReturnCount()
        {
            // Arrange
            var pendingNotifications = new List<NotificationLog>
            {
                new NotificationLog { Id = 1 },
                new NotificationLog { Id = 2 }
            };

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ReturnsAsync(pendingNotifications);

            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(1, NotificationAction.Dismissed))
                .ReturnsAsync(1);

            _mockNotificationLogRepository.Setup(x => x.RecordActionAsync(2, NotificationAction.Dismissed))
                .ReturnsAsync(0); // This one fails

            // Act
            var result = await service.MarkAllAsReadAsync();

            // Assert
            result.Should().Be(1); // Only one succeeded
        }

        [Fact]
        public async Task MarkAllAsReadAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetPendingNotificationsAsync())
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.MarkAllAsReadAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockNotificationLogRepository.Verify(x => x.GetPendingNotificationsAsync(), Times.Once);
        }

        #endregion

        #region GetNotificationStatisticsAsync Tests
        [Fact]
        public async Task GetNotificationStatisticsAsync_ShouldReturnStatistics()
        {
            // Arrange
            var expectedStats = new NotificationStatistics
            {
                TotalNotifications = 100,
                ClickedNotifications = 25,
                DismissedNotifications = 50,
                FailedDeliveries = 20,
                ClickThroughRate = 25.0, // 25/100 * 100 = 25%
                FirstNotificationDate = DateTime.UtcNow.AddDays(-30),
                LastNotificationDate = DateTime.UtcNow
            };

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetStatisticsAsync())
                .ReturnsAsync(expectedStats);

            // Act
            var result = await service.GetNotificationStatisticsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedStats);
            _mockNotificationLogRepository.Verify(x => x.GetStatisticsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetNotificationStatisticsAsync_WhenNoNotifications_ShouldReturnEmptyStatistics()
        {
            // Arrange
            var expectedStats = new NotificationStatistics(); // Todas las propiedades en 0/default

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetStatisticsAsync())
                .ReturnsAsync(expectedStats);

            // Act
            var result = await service.GetNotificationStatisticsAsync();

            // Assert
            result.Should().NotBeNull();
            result.TotalNotifications.Should().Be(0);
            result.ClickedNotifications.Should().Be(0);
            result.DismissedNotifications.Should().Be(0);
            result.FailedDeliveries.Should().Be(0);
            result.ClickThroughRate.Should().Be(0);
        }

        [Fact]
        public async Task GetNotificationStatisticsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetStatisticsAsync())
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.GetNotificationStatisticsAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockNotificationLogRepository.Verify(x => x.GetStatisticsAsync(), Times.Once);
        }

            #endregion

            #region CleanupOldNotificationsAsync Tests

            [Fact]
        public async Task CleanupOldNotificationsAsync_WithDefaultRetention_ShouldCleanup30DaysOld()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.CleanupOldLogsAsync(30))
                .ReturnsAsync(50);

            // Act
            var result = await service.CleanupOldNotificationsAsync();

            // Assert
            result.Should().Be(50);
            _mockNotificationLogRepository.Verify(x => x.CleanupOldLogsAsync(30), Times.Once);
        }

        [Fact]
        public async Task CleanupOldNotificationsAsync_WithCustomRetention_ShouldCleanupWithSpecifiedDays()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.CleanupOldLogsAsync(7))
                .ReturnsAsync(25);

            // Act
            var result = await service.CleanupOldNotificationsAsync(7);

            // Assert
            result.Should().Be(25);
            _mockNotificationLogRepository.Verify(x => x.CleanupOldLogsAsync(7), Times.Once);
        }
        [Fact]
        public async Task CleanupOldNotificationsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.CleanupOldLogsAsync(30))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await service.CleanupOldNotificationsAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");

            _mockNotificationLogRepository.Verify(
                x => x.CleanupOldLogsAsync(30),
                Times.Once);
        }

        #endregion

        #region ShouldNotifyAsync Tests

        [Fact]
        public async Task ShouldNotifyAsync_WhenRecentNotificationExists_ShouldReturnFalse()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            var recentNotifications = new List<NotificationLog>
            {
                new NotificationLog
                {
                    ArticleId = 1,
                    RuleId = 1,
                    SentAt = DateTime.UtcNow.AddMinutes(-4) // Less than 5 minutes ago
                }
            };

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(recentNotifications);

            // Act
            var result = await service.ShouldNotifyAsync(1, 1);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldNotifyAsync_WhenNoRecentNotification_ShouldReturnTrue()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(new List<NotificationLog>());

            // Act
            var result = await service.ShouldNotifyAsync(1);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldNotifyAsync_WhenNotificationOlderThan5Minutes_ShouldReturnTrue()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            var recentNotifications = new List<NotificationLog>
            {
                new NotificationLog
                {
                    ArticleId = 1,
                    RuleId = 1,
                    SentAt = DateTime.UtcNow.AddMinutes(-6) // More than 5 minutes ago
                }
            };

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ReturnsAsync(recentNotifications);

            // Act
            var result = await service.ShouldNotifyAsync(1, 1);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldNotifyAsync_WhenRepositoryThrows_ShouldReturnFalse()
        {
            // Arrange
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            _mockNotificationLogRepository.Setup(x => x.GetRecentAsync(10))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            var result = await service.ShouldNotifyAsync(1);

            // Assert
            result.Should().BeFalse();
            _mockNotificationLogRepository.Verify(x => x.GetRecentAsync(10), Times.Once);
        }

        #endregion

        #region GetArticleNotificationHistoryAsync Tests

        [Fact]
        public async Task GetArticleNotificationHistoryAsync_ShouldReturnArticleHistory()
        {
            // Arrange
            var expectedHistory = new List<NotificationLog>
            {
                new NotificationLog { Id = 1, ArticleId = 1 },
                new NotificationLog { Id = 2, ArticleId = 1 }
            };

            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);
            _mockNotificationLogRepository.Setup(x => x.GetByArticleIdAsync(1))
                .ReturnsAsync(expectedHistory);

            // Act
            var result = await service.GetArticleNotificationHistoryAsync(1);

            // Assert
            result.Should().BeEquivalentTo(expectedHistory);
            _mockNotificationLogRepository.Verify(x => x.GetByArticleIdAsync(1), Times.Once);
        }
        [Fact]
        public async Task GetArticleNotificationHistoryAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            // Usa _service (ya creada en el constructor)
            _mockNotificationLogRepository.Setup(x => x.GetByArticleIdAsync(1))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await _service.GetArticleNotificationHistoryAsync(1);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");

            //_mockLogger.Verify(
            //    x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()),
            //    Times.Once);

            _mockNotificationLogRepository.Verify(x => x.GetByArticleIdAsync(1), Times.Once);
        }

        #endregion

        #region Helper Methods Tests

        [Theory]
        [InlineData("Short Title", "Short Title")]
        [InlineData("Very Long Title That Exceeds One Hundred Characters And Should Be Truncated To Fit Within The Maximum Allowed Length",
                    "Very Long Title That Exceeds One Hundred Characters And Should Be Truncated To Fit Within The Max...")]
        public void GenerateNotificationTitle_ShouldHandleTitleLength(string inputTitle, string expectedTitle)
        {
            // Arrange
            var article = new Article { Title = inputTitle };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationTitle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, null }) as string;

            // Assert
            result.Should().Be(expectedTitle);
        }

        [Fact]
        public void GenerateNotificationTitle_WithRule_ShouldIncludeRuleName()
        {
            // Arrange
            var article = new Article { Title = "Article Title" };
            var rule = new Rule { Name = "Important Rule" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationTitle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, rule }) as string;

            // Assert
            result.Should().Be("Important Rule: Article Title");
        }

        [Theory]
        [InlineData("Short summary", "Short summary")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void GenerateNotificationMessage_ShouldHandleSummary(string summary, string expectedMessage)
        {
            // Arrange
            var article = new Article { Summary = summary };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, null }) as string;

            // Assert
            result.Should().Be(expectedMessage);
        }

        [Fact]
        public void GenerateNotificationMessage_WithRuleTemplate_ShouldFormatTemplate()
        {
            // Arrange
            var article = new Article { Title = "Breaking News", Summary = "Important update" };
            var rule = new Rule
            {
                Name = "Breaking",
                NotificationTemplate = "{RuleName}: {Title} - {Summary}"
            };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, rule }) as string;

            // Assert
            result.Should().Be("Breaking: Breaking News - Important update");
        }

        [Fact]
        public void GenerateNotificationTags_ShouldCreateTagsFromArticleAndRule()
        {
            // Arrange
            var article = new Article { Categories = "Technology,News" };
            var rule = new Rule { Name = "TechAlerts" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationTags",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, rule }) as string;

            // Assert
            result.Should().Be("Rule:TechAlerts,Category:Technology,News");
        }

        [Fact]
        public void GenerateNotificationTags_WithNullRule_ShouldCreateTagsOnlyFromArticle()
        {
            // Arrange
            var article = new Article { Categories = "Technology" };
            var service = new NotificationService(_mockNotificationLogRepository.Object, _mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(NotificationService).GetMethod("GenerateNotificationTags",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { article, null }) as string;

            // Assert
            result.Should().Be("Category:Technology");
        }
        #endregion
    }
}