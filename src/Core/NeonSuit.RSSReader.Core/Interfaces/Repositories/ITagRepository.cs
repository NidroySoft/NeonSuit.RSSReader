using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for <see cref="Tag"/> entity operations.
/// Provides specialized methods for tag management, discovery, usage tracking, and article association beyond basic CRUD.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the contract for persistence operations on tags in the RSS reader application.
/// It supports tag discovery (search, popular, pinned, visible), usage statistics (count, last used),
/// article-tagging relationships, and color-based filtering.
/// </para>
/// <para>
/// Implementations must ensure:
/// - Case-insensitive name lookups and uniqueness checks.
/// - Efficient queries on frequently accessed fields (Name, UsageCount, LastUsedAt, Color).
/// - Safe handling of tag-article many-to-many relationships (via ArticleTag join entity).
/// - Avoidance of full table scans in production scenarios.
/// </para>
/// </remarks>
public interface ITagRepository : IRepository<Tag>
{
    #region Read Single Operations

    /// <summary>
    /// Retrieves a tag by its name (case-insensitive lookup).
    /// </summary>
    /// <param name="name">The tag name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="Tag"/> entity if found; otherwise <see langword="null"/>.</returns>
    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    #endregion

    #region Read Collection Operations

    /// <summary>
    /// Retrieves the most used tags, ordered by usage count.
    /// </summary>
    /// <param name="limit">The maximum number of tags to return. Default is 50.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of popular <see cref="Tag"/> entities.</returns>
    Task<List<Tag>> GetPopularTagsAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tags that have been pinned by the user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of pinned <see cref="Tag"/> entities.</returns>
    Task<List<Tag>> GetPinnedTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tags that are marked as visible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of visible <see cref="Tag"/> entities.</returns>
    Task<List<Tag>> GetVisibleTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a case-insensitive search for tags by name.
    /// </summary>
    /// <param name="searchTerm">The string to search for in tag names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching <see cref="Tag"/> entities.</returns>
    Task<List<Tag>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tags associated with a specific article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="Tag"/> entities linked to the article.</returns>
    Task<List<Tag>> GetTagsByArticleIdAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tags that use a specific color.
    /// </summary>
    /// <param name="color">The color in hexadecimal format (e.g., "#FF5733").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="Tag"/> entities matching the color.</returns>
    Task<List<Tag>> GetTagsByColorAsync(string color, CancellationToken cancellationToken = default);

    #endregion

    #region Existence Checks

    /// <summary>
    /// Checks if a tag with the given name already exists in the database.
    /// </summary>
    /// <param name="name">The name of the tag to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if a tag with this name exists; otherwise <see langword="false"/>.</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    #endregion

    #region Metadata Updates

    /// <summary>
    /// Updates the usage statistics and last used timestamp for a tag.
    /// </summary>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="newUsageCount">The updated total usage count.</param>
    /// <param name="lastUsedAt">The timestamp of the most recent usage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> UpdateUsageMetadataAsync(int tagId, int newUsageCount, DateTime lastUsedAt, CancellationToken cancellationToken = default);

    #endregion
}