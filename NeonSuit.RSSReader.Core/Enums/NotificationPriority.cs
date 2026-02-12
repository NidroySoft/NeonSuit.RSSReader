namespace NeonSuit.RSSReader.Core.Enums
{
    public enum NotificationPriority
    {
        Low = 0,        // Sin sonido, no persistente
        Normal = 1,     // Sonido normal
        High = 2,       // Sonido persistente
        Critical = 3    // Sonido + requiere acción
    }
}