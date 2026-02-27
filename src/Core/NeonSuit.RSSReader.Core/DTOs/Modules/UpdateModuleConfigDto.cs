using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Modules
{
    /// <summary>
    /// Data Transfer Object for updating module configuration.
    /// </summary>
    public class UpdateModuleConfigDto
    {
        /// <summary>
        /// Module ID.
        /// </summary>
        [Required]
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>
        /// Configuration values.
        /// </summary>
        public Dictionary<string, object> Config { get; set; } = new();
    }
}