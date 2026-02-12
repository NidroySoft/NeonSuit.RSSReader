namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Estado de lectura de un artículo
    /// </summary>
    public enum ArticleStatus
    {
        Unread = 0,      // No leído
        Read = 1,        // Leído
        Starred = 2,     // Marcado como favorito
        Archived = 3     // Archivado (oculto de la vista principal)
    }
}
