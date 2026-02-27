using AngleSharp;
using CodeHollow.FeedReader;
using NeonSuit.RSSReader.Core.Extensions;
using NeonSuit.RSSReader.Core.Interfaces.RssFeedParser;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Feed = NeonSuit.RSSReader.Core.Models.Feed;

namespace NeonSuit.RSSReader.Services.RssFeedParser;

/// <summary>
/// Implementation of <see cref="IRssFeedParser"/> using CodeHollow.FeedReader for RSS/Atom parsing.
/// Handles downloading, decompression, cleaning, sanitization, and structured extraction of feed metadata and articles.
/// </summary>
internal class RssFeedParser : IRssFeedParser
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private static readonly Regex _hexColorRegex = new Regex(
        @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string DefaultColor = "#3498db";
    private const int MaxContentLength = 1_000_000; // 1MB max per article content
    private const int MaxSummaryLength = 5_000;     // 5KB max summary

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RssFeedParser"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
    public RssFeedParser(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger.ForContext<RssFeedParser>();

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9," +
            "application/rss+xml,application/atom+xml;q=0.8,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };

#if DEBUG
        _logger.Debug("RssFeedParser initialized with optimized HttpClient");
#endif
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("URL must be absolute and valid.", nameof(url));

        try
        {
            _logger.Debug("Starting full feed parse from URL: {Url}", url);

            var feedContent = await ReadAndCleanFeedAsync(url, cancellationToken);
            var feed = MapToFeed(feedContent, url);
            var articles = feedContent.Items
                .Select(item => ParseArticle(item, feed.Id))
                .ToList();

            _logger.Information("Successfully parsed feed {Url}: {ArticleCount} articles, Title: {Title}",
                url, articles.Count, feed.Title);

            return (feed, articles);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ParseFeedAsync cancelled for {Url}", url);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "Network error parsing feed {Url}", url);
            throw;
        }       
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error parsing feed {Url}", url);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> ParseArticlesAsync(string url, int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (feedId <= 0)
            throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be positive.");

        try
        {
            _logger.Debug("Starting incremental article parse for feed {FeedId}, URL: {Url}", feedId, url);

            var feedContent = await ReadAndCleanFeedAsync(url, cancellationToken);
            var articles = feedContent.Items
                .Select(item => ParseArticle(item, feedId))
                .ToList();

            _logger.Information("Parsed {Count} articles from feed {FeedId}", articles.Count, feedId);
            return articles;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ParseArticlesAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "Network error parsing articles from {Url}", url);
            throw;
        }       
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error parsing articles from {Url}", url);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateReaderViewHtmlAsync(Article article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = article.Content ?? article.Summary ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return "<p>Sin contenido disponible.</p>";

            // Config AngleSharp (safe parsing, no scripts)
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(content), cancellationToken);

            // Remove unwanted elements (ads, scripts, styles, navbars, footers, etc.)
            var unwantedSelectors = new[]
            {
                "script", "style", "noscript", "iframe", "object", "embed",
                "[class*='ad-']", "[class*='ads-']", "[id*='ad-']", "[id*='ads-']",
                ".advert", ".banner", ".sponsored", ".tracking", "header", "footer", "nav",
                ".social-share", ".related-articles", ".comments"
            };

            foreach (var selector in unwantedSelectors)
            {
                document.QuerySelectorAll(selector).ToList().ForEach(el => el.Remove());
            }

            // Extract main content (try article, main, or body)
            var mainContent = document.QuerySelector("article")
                           ?? document.QuerySelector("main")
                           ?? document.Body;

            var cleanedHtml = mainContent?.InnerHtml ?? document.Body?.InnerHtml ?? content;

            // Build clean HTML wrapper
            var html = $@"
                    <!DOCTYPE html>
                    <html lang=""{(article.Language ?? "es")}"">
                    <head>
                        <meta charset=""UTF-8"">
                        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                        <title>{WebUtility.HtmlEncode(article.Title ?? "Artículo")}</title>
                        <style>
                            :root {{
                                --bg: #ffffff;
                                --text: #1a1a1a;
                                --meta: #555;
                                --link: #0066cc;
                            }}
                            @media (prefers-color-scheme: dark) {{
                                :root {{
                                    --bg: #121212;
                                    --text: #e0e0e0;
                                    --meta: #aaa;
                                    --link: #66b3ff;
                                }}
                            }}
                            body {{
                                font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
                                background: var(--bg);
                                color: var(--text);
                                line-height: 1.7;
                                margin: 0;
                                padding: 20px;
                                max-width: 900px;
                                margin-left: auto;
                                margin-right: auto;
                            }}
                            h1, h2, h3 {{ margin: 1.2em 0 0.6em; }}
                            img {{ max-width: 100%; height: auto; border-radius: 8px; margin: 1rem 0; }}
                            a {{ color: var(--link); text-decoration: none; }}
                            a:hover {{ text-decoration: underline; }}
                            .meta {{ color: var(--meta); font-size: 0.95rem; margin-bottom: 1.8rem; }}
                        </style>
                    </head>
                    <body>
                        <h1>{WebUtility.HtmlEncode(article.Title ?? "Sin título")}</h1>
                        <div class=""meta"">
                            {WebUtility.HtmlEncode(article.Author ?? "Autor desconocido")} • 
                            {article.PublishedDate:dd 'de' MMMM 'de' yyyy}
                        </div>
                        <div>{cleanedHtml}</div>
                    </body>
                    </html>";
            return html;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GenerateReaderViewHtmlAsync cancelled for article ID {ArticleId}", article.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate reader view HTML for article ID {ArticleId}", article.Id);
            return "<p>Error al generar vista limpia: " + WebUtility.HtmlEncode(ex.Message) + "</p>";
        }
    }

    #endregion

    #region Private Methods

    private async Task<CodeHollow.FeedReader.Feed> ReadAndCleanFeedAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _logger.Debug("Downloading feed content: {Url}", url);

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Fallback manual decompression if auto failed (rare)
            if (LooksLikeBinaryData(rawContent))
            {
                _logger.Warning("Auto-decompression failed - attempting manual decompression");
                rawContent = await TryManualDecompressionAsync(response, cancellationToken);
            }

            _logger.Debug("Downloaded {Size} bytes from {Url}", rawContent.Length, url);

            var cleanXml = CleanFeedContent(rawContent);

            try
            {
                return FeedReader.ReadFromString(cleanXml);
            }
            catch (Exception parseEx)
            {
                _logger.Error(parseEx, "Feed parse failed. First 1000 chars preview:\n{Preview}",
                    cleanXml.Length > 1000 ? cleanXml.Substring(0, 1000) : cleanXml);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ReadAndCleanFeedAsync cancelled for {Url}", url);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read or clean feed {Url}", url);
            throw;
        }
    }

    private static string CleanFeedContent(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;

        // Remove BOM if present
        if (result.Length > 0 && result[0] == '\uFEFF')
            result = result.Substring(1);

        // Remove invalid XML 1.0 control chars
        result = Regex.Replace(result, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // Fix common malformed attribute spacing
        result = Regex.Replace(result, @"(\w+\s*=\s*""[^""]*?)\s+""", "$1\"");
        result = Regex.Replace(result, @"(\w+\s*=\s*'[^']*?)\s+'", "$1'");

        // Normalize tag spacing
        result = Regex.Replace(result, @"<\s+", "<");
        result = Regex.Replace(result, @"\s+>", ">");

        // Replace common HTML entities with numeric equivalents
        result = result
            .Replace("&nbsp;", "&#160;")
            .Replace("&bull;", "&#8226;")
            .Replace("&ndash;", "&#8211;")
            .Replace("&mdash;", "&#8212;")
            .Replace("&rsquo;", "&#8217;")
            .Replace("&lsquo;", "&#8216;")
            .Replace("&rdquo;", "&#8221;")
            .Replace("&ldquo;", "&#8220;")
            .Replace("&hellip;", "&#8230;")
            .Replace("&trade;", "&#8482;")
            .Replace("&copy;", "&#169;")
            .Replace("&reg;", "&#174;")
            .Replace("&euro;", "&#8364;")
            .Replace("&pound;", "&#163;")
            .Replace("&yen;", "&#165;")
            .Replace("&cent;", "&#162;");

        // Escape remaining & that are not valid entities
        result = Regex.Replace(result, @"&(?![a-zA-Z]+;|#[0-9]+;|#x[0-9a-fA-F]+;)", "&amp;");

        return result;
    }

    private static bool LooksLikeBinaryData(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length < 100) return false;

        var sample = content.Substring(0, Math.Min(200, content.Length));
        var controlCount = sample.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

        return controlCount > 10; // Heuristic threshold
    }

    private async Task<string> TryManualDecompressionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Try Brotli
            try
            {
                await using var ms = new MemoryStream(bytes);
                await using var brotli = new BrotliStream(ms, CompressionMode.Decompress);
                using var reader = new StreamReader(brotli, Encoding.UTF8);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch { }

            // Try GZip
            try
            {
                await using var ms = new MemoryStream(bytes);
                await using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch { }

            // Try Deflate
            try
            {
                await using var ms = new MemoryStream(bytes);
                await using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                using var reader = new StreamReader(deflate, Encoding.UTF8);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch { }

            // Fallback: treat as UTF-8
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Manual decompression failed for response");
            throw;
        }
    }

    private static Feed MapToFeed(CodeHollow.FeedReader.Feed feedContent, string url)
    {
        return new Feed
        {
            Url = url,
            Title = WebUtility.HtmlDecode(feedContent.Title?.Trim() ?? "Untitled Feed"),
            Description = WebUtility.HtmlDecode(feedContent.Description?.Trim() ?? string.Empty),
            WebsiteUrl = feedContent.Link ?? url,
            IconUrl = feedContent.ImageUrl,
            Language = feedContent.Language ?? "en",
            LastUpdated = DateTime.UtcNow,
        };
    }

    private Article ParseArticle(FeedItem item, int feedId)
    {
        var content = WebUtility.HtmlDecode(item.Content ?? item.Description ?? string.Empty);
        var summary = WebUtility.HtmlDecode(item.Description?.StripHtml()?.Trim() ?? string.Empty);

        // Truncate large content to prevent DB/memory issues
        if (content.Length > MaxContentLength)
            content = content.Substring(0, MaxContentLength) + "...";

        if (summary.Length > MaxSummaryLength)
            summary = summary.Substring(0, MaxSummaryLength) + "...";

        var article = new Article
        {
            Guid = item.Id ?? item.Link ?? Guid.NewGuid().ToString(),
            Title = WebUtility.HtmlDecode(item.Title?.Trim() ?? "Untitled"),
            Link = item.Link ?? string.Empty,
            Content = content,
            Summary = summary,
            Author = WebUtility.HtmlDecode(item.Author ?? string.Empty),
            PublishedDate = item.PublishingDate ?? DateTime.UtcNow,
            AddedDate = DateTime.UtcNow,
            ImageUrl = ExtractImageUrl(item) ?? string.Empty,
            Categories = string.Join(", ", item.Categories?.Distinct() ?? Enumerable.Empty<string>()),
            FeedId = feedId
        };

        return article;
    }

    private static string? ExtractImageUrl(FeedItem item)
    {
        var element = item.SpecificItem?.Element;

        // 1. Media RSS content
        var mediaUrl = element?
            .Element(XName.Get("content", "http://search.yahoo.com/mrss/"))?
            .Attribute("url")?.Value;

        if (!string.IsNullOrEmpty(mediaUrl)) return mediaUrl;

        // 2. Media RSS thumbnail
        mediaUrl = element?
            .Element(XName.Get("thumbnail", "http://search.yahoo.com/mrss/"))?
            .Attribute("url")?.Value;

        if (!string.IsNullOrEmpty(mediaUrl)) return mediaUrl;

        // 3. Enclosure (image type)
        var enclosure = element?.Element("enclosure");
        var type = enclosure?.Attribute("type")?.Value;
        if (type?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)
            return enclosure?.Attribute("url")?.Value;

        // 4. First <img> in HTML content/description
        var html = item.Content ?? item.Description;
        if (!string.IsNullOrEmpty(html))
        {
            var match = Regex.Match(html, @"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(html, @"<img[^>]+src='([^']+)'", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
        }

        return null;
    }

    #endregion
}