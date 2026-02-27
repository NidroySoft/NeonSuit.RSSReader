using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NeonSuit.RSSReader.Core.DTOs.Modules;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for managing plugable modules.
    /// </summary>
    public interface IModuleService
    {
        #region Events

        /// <summary>
        /// Raised when a module is loaded.
        /// </summary>
        event EventHandler<ModuleInfoDto> OnModuleLoaded;

        /// <summary>
        /// Raised when a module is unloaded.
        /// </summary>
        event EventHandler<ModuleInfoDto> OnModuleUnloaded;

        /// <summary>
        /// Raised when a module status changes.
        /// </summary>
        event EventHandler<ModuleInfoDto> OnModuleStatusChanged;

        #endregion

        #region Module Discovery

        /// <summary>
        /// Scans a directory for modules and loads them.
        /// </summary>
        /// <param name="directory">Directory to scan.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LoadModulesFromDirectoryAsync(string directory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a module from an assembly file.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleInfoDto> LoadModuleAsync(string assemblyPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unloads a module.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleActionResultDto> UnloadModuleAsync(string moduleId, CancellationToken cancellationToken = default);

        #endregion

        #region Module Management

        /// <summary>
        /// Gets all loaded modules.
        /// </summary>
        Task<List<ModuleInfoDto>> GetAllModulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific module by ID.
        /// </summary>
        Task<ModuleInfoDto?> GetModuleAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a module.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleActionResultDto> StartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops a module.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleActionResultDto> StopModuleAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restarts a module.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleActionResultDto> RestartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets modules by status.
        /// </summary>
        Task<List<ModuleInfoDto>> GetModulesByStatusAsync(string status, CancellationToken cancellationToken = default);

        #endregion

        #region Module Configuration

        /// <summary>
        /// Gets module configuration schema.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        Task<ModuleConfigSchemaDto?> GetModuleConfigSchemaAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets module configuration.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        Task<Dictionary<string, object>?> GetModuleConfigAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates module configuration.
        /// </summary>
        /// <param name="updateDto">Update DTO.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<ModuleActionResultDto> UpdateModuleConfigAsync(UpdateModuleConfigDto updateDto, CancellationToken cancellationToken = default);

        #endregion

        #region Module Dependencies

        /// <summary>
        /// Resolves module dependencies.
        /// </summary>
        /// <param name="moduleId">Module ID.</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        Task<List<string>> GetUnresolvedDependenciesAsync(string moduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if all dependencies are resolved.
        /// </summary>
        Task<bool> AreDependenciesResolvedAsync(string moduleId, CancellationToken cancellationToken = default);

        #endregion
    }
}