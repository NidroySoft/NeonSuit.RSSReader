namespace NeonSuit.RSSReader.Core.DTOs.Opml
{
    /// <summary>
    /// Data Transfer Object for OPML validation results.
    /// </summary>
    public class OpmlValidationResultDto
    {
        /// <summary>
        /// Indicates whether the OPML file is valid and can be imported.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of valid feeds detected in the OPML file.
        /// </summary>
        public int FeedCount { get; set; }

        /// <summary>
        /// List of category names detected in the OPML structure.
        /// </summary>
        public List<string> DetectedCategories { get; set; } = new();

        /// <summary>
        /// OPML specification version detected.
        /// </summary>
        public string? OpmlVersion { get; set; }
    }
}