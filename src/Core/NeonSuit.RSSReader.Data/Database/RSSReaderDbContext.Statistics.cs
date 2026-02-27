// =======================================================
// Data/Database/RssReaderDbContext.Statistics.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.DTOs.System;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        /// <inheritdoc/>
        public async Task<DatabaseStatsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var dbPath = DatabasePath;
                var stats = new DatabaseStatsDto
                {
                    TotalSize = string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)
                        ? 0
                        : new FileInfo(dbPath).Length,
                    ArticleCount = await Articles.CountAsync(cancellationToken),
                    FeedCount = await Feeds.CountAsync(cancellationToken),
                    RuleCount = await Rules.CountAsync(cancellationToken),
                    NotificationCount = await NotificationLogs.CountAsync(cancellationToken),
                    TagCount = await Tags.CountAsync(cancellationToken)
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
    }
}