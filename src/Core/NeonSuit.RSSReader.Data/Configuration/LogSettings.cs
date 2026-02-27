// =======================================================
// Data/Configuration/LogSettings.cs
// =======================================================

using NeonSuit.RSSReader.Data.Logging;
using Serilog.Events;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NeonSuit.RSSReader.Data.Configuration
{
    /// <summary>
    /// Configuration settings for the application's logging system.
    /// Implements INotifyPropertyChanged to support real-time UI updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class manages all logging-related settings including:
    /// <list type="bullet">
    /// <item><description>Log levels and verbosity</description></item>
    /// <item><description>File retention policies</description></item>
    /// <item><description>Output targets (console, file, debug window)</description></item>
    /// <item><description>Log directory and file management</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Settings can be modified at runtime and applied via <see cref="ApplySettings"/>,
    /// which updates the global logging configuration.
    /// </para>
    /// </remarks>
    public class LogSettings : INotifyPropertyChanged
    {
        private bool _enableLogging = true;
        private string _logLevel = "Information";
        private int _retentionDays = 7;
        private int _maxFileSizeMB = 10;
        private bool _enableConsoleLogging = true;
        private bool _enableFileLogging = true;
        private bool _enableDebugWindow = false;
        private bool _compressOldLogs = false;
        private string _logDirectory = string.Empty;

        #region Core Settings

        /// <summary>
        /// Master switch to enable or disable logging entirely.
        /// </summary>
        public bool EnableLogging
        {
            get => _enableLogging;
            set => SetField(ref _enableLogging, value);
        }

        /// <summary>
        /// Minimum log level to capture.
        /// </summary>
        /// <value>One of: Verbose, Debug, Information, Warning, Error, Fatal</value>
        public string LogLevel
        {
            get => _logLevel;
            set => SetField(ref _logLevel, value);
        }

        /// <summary>
        /// Number of days to retain log files before automatic cleanup.
        /// </summary>
        /// <value>Range: 1-365 days. Default: 7 days.</value>
        public int RetentionDays
        {
            get => _retentionDays;
            set => SetField(ref _retentionDays, Math.Clamp(value, 1, 365));
        }

        /// <summary>
        /// Maximum size per log file in megabytes before rotation.
        /// </summary>
        /// <value>Range: 1-100 MB. Default: 10 MB.</value>
        public int MaxFileSizeMB
        {
            get => _maxFileSizeMB;
            set => SetField(ref _maxFileSizeMB, Math.Clamp(value, 1, 100));
        }

        #endregion

        #region Output Targets

        /// <summary>
        /// Enables logging to console output (useful for debugging).
        /// </summary>
        public bool EnableConsoleLogging
        {
            get => _enableConsoleLogging;
            set => SetField(ref _enableConsoleLogging, value);
        }

        /// <summary>
        /// Enables logging to rotating files on disk.
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set => SetField(ref _enableFileLogging, value);
        }

        /// <summary>
        /// Enables a real-time debug window for log monitoring.
        /// </summary>
        public bool EnableDebugWindow
        {
            get => _enableDebugWindow;
            set => SetField(ref _enableDebugWindow, value);
        }

        /// <summary>
        /// Compresses older log files to save disk space.
        /// </summary>
        public bool CompressOldLogs
        {
            get => _compressOldLogs;
            set => SetField(ref _compressOldLogs, value);
        }

        /// <summary>
        /// Custom directory for log files. If empty, default location is used.
        /// </summary>
        public string LogDirectory
        {
            get => _logDirectory;
            set => SetField(ref _logDirectory, value);
        }

        #endregion

        #region Static Lists

        /// <summary>
        /// Available log levels for UI dropdown binding.
        /// </summary>
        public static string[] AvailableLogLevels { get; } = new[]
        {
            "Verbose",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Fatal"
        };

        #endregion

        #region Computed Properties

        /// <summary>
        /// Converts the string LogLevel to Serilog's LogEventLevel enum.
        /// </summary>
        public LogEventLevel SerilogLevel => _logLevel switch
        {
            "Verbose" => LogEventLevel.Verbose,
            "Debug" => LogEventLevel.Debug,
            "Warning" => LogEventLevel.Warning,
            "Error" => LogEventLevel.Error,
            "Fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

        /// <summary>
        /// Gets the fully resolved log directory path.
        /// </summary>
        /// <remarks>
        /// Uses custom directory if specified and valid, otherwise falls back to
        /// LocalApplicationData\NeonSuit\RSSReader\Logs.
        /// </remarks>
        public string FullLogDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_logDirectory) && Directory.Exists(_logDirectory))
                    return _logDirectory;

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NeonSuit", "RSSReader", "Logs");
            }
        }

        /// <summary>
        /// Gets the full path to today's log file.
        /// </summary>
        public string CurrentLogFilePath => Path.Combine(FullLogDirectory, $"neonsuit_{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// Gets the current size of today's log file in bytes.
        /// </summary>
        public long CurrentLogFileSize
        {
            get
            {
                var path = CurrentLogFilePath;
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
        }

        /// <summary>
        /// Gets a human-readable string of total disk space used by all log files.
        /// </summary>
        public string TotalLogSpaceUsed
        {
            get
            {
                if (!Directory.Exists(FullLogDirectory))
                    return "0 MB";

                var files = Directory.GetFiles(FullLogDirectory, "*.log");
                var totalBytes = files.Sum(f => new FileInfo(f).Length);

                return FormatBytes(totalBytes);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the current settings to the global logging configuration in real-time.
        /// </summary>
        public void ApplySettings()
        {
            AppLoggerConfig.UpdateConfiguration(this);
        }

        /// <summary>
        /// Restores all settings to their default values.
        /// </summary>
        public void ResetToDefaults()
        {
            EnableLogging = true;
            LogLevel = "Information";
            RetentionDays = 7;
            MaxFileSizeMB = 10;
            EnableConsoleLogging = true;
            EnableFileLogging = true;
            EnableDebugWindow = false;
            CompressOldLogs = false;
            LogDirectory = string.Empty;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Formats a byte count into a human-readable string.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0):F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{(bytes / (1024.0 * 1024.0)):F1} MB";

            return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a field value and raises PropertyChanged if the value changed.
        /// </summary>
        /// <typeparam name="T">Type of the field.</typeparam>
        /// <param name="field">Reference to the backing field.</param>
        /// <param name="value">New value to set.</param>
        /// <param name="propertyName">Name of the property (auto-filled by compiler).</param>
        /// <returns>True if the value was changed; otherwise false.</returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}