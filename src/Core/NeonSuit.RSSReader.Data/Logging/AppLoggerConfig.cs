// =======================================================
// Data/Logging/AppLoggerConfig.cs
// =======================================================

using NeonSuit.RSSReader.Data.Configuration;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace NeonSuit.RSSReader.Data.Logging
{
    /// <summary>
    /// Centralized Serilog configuration manager with support for runtime configuration changes.
    /// Handles logger initialization, persistence of settings, and log file management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This static class provides:
    /// <list type="bullet">
    /// <item><description>Thread-safe singleton logger instance</description></item>
    /// <item><description>Runtime configuration updates via <see cref="UpdateConfiguration"/></description></item>
    /// <item><description>Persistence of log settings to JSON</description></item>
    /// <item><description>Log file management (cleanup, compression, statistics)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// // Initialize at application startup
    /// var logger = AppLoggerConfig.InitializeLogger();
    /// 
    /// // Get logger for specific class
    /// var classLogger = AppLoggerConfig.CreateLogger&lt;MyClass&gt;();
    /// 
    /// // Update settings at runtime
    /// AppLoggerConfig.UpdateConfiguration(new LogSettings { LogLevel = "Debug" });
    /// </code>
    /// </para>
    /// </remarks>
    public static class AppLoggerConfig
    {
        private static ILogger? _logger;
        private static readonly object _lock = new();
        private static LogSettings? _currentSettings;
        private static string _defaultLogPath = string.Empty;

        #region Initialization

        /// <summary>
        /// Initializes the logger with default or saved settings.
        /// </summary>
        /// <param name="settings">Optional settings to override saved/default values.</param>
        /// <returns>The configured ILogger instance.</returns>
        /// <remarks>
        /// This method is thread-safe and ensures only one logger instance is created.
        /// If settings are provided, they are saved and used; otherwise, saved settings
        /// are loaded or defaults are created.
        /// </remarks>
        public static ILogger InitializeLogger(LogSettings? settings = null)
        {
            lock (_lock)
            {
                if (_logger != null && settings == null)
                    return _logger;

                _currentSettings = settings ?? LoadSavedSettings() ?? CreateDefaultSettings();

                // Persist settings for future sessions
                SaveSettings(_currentSettings);

                // Ensure log directory exists
                EnsureLogDirectoryExists(_currentSettings.FullLogDirectory);

                _defaultLogPath = Path.Combine(_currentSettings.FullLogDirectory, "neonsuit_.log");

                // Configure Serilog pipeline
                _logger = CreateLoggerConfiguration(_currentSettings).CreateLogger();
                Log.Logger = _logger;

                LogInitializationInfo();

                // Start background compression if enabled
                if (_currentSettings.CompressOldLogs)
                {
                    Task.Run(() => CompressOldLogsAsync());
                }

                return _logger;
            }
        }

        /// <summary>
        /// Creates the Serilog logger configuration based on provided settings.
        /// </summary>
        private static LoggerConfiguration CreateLoggerConfiguration(LogSettings settings)
        {
            var config = new LoggerConfiguration();

            if (settings.EnableLogging)
            {
                // Base level and overrides
                config.MinimumLevel.Is(settings.SerilogLevel)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning);

                // Enrichment
                config.Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "NeonSuit.RSSReader")
                    .Enrich.WithProperty("Version", GetAppVersion())
                    .Enrich.WithProperty("DeviceName", Environment.MachineName)
                    .Enrich.WithThreadId()
                    .Enrich.WithProcessId();

                // Console sink
                if (settings.EnableConsoleLogging)
                {
                    config.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        restrictedToMinimumLevel: settings.SerilogLevel);
                }

                // File sink
                if (settings.EnableFileLogging)
                {
                    config.WriteTo.Async(a => a.File(_defaultLogPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: settings.RetentionDays,
                        fileSizeLimitBytes: settings.MaxFileSizeMB * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: settings.SerilogLevel,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}"));
                }

                // Debug sink
                if (settings.EnableDebugWindow)
                {
                    config.WriteTo.Debug(restrictedToMinimumLevel: settings.SerilogLevel);
                }
            }

#if DEBUG
            // Always enable Debug sink in DEBUG builds for Visual Studio output window
            config.WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug);
