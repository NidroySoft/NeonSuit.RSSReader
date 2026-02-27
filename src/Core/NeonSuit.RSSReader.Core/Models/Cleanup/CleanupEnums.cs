namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Risk levels for cleanup operations, indicating the potential impact
    /// based on the percentage of articles that would be deleted.
    /// </summary>
    public enum CleanupRiskLevel
    {
        /// <summary>
        /// Minimal risk - less than 10% of articles would be deleted.
        /// </summary>
        Minimal,

        /// <summary>
        /// Low risk - between 10% and 25% of articles would be deleted.
        /// </summary>
        Low,

        /// <summary>
        /// Medium risk - between 25% and 50% of articles would be deleted.
        /// </summary>
        Medium,

        /// <summary>
        /// High risk - between 50% and 75% of articles would be deleted.
        /// </summary>
        High,

        /// <summary>
        /// Critical risk - more than 75% of articles would be deleted.
        /// </summary>
        Critical
    }
}