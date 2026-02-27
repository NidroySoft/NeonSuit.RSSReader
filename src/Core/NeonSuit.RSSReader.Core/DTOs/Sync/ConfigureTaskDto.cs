using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for configuring a synchronization task.
    /// </summary>
    public class ConfigureTaskDto
    {
        /// <summary>
        /// Type of the task to configure.
        /// </summary>
        [Required]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the task should be enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Task interval in minutes.
        /// </summary>
        [Range(1, 10080)]
        public int? IntervalMinutes { get; set; }
    }
}