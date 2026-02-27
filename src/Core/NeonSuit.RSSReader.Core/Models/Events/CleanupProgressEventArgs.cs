using NeonSuit.RSSReader.Core.Interfaces.Services;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupProgress"/> event.
    /// Reports progress updates during a cleanup operation.
    /// </summary>
    public class CleanupProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupProgressEventArgs"/> class.
        /// </summary>
        /// <param name="currentStep">Description of the current cleanup step being executed.</param>
        /// <param name="stepNumber">The current step number (1-based).</param>
        /// <param name="totalSteps">The total number of steps in the cleanup process.</param>
        /// <exception cref="ArgumentNullException">Thrown when currentStep is null.</exception>
        public CleanupProgressEventArgs(string currentStep, int stepNumber, int totalSteps)
        {
            CurrentStep = currentStep ?? throw new ArgumentNullException(nameof(currentStep));
            StepNumber = stepNumber;
            TotalSteps = totalSteps;
        }

        /// <summary>
        /// Gets the description of the current cleanup step.
        /// </summary>
        public string CurrentStep { get; }

        /// <summary>
        /// Gets the current step number (1-based).
        /// </summary>
        public int StepNumber { get; }

        /// <summary>
        /// Gets the total number of steps in the cleanup process.
        /// </summary>
        public int TotalSteps { get; }

        /// <summary>
        /// Gets the completion percentage (0-100).
        /// </summary>
        public int PercentComplete => TotalSteps > 0 ? (StepNumber * 100) / TotalSteps : 0;

        /// <summary>
        /// Gets a value indicating whether this is the final step.
        /// </summary>
        public bool IsFinalStep => StepNumber >= TotalSteps;
    }
}