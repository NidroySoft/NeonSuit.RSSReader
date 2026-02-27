// =======================================================
// Core/DTOs/Tags/MergeTagsDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Tags
{
    /// <summary>
    /// Data Transfer Object for merging two tags.
    /// </summary>
    public class MergeTagsDto
    {
        /// <summary>
        /// ID of the source tag (will be merged INTO target).
        /// </summary>
        [Required]
        public int SourceTagId { get; set; }

        /// <summary>
        /// ID of the target tag (will receive all articles from source).
        /// </summary>
        [Required]
        public int TargetTagId { get; set; }

        /// <summary>
        /// Whether to delete the source tag after merge.
        /// </summary>
        public bool DeleteSource { get; set; } = true;
    }
}