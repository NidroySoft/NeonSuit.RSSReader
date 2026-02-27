namespace NeonSuit.RSSReader.Core.DTOs.Modules
{
    /// <summary>
    /// Data Transfer Object for module information.
    /// </summary>
    public class ModuleInfoDto
    {
        /// <summary>
        /// Module unique identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Module display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Module version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Module description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Module author.
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Current module status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Module dependencies.
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>
        /// Whether the module has configuration.
        /// </summary>
        public bool HasConfig { get; set; }

        /// <summary>
        /// Module type (e.g., "Service", "UI", "Background").
        /// </summary>
        public string ModuleType { get; set; } = string.Empty;

        /// <summary>
        /// Module assembly location.
        /// </summary>
        public string? AssemblyLocation { get; set; }

        /// <summary>
        /// Module load time.
        /// </summary>
        public DateTime? LoadTime { get; set; }
    }
}