// =======================================================
// Core/DTOs/Tags/TagSummaryDto.cs
// =======================================================

namespace NeonSuit.RSSReader.Core.DTOs.Tags
{
    /// <summary>
    /// Lightweight Data Transfer Object for tag list views.
    /// Used in tag clouds, dropdowns, and article tagging UI.
    /// </summary>
    public class TagSummaryDto
    {
        /// <summary>
        /// Unique identifier of the tag.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Display name of the tag.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Color in hexadecimal format.
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Whether this tag is pinned.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Number of articles using this tag.
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Display name with pin indicator.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}