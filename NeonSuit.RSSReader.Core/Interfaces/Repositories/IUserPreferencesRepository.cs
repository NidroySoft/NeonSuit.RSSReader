using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    public interface IUserPreferencesRepository : IRepository<UserPreferences>
    {
        Task<UserPreferences?> GetByKeyAsync(string key);
        Task<string> GetValueAsync(string key, string defaultValue = "");
        Task SetValueAsync(string key, string value);
        Task<bool> GetBoolAsync(string key, bool defaultValue = false);
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task<double> GetDoubleAsync(string key, double defaultValue = 0.0);
        Task ResetToDefaultAsync(string key);
        Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync();
    }
}
