namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Module status enumeration.
    /// </summary>
    public enum ModuleStatus
    {
        /// <summary>Module is not initialized.</summary>
        NotInitialized,
        /// <summary>Module is initializing.</summary>
        Initializing,
        /// <summary>Module is initialized but not started.</summary>
        Initialized,
        /// <summary>Module is starting.</summary>
        Starting,
        /// <summary>Module is running.</summary>
        Running,
        /// <summary>Module is stopping.</summary>
        Stopping,
        /// <summary>Module is stopped.</summary>
        Stopped,
        /// <summary>Module is in error state.</summary>
        Error
    }
}