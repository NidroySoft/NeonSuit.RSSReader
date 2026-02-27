// =======================================================
// Core/DTOs/Preferences/ExportImportDto.cs
// =======================================================

using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Preferences
{
    /// <summary>
    /// Data Transfer Object for preference export/import operations.
    /// </summary>
    public class ExportImportDto
    {
        /// <summary>
        /// Export version for compatibility checking.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Export timestamp.
        /// </summary>
        public DateTime ExportedAt { get; set; }

        /// <summary>
        /// Application version that created this export.
        /// </summary>
        public string AppVersion { get; set; } = string.Empty;

        /// <summary>
        /// Dictionary of preferences.
        /// </summary>
        public Dictionary<string, string> Preferences { get; set; } = new();
    }
}