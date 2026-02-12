namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Notification delivery types
    /// </summary>
    public enum NotificationType
    {
        Toast = 0,      // Solo notificación visual en centro de notificaciones
        Sound = 1,      // Solo sonido de alerta
        Both = 2,       // Visual + sonido
        Silent = 3,     // Solo registro interno (sin interrumpir al usuario)
        Banner = 4      // Banner emergente (Toast pero más prominente)
    }
}
