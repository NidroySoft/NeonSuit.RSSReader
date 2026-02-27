// =======================================================
// Core/DTOs/Preferences/UpdatePreferenceDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Data Transfer Object for updating a preference value.
    /// </summary>
    public class UpdatePreferenceDto
    {
        /// <summary>
        /// Preference key to update.
        /// </summary>
        [Required]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// New value for the preference.
        /// </summary>
        [Required]
        [MaxLength(4000)]
        public string Value { get; set; } = string.Empty;
    }
}