// =======================================================
// Core/DTOs/Preferences/PreferenceCategoryDto.cs
// =======================================================

using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Data Transfer Object for categorized preferences in UI.
    /// Used for rendering grouped settings panels.
    /// </summary>
    public class PreferenceCategoryDto
    {
        /// <summary>
        /// Category name (e.g., "Interfaz", "Lectura", "Feeds").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Preferences in this category.
        /// </summary>
        public List<PreferenceSummaryDto> Preferences { get; set; } = new();
    }
}