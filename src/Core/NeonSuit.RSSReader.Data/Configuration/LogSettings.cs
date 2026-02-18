using NeonSuit.RSSReader.Data.Logging;
using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NeonSuit.RSSReader.Data.Configuration
{
    /// <summary>
    /// Configuración del sistema de logging.
    /// </summary>
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

        /// <summary>
        /// Habilita o deshabilita el logging completamente.
        /// </summary>
        public bool EnableLogging
        {
            get => _enableLogging;
            set => SetField(ref _enableLogging, value);
        }

        /// <summary>
        /// Nivel mínimo de logging (Verbose, Debug, Information, Warning, Error, Fatal).
        /// </summary>
        public string LogLevel
        {
            get => _logLevel;
            set => SetField(ref _logLevel, value);
        }

        /// <summary>
        /// Días de retención de archivos de log (1-365).
        /// </summary>
        public int RetentionDays
        {
            get => _retentionDays;
            set => SetField(ref _retentionDays, Math.Clamp(value, 1, 365));
        }

        /// <summary>
        /// Tamaño máximo por archivo en MB (1-100).
        /// </summary>
        public int MaxFileSizeMB
        {
            get => _maxFileSizeMB;
            set => SetField(ref _maxFileSizeMB, Math.Clamp(value, 1, 100));
        }

        /// <summary>
        /// Habilita logging en consola (útil para debugging).
        /// </summary>
        public bool EnableConsoleLogging
        {
            get => _enableConsoleLogging;
            set => SetField(ref _enableConsoleLogging, value);
        }

        /// <summary>
        /// Habilita logging en archivos.
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set => SetField(ref _enableFileLogging, value);
        }

        /// <summary>
        /// Muestra ventana de debugging en tiempo real.
        /// </summary>
        public bool EnableDebugWindow
        {
            get => _enableDebugWindow;
            set => SetField(ref _enableDebugWindow, value);
        }

        /// <summary>
        /// Comprime logs antiguos para ahorrar espacio.
        /// </summary>
        public bool CompressOldLogs
        {
            get => _compressOldLogs;
            set => SetField(ref _compressOldLogs, value);
        }

        /// <summary>
        /// Directorio personalizado para logs (vacío = por defecto).
        /// </summary>
        public string LogDirectory
        {
            get => _logDirectory;
            set => SetField(ref _logDirectory, value);
        }

        /// <summary>
        /// Niveles de log disponibles para UI.
        /// </summary>
        [Ignore]
        public static string[] AvailableLogLevels => new[]
        {
            "Verbose",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Fatal"
        };

        /// <summary>
        /// Convierte el string LogLevel a Serilog LogEventLevel.
        /// </summary>
        [Ignore]
        public Serilog.Events.LogEventLevel SerilogLevel => _logLevel switch
        {
            "Verbose" => Serilog.Events.LogEventLevel.Verbose,
            "Debug" => Serilog.Events.LogEventLevel.Debug,
            "Warning" => Serilog.Events.LogEventLevel.Warning,
            "Error" => Serilog.Events.LogEventLevel.Error,
            "Fatal" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };

        /// <summary>
        /// Ruta completa del directorio de logs.
        /// </summary>
        [Ignore]
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
        /// Ruta completa del archivo de log actual.
        /// </summary>
        [Ignore]
        public string CurrentLogFilePath => Path.Combine(FullLogDirectory, $"neonsuit_{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// Tamaño actual del archivo de log.
        /// </summary>
        [Ignore]
        public long CurrentLogFileSize
        {
            get
            {
                var path = CurrentLogFilePath;
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
        }

        /// <summary>
        /// Espacio total utilizado por logs.
        /// </summary>
        [Ignore]
        public string TotalLogSpaceUsed
        {
            get
            {
                if (!Directory.Exists(FullLogDirectory))
                    return "0 MB";

                var files = Directory.GetFiles(FullLogDirectory, "*.log");
                var totalBytes = files.Sum(f => new FileInfo(f).Length);

                if (totalBytes < 1024)
                    return $"{totalBytes} B";
                if (totalBytes < 1024 * 1024)
                    return $"{(totalBytes / 1024.0):F1} KB";

                return $"{(totalBytes / (1024.0 * 1024.0)):F1} MB";
            }
        }

        /// <summary>
        /// Actualiza la configuración de logging en tiempo real.
        /// </summary>
        public void ApplySettings()
        {
            AppLoggerConfig.UpdateConfiguration(this);
        }

        /// <summary>
        /// Restaura valores por defecto.
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

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}