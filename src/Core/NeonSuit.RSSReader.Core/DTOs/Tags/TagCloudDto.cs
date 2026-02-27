// =======================================================
// Core/DTOs/Tags/TagCloudDto.cs
// =======================================================

namespace NeonSuit.RSSReader.Core.DTOs.Tags
{
    /// <summary>
    /// Data Transfer Object for tag cloud display.
    /// Optimized for visual representation with size/frequency scaling.
    /// </summary>
    public class TagCloudDto
    {
        /// <summary>
        /// Name of the tag.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Color of the tag.
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Usage count (determines size in cloud).
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// Normalized weight (0-1) for scaling.
        /// </summary>
        public double NormalizedWeight { get; set; }

        /// <summary>
        /// CSS class or size factor.
        /// </summary>
        public string SizeClass { get; set; } = string.Empty;
    }
}