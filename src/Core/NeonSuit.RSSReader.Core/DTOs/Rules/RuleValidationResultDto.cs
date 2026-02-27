using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for rule validation results.
    /// </summary>
    public class RuleValidationResultDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleValidationResultDto"/> class.
        /// </summary>
        public RuleValidationResultDto()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of validation errors.
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets or sets the list of validation warnings (non-critical issues).
        /// </summary>
        public List<string> Warnings { get; set; }
    }
}