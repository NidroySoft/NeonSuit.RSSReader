using SQLite;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a business rule for automated article processing.
    /// Supports complex conditions and multiple actions.
    /// </summary>
    [Table("Rules")]
    public class Rule
    {
        public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Descriptive name for the rule (e.g., "Alertas Cuba Urgentes")
        /// </summary>
        [Required, System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description explaining the rule's purpose
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        // ===== CONDICIONES =====

        /// <summary>
        /// Field where the search will be performed
        /// </summary>
        public RuleFieldTarget Target { get; set; } = RuleFieldTarget.Title;

        /// <summary>
        /// Search operator for the main condition
        /// </summary>
        public RuleOperator Operator { get; set; } = RuleOperator.Contains;

        /// <summary>
        /// Value to compare against
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Second value (for range operations like Between)
        /// </summary>
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the search is case-sensitive
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// For regex operations: the regex pattern
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Group identifier for complex conditions (AND/OR groups)
        /// 0 = main condition, 1+ = additional condition groups
        /// </summary>
        public int ConditionGroup { get; set; }

        /// <summary>
        /// Logical operator to combine with next condition
        /// </summary>
        public LogicalOperator NextConditionOperator { get; set; } = LogicalOperator.AND;

        // ===== ALCANCE =====

        /// <summary>
        /// Where the rule applies
        /// </summary>
        public RuleScope Scope { get; set; } = RuleScope.AllFeeds;

        /// <summary>
        /// JSON array of feed IDs (if Scope = SpecificFeeds)
        /// e.g., "[1, 5, 12]"
        /// </summary>
        public string FeedIds { get; set; } = "[]";

        /// <summary>
        /// JSON array of category IDs (if Scope = SpecificCategories)
        /// </summary>
        public string CategoryIds { get; set; } = "[]";

        // ===== ACCIONES =====

        /// <summary>
        /// Primary action type
        /// </summary>
        public RuleActionType ActionType { get; set; } = RuleActionType.SendNotification;

        /// <summary>
        /// JSON array of tag IDs to apply (for ApplyTags action)
        /// </summary>
        public string TagIds { get; set; } = "[]";

        /// <summary>
        /// Category ID to move article to (for MoveToCategory action)
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Path to sound file for PlaySound action
        /// </summary>
        public string SoundPath { get; set; } = string.Empty;

        /// <summary>
        /// Template for notification (supports variables like {Title}, {Source})
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string NotificationTemplate { get; set; } = "{Title}\n\n{Source}";

        /// <summary>
        /// Notification priority level
        /// </summary>
        public NotificationPriority NotificationPriority { get; set; } = NotificationPriority.Normal;

        /// <summary>
        /// Color for highlighting articles (in hex, e.g., "#FF0000")
        /// </summary>
        public string HighlightColor { get; set; } = string.Empty;

        // ===== CONFIGURACIÓN =====

        /// <summary>
        /// Execution order (lower numbers execute first)
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Whether the rule is currently active
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Stop processing other rules if this one matches
        /// </summary>
        public bool StopOnMatch { get; set; }

        /// <summary>
        /// Only apply to new articles (not retroactively)
        /// </summary>
        public bool OnlyNewArticles { get; set; } = true;

        /// <summary>
        /// Date when the rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last time the rule was modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of times this rule has been triggered (statistics)
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Last time this rule matched an article
        /// </summary>
        public DateTime? LastMatchDate { get; set; }

        // ===== PROPIEDADES DE NAVEGACIÓN (no persistentes) =====

        [Ignore]
        public List<int> FeedIdList =>
            string.IsNullOrEmpty(FeedIds) ? new List<int>() :
            System.Text.Json.JsonSerializer.Deserialize<List<int>>(FeedIds) ?? new List<int>();

        [Ignore]
        public List<int> CategoryIdList =>
            string.IsNullOrEmpty(CategoryIds) ? new List<int>() :
            System.Text.Json.JsonSerializer.Deserialize<List<int>>(CategoryIds) ?? new List<int>();

        [Ignore]
        public List<int> TagIdList =>
            string.IsNullOrEmpty(TagIds) ? new List<int>() :
            System.Text.Json.JsonSerializer.Deserialize<List<int>>(TagIds) ?? new List<int>();

        /// <summary>
        /// Human-readable description of the rule
        /// </summary>
        [Ignore]
        public string Summary => $"{Target} {Operator} \"{Value}\"";

        /// <summary>
        /// Example: "Título contiene 'Cuba' Y Contenido contiene 'urgente'"
        /// </summary>
        [Ignore]
        public string HumanReadableCondition => GetHumanReadableCondition();

        private string GetHumanReadableCondition()
        {
            var targetStr = Target switch
            {
                RuleFieldTarget.Title => "Título",
                RuleFieldTarget.Content => "Contenido",
                RuleFieldTarget.Author => "Autor",
                RuleFieldTarget.Categories => "Categorías",
                RuleFieldTarget.AllFields => "Título y Contenido",
                RuleFieldTarget.AnyField => "Título o Contenido",
                _ => "Campo"
            };

            var operatorStr = Operator switch
            {
                RuleOperator.Contains => "contiene",
                RuleOperator.Equals => "es igual a",
                RuleOperator.StartsWith => "comienza con",
                RuleOperator.EndsWith => "termina con",
                RuleOperator.NotContains => "no contiene",
                RuleOperator.NotEquals => "no es igual a",
                RuleOperator.Regex => "coincide con el patrón",
                RuleOperator.GreaterThan => "es mayor que",
                RuleOperator.LessThan => "es menor que",
                RuleOperator.IsEmpty => "está vacío",
                RuleOperator.IsNotEmpty => "no está vacío",
                _ => ""
            };

            return $"{targetStr} {operatorStr} \"{Value}\"";
        }

        /// <summary>
        /// Collection of conditions for complex rule logic (if used)
        /// </summary>
        [Ignore]
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();

        /// <summary>
        /// Indicates whether this rule uses the advanced conditions system
        /// </summary>
        public bool UsesAdvancedConditions { get; set; } = false;
    }
}