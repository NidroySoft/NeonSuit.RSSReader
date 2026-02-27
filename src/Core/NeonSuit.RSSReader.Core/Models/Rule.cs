using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a business rule for automated article processing.
    /// Supports complex conditions, multiple actions, and scope-based filtering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rules define automated actions to be performed when articles match specific conditions.
    /// Each rule includes:
    /// <list type="bullet">
    /// <item><description>Conditions: Define what articles trigger the rule</description></item>
    /// <item><description>Scope: Restrict which feeds/categories the rule applies to</description></item>
    /// <item><description>Actions: What to do when conditions are met (notify, tag, mark read, etc.)</description></item>
    /// <item><description>Configuration: Priority, enabled status, execution order</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Rules can be simple (single condition) or complex (multiple conditions with AND/OR logic)
    /// using the <see cref="UsesAdvancedConditions"/> flag and <see cref="Conditions"/> collection.
    /// </para>
    /// <para>
    /// Performance optimizations:
    /// <list type="bullet">
    /// <item><description>JSON fields store arrays of IDs efficiently</description></item>
    /// <item><description>Indexed on IsEnabled and Priority for fast active rule retrieval</description></item>
    /// <item><description>MatchCount and LastMatchDate for analytics without separate tables</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [Table("Rules")]
    public class Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class.
        /// Sets default values for JSON arrays, timestamps, and collections.
        /// </summary>
        public Rule()
        {
            FeedIds = "[]";
            CategoryIds = "[]";
            TagIds = "[]";
            NotificationTemplate = "{Title}\n\n{Source}";
            CreatedAt = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
            Priority = 100;
            IsEnabled = true;
            OnlyNewArticles = true;
            NotificationPriority = NotificationPriority.Normal;
            Operator = RuleOperator.Contains;
            Target = RuleFieldTarget.Title;
            ActionType = RuleActionType.SendNotification;
            NextConditionOperator = LogicalOperator.AND;
            Conditions = new HashSet<RuleCondition>();
            NotificationLogs = new HashSet<NotificationLog>();
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Basic Information

        /// <summary>
        /// Descriptive name for the rule (e.g., "Alertas Cuba Urgentes").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description explaining the rule's purpose.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        #endregion

        #region Conditions

        /// <summary>
        /// Field where the search will be performed.
        /// </summary>
        public RuleFieldTarget Target { get; set; }

        /// <summary>
        /// Search operator for the main condition.
        /// </summary>
        public RuleOperator Operator { get; set; }

        /// <summary>
        /// Value to compare against.
        /// </summary>
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Second value (for range operations like Between).
        /// </summary>
        [MaxLength(500)]
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the search is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// For regex operations: the regex pattern.
        /// </summary>
        [MaxLength(500)]
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Group identifier for complex conditions (AND/OR groups).
        /// 0 = main condition, 1+ = additional condition groups.
        /// </summary>
        public int ConditionGroup { get; set; }

        /// <summary>
        /// Logical operator to combine with next condition.
        /// </summary>
        public LogicalOperator NextConditionOperator { get; set; }

        /// <summary>
        /// Indicates whether this rule uses the advanced conditions system.
        /// When true, uses <see cref="Conditions"/> collection instead of simple condition fields.
        /// </summary>
        public bool UsesAdvancedConditions { get; set; }

        #endregion

        #region Scope

        /// <summary>
        /// Where the rule applies (AllFeeds, SpecificFeeds, SpecificCategories).
        /// </summary>
        public RuleScope Scope { get; set; }

        /// <summary>
        /// JSON array of feed IDs (if Scope = SpecificFeeds).
        /// Format: "[1, 5, 12]"
        /// </summary>
        [MaxLength(1000)]
        public string FeedIds { get; set; }

        /// <summary>
        /// JSON array of category IDs (if Scope = SpecificCategories).
        /// Format: "[1, 5, 12]"
        /// </summary>
        [MaxLength(1000)]
        public string CategoryIds { get; set; }

        #endregion

        #region Actions

        /// <summary>
        /// Primary action type to execute when rule conditions are met.
        /// </summary>
        public RuleActionType ActionType { get; set; }

        /// <summary>
        /// JSON array of tag IDs to apply (for ApplyTags action).
        /// Format: "[1, 5, 12]"
        /// </summary>
        [MaxLength(1000)]
        public string TagIds { get; set; }

        /// <summary>
        /// Category ID to move article to (for MoveToCategory action).
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Path to sound file for PlaySound action.
        /// </summary>
        [MaxLength(500)]
        public string? SoundPath { get; set; }

        /// <summary>
        /// Template for notification (supports variables like {Title}, {Source}).
        /// </summary>
        [MaxLength(1000)]
        public string NotificationTemplate { get; set; }

        /// <summary>
        /// Notification priority level.
        /// </summary>
        public NotificationPriority NotificationPriority { get; set; }

        /// <summary>
        /// Color for highlighting articles (in hex format, e.g., "#FF0000").
        /// </summary>
        [MaxLength(20)]
        public string? HighlightColor { get; set; }

        #endregion

        #region Configuration

        /// <summary>
        /// Execution order (lower numbers execute first).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether the rule is currently active and should be evaluated.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Stop processing other rules if this one matches.
        /// </summary>
        public bool StopOnMatch { get; set; }

        /// <summary>
        /// Only apply to new articles (not retroactively).
        /// </summary>
        public bool OnlyNewArticles { get; set; }

        #endregion

        #region Audit & Statistics

        /// <summary>
        /// Date when the rule was created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time the rule was modified (UTC).
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Number of times this rule has been triggered (statistics).
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Last time this rule matched an article (UTC).
        /// </summary>
        public DateTime? LastMatchDate { get; set; }

        #endregion

        #region Relationships

        /// <summary>
        /// Collection of notification logs generated by this rule.
        /// </summary>
        public virtual ICollection<NotificationLog> NotificationLogs { get; set; }

        /// <summary>
        /// Collection of advanced conditions for complex rule logic.
        /// Only used when <see cref="UsesAdvancedConditions"/> is true.
        /// </summary>
        public virtual ICollection<RuleCondition> Conditions { get; set; }

        #endregion

        #region Computed Properties (Not Mapped)

        /// <summary>
        /// Parsed list of feed IDs from the JSON string.
        /// </summary>
        [NotMapped]
        public List<int> FeedIdList
        {
            get
            {
                if (string.IsNullOrEmpty(FeedIds))
                    return new List<int>();

                try
                {
                    return JsonSerializer.Deserialize<List<int>>(FeedIds) ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
        }

        /// <summary>
        /// Parsed list of category IDs from the JSON string.
        /// </summary>
        [NotMapped]
        public List<int> CategoryIdList
        {
            get
            {
                if (string.IsNullOrEmpty(CategoryIds))
                    return new List<int>();

                try
                {
                    return JsonSerializer.Deserialize<List<int>>(CategoryIds) ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
        }

        /// <summary>
        /// Parsed list of tag IDs from the JSON string.
        /// </summary>
        [NotMapped]
        public List<int> TagIdList
        {
            get
            {
                if (string.IsNullOrEmpty(TagIds))
                    return new List<int>();

                try
                {
                    return JsonSerializer.Deserialize<List<int>>(TagIds) ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
        }

        /// <summary>
        /// Human-readable summary of the rule condition.
        /// </summary>
        [NotMapped]
        public string Summary => $"{Target} {Operator} \"{Value}\"";

        /// <summary>
        /// Human-readable condition in Spanish for UI display.
        /// Example: "Título contiene 'Cuba' Y Contenido contiene 'urgente'"
        /// </summary>
        [NotMapped]
        public string HumanReadableCondition => GetHumanReadableCondition();

        /// <summary>
        /// Indicates whether the rule is active (enabled and has valid configuration).
        /// </summary>
        [NotMapped]
        public bool IsActive => IsEnabled && (!OnlyNewArticles || LastMatchDate == null);

        /// <summary>
        /// Gets the rule's health status based on match count recency.
        /// </summary>
        [NotMapped]
        public RuleHealthStatus HealthStatus
        {
            get
            {
                if (!IsEnabled) return RuleHealthStatus.Disabled;
                if (!LastMatchDate.HasValue) return RuleHealthStatus.NeverMatched;

                var daysSinceLastMatch = (DateTime.UtcNow - LastMatchDate.Value).TotalDays;

                return daysSinceLastMatch switch
                {
                    <= 1 => RuleHealthStatus.Active,
                    <= 7 => RuleHealthStatus.Normal,
                    <= 30 => RuleHealthStatus.Infrequent,
                    _ => RuleHealthStatus.Stale
                };
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Generates a human-readable description of the rule condition.
        /// </summary>
        private string GetHumanReadableCondition()
        {
            if (UsesAdvancedConditions && Conditions.Count > 0)
            {
                return $"{Conditions.Count} condiciones avanzadas";
            }

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

        #endregion
    }

    /// <summary>
    /// Health status for rules based on recent activity.
    /// </summary>
    public enum RuleHealthStatus
    {
        /// <summary>Rule is disabled</summary>
        Disabled,
        /// <summary>Rule has never matched any article</summary>
        NeverMatched,
        /// <summary>Rule matched recently (last 24h)</summary>
        Active,
        /// <summary>Rule matches regularly (last 7 days)</summary>
        Normal,
        /// <summary>Rule matches infrequently (last 30 days)</summary>
        Infrequent,
        /// <summary>Rule hasn't matched in over 30 days</summary>
        Stale
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on (IsEnabled, Priority) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(r => new { r.IsEnabled, r.Priority });
// Why (benefit): Faster retrieval of active rules in priority order for rule engine
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Significant performance improvement for rule evaluation

// TODO (High - v1.x): Add index on LastMatchDate for cleanup/analytics queries
// What to do: In OnModelCreating, add: entity.HasIndex(r => r.LastMatchDate);
// Why (benefit): Faster queries for stale rule detection and statistics
// Estimated effort: 20 min
// Risk level: Low
// Potential impact: Better rule maintenance UI performance

// TODO (Medium - v1.x): Add validation for JSON fields using database constraints
// What to do: Add check constraints in SQLite via migration or trigger
// Why (benefit): Ensure FeedIds, CategoryIds, TagIds always contain valid JSON arrays
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Data integrity improvement

// TODO (Medium - v1.x): Add rule versioning for audit trail
// What to do: Add int Version; DateTime? LastVersionCreated; string? ChangeNotes;
// Why (benefit): Track rule modifications over time for debugging
// Estimated effort: 4-6 hours
// Risk level: Low
// Potential impact: Better audit capabilities

// TODO (Low - v1.x): Add time-based activation schedule
// What to do: Add TimeOnly? ActiveFrom; TimeOnly? ActiveTo; List<DayOfWeek> ActiveDays;
// Why (benefit): Support rules that only apply during specific hours/days
// Estimated effort: 1 day
// Risk level: Low
// Potential impact: More flexible rule scheduling

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent lost updates if rule edited while being evaluated
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in concurrent scenarios