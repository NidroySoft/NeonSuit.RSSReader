using NeonSuit.RSSReader.Core.Interfaces.Services;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Event arguments for the <see cref="IArticleTagService.OnArticleTagged"/> event.
    /// Carries data related to an article being tagged, including the article and tag details,
    /// who applied the tag, and optional information about the rule and confidence.
    /// </summary>
    public class ArticleTaggedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleTaggedEventArgs"/> class.
        /// </summary>
        /// <param name="articleId">The unique identifier of the article that was tagged.</param>
        /// <param name="tagId">The unique identifier of the tag that was applied.</param>
        /// <param name="tagName">The name of the tag that was applied.</param>
        /// <param name="appliedBy">The identifier of the entity or user who applied the tag.</param>
        /// <param name="ruleId">The optional unique identifier of the rule that triggered the tagging.</param>
        /// <param name="confidence">The optional confidence score associated with the tag application.</param>
        /// <exception cref="ArgumentNullException">Thrown if tagName or appliedBy is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or tagId is less than or equal to 0.</exception>
        public ArticleTaggedEventArgs(int articleId, int tagId, string tagName, string appliedBy, int? ruleId = null, double? confidence = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);
            ArgumentNullException.ThrowIfNull(tagName);
            ArgumentNullException.ThrowIfNull(appliedBy);

            ArticleId = articleId;
            TagId = tagId;
            TagName = tagName;
            AppliedBy = appliedBy;
            RuleId = ruleId;
            Confidence = confidence;
        }

        /// <summary>
        /// Gets the unique identifier of the article that was tagged.
        /// </summary>
        public int ArticleId { get; }

        /// <summary>
        /// Gets the unique identifier of the tag that was applied.
        /// </summary>
        public int TagId { get; }

        /// <summary>
        /// Gets the name of the tag that was applied.
        /// </summary>
        public string TagName { get; }

        /// <summary>
        /// Gets the identifier of the entity or user who applied the tag.
        /// </summary>
        public string AppliedBy { get; }

        /// <summary>
        /// Gets the optional unique identifier of the rule that triggered the tagging.
        /// </summary>
        public int? RuleId { get; }

        /// <summary>
        /// Gets the optional confidence score associated with the tag application.
        /// </summary>
        public double? Confidence { get; }

        /// <summary>
        /// Gets a value indicating whether this tag was applied automatically by a rule.
        /// </summary>
        public bool IsAutoApplied => AppliedBy != "user";

        /// <summary>
        /// Gets a human-readable summary of the tagging event.
        /// </summary>
        public override string ToString()
        {
            var ruleInfo = RuleId.HasValue ? $" by rule {RuleId}" : "";
            var confidenceInfo = Confidence.HasValue ? $" (confidence: {Confidence:P0})" : "";
            return $"Article {ArticleId} tagged with '{TagName}'{ruleInfo} by {AppliedBy}{confidenceInfo}";
        }
    }
}