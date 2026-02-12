using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.FeedParser
{
    public interface IFeedParser
    {
        /// <summary>
        /// Parse a complete feed from a URL
        /// </summary>
        /// <param name="url">Feed URL</param>
        /// <returns>Tuple containing the feed and its articles</returns>
        Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url);

        /// <summary>
        /// Parse only articles from a feed URL
        /// </summary>
        /// <param name="url">Feed URL</param>
        /// <param name="feedId">Feed ID to assign to articles</param>
        /// <returns>List of parsed articles</returns>
        Task<List<Article>> ParseArticlesAsync(string url, int feedId);
    }
}