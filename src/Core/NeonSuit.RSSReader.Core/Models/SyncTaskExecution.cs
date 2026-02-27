using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Records a single execution of a sync task.
    /// </summary>
    [Table("SyncTaskExecutions")]
    public class SyncTaskExecution
    {

        /// <summary>
        /// Primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Type of task executed.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was a manual trigger.
        /// </summary>
        public bool IsManual { get; set; }

        /// <summary>
        /// Request ID for tracking.
        /// </summary>
        [MaxLength(50)]
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Start time of execution.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time of execution.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Whether execution was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Results summary (JSON).
        /// </summary>
        public string? ResultsJson { get; set; }

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }
    }
}