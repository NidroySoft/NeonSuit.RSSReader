namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization progress updates.
    /// </summary>
    public class SyncProgressDto
    {
        /// <summary>
        /// Type of task reporting progress.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Description of current operation.
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Current progress count.
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Total items to process.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Calculated percentage complete.
        /// </summary>
        public double Percentage { get; set; }
    }
}