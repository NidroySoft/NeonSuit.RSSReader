using NeonSuit.RSSReader.Core.DTOs.Cleanup;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models.Cleanup;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupStarting"/> event.
    /// Contains configuration and timing information about the cleanup operation that is about to begin.
    /// </summary>
    public class CleanupStartingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupStartingEventArgs"/> class.
        /// </summary>
        /// <param name="configuration">The cleanup configuration that will be used for this operation.</param>
        /// <param name="startTime">The UTC timestamp when the cleanup operation is starting.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
        public CleanupStartingEventArgs(CleanupConfigurationDto configuration, DateTime startTime)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            StartTime = startTime;
        }

        /// <summary>
        /// Gets the cleanup configuration that will be used for this operation.
        /// </summary>
        public CleanupConfigurationDto Configuration { get; }

        /// <summary>
        /// Gets the UTC timestamp when the cleanup operation is starting.
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets a value indicating whether this is an automatic scheduled cleanup
        /// or a manually triggered cleanup.
        /// </summary>
        public bool IsAutomatic => Configuration.AutoCleanupEnabled;

        /// <summary>
        /// Gets a summary of the cleanup operation for logging or display purposes.
        /// </summary>
        public override string ToString()
        {
            return $"Cleanup starting at {StartTime:yyyy-MM-dd HH:mm:ss UTC} - " +
                   $"Retention: {Configuration.ArticleRetentionDays} days, " +
                   $"Keep Favorites: {Configuration.KeepFavorites}, " +
                   $"Keep Unread: {Configuration.KeepUnread}";
        }
    }
}