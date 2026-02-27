using NeonSuit.RSSReader.Core.Interfaces.Services;
using System;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Event arguments for preference change notifications.
    /// Provides data about which preference was modified and its new value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event args class is used by <see cref="ISettingsService.OnPreferenceChanged"/>
    /// to notify subscribers about preference modifications in real-time.
    /// </para>
    /// <para>
    /// Subscribers (typically UI components) can use this information to:
    /// <list type="bullet">
    /// <item><description>Update UI immediately without polling</description></item>
    /// <item><description>React to specific preference changes (e.g., theme change)</description></item>
    /// <item><description>Log preference modifications for audit purposes</description></item>
    /// <item><description>Synchronize settings across components</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This class is immutable to prevent accidental modification after creation.
    /// All properties are get-only and set via constructor.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Suscribirse al evento
    /// settingsService.OnPreferenceChanged += (sender, args) =>
    /// {
    ///     if (args.Key == PreferenceKeys.Theme)
    ///     {
    ///         ApplyTheme(args.NewValue);
    ///     }
    /// };
    /// </code>
    /// </example>
    public class PreferenceChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreferenceChangedEventArgs"/> class.
        /// </summary>
        /// <param name="key">The preference key that changed.</param>
        /// <param name="newValue">The new value of the preference.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null or empty.</exception>
        public PreferenceChangedEventArgs(string key, string newValue)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Preference key cannot be null or empty", nameof(key));

            Key = key;
            NewValue = newValue ?? string.Empty;
        }

        /// <summary>
        /// Gets the preference key that was changed.
        /// </summary>
        /// <remarks>
        /// This should match one of the constants defined in <see cref="PreferenceKeys"/>.
        /// Example: <c>"theme"</c>, <c>"auto_mark_as_read"</c>, <c>"notification_enabled"</c>
        /// </remarks>
        public string Key { get; }

        /// <summary>
        /// Gets the new value of the preference.
        /// </summary>
        /// <remarks>
        /// Values are always stored as strings, but can be parsed to their target types
        /// using the helper methods in <see cref="PreferenceHelper"/>.
        /// </remarks>
        public string NewValue { get; }

        /// <summary>
        /// Returns a string representation of the event arguments for logging.
        /// </summary>
        /// <returns>Formatted string with key and new value.</returns>
        public override string ToString()
        {
            return $"PreferenceChanged: '{Key}' = '{NewValue}'";
        }
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (Low - v1.x): Add OldValue property to track previous value
// What to do: Add string? OldValue { get; } and populate in constructor
// Why (benefit): Allow subscribers to know what changed (e.g., for undo functionality)
// Estimated effort: 30 min
// Risk level: Low (backward compatible if old value is optional)
// Potential impact: Enables undo/redo functionality in settings UI

// TODO (Low - v1.x): Add ChangeType enum to distinguish between add, update, reset
// What to do: Add PreferenceChangeType enum (Added, Updated, Reset, Deleted)
// Why (benefit): Subscribers can react differently to different change types
// Estimated effort: 1 hour
// Risk level: Low
// Potential impact: More granular event handling