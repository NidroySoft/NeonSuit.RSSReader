using System;

namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Represents the result of an article deletion operation.
    /// Contains statistics about articles that were deleted, including counts and date ranges.
    /// </summary>
    public class ArticleDeletionResult
    {
        /// <summary>
        /// Gets or sets the total number of articles before cleanup started.
        /// </summary>
        public int TotalArticlesBefore { get; set; }

        /// <summary>
        /// Gets or sets the number of articles actually deleted.
        /// </summary>
        public int ArticlesDeleted { get; set; }

        /// <summary>
        /// Gets the number of articles remaining after cleanup.
        /// </summary>
        public int ArticlesRemaining => TotalArticlesBefore - ArticlesDeleted;

        /// <summary>
        /// Gets or sets the number of articles matching deletion criteria before the operation.
        /// </summary>
        public int ArticlesFound { get; set; }

        /// <summary>
        /// Gets or sets the publication date of the oldest article that was deleted.
        /// </summary>
        public DateTime? OldestArticleDeleted { get; set; }

        /// <summary>
        /// Gets or sets the publication date of the newest article that was deleted.
        /// </summary>
        public DateTime? NewestArticleDeleted { get; set; }

        /// <summary>
        /// Gets or sets the cutoff date used for deletion.
        /// </summary>
        public DateTime CutoffDateUsed { get; set; }

        /// <summary>
        /// Gets a value indicating whether any articles were deleted.
        /// </summary>
        public bool AnyDeleted => ArticlesDeleted > 0;

        /// <summary>
        /// Gets the date range of deleted articles as a formatted string.
        /// </summary>
        public string DateRangeFormatted => (OldestArticleDeleted.HasValue && NewestArticleDeleted.HasValue)
            ? $"{OldestArticleDeleted:yyyy-MM-dd} to {NewestArticleDeleted:yyyy-MM-dd}"
            : "No date range available";

        /// <summary>
        /// Gets a brief summary of the article cleanup.
        /// </summary>
        public override string ToString()
        {
            return $"{ArticlesDeleted} articles deleted, {ArticlesRemaining} remaining";
        }
    }
}