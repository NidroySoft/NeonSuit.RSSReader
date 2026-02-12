using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a centralized settings management service.
    /// Provides thread-safe access to application preferences with notification events.
    /// </summary>
    public interface ISettingsService
    {
        // Retrieval Methods
        Task<string> GetValueAsync(string key, string defaultValue = "");
        Task<bool> GetBoolAsync(string key, bool defaultValue = false);
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task<double> GetDoubleAsync(string key, double defaultValue = 0.0);

        // Update Methods
        Task SetValueAsync(string key, string value);
        Task SetBoolAsync(string key, bool value);
        Task SetIntAsync(string key, int value);
        Task SetDoubleAsync(string key, double value);

        // Maintenance Methods
        Task ResetToDefaultAsync(string key);
        Task ResetAllToDefaultsAsync();
        Task ExportToFileAsync(string filePath);
        Task ImportFromFileAsync(string filePath);

        // Data Organization
        Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync();

        /// <summary>
        /// Event triggered whenever a preference value is successfully updated.
        /// </summary>
        event EventHandler<PreferenceChangedEventArgs> OnPreferenceChanged;
    }
}