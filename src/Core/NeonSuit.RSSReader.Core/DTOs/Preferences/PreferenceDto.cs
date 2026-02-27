// =======================================================
// Core/DTOs/Preferences/PreferenceDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Data Transfer Object for user preference information.
    /// Used for displaying and editing individual preferences.
    /// </summary>
    public class PreferenceDto
    {
        /// <summary>
        /// Unique identifier of the preference.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Unique preference key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// String value of the preference.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp of last modification.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Boolean typed value (calculated).
        /// </summary>
        public bool BoolValue { get; set; }

        /// <summary>
        /// Integer typed value (calculated).
        /// </summary>
        public int IntValue { get; set; }

        /// <summary>
        /// Double typed value (calculated).
        /// </summary>
        public double DoubleValue { get; set; }

        /// <summary>
        /// Data type of the preference (for UI).
        /// </summary>
        public PreferenceType Type { get; set; }

        /// <summary>
        /// Display name derived from the key.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this preference controls.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Category for UI organization.
        /// </summary>
        public string? Category { get; set; }
    }
}