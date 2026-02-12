namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Frecuencia de actualización de los feeds
    /// </summary>
    public enum FeedUpdateFrequency
    {
        Manual = 0,          // Solo manual
        Every15Minutes = 15,
        Every30Minutes = 30,
        EveryHour = 60,
        Every3Hours = 180,
        Every6Hours = 360,
        Every12Hours = 720,
        Daily = 1440
    }
}
