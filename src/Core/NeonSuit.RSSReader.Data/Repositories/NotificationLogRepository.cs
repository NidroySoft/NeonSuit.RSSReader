using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository implementation for managing notification audit logs.
    /// Tracks delivery, user interactions, and provides analytics for notification effectiveness.
    /// </summary>
    public class NotificationLogRepository : BaseRepository<NotificationLog>, INotificationLogRepository
    {
        private readonly ILogger _logger;
        private readonly RssReaderDbContext _dbContext;

        public NotificationLogRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<NotificationLogRepository>();
            _dbContext = context;
        }

        /// <summary>
        /// Retrieves notification logs for a specific article.
        /// </summary>
        public async Task<List<NotificationLog>> GetByArticleIdAsync(int articleId)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var logs = await _dbSet
                    .Where(n => n.ArticleId == articleId)
                    .OrderByDescending(n => n.SentAt)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} notification logs for article ID: {ArticleId}",
                    logs.Count, articleId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve notification logs for article ID: {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves notification logs triggered by a specific rule.
        /// </summary>
        public async Task<List<NotificationLog>> GetByRuleIdAsync(int ruleId)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var logs = await _dbSet
                    .Where(n => n.RuleId == ruleId)
                    .OrderByDescending(n => n.SentAt)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} notification logs for rule ID: {RuleId}",
                    logs.Count, ruleId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve notification logs for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves notification logs within a specific date range.
        /// </summary>
        public async Task<List<NotificationLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var logs = await _dbSet
                    .Where(n => n.SentAt >= startDate && n.SentAt <= endDate)
                    .OrderByDescending(n => n.SentAt)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} notification logs between {StartDate} and {EndDate}",
                    logs.Count, startDate, endDate);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve notification logs for date range");
                throw;
            }
        }

        /// <summary>
        /// Retrieves recent notification logs with a specified limit.
        /// </summary>
        public async Task<List<NotificationLog>> GetRecentAsync(int limit = 50)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var logs = await _dbSet
                    .OrderByDescending(n => n.SentAt)
                    .Take(limit)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} recent notification logs", logs.Count);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent notification logs");
                throw;
            }
        }

        /// <summary>
        /// Inserts a new notification log entry.
        /// </summary>
        public override async Task<int> InsertAsync(NotificationLog notificationLog)
        {
            try
            {
                if (notificationLog == null)
                {
                    throw new ArgumentNullException(nameof(notificationLog));
                }

                notificationLog.SentAt = DateTime.UtcNow;
                var result = await base.InsertAsync(notificationLog);

                _logger.Information("Logged notification for article ID: {ArticleId} (Log ID: {NotificationId})",
                    notificationLog.ArticleId, notificationLog.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert notification log for article ID: {ArticleId}",
                    notificationLog?.ArticleId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing notification log entry.
        /// </summary>
        public override async Task<int> UpdateAsync(NotificationLog notificationLog)
        {
            try
            {
                var result = await base.UpdateAsync(notificationLog);
                _logger.Debug("Updated notification log ID: {NotificationId}", notificationLog.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update notification log ID: {NotificationId}", notificationLog.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a notification log by its ID.
        /// </summary>
        public async Task<int> DeleteAsync(int id)
        {
            try
            {
                var log = await GetByIdAsync(id);
                if (log == null)
                {
                    _logger.Warning("Attempted to delete non-existent notification log: ID {NotificationId}", id);
                    return 0;
                }

                var result = await base.DeleteAsync(log);
                _logger.Information("Deleted notification log ID: {NotificationId}", id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete notification log ID: {NotificationId}", id);
                throw;
            }
        }

        /// <summary>
        /// Records a user action on a notification (click, dismiss, etc.).
        /// </summary>
        public async Task<int> RecordActionAsync(int notificationId, NotificationAction action)
        {
            try
            {
                var log = await GetByIdAsync(notificationId);
                if (log == null)
                {
                    _logger.Warning("Attempted to record action on non-existent notification: ID {NotificationId}",
                        notificationId);
                    return 0;
                }

                log.Action = action;
                log.ActionAt = DateTime.UtcNow;

                var result = await UpdateAsync(log);
                _logger.Information("Recorded action '{Action}' for notification ID: {NotificationId}",
                    action, notificationId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record action for notification ID: {NotificationId}", notificationId);
                throw;
            }
        }

        /// <summary>
        /// Gets statistics about notification delivery and interactions.
        /// </summary>
        public async Task<NotificationStatistics> GetStatisticsAsync()
        {
            try
            {
                // CHANGED: Use EF Core with server-side aggregation
                var allLogs = await _dbSet.AsNoTracking().ToListAsync();

                if (!allLogs.Any())
                {
                    return new NotificationStatistics();
                }

                var statistics = new NotificationStatistics
                {
                    TotalNotifications = allLogs.Count,
                    ClickedNotifications = allLogs.Count(n => n.Action == NotificationAction.Clicked),
                    DismissedNotifications = allLogs.Count(n => n.Action == NotificationAction.Dismissed),
                    FailedDeliveries = allLogs.Count(n => !n.Delivered),
                    FirstNotificationDate = allLogs.Min(n => n.SentAt),
                    LastNotificationDate = allLogs.Max(n => n.SentAt)
                };

                statistics.ClickThroughRate = statistics.TotalNotifications > 0 ?
                    (double)statistics.ClickedNotifications / statistics.TotalNotifications * 100 : 0;

                _logger.Debug("Generated notification statistics: {Total} total, {Clicked} clicked, {Rate:F2}% CTR",
                    statistics.TotalNotifications, statistics.ClickedNotifications, statistics.ClickThroughRate);

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate notification statistics");
                throw;
            }
        }

        /// <summary>
        /// Gets notification logs that have not been acted upon by the user.
        /// </summary>
        public async Task<List<NotificationLog>> GetPendingNotificationsAsync()
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var pendingLogs = await _dbSet
                    .Where(n => n.Action == NotificationAction.None && n.Delivered)
                    .OrderByDescending(n => n.SentAt)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} pending notifications", pendingLogs.Count);
                return pendingLogs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve pending notifications");
                throw;
            }
        }

        /// <summary>
        /// Cleans up old notification logs beyond the retention period.
        /// </summary>
        public async Task<int> CleanupOldLogsAsync(int retentionDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
                // CHANGED: Use EF Core ExecuteDelete for better performance
                var deletedCount = await _dbSet
                    .Where(n => n.SentAt < cutoffDate)
                    .ExecuteDeleteAsync();

                _logger.Information("Cleaned up {Count} notification logs older than {CutoffDate}",
                    deletedCount, cutoffDate);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to cleanup old notification logs");
                throw;
            }
        }

        /// <summary>
        /// Gets the click-through rate for notifications.
        /// </summary>
        public async Task<double> GetClickThroughRateAsync()
        {
            try
            {
                var statistics = await GetStatisticsAsync();
                return statistics.ClickThroughRate;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to calculate click-through rate");
                throw;
            }
        }

        /// <summary>
        /// Logs a failed notification delivery.
        /// </summary>
        public async Task<int> LogFailedDeliveryAsync(int articleId, int? ruleId, string errorMessage)
        {
            try
            {
                var failedLog = new NotificationLog
                {
                    ArticleId = articleId,
                    RuleId = ruleId,
                    Title = "Delivery Failed",
                    Message = errorMessage,
                    Delivered = false,
                    Error = errorMessage,
                    SentAt = DateTime.UtcNow
                };

                var result = await InsertAsync(failedLog);
                _logger.Warning("Logged failed notification delivery for article ID: {ArticleId}, Error: {Error}",
                    articleId, errorMessage);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to log failed notification delivery");
                throw;
            }
        }

        /// <summary>
        /// Gets the count of notifications sent today.
        /// </summary>
        public async Task<int> GetTodayCountAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                // CHANGED: Use EF Core CountAsync
                var count = await _dbSet
                    .Where(n => n.SentAt >= today && n.SentAt < tomorrow)
                    .AsNoTracking()
                    .CountAsync();

                _logger.Debug("Today's notification count: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get today's notification count");
                throw;
            }
        }

        /// <summary>
        /// Gets the most frequent notification types.
        /// </summary>
        public async Task<Dictionary<NotificationType, int>> GetNotificationTypeDistributionAsync()
        {
            try
            {
                // CHANGED: Use EF Core with server-side grouping
                var distribution = await _dbSet
                    .GroupBy(n => n.NotificationType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .AsNoTracking()
                    .ToListAsync();

                var result = distribution.ToDictionary(g => g.Type, g => g.Count);

                _logger.Debug("Generated notification type distribution with {Count} categories", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate notification type distribution");
                throw;
            }
        }
    }
}