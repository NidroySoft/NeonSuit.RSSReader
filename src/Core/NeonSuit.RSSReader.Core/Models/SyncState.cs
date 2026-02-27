using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents the overall synchronization state.
    /// </summary>
    [Table("SyncStates")]
    public class SyncState
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Current service status.
        /// </summary>
        public string CurrentStatus { get; set; } = "Stopped";

        /// <summary>
        /// Whether the service is paused.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Last time a sync completed.
        /// </summary>
        public DateTime? LastSyncCompleted { get; set; }

        /// <summary>
        /// Maximum sync duration in minutes.
        /// </summary>
        public int MaxSyncDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Timestamp of last state update.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}