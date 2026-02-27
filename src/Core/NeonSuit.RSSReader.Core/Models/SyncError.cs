using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Records a synchronization error.
    /// </summary>
    [Table("SyncErrors")]
    public class SyncError
    {

        /// <summary>
        /// Unique identifier for the error.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Time when error occurred.
        /// </summary>
        public DateTime ErrorTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Task type where error occurred.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Error message.
        /// </summary>
        [Required]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Stack trace (if available).
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Whether the error is recoverable.
        /// </summary>
        public bool IsRecoverable { get; set; }

        /// <summary>
        /// Retry count for this error.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Whether this error has been resolved.
        /// </summary>
        public bool Resolved { get; set; }

        /// <summary>
        /// Resolution notes.
        /// </summary>
        public string? ResolutionNotes { get; set; }
    }
}