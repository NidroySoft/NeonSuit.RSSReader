namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Result of orphan record removal operation.
    /// Tracks removal of orphaned records from junction tables and entities that have lost their parent references.
    /// </summary>
    /// <remarks>
    /// Orphaned records are records that reference non-existent parent entities,
    /// typically caused by cascading delete failures or data corruption.
    /// </remarks>
    public class OrphanRemovalResult
    {
        /// <summary>
        /// Gets or sets the number of orphaned article-tag associations removed.
        /// These are records in the ArticleTags junction table where either
        /// the ArticleId references a non-existent Article, or the TagId
        /// references a non-existent Tag.
        /// </summary>
        public int OrphanedArticleTagsRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned articles removed.
        /// These are articles whose FeedId references a feed that no longer exists.
        /// </summary>
        public int OrphanedArticlesRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned categories removed.
        /// These are categories that are no longer referenced by any feed.
        /// </summary>
        public int OrphanedCategoriesRemoved { get; set; }

        /// <summary>
        /// Gets the total number of orphaned records removed across all tables.
        /// </summary>
        public int TotalRecordsRemoved =>
            OrphanedArticleTagsRemoved + OrphanedArticlesRemoved + OrphanedCategoriesRemoved;

        /// <summary>
        /// Gets a value indicating whether any orphaned records were found and removed.
        /// </summary>
        public bool AnyRemoved => TotalRecordsRemoved > 0;

        /// <summary>
        /// Gets a human-readable summary of the orphan cleanup operation.
        /// </summary>
        public override string ToString()
        {
            if (!AnyRemoved)
                return "No orphaned records found";

            var parts = new List<string>();

            if (OrphanedArticleTagsRemoved > 0)
                parts.Add($"{OrphanedArticleTagsRemoved} article-tag associations");

            if (OrphanedArticlesRemoved > 0)
                parts.Add($"{OrphanedArticlesRemoved} orphaned articles");

            if (OrphanedCategoriesRemoved > 0)
                parts.Add($"{OrphanedCategoriesRemoved} orphaned categories");

            return $"Removed {TotalRecordsRemoved} orphaned records: {string.Join(", ", parts)}";
        }
    }
}