using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a single user preference stored in the database.
    /// Implements INotifyPropertyChanged to support two-way data binding in UI layers.
    /// </summary>
    /// <remarks>
    /// All values are persisted as strings. Use typed accessors (BoolValue, IntValue, DoubleValue)
    /// for convenient and type-safe access from business logic or view-models.
    /// LastModified is maintained automatically for cache invalidation and audit purposes.
    /// </remarks>
    [Table("UserPreferences")]
    public class UserPreferences : INotifyPropertyChanged
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="UserPreferences"/> class.
        /// Sets LastModified to current UTC time.
        /// </summary>
        public UserPreferences()
        {
            LastModified = DateTime.UtcNow;
        }

        #endregion

        #region Persisted Properties

        /// <summary>
        /// Primary key (auto-increment).
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Unique preference identifier (case-sensitive).
        /// </summary>
        /// <remarks>
        /// Should be indexed with uniqueness constraint in DbContext configuration.
        /// Recommended format: "Category.SubCategory.KeyName"
        /// </remarks>
        [Required]
        [MaxLength(255)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Serialized string value of the preference.
        /// </summary>
        [MaxLength(4000)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp of the last modification.
        /// Used for cache invalidation and optimistic concurrency detection.
        /// </summary>
        public DateTime LastModified { get; set; }

        #endregion

        #region Typed Accessors (Not Mapped)

        /// <summary>
        /// Convenience boolean wrapper around the string Value.
        /// </summary>
        [NotMapped]
        public bool BoolValue
        {
            get => bool.TryParse(Value, out bool result) && result;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// Convenience integer wrapper around the string Value.
        /// Returns 0 when parsing fails.
        /// </summary>
        [NotMapped]
        public int IntValue
        {
            get => int.TryParse(Value, out int result) ? result : 0;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// Convenience double wrapper around the string Value.
        /// Returns 0.0 when parsing fails.
        /// </summary>
        [NotMapped]
        public double DoubleValue
        {
            get => double.TryParse(Value, out double result) ? result : 0.0;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
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

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add unique index configuration in DbContext for Key property
// What to do: In OnModelCreating, add: entity.HasIndex(e => e.Key).IsUnique();
// Why: Ensure key uniqueness at database level for performance and data integrity
// Implementation: Add fluent configuration in RssReaderDbContext
// Risk level: Low - requires migration but doesn't affect existing code
// Estimated effort: 30 minutes

// TODO (Medium - v1.x): Add concurrency token for optimistic concurrency
// What to do: Add Timestamp/RowVersion property to handle concurrent updates
// Why: Prevent lost updates when multiple threads modify same preference
// Implementation: Add byte[] RowVersion with [Timestamp] attribute
// Risk level: Medium - requires database schema change
// Estimated effort: 4 hours

// TODO (Low - v1.x): Add support for preference groups/categories in UI
// What to do: Use GetCategorizedKeys() to build settings UI with collapsible sections
// Why: Improve user experience when navigating many preferences
// Implementation: Bind UI to categorized dictionary with group headers
// Risk level: Low - UI-only change
// Estimated effort: 4 hours

// TODO (Medium - v1.x): Add migration support for preference keys
// What to do: Handle renamed or deprecated preference keys across versions
// Why: Maintain user settings when keys change between application versions
// Implementation: Add migration map in PreferenceHelper, run on settings load
// Risk level: Medium - must handle edge cases carefully
// Estimated effort: 1 day

// TODO (Critical - v2.0): Add encryption for sensitive preferences
// What to do: Automatically encrypt/decrypt keys marked as sensitive (API keys, tokens)
// Why: Store credentials securely
// Implementation: Add [Sensitive] attribute and encrypt/decrypt on read/write
// Risk level: Critical - security features must be flawless
// Estimated effort: 2 days

// TODO (High - v1.x): Add preference change tracking for undo functionality
// What to do: Store previous values to allow reverting changes
// Why: Prevent accidental misconfiguration
// Implementation: Keep in-memory history of changes with timestamps
// Risk level: Low - doesn't affect core functionality
// Estimated effort: 4 hours

// TODO (Medium - v1.x): Add support for preference dependencies
// What to do: Define dependencies between preferences (e.g., enabling proxy requires address/port)
// Why: Prevent inconsistent configurations
// Implementation: Add dependency validation rules
// Risk level: Medium - adds validation complexity
// Estimated effort: 1 day