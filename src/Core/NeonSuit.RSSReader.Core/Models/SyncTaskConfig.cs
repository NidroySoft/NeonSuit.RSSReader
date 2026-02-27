using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Configuration for a synchronization task.
    /// </summary>
    [Table("SyncTaskConfigs")]
    public class SyncTaskConfig
    {

        /// <summary>
        /// Unique identifier.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Type of task (stored as string for database compatibility).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the task.
        /// </summary>
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the task is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Task interval in minutes.
        /// </summary>
        public int IntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Priority level (stored as string).
        /// </summary>
        [MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        /// <summary>
        /// Maximum number of retries on failure.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Delay between retries in minutes.
        /// </summary>
        public int RetryDelayMinutes { get; set; } = 5;

        /// <summary>
        /// Last time this task was scheduled.
        /// </summary>
        public DateTime? LastScheduled { get; set; }

        /// <summary>
        /// Next scheduled run time.
        /// </summary>
        public DateTime? NextScheduled { get; set; }

        /// <summary>
        /// Timestamp of last configuration update.
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}