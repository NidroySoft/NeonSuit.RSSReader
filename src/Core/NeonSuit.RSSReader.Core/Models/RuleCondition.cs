using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents an individual condition within a rule.
    /// Allows building complex logical expressions with multiple criteria.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rule conditions are the building blocks for complex rule logic.
    /// Each condition evaluates a specific article field against a value using a comparison operator.
    /// </para>
    /// <para>
    /// Key features:
    /// <list type="bullet">
    /// <item><description>Grouping: Conditions can be grouped using <see cref="GroupId"/> for AND/OR logic</description></item>
    /// <item><description>Ordering: Within a group, conditions are evaluated in <see cref="Order"/> sequence</description></item>
    /// <item><description>Negation: Each condition can be negated with <see cref="Negate"/></description></item>
    /// <item><description>Rich operators: Contains, Equals, Regex, Greater/Less Than, etc.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example: "Title contains 'news' AND (Author equals 'John' OR Author equals 'Jane')"
    /// would use two conditions in group 0 (AND) and two conditions in group 1 (OR).
    /// </para>
    /// </remarks>
    [Table("RuleConditions")]
    public class RuleCondition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleCondition"/> class.
        /// Sets default values for operators and flags.
        /// </summary>
        public RuleCondition()
        {
            Field = RuleFieldTarget.Title;
            Operator = RuleOperator.Contains;
            CombineWithNext = LogicalOperator.AND;
            GroupId = 0;
            Order = 0;
            IsCaseSensitive = false;
            Negate = false;
            Value = string.Empty;
            Value2 = string.Empty;
            RegexPattern = string.Empty;
            DateFormat = string.Empty;
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Foreign Keys

        /// <summary>
        /// Foreign key to the parent Rule.
        /// </summary>
        [Required]
        public int RuleId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Navigation property to the parent rule.
        /// </summary>
        [ForeignKey(nameof(RuleId))]
        public virtual Rule Rule { get; set; } = null!;

        #endregion

        #region Grouping & Ordering

        /// <summary>
        /// Group identifier for organizing conditions (0 = default group).
        /// Conditions in the same group are evaluated together with the same logical operator.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Execution order within the group (ascending).
        /// Lower values are evaluated first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// How this condition combines with the NEXT condition in the same group.
        /// </summary>
        public LogicalOperator CombineWithNext { get; set; }

        #endregion

        #region Condition Definition

        /// <summary>
        /// Which article field to evaluate.
        /// </summary>
        public RuleFieldTarget Field { get; set; }

        /// <summary>
        /// Comparison operator to apply.
        /// </summary>
        public RuleOperator Operator { get; set; }

        /// <summary>
        /// Primary value to compare against.
        /// </summary>
        [MaxLength(500)]
        public string Value { get; set; }

        /// <summary>
        /// Secondary value (for BETWEEN, NOT BETWEEN operations).
        /// </summary>
        [MaxLength(500)]
        public string Value2 { get; set; }

        /// <summary>
        /// Whether the comparison is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// Whether to negate this entire condition (NOT).
        /// </summary>
        public bool Negate { get; set; }

        #endregion

        #region Specialized Operators

        /// <summary>
        /// For Regex operator: the pattern to match.
        /// </summary>
        [MaxLength(500)]
        public string RegexPattern { get; set; }

        /// <summary>
        /// For date comparisons: format string (e.g., "yyyy-MM-dd").
        /// </summary>
        [MaxLength(50)]
        public string DateFormat { get; set; }

        #endregion

        #region Computed Properties (Not Mapped)

        /// <summary>
        /// Human-readable description of the condition for UI display.
        /// </summary>
        [NotMapped]
        public string HumanReadable => GetHumanReadable();

        /// <summary>
        /// Indicates whether the condition is valid (has required fields for its operator).
        /// </summary>
        [NotMapped]
        public bool IsValid => ValidateCondition();

        /// <summary>
        /// Gets the display name of the field in Spanish.
        /// </summary>
        [NotMapped]
        public string FieldDisplayName => Field switch
        {
            RuleFieldTarget.Title => "Título",
            RuleFieldTarget.Content => "Contenido",
            RuleFieldTarget.Author => "Autor",
            RuleFieldTarget.Categories => "Categorías",
            RuleFieldTarget.AllFields => "Título y Contenido",
            RuleFieldTarget.AnyField => "Título o Contenido",
            _ => "Campo"
        };

        /// <summary>
        /// Gets the display name of the operator in Spanish.
        /// </summary>
        [NotMapped]
        public string OperatorDisplayName => Operator switch
        {
            RuleOperator.Contains => "contiene",
            RuleOperator.Equals => "es igual a",
            RuleOperator.StartsWith => "comienza con",
            RuleOperator.EndsWith => "termina con",
            RuleOperator.NotContains => "no contiene",
            RuleOperator.NotEquals => "no es igual a",
            RuleOperator.Regex => "coincide con",
            RuleOperator.GreaterThan => "es mayor que",
            RuleOperator.LessThan => "es menor que",
            RuleOperator.IsEmpty => "está vacío",
            RuleOperator.IsNotEmpty => "no está vacío",
            _ => "???"
        };

        /// <summary>
        /// Gets the display name of the logical operator in Spanish.
        /// </summary>
        [NotMapped]
        public string CombineWithNextDisplayName => CombineWithNext switch
        {
            LogicalOperator.AND => "Y",
            LogicalOperator.OR => "O",
            _ => "Y"
        };

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Generates a human-readable description of the condition.
        /// </summary>
        private string GetHumanReadable()
        {
            var negatePrefix = Negate ? "NO " : "";
            var caseSensitiveSuffix = IsCaseSensitive ? " (case-sensitive)" : "";

            // Regex special case
            if (Operator == RuleOperator.Regex && !string.IsNullOrEmpty(RegexPattern))
                return $"{negatePrefix}{FieldDisplayName} {OperatorDisplayName} '{RegexPattern}'{caseSensitiveSuffix}";

            // Empty/NotEmpty special case
            if (Operator == RuleOperator.IsEmpty || Operator == RuleOperator.IsNotEmpty)
                return $"{negatePrefix}{FieldDisplayName} {OperatorDisplayName}{caseSensitiveSuffix}";

            // Range operators (if Value2 is used)
            if (Operator == RuleOperator.Between || Operator == RuleOperator.NotBetween)
                return $"{negatePrefix}{FieldDisplayName} {OperatorDisplayName} '{Value}' y '{Value2}'{caseSensitiveSuffix}";

            // Standard case
            return $"{negatePrefix}{FieldDisplayName} {OperatorDisplayName} '{Value}'{caseSensitiveSuffix}";
        }

        /// <summary>
        /// Validates that the condition has all required fields for its operator.
        /// </summary>
        private bool ValidateCondition()
        {
            try
            {
                // Basic validation for value requirements
                if (RequiresValue() && string.IsNullOrWhiteSpace(Value))
                    return false;

                if (RequiresSecondValue() && string.IsNullOrWhiteSpace(Value2))
                    return false;

                // Operator-specific validation
                return Operator switch
                {
                    RuleOperator.Regex => ValidateRegexPattern(),
                    RuleOperator.GreaterThan or RuleOperator.LessThan or
                    RuleOperator.Between or RuleOperator.NotBetween => ValidateNumericOrDate(),
                    _ => true
                };
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if the operator requires a value.
        /// </summary>
        private bool RequiresValue()
        {
            return Operator != RuleOperator.IsEmpty &&
                   Operator != RuleOperator.IsNotEmpty;
        }

        /// <summary>
        /// Determines if the operator requires a second value.
        /// </summary>
        private bool RequiresSecondValue()
        {
            return Operator == RuleOperator.Between ||
                   Operator == RuleOperator.NotBetween;
        }

        /// <summary>
        /// Validates a regex pattern.
        /// </summary>
        private bool ValidateRegexPattern()
        {
            if (string.IsNullOrWhiteSpace(RegexPattern))
                return false;

            try
            {
                _ = new Regex(RegexPattern);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates numeric or date values for comparison operators.
        /// </summary>
        private bool ValidateNumericOrDate()
        {
            // Try numeric
            if (double.TryParse(Value, out _))
                return true;

            // Try date
            if (DateTime.TryParse(Value, out _))
                return true;

            return false;
        }

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on (RuleId, GroupId, Order) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(c => new { c.RuleId, c.GroupId, c.Order });
// Why (benefit): Fast retrieval of conditions for a rule in correct evaluation order
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Significantly faster rule loading

// TODO (High - v1.x): Add index on RuleId for foreign key queries
// What to do: In OnModelCreating, add: entity.HasIndex(c => c.RuleId);
// Why (benefit): Speeds up queries that load all conditions for a rule
// Estimated effort: 15 min
// Risk level: Low
// Potential impact: Improved performance when editing rules

// TODO (Medium - v1.x): Add validation for Value based on Field type
// What to do: Add validation rules per field (e.g., dates for PublishedDate)
// Why (benefit): Prevent invalid comparisons (e.g., string vs date)
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Better data integrity and user feedback

// TODO (Low - v1.x): Add support for custom functions in conditions
// What to do: Add FunctionName property for predefined functions (LENGTH, WORD_COUNT)
// Why (benefit): Enable conditions like "word count > 500"
// Estimated effort: 2 days
// Risk level: Medium
// Potential impact: More powerful rule conditions

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent lost updates if conditions modified concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in concurrent scenarios