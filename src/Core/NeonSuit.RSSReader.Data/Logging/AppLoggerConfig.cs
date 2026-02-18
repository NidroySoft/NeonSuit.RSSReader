using NeonSuit.RSSReader.Data.Configuration;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

namespace NeonSuit.RSSReader.Data.Logging
{
    /// <summary>
    /// Centralized Serilog configuration with support for runtime changes.
    /// </summary>
    public static class AppLoggerConfig
    {
        private static ILogger? _logger;
        private static readonly object _lock = new();
        private static LogSettings? _currentSettings;
        private static string _defaultLogPath = string.Empty;

        /// <summary>
        /// Initializes the logger with default or saved settings.
        /// </summary>
        public static ILogger InitializeLogger(LogSettings? settings = null)
        {
            lock (_lock)
            {
                if (_logger != null && settings == null)
                    return _logger;

                _currentSettings = settings ?? LoadSavedSettings() ?? CreateDefaultSettings();

                // Persist settings
                SaveSettings(_currentSettings);

                // Ensure log directory exists
                var logDir = _currentSettings.FullLogDirectory;
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                _defaultLogPath = Path.Combine(logDir, "neonsuit_.log");

                // Configure Serilog
                var loggerConfig = new LoggerConfiguration();

                if (_currentSettings.EnableLogging)
                {
                    // Base level
                    loggerConfig.MinimumLevel.Is(_currentSettings.SerilogLevel);

                    // Overrides
                    loggerConfig.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
                    loggerConfig.MinimumLevel.Override("System", LogEventLevel.Warning);

                    // Enrichment
                    loggerConfig.Enrich.FromLogContext()
                        .Enrich.WithProperty("Application", "NeonSuit.RSSReader")
                        .Enrich.WithProperty("Version", GetAppVersion())
                        .Enrich.WithProperty("DeviceName", Environment.MachineName)
                        .Enrich.WithThreadId()
                        .Enrich.WithProcessId();

                    // Console sink (if enabled)
                    if (_currentSettings.EnableConsoleLogging)
                    {
                        loggerConfig.WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            restrictedToMinimumLevel: _currentSettings.SerilogLevel);
                    }

                    // File sink (if enabled)
                    if (_currentSettings.EnableFileLogging)
                    {
                        loggerConfig.WriteTo.Async(a => a.File(_defaultLogPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: _currentSettings.RetentionDays,
                            fileSizeLimitBytes: _currentSettings.MaxFileSizeMB * 1024 * 1024,
                            rollOnFileSizeLimit: true,
                            restrictedToMinimumLevel: _currentSettings.SerilogLevel,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}"));
                    }

                    // Debug sink (if enabled)
                    if (_currentSettings.EnableDebugWindow)
                    {
                        loggerConfig.WriteTo.Debug(restrictedToMinimumLevel: _currentSettings.SerilogLevel);
                    }
                }
                else
                {
                    // Logging disabled - null sink would be configured here if necessary
                }

#if DEBUG
                // ✅ SIEMPRE agregar Debug sink en modo DEBUG (aparece en Output Window de VS)
                loggerConfig.WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug);
#endif

                _logger = loggerConfig.CreateLogger();
                Log.Logger = _logger;

                _logger.Information("=== Logger Initialized ===");
                _logger.Information("Settings: Level={LogLevel}, Retention={RetentionDays} days, MaxSize={MaxFileSizeMB}MB",
                    _currentSettings.LogLevel, _currentSettings.RetentionDays, _currentSettings.MaxFileSizeMB);
                _logger.Information("Log directory: {LogDirectory}", _currentSettings.FullLogDirectory);

                // Initial cleanup if enabled
                if (_currentSettings.CompressOldLogs)
                {
                    Task.Run(() => CompressOldLogs());
                }

                return _logger;
            }
        }

