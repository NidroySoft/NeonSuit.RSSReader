using CodeHollow.FeedReader;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Services.RssFeedParser
{
    public class RssFeedParser : IRssFeedParser
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public RssFeedParser(ILogger logger)
        {
            _logger = logger.ForContext<RssFeedParser>();

            // ✅ SOCKETSHTTPHANDLER - Mejor soporte para Brotli y HTTP/2
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All, // GZip + Deflate + Brotli
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                EnableMultipleHttp2Connections = true,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan
            };

            _httpClient = new HttpClient(handler);

            // Headers realistas de Chrome
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // ✅ NO agregar Accept-Encoding manualmente - SocketsHttpHandler lo maneja automáticamente
            // Si lo agregas manualmente, interfiere con la descompresión automática

            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9," +
                "application/rss+xml,application/atom+xml;q=0.8," +
                "image/avif,image/webp,*/*;q=0.8");

            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua",
                "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _logger.Debug("RssFeedParser initialized with SocketsHttpHandler and full decompression support");
        }

        public async Task<(Core.Models.Feed feed, List<Article> articles)> ParseFeedAsync(string url)
        {
            try
            {
                _logger.Debug("Parsing feed from URL: {Url}", url);

                var feedContent = await ReadAndCleanFeedAsync(url);

                var feed = new Core.Models.Feed
                {
                    Url = url,
                    Title = feedContent.Title ?? "Sin título",
                    Description = feedContent.Description ?? string.Empty,
                    WebsiteUrl = feedContent.Link ?? url,
                    IconUrl = feedContent.ImageUrl,
                    LastUpdated = DateTime.UtcNow,
                    Language = feedContent.Language
                };

                var articles = feedContent.Items.Select(item =>
                {
                    var article = ParseArticle(item);
                    article.FeedId = 0;
                    return article;
                }).ToList();

                _logger.Information("Successfully parsed feed {Url}: {ArticleCount} articles, Title: {Title}",
                    url, articles.Count, feed.Title);

                return (feed, articles);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Network error parsing feed {Url}", url);
                throw new InvalidOperationException($"Network error: Could not reach feed server. {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Timeout parsing feed {Url}", url);
                throw new InvalidOperationException("Timeout: Feed server took too long to respond.", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse feed {Url}", url);
                throw new InvalidOperationException($"Parse error: {ex.Message}", ex);
            }
        }

        public async Task<List<Article>> ParseArticlesAsync(string url, int feedId)
        {
            try
            {
                _logger.Debug("Parsing articles from feed {FeedId}, URL: {Url}", feedId, url);

                var feedContent = await ReadAndCleanFeedAsync(url);
                var articles = feedContent.Items.Select(item =>
                {
                    var article = ParseArticle(item);
                    article.FeedId = feedId;
                    return article;
                }).ToList();

                _logger.Information("Parsed {Count} articles from feed {FeedId}", articles.Count, feedId);

                return articles;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Network error parsing articles from {Url}", url);
                throw new InvalidOperationException($"Network error: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Timeout parsing articles from {Url}", url);
                throw new InvalidOperationException("Timeout: Server took too long to respond.", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse articles from {Url}", url);
                throw new InvalidOperationException($"Parse error: {ex.Message}", ex);
            }
        }

        private async Task<CodeHollow.FeedReader.Feed> ReadAndCleanFeedAsync(string url)
        {
            try
            {
                _logger.Debug("Downloading feed content: {Url}", url);

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // ✅ LECTURA SEGURA: Verificar Content-Encoding por si acaso
                var contentEncoding = response.Content.Headers.ContentEncoding;
                _logger.Debug("Response encoding: {Encoding}, Content-Type: {ContentType}",
                    string.Join(", ", contentEncoding),
                    response.Content.Headers.ContentType?.MediaType);

                // ✅ LEER COMO STRING - SocketsHttpHandler ya descomprimió automáticamente
                var rawXml = await response.Content.ReadAsStringAsync();

                // Si llega basura binaria, intentar descompresión manual de Brotli como fallback
                if (LooksLikeBinaryData(rawXml))
                {
                    _logger.Warning("Content appears compressed, attempting manual decompression");
                    rawXml = await TryManualDecompression(response);
                }

                _logger.Debug("Downloaded {Size} bytes from {Url}", rawXml.Length, url);

                // ✅ LIMPIEZA DE ENTIDADES HTML - ORDEN CORRECTO
                var cleanXml = CleanXmlEntities(rawXml);

                // ✅ CORREGIR ESPACIOS EN ATRIBUTOS (problema común en feeds mal formados)
                cleanXml = FixAttributeSpaces(cleanXml);

                // ✅ LIMPIAR ESPACIOS ILEGALES EN ETIQUETAS
                cleanXml = Regex.Replace(cleanXml, @"<\s+", "<");
                cleanXml = Regex.Replace(cleanXml, @"\s+>", ">");

                // ✅ ELIMINAR CARACTERES DE CONTROL NO VÁLIDOS EN XML 1.0
                cleanXml = Regex.Replace(cleanXml, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

                // ✅ REMOVER BOM SI PERSISTE
                if (cleanXml.Length > 0 && cleanXml[0] == '\uFEFF')
                    cleanXml = cleanXml.Substring(1);

                _logger.Debug("Feed content cleaned, final size: {Size} bytes", cleanXml.Length);

                // Intentar parsear con debug si falla
                try
                {
                    return FeedReader.ReadFromString(cleanXml);
                }
                catch (Exception parseEx)
                {
                    _logger.Error(parseEx, "XML Parse failed. First 1000 chars:\n{Preview}",
                        cleanXml.Substring(0, Math.Min(1000, cleanXml.Length)));
                    throw;
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read/clean feed {Url}", url);
                throw new InvalidOperationException($"Feed format error: {ex.Message}", ex);
            }
        }

        private static bool LooksLikeBinaryData(string content)
        {
            if (string.IsNullOrEmpty(content) || content.Length < 100) return false;

            // Contar caracteres de control/binarios en los primeros 200 caracteres
            var sample = content.Substring(0, Math.Min(200, content.Length));
            var binaryCount = sample.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

            return binaryCount > 10; // Si hay más de 10 caracteres de control, probablemente es binario
        }

        private async Task<string> TryManualDecompression(HttpResponseMessage response)
        {
            try
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();

                // Intentar Brotli
                try
                {
                    using var brotliStream = new BrotliStream(new MemoryStream(bytes), CompressionMode.Decompress);
                    using var reader = new StreamReader(brotliStream, Encoding.UTF8);
                    return await reader.ReadToEndAsync();
                }
                catch { /* Ignorar y probar siguiente */ }

                // Intentar GZip
                try
                {
                    using var gzipStream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                    using var reader = new StreamReader(gzipStream, Encoding.UTF8);
                    return await reader.ReadToEndAsync();
                }
                catch { /* Ignorar y probar siguiente */ }

                // Intentar Deflate
                try
                {
                    using var deflateStream = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress);
                    using var reader = new StreamReader(deflateStream, Encoding.UTF8);
                    return await reader.ReadToEndAsync();
                }
                catch { /* Ignorar */ }

                // Si nada funciona, devolver como UTF-8 crudo
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decompress response: {ex.Message}", ex);
            }
        }

        private static string CleanXmlEntities(string input)
        {
            // ✅ ORDEN CORRECTO: Específicos primero, & al final

            // 1. Preservar entidades XML válidas temporalmente
            var temp = input
                .Replace("&amp;", "\u0000AMP\u0000")  // Marcador temporal
                .Replace("&lt;", "\u0000LT\u0000")
                .Replace("&gt;", "\u0000GT\u0000")
                .Replace("&quot;", "\u0000QUOT\u0000")
                .Replace("&apos;", "\u0000APOS\u0000");

            // 2. Convertir entidades HTML comunes a numéricas
            temp = temp
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

            // 3. Escapar & restantes que no son entidades válidas
            temp = Regex.Replace(temp, @"&(?![a-zA-Z]+;|#[0-9]+;|#x[0-9a-fA-F]+;)", "&amp;");

            // 4. Restaurar entidades XML válidas
            return temp
                .Replace("\u0000AMP\u0000", "&amp;")
                .Replace("\u0000LT\u0000", "&lt;")
                .Replace("\u0000GT\u0000", "&gt;")
                .Replace("\u0000QUOT\u0000", "&quot;")
                .Replace("\u0000APOS\u0000", "&apos;");
        }

        private static string FixAttributeSpaces(string input)
        {
            // ✅ CORREGIDO: Múltiples pasadas para casos complejos y espacios Unicode

            var result = input;

            // Pasada 1: Espacios regulares antes de comillas de cierre
            // Patrón: atributo="valor " → atributo="valor"
            result = Regex.Replace(result, @"(\s+\w+\s*=)\s*(""[^""]*?)\s+""", "$1$2\"");
            result = Regex.Replace(result, @"(\s+\w+\s*=)\s*('[^']*?)\s+'", "$1$2'");

            // Pasada 2: Espacios específicamente al final de URLs (caso CiberCuba)
            // Captura el patrón exacto: url="... " (espacio antes de comilla)
            result = Regex.Replace(result, @"(url\s*=\s*""[^""]*?)\s+""", "$1\"");
            result = Regex.Replace(result, @"(href\s*=\s*""[^""]*?)\s+""", "$1\"");
            result = Regex.Replace(result, @"(xml:base\s*=\s*""[^""]*?)\s+""", "$1\"");

            // Pasada 3: Cualquier atributo con espacio antes de comilla (catch-all)
            result = Regex.Replace(result, @"(\w+:\w+\s*=\s*""[^""]*?)\s+""", "$1\"");
            result = Regex.Replace(result, @"(\w+:\w+\s*=\s*'[^']*?)\s+'", "$1'");

            return result;
        }

        private Article ParseArticle(FeedItem item)
        {
            var article = new Article
            {
                Guid = item.Id ?? item.Link ?? Guid.NewGuid().ToString(),
                Title = WebUtility.HtmlDecode(item.Title?.Trim() ?? "Sin título"),
                Link = item.Link ?? string.Empty,
                Content = WebUtility.HtmlDecode(item.Content ?? item.Description ?? string.Empty),
                Summary = WebUtility.HtmlDecode(item.Description?.StripHtml()?.Trim() ?? string.Empty),
                Author = WebUtility.HtmlDecode(item.Author ?? string.Empty),
                PublishedDate = item.PublishingDate ?? DateTime.UtcNow,
                AddedDate = DateTime.UtcNow,
                ImageUrl = ExtractImageUrl(item) ?? string.Empty,
                Categories = string.Join(", ", item.Categories?.Distinct() ?? new List<string>())
            };

            // Limitar longitudes
            if (article.Content?.Length > 1000000)
                article.Content = article.Content.Substring(0, 1000000) + "...";

            if (article.Summary?.Length > 5000)
                article.Summary = article.Summary.Substring(0, 5000) + "...";

            return article;
        }

        private static string? ExtractImageUrl(FeedItem item)
        {
            var specificElement = item.SpecificItem?.Element;

            // Media RSS
            var mediaContent = specificElement?
                .Element(XName.Get("content", "http://search.yahoo.com/mrss/")) ??
                specificElement?
                .Element(XName.Get("group", "http://search.yahoo.com/mrss/"))?
                .Element(XName.Get("content", "http://search.yahoo.com/mrss/"));

            var url = mediaContent?.Attribute("url")?.Value;

            // Thumbnail
            if (string.IsNullOrEmpty(url))
            {
                var thumbnail = specificElement?
                    .Element(XName.Get("thumbnail", "http://search.yahoo.com/mrss/"));
                url = thumbnail?.Attribute("url")?.Value;
            }

            // Enclosure
            if (string.IsNullOrEmpty(url))
            {
                var enclosure = specificElement?.Element("enclosure");
                var type = enclosure?.Attribute("type")?.Value;
                if (type?.Contains("image") == true || type?.Contains("jpeg") == true || type?.Contains("png") == true)
                    url = enclosure?.Attribute("url")?.Value;
            }

            // Imagen en contenido HTML
            if (string.IsNullOrEmpty(url))
            {
                var html = item.Content ?? item.Description;
                url = ExtractFirstImageFromHtml(html);
            }

            return url;
        }

        private static string? ExtractFirstImageFromHtml(string? html)
        {
            if (string.IsNullOrEmpty(html)) return null;

            var match = Regex.Match(html, @"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(html, @"<img[^>]+src='([^']+)'", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public static class StringExtensions
    {
        public static string StripHtml(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty)
                       .Replace("&nbsp;", " ")
                       .Trim();
        }
    }
}