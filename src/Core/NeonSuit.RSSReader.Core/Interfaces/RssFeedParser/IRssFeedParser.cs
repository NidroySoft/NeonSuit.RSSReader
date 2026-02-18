using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.FeedParser
{
    /// <summary>
    /// Define las operaciones necesarias para descargar, limpiar y parsear feeds RSS/Atom.
    /// </summary>
    public interface IRssFeedParser
    {
        /// <summary>
        /// Obtiene la información general de un feed y su lista inicial de artículos.
        /// </summary>
        /// <param name="url">La URL del feed RSS/Atom.</param>
        /// <returns>Una tupla con el objeto Feed configurado y la lista de artículos encontrados.</returns>
        Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url);

        /// <summary>
        /// Obtiene únicamente los artículos de un feed existente.
        /// </summary>
        /// <param name="url">La URL del feed.</param>
        /// <param name="feedId">El ID del feed en la base de datos para asociar los artículos.</param>
        /// <returns>Una lista de artículos procesados.</returns>
        Task<List<Article>> ParseArticlesAsync(string url, int feedId);
    }
}