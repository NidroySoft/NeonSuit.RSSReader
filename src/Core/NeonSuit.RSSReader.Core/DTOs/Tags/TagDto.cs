// =======================================================
// Core/DTOs/Tags/TagDto.cs
// =======================================================

using System;

namespace NeonSuit.RSSReader.Core.DTOs.Tags
{
    /// <summary>
    /// Data Transfer Object for complete tag information.
    /// Used in tag management views and tag details.
    /// </summary>
    public class TagDto
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
        /// Optional description of the tag's purpose.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Color in hexadecimal format (#RRGGBB or #RRGGBBAA).
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Optional icon identifier.
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Whether this tag is pinned in the UI.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Whether this tag is visible in tag clouds and lists.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Timestamp when the tag was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time this tag was used.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Number of articles using this tag.
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Calculated darker shade for UI borders/text.
        /// </summary>
        public string DarkColor { get; set; } = string.Empty;

        /// <summary>
        /// Display name with pin indicator.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Time ago string since last use.
        /// </summary>
        public string LastUsedTimeAgo { get; set; } = string.Empty;
    }
}