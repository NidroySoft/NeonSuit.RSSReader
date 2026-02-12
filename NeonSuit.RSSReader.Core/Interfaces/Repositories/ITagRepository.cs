using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for Tag entity operations.
    /// Provides specialized methods for tag management beyond basic CRUD.
    /// </summary>
    public interface ITagRepository : IRepository<Tag>
    {
        /// <summary>
        /// Retrieves a tag by its name (case-insensitive).
        /// </summary>
        /// <param name="name">The tag name to search for.</param>
        /// <returns>The Tag object or null if not found.</returns>
        Task<Tag?> GetByNameAsync(string name);

        /// <summary>
        /// Retrieves all tags with usage statistics, ordered by usage count descending.
        /// </summary>
        Task<List<Tag>> GetPopularTagsAsync(int limit = 50);

        /// <summary>
        /// Retrieves pinned/featured tags for quick access in UI.
        /// </summary>
        Task<List<Tag>> GetPinnedTagsAsync();

        /// <summary>
        /// Retrieves tags that are visible in the tag cloud.
        /// </summary>
        Task<List<Tag>> GetVisibleTagsAsync();

        /// <summary>
        /// Searches tags by name with partial matching.
        /// </summary>
        /// <param name="searchTerm">The search term to match against tag names.</param>
        Task<List<Tag>> SearchByNameAsync(string searchTerm);

        /// <summary>
        /// Updates the LastUsedAt timestamp for a tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag to update.</param>
        Task UpdateLastUsedAsync(int tagId);

        /// <summary>
        /// Increments the usage count for a tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag to update.</param>
        /// <param name="increment">The amount to increment (default: 1).</param>
        Task IncrementUsageCountAsync(int tagId, int increment = 1);

        /// <summary>
        /// Retrieves tags associated with a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        Task<List<Tag>> GetTagsByArticleIdAsync(int articleId);

        /// <summary>
        /// Retrieves tags by their color.
        /// </summary>
        /// <param name="color">The color in hexadecimal format.</param>
        Task<List<Tag>> GetTagsByColorAsync(string color);

        /// <summary>
        /// Checks if a tag with the given name already exists.
        /// </summary>
        Task<bool> ExistsByNameAsync(string name);
    }
}