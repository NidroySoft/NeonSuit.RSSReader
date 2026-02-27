using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for tag management operations.
    /// Provides business logic, validation, and event notifications for tag operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all tag-related business logic including:
    /// <list type="bullet">
    /// <item>Tag CRUD operations with validation and duplicate prevention</item>
    /// <item>Tag assignment to articles with atomic batch operations</item>
    /// <item>Tag popularity tracking and usage statistics</item>
    /// <item>Pin/unpin functionality for frequently used tags</item>
    /// <item>Tag merging for consolidation and cleanup</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface ITagService
    {
        #region Events

        /// <summary>
        /// Event triggered when a tag is created, updated, deleted, pinned, unpinned, or visibility changes.
        /// </summary>
        event EventHandler<TagChangedEventArgs> OnTagChanged;

        #endregion

        #region Basic CRUD Operations

        /// <summary>
        /// Retrieves a tag by its unique identifier.
        /// </summary>
        /// <param name="id">The tag identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The tag DTO if found; otherwise, null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="id"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> GetTagAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a tag by its name (case-insensitive).
        /// </summary>
        /// <param name="name">The tag name to search for.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The tag DTO if found; otherwise, null.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> GetTagByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all tags in the system.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of all tag DTOs, ordered by name.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagSummaryDto>> GetAllTagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new tag with the specified properties.
        /// </summary>
        /// <param name="createDto">The DTO containing tag creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created tag DTO with generated ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if tag name is invalid or color format is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a tag with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto> CreateTagAsync(CreateTagDto createDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag to update.</param>
        /// <param name="updateDto">The DTO containing tag update data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated tag DTO if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tagId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentException">Thrown if tag has invalid name or color format is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if another tag with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> UpdateTagAsync(int tagId, UpdateTagDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a tag by its ID.
        /// </summary>
        /// <param name="id">The ID of the tag to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if deletion was successful; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="id"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> DeleteTagAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a tag with the specified name already exists.
        /// </summary>
        /// <param name="name">The tag name to check.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if a tag with this name exists; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> TagExistsAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Specialized Retrieval

        /// <summary>
        /// Retrieves the most frequently used tags.
        /// </summary>
        /// <param name="limit">Maximum number of tags to return. Default: 50.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of tag DTOs ordered by usage count (most used first).</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="limit"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetPopularTagsAsync(int limit = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves tags that have been pinned by the user.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of pinned tag DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetPinnedTagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves tags that are marked as visible (not hidden).
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of visible tag DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetVisibleTagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for tags matching a partial name.
        /// </summary>
        /// <param name="searchTerm">The search term (case-insensitive).</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of tag DTOs whose names contain the search term.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="searchTerm"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> SearchTagsAsync(string searchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all tags associated with a specific article.
        /// </summary>
        /// <param name="articleId">The article ID.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of tag DTOs for the specified article.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetTagsByArticleAsync(int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves tags that have a specific color.
        /// </summary>
        /// <param name="color">The hex color code (e.g., "#FF5733").</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of tag DTOs with the specified color.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="color"/> is null or invalid format.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetTagsByColorAsync(string color, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a tag cloud based on usage frequency.
        /// </summary>
        /// <param name="minWeight">Minimum weight to include in cloud.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of tag cloud DTOs with normalized weights.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagCloudDto>> GetTagCloudAsync(int minWeight = 1, CancellationToken cancellationToken = default);

        #endregion

        #region Tag Operations

        /// <summary>
        /// Gets an existing tag by name or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="name">The tag name.</param>
        /// <param name="color">Optional color for the tag if created.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The existing or newly created tag DTO.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto> GetOrCreateTagAsync(string name, string? color = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the usage count for a tag.
        /// </summary>
        /// <param name="tagId">The tag ID.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated tag DTO.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tagId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> UpdateTagUsageAsync(int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Toggles the pinned status of a tag.
        /// </summary>
        /// <param name="tagId">The tag ID.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated tag DTO.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tagId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> TogglePinStatusAsync(int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Toggles the visibility status of a tag.
        /// </summary>
        /// <param name="tagId">The tag ID.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated tag DTO.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tagId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto?> ToggleVisibilityAsync(int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Merges two tags, moving all article associations from source to target.
        /// </summary>
        /// <param name="mergeDto">The DTO containing merge information.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The merged tag DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="mergeDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if either tag ID is less than or equal to 0.</exception>
        /// <exception cref="InvalidOperationException">Thrown if source and target are the same tag or tags not found.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<TagDto> MergeTagsAsync(MergeTagsDto mergeDto, CancellationToken cancellationToken = default);

        #endregion

        #region Batch Operations

        /// <summary>
        /// Imports multiple tags in a single batch operation.
        /// </summary>
        /// <param name="importDtos">The list of tag DTOs to import.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The list of imported tag DTOs with generated IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="importDtos"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if any tag in the list is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> ImportTagsAsync(List<CreateTagDto> importDtos, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or creates multiple tags by name in a single batch operation.
        /// </summary>
        /// <param name="tagNames">The list of tag names to get or create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The list of existing or newly created tag DTOs.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tagNames"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if any tag name is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> GetOrCreateTagsAsync(IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Assigns multiple tags to an article in a single batch operation.
        /// </summary>
        /// <param name="articleId">The article ID.</param>
        /// <param name="tagIds">The list of tag IDs to assign.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The list of assigned tag DTOs.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tagIds"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> AssignTagsToArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple tags from an article in a single batch operation.
        /// </summary>
        /// <param name="articleId">The article ID.</param>
        /// <param name="tagIds">The list of tag IDs to remove.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The list of remaining tag DTOs for the article.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tagIds"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<TagDto>> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Gets the total number of tags in the system.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The total tag count.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> GetTotalTagCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets tag usage statistics for dashboard display.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping tag names to usage counts.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<string, int>> GetTagUsageStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets suggested tag names based on a partial name input.
        /// </summary>
        /// <param name="partialName">The partial tag name to match.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of matching tag names, ordered by popularity.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="partialName"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<string>> GetSuggestedTagNamesAsync(string partialName, CancellationToken cancellationToken = default);

        #endregion
    }
}