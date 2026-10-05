using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Database;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using System.Text.Json;

namespace NeonSuit.RSSReader.Services;

/// <summary>Executes real background work within a single, short-lived dependency scope.</summary>
internal sealed class SyncTaskExecutor(IServiceProvider services)
{
    public async Task<Dictionary<string, object>> ExecuteAsync(SyncTaskType taskType,
        Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new Dictionary<string, object> { ["success"] = true, ["timestamp"] = DateTime.UtcNow };
        switch (taskType)
        {
            case SyncTaskType.FeedUpdate:
                var feeds = services.GetRequiredService<IFeedService>();
                var articles = services.GetRequiredService<IArticleRepository>();
                var before = await articles.CountAsync(cancellationToken);
                if (parameters?.TryGetValue("feedId", out var id) == true)
                {
                    int feedId = id is JsonElement json ? json.GetInt32() : Convert.ToInt32(id);
                    if (!await feeds.RefreshFeedAsync(feedId, cancellationToken))
                        throw new InvalidOperationException($"Feed {feedId} could not be refreshed.");
                    result["feeds_updated"] = 1;
                    result["feed_id"] = feedId;
                }
                else
                {
                    var due = await services.GetRequiredService<IFeedRepository>().GetFeedsToUpdateAsync(cancellationToken);
                    int refreshed = 0;
                    foreach (var feed in due)
                    {
                        if (!await feeds.RefreshFeedAsync(feed.Id, cancellationToken))
                            throw new InvalidOperationException($"Feed {feed.Id} could not be refreshed; {refreshed} earlier feeds succeeded.");
                        refreshed++;
                    }
                    result["feeds_updated"] = refreshed;
                }
                result["articles_fetched"] = Math.Max(0, await articles.CountAsync(cancellationToken) - before);
                break;
            case SyncTaskType.RuleProcessing:
            case SyncTaskType.TagProcessing:
                // Both use the same rule pipeline. Articles are marked processed only after all actions succeed.
                var repository = services.GetRequiredService<IArticleRepository>();
                var rules = services.GetRequiredService<IRuleService>();
                var tagLinks = services.GetRequiredService<IArticleTagRepository>();
                var tagsBefore = await tagLinks.CountAsync(cancellationToken);
                int processed = 0, matched = 0, applied = 0;
                while (true)
                {
                    var batch = await repository.GetUnprocessedArticlesAsync(100, cancellationToken);
                    if (batch.Count == 0) break;
                    foreach (var article in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var matches = await rules.EvaluateArticleAgainstRulesAsync(article.Id, cancellationToken);
                        foreach (var rule in matches)
                        {
                            if (!await rules.ExecuteRuleActionsAsync(rule.Id, article.Id, cancellationToken))
                                throw new InvalidOperationException($"Rule {rule.Id} failed for article {article.Id}.");
                            applied++;
                        }
                        if (matches.Count > 0) matched++;
                        await services.GetRequiredService<IArticleService>().MarkAsProcessedAsync(article.Id, cancellationToken);
                        processed++;
                    }
                }
                result["articles_processed"] = processed;
                result["articles_matched"] = matched;
                result["rules_applied"] = applied;
                result["tags_applied"] = Math.Max(0, await tagLinks.CountAsync(cancellationToken) - tagsBefore);
                break;
            case SyncTaskType.ArticleCleanup:
                var cleanup = await services.GetRequiredService<IDatabaseCleanupService>().PerformCleanupAsync(cancellationToken);
                if (!cleanup.Success && !cleanup.Skipped)
                    throw new InvalidOperationException(string.Join("; ", cleanup.Errors));
                result["articles_cleaned"] = cleanup.ArticleCleanup?.ArticlesDeleted ?? 0;
                result["space_freed_mb"] = cleanup.SpaceFreedMB;
                result["skipped"] = cleanup.Skipped;
                break;
            case SyncTaskType.BackupCreation:
                var database = services.GetRequiredService<IRSSReaderDbContext>();
                var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database.DatabasePath!))!, "backups");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.db");
                await database.BackupAsync(path, cancellationToken);
                result["backup_path"] = path;
                result["backup_size_mb"] = new FileInfo(path).Length / (1024d * 1024d);
                break;
            case SyncTaskType.StatisticsUpdate:
                var stats = await services.GetRequiredService<IDatabaseCleanupService>().GetStatisticsAsync(cancellationToken);
                result["feeds_count"] = stats.FeedCount;
                result["articles_count"] = stats.TotalArticleCount;
                result["tags_count"] = await services.GetRequiredService<ITagRepository>().CountAsync(cancellationToken);
                break;
            case SyncTaskType.CacheMaintenance:
                var cache = await services.GetRequiredService<IDatabaseCleanupService>().CleanupImageCacheAsync(cancellationToken: cancellationToken);
                result["cache_entries_cleared"] = cache.ImagesDeleted;
                result["cache_size_freed_mb"] = cache.SpaceFreedMB;
                break;
            case SyncTaskType.FullSync:
                // Keep destructive retention and backup on their own configured schedules.
                foreach (var type in new[] { SyncTaskType.FeedUpdate, SyncTaskType.RuleProcessing, SyncTaskType.StatisticsUpdate })
                    foreach (var pair in await ExecuteAsync(type, parameters, cancellationToken)) result[pair.Key] = pair.Value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(taskType));
        }
        return result;
    }
}
