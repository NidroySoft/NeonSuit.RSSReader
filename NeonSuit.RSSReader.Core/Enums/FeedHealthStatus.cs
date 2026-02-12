namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Estado de salud de un feed basado en éxito/fallo de actualizaciones
    /// </summary>
    public enum FeedHealthStatus
    {
        /// <summary>Feed se actualiza correctamente</summary>
        Healthy = 0,

        /// <summary>Algunos fallos, pero aún funciona parcialmente</summary>
        Warning = 1,

        /// <summary>Múltiples fallos consecutivos</summary>
        Error = 2,

        /// <summary>Feed está inactivo/pausado por el usuario</summary>
        Paused = 3,

        /// <summary>URL del feed es inválida o inaccesible</summary>
        Invalid = 4
    }
}