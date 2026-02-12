using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for notification management and delivery.
    /// </summary>
    public interface INotificationService
    {
        // Evento para que el Frontend (WPF) capture la notificación y muestre el Toast
        event EventHandler<NotificationLog>? OnNotificationCreated;

        Task<NotificationLog?> SendNotificationAsync(
            Article article,
            Rule? rule = null,
            NotificationType notificationType = NotificationType.Toast,
            NotificationPriority priority = NotificationPriority.Normal);

        Task<bool> RecordNotificationActionAsync(int notificationId, NotificationAction action);
        Task<List<NotificationLog>> GetRecentNotificationsAsync(int limit = 20);
        Task<List<NotificationLog>> GetPendingNotificationsAsync();
        Task<int> MarkAllAsReadAsync();
        Task<NotificationStatistics> GetNotificationStatisticsAsync();
        Task<int> CleanupOldNotificationsAsync(int retentionDays = 30);
        Task<bool> ShouldNotifyAsync(int articleId, int? ruleId = null);
        Task<List<NotificationLog>> GetArticleNotificationHistoryAsync(int articleId);
    }
}