// =======================================================
// Core/DTOs/Tags/UpdateTagDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Tags
{
    /// <summary>
    /// Data Transfer Object for updating an existing tag.
    /// All properties are optional to support partial updates.
    /// </summary>
    public class UpdateTagDto
    {
        /// <summary>
        /// Display name of the tag.
        /// </summary>
        [MaxLength(50)]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description.
        /// </summary>
        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// Color in hexadecimal format.
        /// </summary>
        [MaxLength(9)]
        [RegularExpression("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
        public string? Color { get; set; }

        /// <summary>
        /// Optional icon identifier.
        /// </summary>
        [MaxLength(30)]
        public string? Icon { get; set; }

        /// <summary>
        /// Whether this tag should be pinned.
        /// </summary>
        public bool? IsPinned { get; set; }

        /// <summary>
        /// Whether this tag should be visible.
        /// </summary>
        public bool? IsVisible { get; set; }
    }
}