using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationLogRepository _notificationLogRepository;
        private readonly ILogger _logger;

        // Implementación del evento
        public event EventHandler<NotificationLog>? OnNotificationCreated;

        public NotificationService(
         INotificationLogRepository notificationLogRepository,
         ILogger logger)
        {
            _notificationLogRepository = notificationLogRepository ??
                throw new ArgumentNullException(nameof(notificationLogRepository));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger)))
         .ForContext<NotificationService>();
        }

        public async Task<NotificationLog?> SendNotificationAsync(
            Article article,
            Rule? rule = null,
            NotificationType notificationType = NotificationType.Toast,
            NotificationPriority priority = NotificationPriority.Normal)
        {
            try
            {
                if (article == null) throw new ArgumentNullException(nameof(article));

                _logger.Information("Sending notification for article: {ArticleTitle} (ID: {ArticleId})",
                    article.Title, article.Id);

                if (!await ShouldNotifyAsync(article.Id, rule?.Id))
                {
                    _logger.Debug("Notification suppressed for article ID: {ArticleId}", article.Id);
                    return null;
                }

                var notificationLog = new NotificationLog
                {
                    ArticleId = article.Id,
                    RuleId = rule?.Id,
                    NotificationType = notificationType,
                    Priority = priority,
                    Title = GenerateNotificationTitle(article, rule),
                    Message = GenerateNotificationMessage(article, rule),
                    Duration = GetNotificationDuration(priority),
                    Tags = GenerateNotificationTags(article, rule),
                    Delivered = true,
                    SentAt = DateTime.UtcNow
                };

                await _notificationLogRepository.InsertAsync(notificationLog);

                // --- DISPARO DEL EVENTO ---
                // Esto permite que el WPF muestre el Toast sin que esta clase sepa qué es Windows
                OnNotificationCreated?.Invoke(this, notificationLog);

                _logger.Information("Notification sent successfully for article ID: {ArticleId}", article.Id);

                return notificationLog;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send notification for article ID: {ArticleId}", article?.Id);
                try
                {
                    var failedLog = new NotificationLog
                    {
                        ArticleId = article?.Id ?? 0,
                        RuleId = rule?.Id,
                        Title = "Delivery Failed",
                        Message = ex.Message,
                        Delivered = false,
                        Error = ex.Message,
                        SentAt = DateTime.UtcNow
                    };
                    await _notificationLogRepository.InsertAsync(failedLog);
                }
                catch (Exception logEx)
                {
                    _logger.Error(logEx, "Failed to log failed notification delivery");
                }
                throw;
            }
        }

        public async Task<bool> RecordNotificationActionAsync(int notificationId, NotificationAction action)
        {
            try
            {
                _logger.Debug("Recording action '{Action}' for notification ID: {NotificationId}", action, notificationId);
                var result = await _notificationLogRepository.RecordActionAsync(notificationId, action);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record action for notification ID: {NotificationId}", notificationId);
                throw;
            }
        }

        public async Task<List<NotificationLog>> GetRecentNotificationsAsync(int limit = 20)
        {
            try
            {
                return await _notificationLogRepository.GetRecentAsync(limit);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent notifications");
                throw;
            }
        }

        public async Task<List<NotificationLog>> GetPendingNotificationsAsync()
        {
            try
            {
                return await _notificationLogRepository.GetPendingNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve pending notifications");
                throw;
            }
        }

        public async Task<int> MarkAllAsReadAsync()
        {
            try
            {
                var pending = await GetPendingNotificationsAsync();
                var markedCount = 0;
                foreach (var n in pending)
                {
                    if (await RecordNotificationActionAsync(n.Id, NotificationAction.Dismissed))
                        markedCount++;
                }
                return markedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to mark all as read");
                throw;
            }
        }

        public async Task<NotificationStatistics> GetNotificationStatisticsAsync()
        {
            try
            {
                return await _notificationLogRepository.GetStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate statistics");
                throw;
            }
        }

        public async Task<int> CleanupOldNotificationsAsync(int retentionDays = 30)
        {
            try
            {
                return await _notificationLogRepository.CleanupOldLogsAsync(retentionDays);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to cleanup old notifications");
                throw;
            }
        }

        public async Task<bool> ShouldNotifyAsync(int articleId, int? ruleId = null)
        {
            try
            {
                var recent = await _notificationLogRepository.GetRecentAsync(10);
                return !recent.Any(n =>
                    n.ArticleId == articleId &&
                    n.RuleId == ruleId &&
                    n.SentAt > DateTime.UtcNow.AddMinutes(-5));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check notification eligibility");
                return false;
            }
        }

        public async Task<List<NotificationLog>> GetArticleNotificationHistoryAsync(int articleId)
        {
            try
            {
                return await _notificationLogRepository.GetByArticleIdAsync(articleId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve history");
                throw;
            }
        }

        private string GenerateNotificationTitle(Article article, Rule? rule) =>
            (rule != null && !string.IsNullOrEmpty(rule.Name)) ? $"{rule.Name}: {article.Title}" :
            (article.Title.Length > 100 ? article.Title.Substring(0, 97) + "..." : article.Title);

        private string GenerateNotificationMessage(Article article, Rule? rule)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.NotificationTemplate))
                return FormatNotificationTemplate(rule.NotificationTemplate, article, rule);

            return !string.IsNullOrEmpty(article.Summary) ?
                (article.Summary.Length > 200 ? article.Summary.Substring(0, 197) + "..." : article.Summary) : "";
        }

        private string FormatNotificationTemplate(string template, Article article, Rule rule) =>
            template.Replace("{Title}", article.Title ?? "")
                    .Replace("{Summary}", article.Summary ?? "")
                    .Replace("{RuleName}", rule.Name ?? "");

        private string GenerateNotificationTags(Article article, Rule? rule)
        {
            var tags = new List<string>();
            if (rule != null) tags.Add($"Rule:{rule.Name}");
            if (!string.IsNullOrEmpty(article.Categories)) tags.Add($"Category:{article.Categories}");
            return string.Join(",", tags);
        }

        private int GetNotificationDuration(NotificationPriority priority) =>
            priority switch
            {
                NotificationPriority.Low => 5,
                NotificationPriority.High => 10,
                NotificationPriority.Critical => 15,
                _ => 7
            };
    }
}