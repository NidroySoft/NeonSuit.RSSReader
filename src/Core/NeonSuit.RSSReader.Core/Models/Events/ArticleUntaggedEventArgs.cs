using NeonSuit.RSSReader.Core.Interfaces.Services;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Event arguments for the <see cref="IArticleTagService.OnArticleUntagged"/> event.
    /// Carries data related to a tag being removed from an article.
    /// </summary>
    public class ArticleUntaggedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleUntaggedEventArgs"/> class.
        /// </summary>
        /// <param name="articleId">The unique identifier of the article from which the tag was removed.</param>
        /// <param name="tagId">The unique identifier of the tag that was removed.</param>
        /// <param name="tagName">The name of the tag that was removed.</param>
        /// <exception cref="ArgumentNullException">Thrown if tagName is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or tagId is less than or equal to 0.</exception>
        public ArticleUntaggedEventArgs(int articleId, int tagId, string tagName)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);
            ArgumentNullException.ThrowIfNull(tagName);

            ArticleId = articleId;
            TagId = tagId;
            TagName = tagName;
        }

        /// <summary>
        /// Gets the unique identifier of the article from which the tag was removed.
        /// </summary>
        public int ArticleId { get; }

        /// <summary>
        /// Gets the unique identifier of the tag that was removed.
        /// </summary>
        public int TagId { get; }

        /// <summary>
        /// Gets the name of the tag that was removed.
        /// </summary>
        public string TagName { get; }

        /// <summary>
        /// Gets a human-readable summary of the untagging event.
        /// </summary>
        public override string ToString()
        {
            return $"Tag '{TagName}' removed from article {ArticleId}";
        }
    }
}