using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for tag management operations.
    /// Provides business logic and validation for tag operations.
    /// </summary>
    public interface ITagService
    {
        // Basic CRUD Operations
        Task<Tag> GetTagAsync(int id);
        Task<Tag?> GetTagByNameAsync(string name);
        Task<List<Tag>> GetAllTagsAsync();
        Task<int> CreateTagAsync(Tag tag);
        Task UpdateTagAsync(Tag tag);
        Task DeleteTagAsync(int id);
        Task<bool> TagExistsAsync(string name);

        // Specialized Retrieval
        Task<List<Tag>> GetPopularTagsAsync(int limit = 50);
        Task<List<Tag>> GetPinnedTagsAsync();
        Task<List<Tag>> GetVisibleTagsAsync();
        Task<List<Tag>> SearchTagsAsync(string searchTerm);
        Task<List<Tag>> GetTagsByArticleAsync(int articleId);
        Task<List<Tag>> GetTagsByColorAsync(string color);

        // Tag Operations
        Task<Tag> GetOrCreateTagAsync(string name, string? color = null);
        Task UpdateTagUsageAsync(int tagId);
        Task TogglePinStatusAsync(int tagId);
        Task ToggleVisibilityAsync(int tagId);
        Task MergeTagsAsync(int sourceTagId, int targetTagId);

        // Batch Operations
        Task<List<Tag>> ImportTagsAsync(List<Tag> tags);
        Task<List<Tag>> GetOrCreateTagsAsync(IEnumerable<string> tagNames);
        Task AssignTagsToArticleAsync(int articleId, IEnumerable<int> tagIds);
        Task RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds);

        // Statistics
        Task<int> GetTotalTagCountAsync();
        Task<Dictionary<string, int>> GetTagUsageStatisticsAsync();
        Task<List<string>> GetSuggestedTagNamesAsync(string partialName);

        /// <summary>
        /// Event triggered when a tag is created, updated, or deleted.
        /// </summary>
        event EventHandler<TagChangedEventArgs> OnTagChanged;
    }

    /// <summary>
    /// Event arguments for tag change notifications.
    /// </summary>
    public class TagChangedEventArgs : EventArgs
    {
        public int TagId { get; }
        public string TagName { get; }
        public TagChangeType ChangeType { get; }

        public TagChangedEventArgs(int tagId, string tagName, TagChangeType changeType)
        {
            TagId = tagId;
            TagName = tagName;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// Type of change performed on a tag.
    /// </summary>
    public enum TagChangeType
    {
        Created,
        Updated,
        Deleted,
        Pinned,
        Unpinned,
        VisibilityChanged
    }
}