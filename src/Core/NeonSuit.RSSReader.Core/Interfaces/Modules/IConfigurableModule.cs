using NeonSuit.RSSReader.Core.Interfaces.Modules;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Modules
{
    /// <summary>
    /// Interface for modules that support configuration.
    /// </summary>
    public interface IConfigurableModule : IModule
    {
        /// <summary>
        /// Gets the module configuration schema.
        /// </summary>
        ModuleConfigSchema ConfigSchema { get; }

        /// <summary>
        /// Gets the current module configuration.
        /// </summary>
        IReadOnlyDictionary<string, object> Config { get; }

        /// <summary>
        /// Updates the module configuration.
        /// </summary>
        /// <param name="newConfig">New configuration values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateConfigAsync(IReadOnlyDictionary<string, object> newConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a configuration.
        /// </summary>
        /// <param name="config">Configuration to validate.</param>
        /// <returns>Validation result.</returns>
        ModuleConfigValidationResult ValidateConfig(IReadOnlyDictionary<string, object> config);
    }

    /// <summary>
    /// Module configuration schema.
    /// </summary>
    public class ModuleConfigSchema
    {
        /// <summary>
        /// Gets or sets the configuration properties.
        /// </summary>
        public Dictionary<string, ModuleConfigProperty> Properties { get; set; } = new();
    }

    /// <summary>
    /// Module configuration property.
    /// </summary>
    public class ModuleConfigProperty
    {
        /// <summary>
        /// Gets or sets the property type.
        /// </summary>
        public ModuleConfigPropertyType Type { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default value.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets whether the property is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Gets or sets the minimum value (for numeric types).
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Gets or sets the maximum value (for numeric types).
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Gets or sets the allowed values (for enum/select types).
        /// </summary>
        public string[]? AllowedValues { get; set; }

        /// <summary>
        /// Gets or sets the regular expression pattern (for string validation).
        /// </summary>
        public string? RegexPattern { get; set; }
    }

    /// <summary>
    /// Module configuration property types.
    /// </summary>
    public enum ModuleConfigPropertyType
    {
        /// <summary>String property.</summary>
        String,
        /// <summary>Boolean property.</summary>
        Boolean,
        /// <summary>Integer property.</summary>
        Integer,
        /// <summary>Decimal property.</summary>
        Decimal,
        /// <summary>Enumeration/Select property.</summary>
        Enum,
        /// <summary>Password property (masked input).</summary>
        Password,
        /// <summary>File path property.</summary>
        FilePath,
        /// <summary>Directory path property.</summary>
        DirectoryPath,
        /// <summary>URL property.</summary>
        Url,
        /// <summary>Email property.</summary>
        Email,
        /// <summary>Color property (hex).</summary>
        Color
    }

    /// <summary>
    /// Module configuration validation result.
    /// </summary>
    public class ModuleConfigValidationResult
    {
        /// <summary>
        /// Gets or sets whether the configuration is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the validation errors.
        /// </summary>
        public Dictionary<string, string> Errors { get; set; } = new();
    }
}