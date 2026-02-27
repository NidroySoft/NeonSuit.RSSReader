using NeonSuit.RSSReader.Core.Interfaces.Modules;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Modules
{
    /// <summary>
    /// Interface for modules with custom lifecycle events.
    /// </summary>
    public interface IModuleWithLifecycle : IModule
    {
        /// <summary>
        /// Called before module initialization.
        /// </summary>
        Task OnBeforeInitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called after module initialization.
        /// </summary>
        Task OnAfterInitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called before module start.
        /// </summary>
        Task OnBeforeStartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called after module start.
        /// </summary>
        Task OnAfterStartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called before module stop.
        /// </summary>
        Task OnBeforeStopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called after module stop.
        /// </summary>
        Task OnAfterStopAsync(CancellationToken cancellationToken = default);
    }
}