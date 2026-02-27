using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Modules;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Modules;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Modules;
using Serilog;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IModuleService"/> for managing plugable modules.
    /// </summary>
    internal class ModuleService : IModuleService
    {
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, ModuleContainer> _modules;
        private readonly ConcurrentDictionary<string, AssemblyLoadContext> _loadContexts;

        // Event handlers
        private event EventHandler<ModuleInfoDto>? _onModuleLoaded;
        private event EventHandler<ModuleInfoDto>? _onModuleUnloaded;
        private event EventHandler<ModuleInfoDto>? _onModuleStatusChanged;

        #region Events

        /// <inheritdoc />
        public event EventHandler<ModuleInfoDto> OnModuleLoaded
        {
            add => _onModuleLoaded += value;
            remove => _onModuleLoaded -= value;
        }

        /// <inheritdoc />
        public event EventHandler<ModuleInfoDto> OnModuleUnloaded
        {
            add => _onModuleUnloaded += value;
            remove => _onModuleUnloaded -= value;
        }

        /// <inheritdoc />
        public event EventHandler<ModuleInfoDto> OnModuleStatusChanged
        {
            add => _onModuleStatusChanged += value;
            remove => _onModuleStatusChanged -= value;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        /// <param name="mapper">AutoMapper instance.</param>
        /// <param name="logger">Logger instance.</param>
        public ModuleService(
            IServiceProvider serviceProvider,
            IMapper mapper,
            ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _logger = logger.ForContext<ModuleService>();

            _modules = new ConcurrentDictionary<string, ModuleContainer>();
            _loadContexts = new ConcurrentDictionary<string, AssemblyLoadContext>();

            _logger.Debug("ModuleService initialized");
        }

        #endregion

        #region Module Discovery

        /// <inheritdoc />
        public async Task LoadModulesFromDirectoryAsync(string directory, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    _logger.Warning("Module directory not found: {Directory}", directory);
                    return;
                }

                var assemblyFiles = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
                _logger.Information("Found {Count} potential modules in {Directory}", assemblyFiles.Length, directory);

                foreach (var assemblyFile in assemblyFiles)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await LoadModuleAsync(assemblyFile, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to load module from {Assembly}", assemblyFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to scan directory for modules: {Directory}", directory);
                throw new InvalidOperationException($"Failed to scan directory for modules: {directory}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<ModuleInfoDto> LoadModuleAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
                var contextName = $"{fileName}_{Guid.NewGuid():N}";

                // Create isolated load context
                var loadContext = new AssemblyLoadContext(contextName, isCollectible: true);
                _loadContexts[contextName] = loadContext;

                // Load the assembly
                using var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read);
                var assembly = loadContext.LoadFromStream(fs);

                // Find module types
                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    .ToList();

                if (!moduleTypes.Any())
                {
                    _logger.Warning("No IModule implementations found in {Assembly}", assemblyPath);
                    return null!;
                }

                var modules = new List<IModule>();

                foreach (var moduleType in moduleTypes)
                {
                    try
                    {
                        // Create instance with dependency injection support
                        var constructor = moduleType.GetConstructors().FirstOrDefault();
                        var parameters = constructor?.GetParameters() ?? Array.Empty<ParameterInfo>();
                        var args = parameters.Select(p => _serviceProvider.GetService(p.ParameterType)).ToArray();

                        var module = Activator.CreateInstance(moduleType, args) as IModule;
                        if (module != null)
                        {
                            var container = new ModuleContainer
                            {
                                Module = module,
                                AssemblyPath = assemblyPath,
                                LoadContext = loadContext,
                                LoadTime = DateTime.UtcNow
                            };

                            if (_modules.TryAdd(module.Id, container))
                            {
                                modules.Add(module);
                                _logger.Information("Module loaded: {ModuleName} v{Version}", module.Name, module.Version);

                                // Initialize module
                                await module.InitializeAsync(cancellationToken).ConfigureAwait(false);
                                await UpdateModuleStatusAsync(module.Id, ModuleStatus.Initialized, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                _logger.Warning("Module with ID {ModuleId} already loaded", module.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to instantiate module type {Type} from {Assembly}", moduleType.Name, assemblyPath);
                    }
                }

                // Return info for the first module (or aggregate)
                var firstModule = modules.FirstOrDefault();
                var moduleInfo = _mapper.Map<ModuleInfoDto>(firstModule);
                moduleInfo.AssemblyLocation = assemblyPath;
                moduleInfo.LoadTime = DateTime.UtcNow;
                moduleInfo.Status = firstModule?.Status.ToString() ?? ModuleStatus.NotInitialized.ToString();
                moduleInfo.ModuleType = "Service";

                _onModuleLoaded?.Invoke(this, moduleInfo);

                return moduleInfo;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load module from {Assembly}", assemblyPath);
                throw new InvalidOperationException($"Failed to load module from {assemblyPath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<ModuleActionResultDto> UnloadModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_modules.TryRemove(moduleId, out var container))
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Module {moduleId} not found"
                    };
                }

                // Stop module if running
                if (container.Module.Status == ModuleStatus.Running)
                {
                    await container.Module.StopAsync(cancellationToken).ConfigureAwait(false);
                }

                var moduleInfo = _mapper.Map<ModuleInfoDto>(container.Module);
                _onModuleUnloaded?.Invoke(this, moduleInfo);

                // Unload the assembly context
                if (container.LoadContext != null)
                {
                    var contextName = container.LoadContext.Name;
                    container.LoadContext.Unload();
                    _loadContexts.TryRemove(contextName!, out _);

                    // Wait for GC to collect
                    for (int i = 0; i < 10; i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                }

                _logger.Information("Module unloaded: {ModuleName}", container.Module.Name);

                return new ModuleActionResultDto
                {
                    Success = true,
                    Message = $"Module {container.Module.Name} unloaded successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to unload module {ModuleId}", moduleId);
                throw new InvalidOperationException($"Failed to unload module {moduleId}", ex);
            }
        }

        #endregion

        #region Module Management

        /// <inheritdoc />
        public Task<List<ModuleInfoDto>> GetAllModulesAsync(CancellationToken cancellationToken = default)
        {
            var modules = _modules.Values
                .Select(c =>
                {
                    var dto = _mapper.Map<ModuleInfoDto>(c.Module);
                    dto.Status = c.Module.Status.ToString();
                    dto.AssemblyLocation = c.AssemblyPath;
                    dto.LoadTime = c.LoadTime;
                    dto.HasConfig = c.Module is IConfigurableModule;
                    return dto;
                })
                .ToList();

            return Task.FromResult(modules);
        }

        /// <inheritdoc />
        public Task<ModuleInfoDto?> GetModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            if (_modules.TryGetValue(moduleId, out var container))
            {
                var dto = _mapper.Map<ModuleInfoDto>(container.Module);
                dto.Status = container.Module.Status.ToString();
                dto.AssemblyLocation = container.AssemblyPath;
                dto.LoadTime = container.LoadTime;
                dto.HasConfig = container.Module is IConfigurableModule;
                return Task.FromResult<ModuleInfoDto?>(dto);
            }

            return Task.FromResult<ModuleInfoDto?>(null);
        }

        /// <inheritdoc />
        public async Task<ModuleActionResultDto> StartModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_modules.TryGetValue(moduleId, out var container))
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Module {moduleId} not found"
                    };
                }

                // Check dependencies
                if (!await AreDependenciesResolvedAsync(moduleId, cancellationToken).ConfigureAwait(false))
                {
                    var unresolved = await GetUnresolvedDependenciesAsync(moduleId, cancellationToken).ConfigureAwait(false);
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Unresolved dependencies: {string.Join(", ", unresolved)}"
                    };
                }

                if (container.Module.Status == ModuleStatus.Running)
                {
                    return new ModuleActionResultDto
                    {
                        Success = true,
                        Message = $"Module {container.Module.Name} is already running"
                    };
                }

                await container.Module.StartAsync(cancellationToken).ConfigureAwait(false);
                await UpdateModuleStatusAsync(moduleId, container.Module.Status, cancellationToken).ConfigureAwait(false);

                _logger.Information("Module started: {ModuleName}", container.Module.Name);

                return new ModuleActionResultDto
                {
                    Success = true,
                    Message = $"Module {container.Module.Name} started successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start module {ModuleId}", moduleId);
                throw new InvalidOperationException($"Failed to start module {moduleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<ModuleActionResultDto> StopModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_modules.TryGetValue(moduleId, out var container))
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Module {moduleId} not found"
                    };
                }

                if (container.Module.Status != ModuleStatus.Running)
                {
                    return new ModuleActionResultDto
                    {
                        Success = true,
                        Message = $"Module {container.Module.Name} is not running"
                    };
                }

                await container.Module.StopAsync(cancellationToken).ConfigureAwait(false);
                await UpdateModuleStatusAsync(moduleId, container.Module.Status, cancellationToken).ConfigureAwait(false);

                _logger.Information("Module stopped: {ModuleName}", container.Module.Name);

                return new ModuleActionResultDto
                {
                    Success = true,
                    Message = $"Module {container.Module.Name} stopped successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to stop module {ModuleId}", moduleId);
                throw new InvalidOperationException($"Failed to stop module {moduleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<ModuleActionResultDto> RestartModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            try
            {
                await StopModuleAsync(moduleId, cancellationToken).ConfigureAwait(false);
                await StartModuleAsync(moduleId, cancellationToken).ConfigureAwait(false);

                return new ModuleActionResultDto
                {
                    Success = true,
                    Message = $"Module restarted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to restart module {ModuleId}", moduleId);
                throw new InvalidOperationException($"Failed to restart module {moduleId}", ex);
            }
        }

        /// <inheritdoc />
        public Task<List<ModuleInfoDto>> GetModulesByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<ModuleStatus>(status, true, out var moduleStatus))
            {
                return Task.FromResult(new List<ModuleInfoDto>());
            }

            var modules = _modules.Values
                .Where(c => c.Module.Status == moduleStatus)
                .Select(c =>
                {
                    var dto = _mapper.Map<ModuleInfoDto>(c.Module);
                    dto.Status = c.Module.Status.ToString();
                    dto.AssemblyLocation = c.AssemblyPath;
                    dto.LoadTime = c.LoadTime;
                    dto.HasConfig = c.Module is IConfigurableModule;
                    return dto;
                })
                .ToList();

            return Task.FromResult(modules);
        }

        #endregion

        #region Module Configuration

        /// <inheritdoc />
        public Task<ModuleConfigSchemaDto?> GetModuleConfigSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            if (!_modules.TryGetValue(moduleId, out var container))
            {
                return Task.FromResult<ModuleConfigSchemaDto?>(null);
            }

            if (container.Module is not IConfigurableModule configurableModule)
            {
                return Task.FromResult<ModuleConfigSchemaDto?>(null);
            }

            var schema = _mapper.Map<ModuleConfigSchemaDto>(configurableModule.ConfigSchema);
            return Task.FromResult<ModuleConfigSchemaDto?>(schema);
        }

        /// <inheritdoc />
        public Task<Dictionary<string, object>?> GetModuleConfigAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            if (!_modules.TryGetValue(moduleId, out var container))
            {
                return Task.FromResult<Dictionary<string, object>?>(null);
            }

            if (container.Module is not IConfigurableModule configurableModule)
            {
                return Task.FromResult<Dictionary<string, object>?>(null);
            }

            return Task.FromResult<Dictionary<string, object>?>(configurableModule.Config.ToDictionary(x => x.Key, x => x.Value));
        }

        /// <inheritdoc />
        public async Task<ModuleActionResultDto> UpdateModuleConfigAsync(UpdateModuleConfigDto updateDto, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_modules.TryGetValue(updateDto.ModuleId, out var container))
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Module {updateDto.ModuleId} not found"
                    };
                }

                if (container.Module is not IConfigurableModule configurableModule)
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = $"Module {container.Module.Name} does not support configuration"
                    };
                }

                // Validate configuration
                var validationResult = configurableModule.ValidateConfig(updateDto.Config);
                if (!validationResult.IsValid)
                {
                    return new ModuleActionResultDto
                    {
                        Success = false,
                        Message = "Configuration validation failed",
                        Data = validationResult.Errors
                    };
                }

                // Stop module if running
                var wasRunning = container.Module.Status == ModuleStatus.Running;
                if (wasRunning)
                {
                    await container.Module.StopAsync(cancellationToken).ConfigureAwait(false);
                }

                // Update configuration
                await configurableModule.UpdateConfigAsync(updateDto.Config, cancellationToken).ConfigureAwait(false);

                // Restart if it was running
                if (wasRunning)
                {
                    await container.Module.StartAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("Module configuration updated: {ModuleName}", container.Module.Name);

                return new ModuleActionResultDto
                {
                    Success = true,
                    Message = $"Module {container.Module.Name} configuration updated successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update module configuration for {ModuleId}", updateDto.ModuleId);
                throw new InvalidOperationException($"Failed to update module configuration for {updateDto.ModuleId}", ex);
            }
        }

        #endregion

        #region Module Dependencies

        /// <inheritdoc />
        public Task<List<string>> GetUnresolvedDependenciesAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            if (!_modules.TryGetValue(moduleId, out var container))
            {
                return Task.FromResult(new List<string>());
            }

            var unresolved = new List<string>();
            foreach (var dependencyId in container.Module.Dependencies)
            {
                if (!_modules.TryGetValue(dependencyId, out var depContainer) ||
                    depContainer.Module.Status != ModuleStatus.Running)
                {
                    unresolved.Add(dependencyId);
                }
            }

            return Task.FromResult(unresolved);
        }

        /// <inheritdoc />
        public Task<bool> AreDependenciesResolvedAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            if (!_modules.TryGetValue(moduleId, out var container))
            {
                return Task.FromResult(false);
            }

            foreach (var dependencyId in container.Module.Dependencies)
            {
                if (!_modules.TryGetValue(dependencyId, out var depContainer) ||
                    depContainer.Module.Status != ModuleStatus.Running)
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates module status and raises event.
        /// </summary>
        private async Task UpdateModuleStatusAsync(string moduleId, ModuleStatus newStatus, CancellationToken cancellationToken)
        {
            if (_modules.TryGetValue(moduleId, out var container))
            {
                var moduleInfo = _mapper.Map<ModuleInfoDto>(container.Module);
                moduleInfo.Status = newStatus.ToString();
                _onModuleStatusChanged?.Invoke(this, moduleInfo);
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Container for loaded module information.
        /// </summary>
        private sealed class ModuleContainer
        {
            public IModule Module { get; set; } = null!;
            public string AssemblyPath { get; set; } = string.Empty;
            public AssemblyLoadContext? LoadContext { get; set; }
            public DateTime LoadTime { get; set; }
        }

        #endregion
    }
}