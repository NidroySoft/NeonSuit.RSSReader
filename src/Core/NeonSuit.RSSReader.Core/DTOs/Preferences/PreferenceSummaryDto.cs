// =======================================================
// Core/DTOs/Preferences/PreferenceSummaryDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Lightweight Data Transfer Object for preference list views.
    /// Used in settings UI for displaying preference lists.
    /// </summary>
    public class PreferenceSummaryDto
    {
        /// <summary>
        /// Unique preference key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Current string value.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the preference.
        /// </summary>
        public PreferenceType Type { get; set; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Category for UI grouping.
        /// </summary>
        public string? Category { get; set; }
    }
}