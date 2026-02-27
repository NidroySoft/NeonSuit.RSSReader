using NeonSuit.RSSReader.Core.Interfaces.Modules;
using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.Modules
{
    /// <summary>
    /// Interface for modules that expose services to other modules.
    /// </summary>
    public interface IModuleWithDependencies : IModule
    {
        /// <summary>
        /// Gets the services provided by this module.
        /// </summary>
        IReadOnlyDictionary<Type, object> ProvidedServices { get; }
    }
}