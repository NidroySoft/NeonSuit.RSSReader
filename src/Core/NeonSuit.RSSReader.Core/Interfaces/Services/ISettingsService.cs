using NeonSuit.RSSReader.Core.DTOs.Preferences;
using NeonSuit.RSSReader.Core.Models.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a centralized settings management service.
    /// Provides thread-safe access to application preferences with notification events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service manages all application settings with the following guarantees:
    /// <list type="bullet">
    /// <item>Thread-safe read/write operations for concurrent access</item>
    /// <item>Type-safe conversion with built-in parsing and validation</item>
    /// <item>Change notifications via <see cref="OnPreferenceChanged"/> event</item>
    /// <item>Persistence across application restarts</item>
    /// <item>Caching for performance (with configurable expiration)</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface ISettingsService
    {
        #region Retrieval Methods

        /// <summary>
        /// Retrieves a string value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="defaultValue">The value to return if the key doesn't exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The stored string value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<string> GetValueAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a boolean value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="defaultValue">The value to return if the key doesn't exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The stored boolean value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="InvalidCastException">Thrown if the stored value cannot be converted to boolean.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an integer value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="defaultValue">The value to return if the key doesn't exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The stored integer value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="InvalidCastException">Thrown if the stored value cannot be converted to integer.</exception>
        /// <exception cref="OverflowException">Thrown if the stored value represents a number less than MinValue or greater than MaxValue.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a double-precision floating point value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="defaultValue">The value to return if the key doesn't exist.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The stored double value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="InvalidCastException">Thrown if the stored value cannot be converted to double.</exception>
        /// <exception cref="OverflowException">Thrown if the stored value represents a number less than MinValue or greater than MaxValue.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<double> GetDoubleAsync(string key, double defaultValue = 0.0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a complete preference DTO for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The preference DTO if found; otherwise, null.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<PreferenceDto?> GetPreferenceAsync(string key, CancellationToken cancellationToken = default);

        #endregion

        #region Update Methods

        /// <summary>
        /// Stores a string value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="value">The value to store (null values are converted to empty string).</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Triggers <see cref="OnPreferenceChanged"/> event after successful persistence.
        /// </remarks>
        Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a boolean value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="value">The boolean value to store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task SetBoolAsync(string key, bool value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores an integer value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="value">The integer value to store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task SetIntAsync(string key, int value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a double-precision floating point value for the specified key.
        /// </summary>
        /// <param name="key">The setting identifier (case-insensitive).</param>
        /// <param name="value">The double value to store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task SetDoubleAsync(string key, double value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a preference using the provided DTO.
        /// </summary>
        /// <param name="updateDto">The DTO containing preference update data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated preference DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if key is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<PreferenceDto> UpdatePreferenceAsync(UpdatePreferenceDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple preferences in a single batch operation.
        /// </summary>
        /// <param name="updates">List of preference updates.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of preferences successfully updated.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updates"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> UpdatePreferencesBatchAsync(List<UpdatePreferenceDto> updates, CancellationToken cancellationToken = default);

        #endregion

        #region Maintenance Methods

        /// <summary>
        /// Resets a specific setting to its default value.
        /// </summary>
        /// <param name="key">The setting identifier to reset.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ResetToDefaultAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets multiple preferences to their default values.
        /// </summary>
        /// <param name="resetDto">The DTO containing reset information.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of preferences successfully reset.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="resetDto"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> ResetPreferencesAsync(ResetPreferencesDto resetDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ResetAllToDefaultsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports all settings to a JSON file at the specified path.
        /// </summary>
        /// <param name="filePath">Full path where the settings file will be created.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if write access to the directory is denied.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory path doesn't exist.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs while creating the file.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ExportToFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports settings from a JSON file at the specified path.
        /// </summary>
        /// <param name="filePath">Full path to the settings file to import.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified file doesn't exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is denied.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the file format is invalid or incompatible.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default);

        #endregion

        #region Data Organization

        /// <summary>
        /// Retrieves all preferences organized by category for UI display.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of preference categories with their preferences.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<PreferenceCategoryDto>> GetAllCategorizedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all preferences as a summary list.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of preference summaries.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<PreferenceSummaryDto>> GetAllSummariesAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Events

        /// <summary>
        /// Event triggered whenever a preference value is successfully updated.
        /// </summary>
        event EventHandler<PreferenceChangedEventArgs> OnPreferenceChanged;

        #endregion
    }
}