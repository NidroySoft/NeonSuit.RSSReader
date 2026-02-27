using NeonSuit.RSSReader.Core.DTOs.Cleanup;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models.Cleanup;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupCompleted"/> event.
    /// Contains the final results of the cleanup operation.
    /// </summary>
    public class CleanupCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="result">The result of the cleanup operation.</param>
        /// <param name="completionTime">The UTC timestamp when the cleanup completed.</param>
        /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
        public CleanupCompletedEventArgs(CleanupResultDto result, DateTime completionTime)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            CompletionTime = completionTime;
        }

        /// <summary>
        /// Gets the result of the cleanup operation.
        /// </summary>
        public CleanupResultDto Result { get; }

        /// <summary>
        /// Gets the UTC timestamp when the cleanup operation completed.
        /// </summary>
        public DateTime CompletionTime { get; }

        /// <summary>
        /// Gets a value indicating whether the cleanup completed successfully.
        /// </summary>
        public bool Success => Result.Success;

        /// <summary>
        /// Gets the duration of the cleanup operation.
        /// </summary>
        public TimeSpan Duration => Result.Duration;

        /// <summary>
        /// Gets a summary of the completed cleanup for logging purposes.
        /// </summary>
        public override string ToString()
        {
            var status = Result.Success ? "completed successfully" : "failed";
            return $"Cleanup {status} at {CompletionTime:yyyy-MM-dd HH:mm:ss UTC} " +
                   $"(Duration: {Duration.TotalSeconds:F1}s, " +
                   $"Articles deleted: {Result.ArticleCleanup?.ArticlesDeleted ?? 0}, " +
                   $"Space freed: {Result.SpaceFreedBytes / (1024 * 1024):F1} MB)";
        }
    }
}