namespace NeonSuit.RSSReader.Core.Enums
{

    /// <summary>
    /// Types of user interactions with notifications
    /// </summary>
    public enum NotificationAction
    {
        None = 0,           // Notificación no interactuada (aún visible)
        Clicked = 1,        // Usuario hizo clic en la notificación
        Dismissed = 2,      // Usuario descartó/cerró la notificación
        Snoozed = 3,        // Usuario pospuso (botón "Posponer")
        Archive = 4,        // Botón personalizado: Archivar
        MarkAsRead = 5,     // Botón personalizado: Marcar como leído
        Star = 6,           // Botón personalizado: Favoritear
        OpenInBrowser = 7,  // Botón personalizado: Abrir en navegador
        Custom1 = 8,        // Botón personalizado 1
        Custom2 = 9         // Botón personalizado 2

    }
}