#endif

            return config;
        }

        /// <summary>
        /// Logs initialization information after logger is created.
        /// </summary>
        private static void LogInitializationInfo()
        {
            if (_logger == null || _currentSettings == null) return;

            _logger.Information("=== Logger Initialized ===");
            _logger.Information("Settings: Level={LogLevel}, Retention={RetentionDays} days, MaxSize={MaxFileSizeMB}MB",
                _currentSettings.LogLevel, _currentSettings.RetentionDays, _currentSettings.MaxFileSizeMB);
            _logger.Information("Log directory: {LogDirectory}", _currentSettings.FullLogDirectory);
        }

        #endregion

        #region Runtime Configuration

        /// <summary>
        /// Updates the logger configuration at runtime.
        /// </summary>
        /// <param name="newSettings">The new settings to apply.</param>
        /// <remarks>
        /// This method closes the existing logger, saves the new settings,
        /// and re-initializes the logger with the updated configuration.
        /// </remarks>
        public static void UpdateConfiguration(LogSettings newSettings)
        {
            ArgumentNullException.ThrowIfNull(newSettings);

            lock (_lock)
            {
                _currentSettings = newSettings;
                SaveSettings(newSettings);

                // Close existing logger and reinitialize
                if (_logger != null)
                {
                    Log.CloseAndFlush();
                    _logger = null;
                }

                InitializeLogger(newSettings);

                _logger?.Information("Log configuration updated at runtime");
            }
        }

        /// <summary>
        /// Retrieves the current log settings.
        /// </summary>
        /// <returns>The current LogSettings instance.</returns>
        public static LogSettings GetCurrentSettings()
        {
            lock (_lock)
            {
                return _currentSettings ?? LoadSavedSettings() ?? CreateDefaultSettings();
            }
        }

        #endregion

        #region Logger Access

        /// <summary>
        /// Retrieves the current ILogger instance.
        /// </summary>
        /// <returns>The global logger instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if logger hasn't been initialized.</exception>
        public static ILogger GetLogger()
        {
            if (_logger == null)
                throw new InvalidOperationException("Logger not initialized. Call InitializeLogger first.");

            return _logger;
        }

        /// <summary>
        /// Creates a logger instance for a specific class context.
        /// </summary>
        /// <typeparam name="T">The class type for context enrichment.</typeparam>
        /// <returns>A contextual logger for the specified type.</returns>
        public static ILogger CreateLogger<T>() where T : class
        {
            return GetLogger().ForContext<T>();
        }

        #endregion

        #region Log File Management

        /// <summary>
        /// Opens the log directory in Windows Explorer.
        /// </summary>
        public static void OpenLogDirectory()
        {
            var settings = GetCurrentSettings();
            if (Directory.Exists(settings.FullLogDirectory))
            {
                Process.Start("explorer.exe", settings.FullLogDirectory);
            }
        }

        /// <summary>
        /// Deletes all log files in the directory.
        /// </summary>
        public static void ClearAllLogs()
        {
            var settings = GetCurrentSettings();
            if (!Directory.Exists(settings.FullLogDirectory)) return;

            var files = Directory.GetFiles(settings.FullLogDirectory, "*.*");
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { /* Ignore individual file errors */ }
            }

            GetLogger().Information("All log files cleared by user");
        }

        /// <summary>
        /// Compresses log files older than the specified number of days.
        /// </summary>
        /// <param name="daysOld">Age threshold in days for compression (default: 7).</param>
        public static async Task CompressOldLogsAsync(int daysOld = 7)
        {
            try
            {
                var settings = GetCurrentSettings();
                var cutoffDate = DateTime.Now.AddDays(-daysOld);
                var logFiles = Directory.GetFiles(settings.FullLogDirectory, "*.log")
                    .Where(f => new FileInfo(f).CreationTime < cutoffDate);

                foreach (var logFile in logFiles)
                {
                    await CompressFileAsync(logFile);
                }
            }
            catch (Exception ex)
            {
                GetLogger().Error(ex, "Failed to compress old logs");
            }
        }

        /// <summary>
        /// Compresses a single log file using GZip.
        /// </summary>
        private static async Task CompressFileAsync(string filePath)
        {
            try
            {
                var compressedFile = filePath + ".gz";
                await using var originalStream = File.OpenRead(filePath);
                await using var compressedStream = File.Create(compressedFile);
                await using var gzipStream = new System.IO.Compression.GZipStream(
                    compressedStream,
                    System.IO.Compression.CompressionLevel.Optimal);

                await originalStream.CopyToAsync(gzipStream);
                File.Delete(filePath);

                GetLogger().Information("Compressed log file: {LogFile}", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                GetLogger().Error(ex, "Failed to compress file: {LogFile}", filePath);
            }
        }

        /// <summary>
        /// Gets statistics about log file usage.
        /// </summary>
        /// <returns>A LogStats object with file statistics.</returns>
        public static LogStats GetLogStats()
        {
            var settings = GetCurrentSettings();
            if (!Directory.Exists(settings.FullLogDirectory))
                return new LogStats();

            var files = Directory.GetFiles(settings.FullLogDirectory, "*.*");
            return new LogStats
            {
                TotalFiles = files.Length,
                TotalSize = files.Sum(f => new FileInfo(f).Length),
                LogFiles = files.Count(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)),
                CompressedFiles = files.Count(f => f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)),
                OldestFile = files.Any() ? files.Min(f => File.GetCreationTime(f)) : DateTime.MinValue,
                NewestFile = files.Any() ? files.Max(f => File.GetCreationTime(f)) : DateTime.MinValue
            };
        }

        #endregion

        #region Settings Persistence

        private static LogSettings CreateDefaultSettings()
        {
            return new LogSettings();
        }

        private static LogSettings? LoadSavedSettings()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<LogSettings>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load log settings: {ex.Message}");
            }
            return null;
        }

        private static void SaveSettings(LogSettings settings)
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var settingsDir = Path.GetDirectoryName(settingsPath);
                EnsureDirectoryExists(settingsDir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save log settings: {ex.Message}");
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeonSuit", "RSSReader", "Config", "logsettings.json");
        }

        #endregion

        #region Helpers

        private static void EnsureLogDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void EnsureDirectoryExists(string? path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        #endregion

        #region LogStats Class

        /// <summary>
        /// Represents statistics about stored log files.
        /// </summary>
        public class LogStats
        {
            /// <summary>Total number of files in the log directory.</summary>
            public int TotalFiles { get; set; }

            /// <summary>Total size of all files in bytes.</summary>
            public long TotalSize { get; set; }

            /// <summary>Number of .log files.</summary>
            public int LogFiles { get; set; }

            /// <summary>Number of compressed (.gz) files.</summary>
            public int CompressedFiles { get; set; }

            /// <summary>Creation date of the oldest file.</summary>
            public DateTime OldestFile { get; set; }

            /// <summary>Creation date of the newest file.</summary>
            public DateTime NewestFile { get; set; }

            /// <summary>Formatted total size (e.g., "2.5 MB").</summary>
            public string FormattedSize
            {
                get
                {
                    if (TotalSize < 1024) return $"{TotalSize} B";
                    if (TotalSize < 1024 * 1024) return $"{(TotalSize / 1024.0):F1} KB";
                    return $"{(TotalSize / (1024.0 * 1024.0)):F1} MB";
                }
            }

            /// <summary>Returns a string representation of the statistics.</summary>
            public override string ToString()
            {
                return $"Files: {TotalFiles} ({LogFiles} logs, {CompressedFiles} compressed), Size: {FormattedSize}";
            }
        }

        #endregion
    }
}