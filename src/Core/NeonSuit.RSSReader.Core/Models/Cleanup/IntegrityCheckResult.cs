namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Result of database integrity check.
    /// Contains validation results, errors, and warnings from integrity verification.
    /// </summary>
    public class IntegrityCheckResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrityCheckResult"/> class.
        /// </summary>
        public IntegrityCheckResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the database passed integrity verification.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error messages from integrity check.
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets or sets the warning messages (e.g., foreign key violations).
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Gets or sets the duration of the integrity check.
        /// </summary>
        public TimeSpan CheckDuration { get; set; }

        /// <summary>
        /// Gets a value indicating whether any errors were found.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Gets a value indicating whether any warnings were found.
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets a brief summary of the integrity check.
        /// </summary>
        public override string ToString()
        {
            if (IsValid)
                return $"Integrity check passed in {CheckDuration.TotalSeconds:F1}s";

            return $"Integrity check failed with {Errors.Count} errors in {CheckDuration.TotalSeconds:F1}s";
        }
    }
}