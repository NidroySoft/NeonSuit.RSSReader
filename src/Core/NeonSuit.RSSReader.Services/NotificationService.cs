using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="INotificationService"/> providing comprehensive notification management.
    /// Handles notification creation, delivery tracking, user actions, and history management.
    /// </summary>
    internal class NotificationService : INotificationService
    {
        private readonly INotificationLogRepository _notificationLogRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IRuleRepository _ruleRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService"/> class.
        /// </summary>
        /// <param name="notificationLogRepository">Repository for notification log data access.</param>
        /// <param name="articleRepository">Repository for article data access.</param>
        /// <param name="ruleRepository">Repository for rule data access.</param>
        /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public NotificationService(
            INotificationLogRepository notificationLogRepository,
            IArticleRepository articleRepository,
            IRuleRepository ruleRepository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(notificationLogRepository);
            ArgumentNullException.ThrowIfNull(articleRepository);
            ArgumentNullException.ThrowIfNull(ruleRepository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _notificationLogRepository = notificationLogRepository;
            _articleRepository = articleRepository;
            _ruleRepository = ruleRepository;
            _mapper = mapper;
            _logger = logger.ForContext<NotificationService>();

#if DEBUG
            _logger.Debug("NotificationService initialized");
#endif
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<NotificationDto>? OnNotificationCreated;

        #endregion

        #region Notification Creation and Delivery

        /// <inheritdoc />
        public async Task<NotificationDto?> SendNotificationAsync(CreateNotificationDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                if (createDto.ArticleId <= 0)
                {
                    _logger.Warning("Attempted to send notification for article with invalid ID: {ArticleId}", createDto.ArticleId);
                    throw new ArgumentException("Article ID must be greater than 0", nameof(createDto));
                }

                _logger.Information("Sending notification for article ID: {ArticleId}", createDto.ArticleId);

                // Get article and rule for template formatting
                var article = await _articleRepository.GetByIdAsync(createDto.ArticleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article {ArticleId} not found for notification", createDto.ArticleId);
                    return null;
                }

                Rule? rule = null;
                if (createDto.RuleId.HasValue)
                {
                    rule = await _ruleRepository.GetByIdAsync(createDto.RuleId.Value, cancellationToken).ConfigureAwait(false);
                }

                if (!await ShouldNotifyAsync(createDto.ArticleId, createDto.RuleId, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Debug("Notification suppressed for article ID: {ArticleId} (duplicate)", createDto.ArticleId);
                    return null;
                }

                var notificationLog = new NotificationLog
                {
                    ArticleId = createDto.ArticleId,
                    RuleId = createDto.RuleId,
                    NotificationType = createDto.NotificationType,
                    Priority = createDto.Priority,
                    Title = !string.IsNullOrWhiteSpace(createDto.Title)
                        ? createDto.Title
                        : GenerateNotificationTitle(article, rule),
                    Message = !string.IsNullOrWhiteSpace(createDto.Message)
                        ? createDto.Message
                        : GenerateNotificationMessage(article, rule),
                    Channel = createDto.Channel ?? "default",
                    Tags = !string.IsNullOrWhiteSpace(createDto.Tags)
                        ? createDto.Tags
                        : GenerateNotificationTags(article, rule),
                    SoundPlayed = createDto.SoundPlayed,
                    Duration = createDto.Duration > 0 ? createDto.Duration : GetNotificationDuration(createDto.Priority),
                    Delivered = true,
                    SentAt = DateTime.UtcNow
                };

                await _notificationLogRepository.InsertAsync(notificationLog, cancellationToken).ConfigureAwait(false);

                var notificationDto = _mapper.Map<NotificationDto>(notificationLog);
                notificationDto.ArticleTitle = article.Title;
                notificationDto.RuleName = rule?.Name;
                notificationDto.TimeAgo = GetTimeAgo(notificationLog.SentAt);

                // Raise event for UI to display notification
                try
                {
                    OnNotificationCreated?.Invoke(this, notificationDto);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error in OnNotificationCreated event handler");
                }

                _logger.Information("Notification sent successfully for article ID: {ArticleId} (Notification ID: {NotificationId})",
                    createDto.ArticleId, notificationLog.Id);

                return notificationDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SendNotificationAsync operation was cancelled for article ID: {ArticleId}", createDto.ArticleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send notification for article ID: {ArticleId}", createDto.ArticleId);
                throw new InvalidOperationException($"Failed to send notification for article {createDto.ArticleId}", ex);
            }
        }

        #endregion

        #region Notification Actions

        /// <inheritdoc />
        public async Task<bool> RecordNotificationActionAsync(UpdateNotificationActionDto actionDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actionDto);

            try
            {
                if (actionDto.NotificationId <= 0)
                {
                    _logger.Warning("Invalid notification ID for action recording: {NotificationId}", actionDto.NotificationId);
                    throw new ArgumentOutOfRangeException(nameof(actionDto.NotificationId), "Notification ID must be greater than 0");
                }

                _logger.Debug("Recording action '{Action}' for notification ID: {NotificationId}", actionDto.Action, actionDto.NotificationId);
                var result = await _notificationLogRepository.RecordActionAsync(actionDto.NotificationId, actionDto.Action, cancellationToken).ConfigureAwait(false);

                if (result > 0)
                {
                    _logger.Information("Action '{Action}' recorded for notification ID: {NotificationId}", actionDto.Action, actionDto.NotificationId);
                }
                else
                {
                    _logger.Warning("Notification ID {NotificationId} not found for action recording", actionDto.NotificationId);
                }

                return result > 0;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RecordNotificationActionAsync operation was cancelled for notification ID: {NotificationId}", actionDto.NotificationId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record action for notification ID: {NotificationId}", actionDto.NotificationId);
                throw new InvalidOperationException($"Failed to record action for notification {actionDto.NotificationId}", ex);
            }
        }

        #endregion

        #region Notification Queries

        /// <inheritdoc />
        public async Task<List<NotificationSummaryDto>> GetRecentNotificationsAsync(int limit = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                if (limit < 1)
                {
                    _logger.Warning("Invalid limit for recent notifications: {Limit}", limit);
                    throw new ArgumentException("Limit must be at least 1", nameof(limit));
                }

                _logger.Debug("Retrieving {Limit} recent notifications", limit);

                var notifications = await _notificationLogRepository.GetRecentAsync(limit, cancellationToken).ConfigureAwait(false);
                var summaryDtos = _mapper.Map<List<NotificationSummaryDto>>(notifications);

                // Enrich with article titles
                foreach (var dto in summaryDtos)
                {
                    var article = await _articleRepository.GetByIdAsync(dto.ArticleId, cancellationToken).ConfigureAwait(false);
                    if (article != null)
                    {
                        dto.ArticleTitle = article.Title;
                    }
                    dto.TimeAgo = GetTimeAgo(dto.SentAt);
                }

                _logger.Debug("Retrieved {Count} recent notifications", summaryDtos.Count);
                return summaryDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRecentNotificationsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent notifications");
                throw new InvalidOperationException("Failed to retrieve recent notifications", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationSummaryDto>> GetPendingNotificationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving pending notifications");

                var notifications = await _notificationLogRepository.GetPendingNotificationsAsync(cancellationToken).ConfigureAwait(false);
                var summaryDtos = _mapper.Map<List<NotificationSummaryDto>>(notifications);

                // Enrich with article titles
                foreach (var dto in summaryDtos)
                {
                    var article = await _articleRepository.GetByIdAsync(dto.ArticleId, cancellationToken).ConfigureAwait(false);
                    if (article != null)
                    {
                        dto.ArticleTitle = article.Title;
                    }
                    dto.TimeAgo = GetTimeAgo(dto.SentAt);
                }

                _logger.Debug("Retrieved {Count} pending notifications", summaryDtos.Count);
                return summaryDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetPendingNotificationsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve pending notifications");
                throw new InvalidOperationException("Failed to retrieve pending notifications", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<NotificationDto>> GetArticleNotificationHistoryAsync(int articleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID for notification history: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                _logger.Debug("Retrieving notification history for article ID: {ArticleId}", articleId);

                var notifications = await _notificationLogRepository.GetByArticleIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                var notificationDtos = _mapper.Map<List<NotificationDto>>(notifications);

                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                var articleTitle = article?.Title ?? "Unknown Article";

                foreach (var dto in notificationDtos)
                {
                    dto.ArticleTitle = articleTitle;
                    dto.TimeAgo = GetTimeAgo(dto.SentAt);

                    if (dto.ActionAt.HasValue)
                    {
                        dto.ResponseTimeSeconds = (dto.ActionAt.Value - dto.SentAt).TotalSeconds;
                    }
                }

                _logger.Debug("Retrieved {Count} notifications for article ID: {ArticleId}", notificationDtos.Count, articleId);
                return notificationDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetArticleNotificationHistoryAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve notification history for article ID: {ArticleId}", articleId);
                throw new InvalidOperationException($"Failed to retrieve notification history for article {articleId}", ex);
            }
        }

        #endregion

        #region Notification Management

        /// <inheritdoc />
        public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Marking all pending notifications as read");

                var pending = await GetPendingNotificationsAsync(cancellationToken).ConfigureAwait(false);
                var markedCount = 0;

                foreach (var notification in pending)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var actionDto = new UpdateNotificationActionDto
                    {
                        NotificationId = notification.Id,
                        Action = NotificationAction.Dismissed
                    };

                    if (await RecordNotificationActionAsync(actionDto, cancellationToken).ConfigureAwait(false))
                        markedCount++;
                }

                _logger.Information("Marked {Count} notifications as read", markedCount);
                return markedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("MarkAllAsReadAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to mark all notifications as read");
                throw new InvalidOperationException("Failed to mark all notifications as read", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> CleanupOldNotificationsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                if (retentionDays < 1)
                {
                    _logger.Warning("Invalid retention days for cleanup: {RetentionDays}", retentionDays);
                    throw new ArgumentException("Retention days must be at least 1", nameof(retentionDays));
                }

                _logger.Information("Cleaning up notifications older than {RetentionDays} days", retentionDays);
                var deletedCount = await _notificationLogRepository.CleanupOldLogsAsync(retentionDays, cancellationToken).ConfigureAwait(false);
                _logger.Information("Deleted {Count} old notifications", deletedCount);
                return deletedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CleanupOldNotificationsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to cleanup old notifications");
                throw new InvalidOperationException("Failed to cleanup old notifications", ex);
            }
        }

        #endregion

        #region Notification Validation

        /// <inheritdoc />
        public async Task<bool> ShouldNotifyAsync(int articleId, int? ruleId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID for notification check: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                _logger.Debug("Checking notification eligibility for article ID: {ArticleId}, Rule ID: {RuleId}", articleId, ruleId);

                // Check for duplicate notifications in the last 5 minutes
                var recent = await _notificationLogRepository.GetRecentAsync(10, cancellationToken).ConfigureAwait(false);
                var shouldNotify = !recent.Any(n =>
                    n.ArticleId == articleId &&
                    n.RuleId == ruleId &&
                    n.SentAt > DateTime.UtcNow.AddMinutes(-5));

                _logger.Debug("Notification eligibility for article ID {ArticleId}: {ShouldNotify}", articleId, shouldNotify);
                return shouldNotify;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ShouldNotifyAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check notification eligibility for article ID: {ArticleId}", articleId);
                // Fail safe - allow notification if we can't determine
                return true;
            }
        }

        #endregion

        #region Statistics

        /// <inheritdoc />
        public async Task<NotificationStatsDto> GetNotificationStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving notification statistics");

                var statistics = await _notificationLogRepository.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
                var statsDto = _mapper.Map<NotificationStatsDto>(statistics);

                // Calculate derived metrics
                var now = DateTime.UtcNow;
                var last24Hours = now.AddDays(-1);
                var last7Days = now.AddDays(-7);
                var last30Days = now.AddDays(-30);

                var allNotifications = await _notificationLogRepository.GetRecentAsync(1000, cancellationToken).ConfigureAwait(false);

                statsDto.Last24Hours = allNotifications.Count(n => n.SentAt >= last24Hours);
                statsDto.Last7Days = allNotifications.Count(n => n.SentAt >= last7Days);
                statsDto.Last30Days = allNotifications.Count(n => n.SentAt >= last30Days);
                statsDto.LastNotificationDate = allNotifications.OrderByDescending(n => n.SentAt).FirstOrDefault()?.SentAt;

                var topAnonList = allNotifications
                    .Where(n => n.RuleId.HasValue)
                    .GroupBy(n => n.RuleId!.Value)
                    .Select(g => new { RuleId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToList();

                var topRules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in topAnonList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var rule = await _ruleRepository.GetByIdAsync(entry.RuleId, cancellationToken).ConfigureAwait(false);
                        var ruleName = rule?.Name ?? $"Rule {entry.RuleId}";
                        // Si ya existe la clave, sumamos (aunque no debería ocurrir por agrupación)
                        topRules[ruleName] = entry.Count;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Debug("GetNotificationStatisticsAsync operation was cancelled while fetching rule {RuleId}", entry.RuleId);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to resolve name for rule {RuleId}, using fallback", entry.RuleId);
                        topRules[$"Rule {entry.RuleId}"] = entry.Count;
                    }
                }

                statsDto.TopRules = topRules;

                _logger.Information("Notification statistics retrieved successfully");
                return statsDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetNotificationStatisticsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate notification statistics");
                throw new InvalidOperationException("Failed to generate notification statistics", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Generates a notification title based on article and rule.
        /// </summary>
        private string GenerateNotificationTitle(Article article, Rule? rule)
        {
            try
            {
                if (rule != null && !string.IsNullOrEmpty(rule.Name))
                {
                    return $"{rule.Name}: {Truncate(article.Title, 100)}";
                }

                return Truncate(article.Title, 100);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error generating notification title, using fallback");
                return article.Title ?? "New Article";
            }
        }

        /// <summary>
        /// Generates a notification message based on article and rule.
        /// </summary>
        private string GenerateNotificationMessage(Article article, Rule? rule)
        {
            try
            {
                if (rule != null && !string.IsNullOrEmpty(rule.NotificationTemplate))
                {
                    return FormatNotificationTemplate(rule.NotificationTemplate, article, rule);
                }

                return Truncate(article.Summary, 200);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error generating notification message, using fallback");
                return Truncate(article.Summary, 200);
            }
        }

        /// <summary>
        /// Formats a notification template by replacing placeholders with actual values.
        /// </summary>
        private string FormatNotificationTemplate(string template, Article article, Rule rule)
        {
            return template.Replace("{Title}", article.Title ?? "")
                           .Replace("{Summary}", article.Summary ?? "")
                           .Replace("{RuleName}", rule.Name ?? "");
        }

        /// <summary>
        /// Generates comma-separated tags for a notification.
        /// </summary>
        private string GenerateNotificationTags(Article article, Rule? rule)
        {
            var tags = new List<string>();

            if (rule != null && !string.IsNullOrEmpty(rule.Name))
                tags.Add($"Rule:{rule.Name}");

            if (!string.IsNullOrEmpty(article.Categories))
                tags.Add($"Category:{article.Categories}");

            return string.Join(",", tags);
        }

        /// <summary>
        /// Gets the display duration in seconds based on notification priority.
        /// </summary>
        private int GetNotificationDuration(NotificationPriority priority)
        {
            return priority switch
            {
                NotificationPriority.Low => 5,
                NotificationPriority.Normal => 7,
                NotificationPriority.High => 10,
                NotificationPriority.Critical => 15,
                _ => 7
            };
        }

        /// <summary>
        /// Truncates a string to the specified maximum length.
        /// </summary>
        private static string Truncate(string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Gets a human-readable time ago string.
        /// </summary>
        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            return timeSpan.TotalSeconds switch
            {
                < 60 => $"{timeSpan.Seconds} seconds ago",
                < 3600 => $"{timeSpan.Minutes} minutes ago",
                < 86400 => $"{timeSpan.Hours} hours ago",
                < 604800 => $"{timeSpan.Days} days ago",
                _ => dateTime.ToString("yyyy-MM-dd")
            };
        }

        #endregion
    }
}