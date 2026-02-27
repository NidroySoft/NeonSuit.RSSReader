using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Modules
{
    /// <summary>
    /// Base interface for all plugable modules.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Gets the unique identifier of the module.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the module.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets the module description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the module author.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets the module dependencies (other module IDs).
        /// </summary>
        string[] Dependencies { get; }

        /// <summary>
        /// Initializes the module.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the module.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the module.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the module status.
        /// </summary>
        ModuleStatus Status { get; }
    }

   
}