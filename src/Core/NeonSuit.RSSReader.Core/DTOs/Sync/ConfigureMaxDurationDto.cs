using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for configuring maximum sync duration.
    /// </summary>
    public class ConfigureMaxDurationDto
    {
        /// <summary>
        /// Maximum duration in minutes.
        /// </summary>
        [Required]
        [Range(1, 1440)]
        public int MaxDurationMinutes { get; set; }
    }
}