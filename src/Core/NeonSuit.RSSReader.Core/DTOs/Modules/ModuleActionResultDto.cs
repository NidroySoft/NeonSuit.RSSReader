namespace NeonSuit.RSSReader.Core.DTOs.Modules
{
    /// <summary>
    /// Data Transfer Object for module action results.
    /// </summary>
    public class ModuleActionResultDto
    {
        /// <summary>
        /// Whether the action was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional data.
        /// </summary>
        public object? Data { get; set; }
    }
}