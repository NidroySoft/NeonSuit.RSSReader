namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Holds the data for the preference change event.
    /// This allows subscribers to know exactly which key changed and what is the new value.
    /// </summary>
    public class PreferenceChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public string NewValue { get; }

        public PreferenceChangedEventArgs(string key, string newValue)
        {
            Key = key;
            NewValue = newValue;
        }
    }
}