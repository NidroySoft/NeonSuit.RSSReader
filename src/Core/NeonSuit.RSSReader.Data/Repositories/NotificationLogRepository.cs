using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository implementation for managing notification audit logs.
    /// Tracks delivery, user interactions, and provides analytics for notification effectiveness.
    /// </summary>
    internal class NotificationLogRepository : BaseRepository<NotificationLog>, INotificationLogRepository
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationLogRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public NotificationLogRepository(RSSReaderDbContext context, ILogger logger) : base(context, logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
            _logger.Debug("NotificationLogRepository initialized");
#endif
        }

        #endregion

        #region CRUD Operations

        /// <inheritdoc />
        public override async Task<NotificationLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByIdAsync cancelled for notification ID {NotificationId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving notification log by ID {NotificationId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<List<NotificationLog>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all notification logs");
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<List<NotificationLog>> GetWhereAsync(Expression<Func<NotificationLog, bool>> predicate, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .Where(predicate)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetWhereAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GetWhereAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> InsertAsync(NotificationLog notificationLog, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notificationLog);

            try
            {
                var result = await base.InsertAsync(notificationLog, cancellationToken).ConfigureAwait(false);
                _logger.Information("Inserted notification log for article ID {ArticleId}", notificationLog.ArticleId);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("InsertAsync cancelled for notification");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.Error(ex, "Database error inserting notification log");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error inserting notification log");
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> UpdateAsync(NotificationLog notificationLog, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notificationLog);

            try
            {
                var result = await base.UpdateAsync(notificationLog, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Updated notification log ID {NotificationId}", notificationLog.Id);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateAsync cancelled for notification ID {NotificationId}", notificationLog.Id);
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.Error(ex, "Concurrency conflict updating notification ID {NotificationId}", notificationLog.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating notification ID {NotificationId}", notificationLog.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            try
            {
                var result = await _dbSet
                    .Where(n => n.Id == id)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Deleted notification log ID {NotificationId}", id);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteAsync cancelled for notification ID {NotificationId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting notification ID {NotificationId}", id);
                throw;
            }
        }

        #endregion

        #region Read Collection Operations

        /// <inheritdoc />
        public async Task<List<NotificationLog>> GetByArticleIdAsync(int articleId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Rule)
                    .Where(n => n.ArticleId == articleId)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByArticleIdAsync cancelled for article {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving notifications for article {ArticleId}", articleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationLog>> GetByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Where(n => n.RuleId == ruleId)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByRuleIdAsync cancelled for rule {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving notifications for rule {RuleId}", ruleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            if (startDate > endDate)
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .Where(n => n.SentAt >= startDate && n.SentAt <= endDate)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByDateRangeAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving notifications by date range");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationLog>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .OrderByDescending(n => n.SentAt)
                    .Take(limit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRecentAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving recent notifications");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationLog>> GetPendingNotificationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(n => n.Article)
                    .Include(n => n.Rule)
                    .Where(n => n.Action == NotificationAction.None && n.Delivered)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetPendingNotificationsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving pending notifications");
                throw;
            }
        }

        #endregion

        #region Action and Status Updates

        /// <inheritdoc />
        public async Task<int> RecordActionAsync(int notificationId, NotificationAction action, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notificationId);

            try
            {
                var result = await _dbSet
                    .Where(n => n.Id == notificationId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(n => n.Action, action)
                        .SetProperty(n => n.ActionAt, DateTime.UtcNow),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Recorded action {Action} for notification ID {NotificationId}", action, notificationId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RecordActionAsync cancelled for notification ID {NotificationId}", notificationId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error recording action for notification ID {NotificationId}", notificationId);
                throw;
            }
        }

        #endregion

        #region Statistics and Analytics

        /// <inheritdoc />
        public async Task<NotificationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var total = await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false);

                if (total == 0)
                    return new NotificationStatistics();

                var clicked = await _dbSet.CountAsync(n => n.Action == NotificationAction.Clicked, cancellationToken).ConfigureAwait(false);
                var dismissed = await _dbSet.CountAsync(n => n.Action == NotificationAction.Dismissed, cancellationToken).ConfigureAwait(false);
                var failed = await _dbSet.CountAsync(n => !n.Delivered, cancellationToken).ConfigureAwait(false);

                var firstDate = await _dbSet.MinAsync(n => (DateTime?)n.SentAt, cancellationToken).ConfigureAwait(false);
                var lastDate = await _dbSet.MaxAsync(n => (DateTime?)n.SentAt, cancellationToken).ConfigureAwait(false);

                var stats = new NotificationStatistics
                {
                    TotalNotifications = total,
                    ClickedNotifications = clicked,
                    DismissedNotifications = dismissed,
                    FailedDeliveries = failed,
                    FirstNotificationDate = firstDate,
                    LastNotificationDate = lastDate
                };

                stats.ClickThroughRate = total > 0 ? (double)clicked / total * 100 : 0;

                _logger.Debug("Generated notification statistics: Total={Total}, CTR={CTR:F2}%", total, stats.ClickThroughRate);
                return stats;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetStatisticsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating notification statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<double> GetClickThroughRateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
                return stats.ClickThroughRate;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetClickThroughRateAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating click-through rate");
                throw;
            }
        }

        #endregion

        #region Maintenance Operations

        /// <inheritdoc />
        public async Task<int> CleanupOldLogsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(retentionDays);

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                var deletedCount = await _dbSet
                    .Where(n => n.SentAt < cutoffDate)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("Cleaned up {Count} notification logs older than {CutoffDate}", deletedCount, cutoffDate);
                return deletedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CleanupOldLogsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error cleaning up old notification logs");
                throw;
            }
        }

        #endregion
    }
}