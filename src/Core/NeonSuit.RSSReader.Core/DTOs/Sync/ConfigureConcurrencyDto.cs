using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for configuring maximum concurrent tasks.
    /// </summary>
    public class ConfigureConcurrencyDto
    {
        /// <summary>
        /// Maximum concurrent tasks allowed.
        /// </summary>
        [Required]
        [Range(1, 10)]
        public int MaxConcurrentTasks { get; set; }
    }
}