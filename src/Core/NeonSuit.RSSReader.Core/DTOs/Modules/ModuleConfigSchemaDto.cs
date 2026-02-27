using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Modules
{
    /// <summary>
    /// Data Transfer Object for module configuration schema.
    /// </summary>
    public class ModuleConfigSchemaDto
    {
        /// <summary>
        /// Configuration properties.
        /// </summary>
        public Dictionary<string, ModuleConfigPropertyDto> Properties { get; set; } = new();
    }

    /// <summary>
    /// Data Transfer Object for module configuration property.
    /// </summary>
    public class ModuleConfigPropertyDto
    {
        /// <summary>
        /// Property type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Default value.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Whether the property is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Minimum value (for numeric types).
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value (for numeric types).
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Allowed values (for enum/select types).
        /// </summary>
        public List<string>? AllowedValues { get; set; }

        /// <summary>
        /// Regular expression pattern (for string validation).
        /// </summary>
        public string? RegexPattern { get; set; }
    }
}