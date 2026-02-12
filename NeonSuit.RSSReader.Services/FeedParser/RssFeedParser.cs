using CodeHollow.FeedReader;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;
using NeonSuit.RSSReader.Core.Models;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Services.FeedParser
{
    public class RssFeedParser : IFeedParser
    {
        private readonly HttpClient _httpClient;

        public RssFeedParser()
        {
            _httpClient = new HttpClient();
            // Le metemos un User-Agent de un Chrome real en Windows para que no nos den el 403
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/xml, application/rss+xml, application/atom+xml, text/html, */*");
        }

        public async Task<(Core.Models.Feed feed, List<Article> articles)> ParseFeedAsync(string url)
        {
            try
            {
                // 1. Bajamos y limpiamos el XML antes de que FeedReader lo toque
                var feedContent = await ReadAndCleanFeedAsync(url);

                var feed = new Core.Models.Feed
                {
                    Url = url,
                    Title = feedContent.Title ?? "Sin título",
                    Description = feedContent.Description ?? string.Empty,
                    WebsiteUrl = feedContent.Link ?? url,
                    IconUrl = feedContent.ImageUrl,
                    LastUpdated = DateTime.UtcNow
                };

                var articles = feedContent.Items.Select(item => ParseArticle(item)).ToList();

                return (feed, articles);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Facho en el Parser NeonSuit: {ex.Message}", ex);
            }
        }

        // Método Pro para limpiar la basura del XML (Soluciona el error '&bull;' y '0x20')
        private async Task<CodeHollow.FeedReader.Feed> ReadAndCleanFeedAsync(string url)
        {
            // Bajamos el contenido crudo
            var response = await _httpClient.GetByteArrayAsync(url);
            string rawXml = System.Text.Encoding.UTF8.GetString(response);

            // LIMPIEZA DE ENTIDADES (Aquí es donde matamos el error 'bull')
            string cleanXml = rawXml
                .Replace("&bull;", "&#8226;")
                .Replace("&nbsp;", "&#160;")
                .Replace("&ndash;", "&#8211;")
                .Replace("&mdash;", "&#8212;")
                .Replace("&rsquo;", "&#8217;")
                .Replace("&lsquo;", "&#8216;")
                .Replace("&rdquo;", "&#8221;")
                .Replace("&ldquo;", "&#8220;");

            // Limpiamos espacios ilegales en etiquetas (Error 0x20)
            cleanXml = Regex.Replace(cleanXml, @"<\s+", "<");

            // Devolvemos el feed parseado desde el string ya saneado
            return FeedReader.ReadFromString(cleanXml);
        }

        public async Task<List<Article>> ParseArticlesAsync(string url, int feedId)
        {
            try
            {
                var feedContent = await ReadAndCleanFeedAsync(url);
                return feedContent.Items.Select(item =>
                {
                    var art = ParseArticle(item);
                    art.FeedId = feedId;
                    return art;
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al parsear artículos: {ex.Message}", ex);
            }
        }

        private Article ParseArticle(FeedItem item)
        {
            return new Article
            {
                Guid = item.Id ?? item.Link ?? Guid.NewGuid().ToString(),
                Title = item.Title ?? "Sin título",
                Link = item.Link ?? string.Empty,
                Content = item.Content ?? item.Description,
                Summary = item.Description ?? string.Empty,
                Author = item.Author,
                PublishedDate = item.PublishingDate ?? DateTime.UtcNow,
                AddedDate = DateTime.UtcNow,
                ImageUrl = ExtractImageUrl(item) ?? string.Empty,
                Categories = string.Join(", ", item.Categories ?? new List<string>())
            };
        }

        private static string? ExtractImageUrl(FeedItem item)
        {
            // 1. Media RSS (Yahoo Namespace)
            var specificElement = item.SpecificItem?.Element;
            var media = specificElement?.Element(XName.Get("content", "http://search.yahoo.com/mrss/"));
            var url = media?.Attribute("url")?.Value;

            if (string.IsNullOrEmpty(url))
            {
                var thumb = specificElement?.Element(XName.Get("thumbnail", "http://search.yahoo.com/mrss/"));
                url = thumb?.Attribute("url")?.Value;
            }

            // 2. Enclosure
            if (string.IsNullOrEmpty(url))
            {
                var enclosure = specificElement?.Element("enclosure");
                if (enclosure?.Attribute("type")?.Value?.Contains("image") == true)
                    url = enclosure.Attribute("url")?.Value;
            }

            // 3. HTML Scraper (Fallback)
            return url ?? ExtractFirstImageFromHtml(item.Content ?? item.Description);
        }

        private static string? ExtractFirstImageFromHtml(string? html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            var match = Regex.Match(html, @"<img.+?src=[""'](.+?)[""'].*?>", RegexOptions.IgnoreCase);
            return match.Success ? match.Value : null;
        }
    }
}