        /// <summary>
        /// Updates the configuration at runtime.
        /// </summary>
        public static void UpdateConfiguration(LogSettings newSettings)
        {
            lock (_lock)
            {
                _currentSettings = newSettings;
                SaveSettings(newSettings);

                // Re-initialize logger with new configuration
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
        /// Retrieves the current settings.
        /// </summary>
        public static LogSettings GetCurrentSettings()
        {
            lock (_lock)
            {
                return _currentSettings ?? LoadSavedSettings() ?? CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Opens the log directory in Windows Explorer.
        /// </summary>
        public static void OpenLogDirectory()
        {
            var settings = GetCurrentSettings();
            if (Directory.Exists(settings.FullLogDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", settings.FullLogDirectory);
            }
        }

        /// <summary>
        /// Deletes all log files in the directory.
        /// </summary>
        public static void ClearAllLogs()
        {
            var settings = GetCurrentSettings();
            if (Directory.Exists(settings.FullLogDirectory))
            {
                foreach (var file in Directory.GetFiles(settings.FullLogDirectory, "*.log"))
                {
                    try { File.Delete(file); } catch { /* Ignore */ }
                }
                foreach (var file in Directory.GetFiles(settings.FullLogDirectory, "*.gz"))
                {
                    try { File.Delete(file); } catch { /* Ignore */ }
                }

                GetLogger().Information("All log files cleared by user");
            }
        }

        /// <summary>
        /// Compresses log files older than the specified number of days.
        /// </summary>
        public static void CompressOldLogs(int daysOld = 7)
        {
            try
            {
                var settings = GetCurrentSettings();
                var cutoffDate = DateTime.Now.AddDays(-daysOld);
                var logFiles = Directory.GetFiles(settings.FullLogDirectory, "*.log")
                    .Where(f => new FileInfo(f).CreationTime < cutoffDate);

                foreach (var logFile in logFiles)
                {
                    var compressedFile = logFile + ".gz";
                    using (var originalFileStream = File.OpenRead(logFile))
                    using (var compressedFileStream = File.Create(compressedFile))
                    using (var compressionStream = new System.IO.Compression.GZipStream(compressedFileStream,
                        System.IO.Compression.CompressionLevel.Optimal))
                    {
                        originalFileStream.CopyTo(compressionStream);
                    }

                    File.Delete(logFile);
                    GetLogger().Information("Compressed log file: {LogFile}", Path.GetFileName(logFile));
                }
            }
            catch (Exception ex)
            {
                GetLogger().Error(ex, "Failed to compress old logs");
            }
        }

        /// <summary>
        /// Gets statistics regarding log file usage.
        /// </summary>
        public static LogStats GetLogStats()
        {
            var settings = GetCurrentSettings();
            if (!Directory.Exists(settings.FullLogDirectory))
                return new LogStats();

            var files = Directory.GetFiles(settings.FullLogDirectory, "*.*");
            return new LogStats
            {
                TotalFiles = files.Length,
                TotalSize = files.Sum(f => f != null ? new FileInfo(f).Length : 0),
                LogFiles = files.Count(f => f.EndsWith(".log")),
                CompressedFiles = files.Count(f => f.EndsWith(".gz")),
                OldestFile = files.Any() ?
                    File.GetCreationTime(files.OrderBy(f => File.GetCreationTime(f)).First()) :
                    DateTime.MinValue,
                NewestFile = files.Any() ?
                    File.GetCreationTime(files.OrderByDescending(f => File.GetCreationTime(f)).First()) :
                    DateTime.MinValue
            };
        }

        // Private helper methods
        private static LogSettings CreateDefaultSettings()
        {
            return new LogSettings();
        }

        private static LogSettings LoadSavedSettings()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    return System.Text.Json.JsonSerializer.Deserialize<LogSettings>(json)
                        ?? CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load log settings: {ex.Message}");
            }
            return CreateDefaultSettings();
        }

        private static void SaveSettings(LogSettings settings)
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var settingsDir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                    Directory.CreateDirectory(settingsDir);

                var json = System.Text.Json.JsonSerializer.Serialize(settings,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        /// <summary>
        /// Retrieves the current ILogger instance.
        /// </summary>
        public static ILogger GetLogger()
        {
            if (_logger == null)
                throw new InvalidOperationException("Logger not initialized. Call InitializeLogger first.");

            return _logger;
        }

        /// <summary>
        /// Creates a logger instance for a specific class context.
        /// </summary>
        public static ILogger CreateLogger<T>() where T : class
        {
            return GetLogger().ForContext<T>();
        }

        /// <summary>
        /// Represents statistics about the stored log files.
        /// </summary>
        public class LogStats
        {
            public int TotalFiles { get; set; }
            public long TotalSize { get; set; }
            public int LogFiles { get; set; }
            public int CompressedFiles { get; set; }
            public DateTime OldestFile { get; set; }
            public DateTime NewestFile { get; set; }

            public string FormattedSize
            {
                get
                {
                    if (TotalSize < 1024) return $"{TotalSize} B";
                    if (TotalSize < 1024 * 1024) return $"{(TotalSize / 1024.0):F1} KB";
                    return $"{(TotalSize / (1024.0 * 1024.0)):F1} MB";
                }
            }

            public override string ToString()
            {
                return $"Files: {TotalFiles} ({LogFiles} logs, {CompressedFiles} compressed), Size: {FormattedSize}";
            }
        }
    }
}