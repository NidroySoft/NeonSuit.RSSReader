// =======================================================
// Core/DTOs/Preferences/ResetPreferencesDto.cs
// =======================================================

using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Data Transfer Object for resetting preferences to defaults.
    /// </summary>
    public class ResetPreferencesDto
    {
        /// <summary>
        /// List of specific keys to reset. If empty, resets all.
        /// </summary>
        public List<string>? Keys { get; set; }

        /// <summary>
        /// Whether to reset all preferences.
        /// </summary>
        public bool ResetAll { get; set; }
    }
}