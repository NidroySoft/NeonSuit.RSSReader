using SQLite;
using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents an individual condition within a rule.
    /// Allows building complex logical expressions with multiple criteria.
    /// </summary>
    [Table("RuleConditions")]
    public class RuleCondition
    {
        public virtual Rule Rule { get; set; } = null!;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the parent Rule
        /// </summary>
        [Indexed, NotNull]
        public int RuleId { get; set; }

        /// <summary>
        /// Group identifier for organizing conditions (0 = default group)
        /// Conditions in the same group are evaluated together
        /// </summary>
        [Indexed]
        public int GroupId { get; set; } = 0;

        /// <summary>
        /// Execution order within the group (ascending)
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Which article field to evaluate
        /// </summary>
        public RuleFieldTarget Field { get; set; } = RuleFieldTarget.Title;

        /// <summary>
        /// Comparison operator
        /// </summary>
        public RuleOperator Operator { get; set; } = RuleOperator.Contains;

        /// <summary>
        /// Value to compare against (primary value)
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Secondary value (for BETWEEN, NOT BETWEEN operations)
        /// </summary>
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the comparison is case-sensitive
        /// </summary>
        public bool IsCaseSensitive { get; set; } = false;

        /// <summary>
        /// For Regex operator: the pattern to match
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// How this condition combines with the NEXT condition in the same group
        /// </summary>
        public LogicalOperator CombineWithNext { get; set; } = LogicalOperator.AND;

        /// <summary>
        /// Whether to negate this entire condition (NOT)
        /// </summary>
        public bool Negate { get; set; } = false;

        /// <summary>
        /// For date comparisons: format string (e.g., "yyyy-MM-dd")
        /// </summary>
        public string DateFormat { get; set; } = string.Empty;

        // ===== PROPIEDADES CALCULADAS =====

        [Ignore]
        public string HumanReadable => GetHumanReadable();

        [Ignore]
        public bool IsValid => ValidateCondition();

        private string GetHumanReadable()
        {
            var fieldName = Field switch
            {
                RuleFieldTarget.Title => "Título",
                RuleFieldTarget.Content => "Contenido",
                RuleFieldTarget.Author => "Autor",
                RuleFieldTarget.Categories => "Categorías",
                RuleFieldTarget.AllFields => "Título y Contenido",
                RuleFieldTarget.AnyField => "Título o Contenido",
                _ => "Campo"
            };

            var opName = Operator switch
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

            var negatePrefix = Negate ? "NO " : "";
            var caseSensitiveSuffix = IsCaseSensitive ? " (case-sensitive)" : "";

            if (Operator == RuleOperator.Regex && !string.IsNullOrEmpty(RegexPattern))
                return $"{negatePrefix}{fieldName} {opName} '{RegexPattern}'{caseSensitiveSuffix}";

            if (Operator == RuleOperator.IsEmpty || Operator == RuleOperator.IsNotEmpty)
                return $"{negatePrefix}{fieldName} {opName}{caseSensitiveSuffix}";

            return $"{negatePrefix}{fieldName} {opName} '{Value}'{caseSensitiveSuffix}";
        }

        private bool ValidateCondition()
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(Value) &&
                Operator != RuleOperator.IsEmpty &&
                Operator != RuleOperator.IsNotEmpty)
            {
                return false;
            }

            // Validación específica por operador
            switch (Operator)
            {
                case RuleOperator.Regex:
                    if (string.IsNullOrWhiteSpace(RegexPattern))
                        return false;
                    try
                    {
                        new System.Text.RegularExpressions.Regex(RegexPattern);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                case RuleOperator.GreaterThan:
                case RuleOperator.LessThan:
                    // Para fechas/números
                    return !string.IsNullOrWhiteSpace(Value);

                default:
                    return true;
            }
        }
    }
}