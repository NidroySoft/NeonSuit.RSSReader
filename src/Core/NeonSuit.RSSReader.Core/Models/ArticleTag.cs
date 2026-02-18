using SQLite;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Join table for many-to-many relationship between Articles and Tags.
    /// Includes metadata about the tagging.
    /// </summary>
    [Table("ArticleTags")]
    public class ArticleTag
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed, NotNull]
        public int ArticleId { get; set; }

        [Indexed, NotNull]
        public int TagId { get; set; }

        /// <summary>
        /// Who applied the tag (system, user, rule)
        /// </summary>
        public string AppliedBy { get; set; } = "user"; // "user", "rule", "system"

        /// <summary>
        /// ID of the rule that applied this tag (if applicable)
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// When the tag was applied
        /// </summary>
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional confidence score for auto-tagging
        /// </summary>
        public double? Confidence { get; set; }

        [Ignore]
        public Article? Article { get; set; }

        [Ignore]
        public Tag? Tag { get; set; }
    }
}