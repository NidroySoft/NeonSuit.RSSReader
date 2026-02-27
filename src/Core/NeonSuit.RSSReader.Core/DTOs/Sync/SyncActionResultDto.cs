namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization action results.
    /// </summary>
    public class SyncActionResultDto
    {
        /// <summary>
        /// Whether the action was triggered successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// ID of the triggered task request.
        /// </summary>
        public string? RequestId { get; set; }
    }
}