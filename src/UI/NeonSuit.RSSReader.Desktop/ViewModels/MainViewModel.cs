// =======================================================
// MainViewModel.cs
// =======================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using NeonSuit.RSSReader.Core.DTOs.Modules;
using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.DTOs.Sync;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Desktop.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NeonSuit.RSSReader.Desktop.ViewModels
{
    /// <summary>
    /// Main ViewModel that serves as the application shell and coordinator for all major components.
    /// Implements the Container/Shell pattern to manage navigation, global state, and cross-cutting concerns
    /// such as synchronization status, unread counts, and real-time notifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ViewModel is the root of the MVVM hierarchy and maintains references to all child ViewModels.
    /// It orchestrates application-wide operations including:
    /// <list type="bullet">
    /// <item><description>Global synchronization status and progress reporting</description></item>
    /// <item><description>Real-time unread count updates across all feeds</description></item>
    /// <item><description>Navigation between different sections of the application</description></item>
    /// <item><description>Module loading and initialization during startup</description></item>
    /// <item><description>Background service event subscriptions for UI reactivity</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Designed to work seamlessly with MaterialDesignInXaml, exposing properties for:
    /// <list type="bullet">
    /// <item><description>Drawer/Hamburger menu state (<see cref="IsDrawerOpen"/>)</description></item>
    /// <item><description>Snackbar notifications (<see cref="SnackbarMessage"/>, <see cref="IsSnackbarActive"/>)</description></item>
    /// <item><description>Dialog hosting (<see cref="IsDialogOpen"/>, <see cref="DialogContent"/>)</description></item>
    /// <item><description>Progress indicators (<see cref="IsBusy"/>, <see cref="Progress"/>)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public partial class MainViewModel : BaseViewModel
    {
        private readonly IFeedService _feedService;
        private readonly ICategoryService _categoryService;
        private readonly IArticleService _articleService;
        private readonly ISyncCoordinatorService _syncCoordinator;
        private readonly INotificationService _notificationService;
        private readonly IModuleService _moduleService;
        private readonly ISettingsService _settingsService;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        /// <param name="feedService">Service for feed operations.</param>
        /// <param name="categoryService">Service for category operations.</param>
        /// <param name="articleService">Service for article operations.</param>
        /// <param name="syncCoordinator">Service for coordinating background sync tasks.</param>
        /// <param name="notificationService">Service for notification management.</param>
        /// <param name="moduleService">Service for module management.</param>
        /// <param name="settingsService">Service for settings management.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="feedListViewModel">Injected FeedListViewModel instance.</param>
        /// <param name="articleListViewModel">Injected ArticleListViewModel instance.</param>
        /// <param name="articleReaderViewModel">Injected ArticleReaderViewModel instance.</param>
        /// <param name="settingsViewModel">Injected SettingsViewModel instance.</param>
        /// <param name="moduleManagerViewModel">Injected ModuleManagerViewModel instance.</param>
        /// <param name="tagManagerViewModel">Injected TagManagerViewModel instance.</param>
        /// <param name="notificationCenterViewModel">Injected NotificationCenterViewModel instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency is null.</exception>
        public MainViewModel(
            IFeedService feedService,
            ICategoryService categoryService,
            IArticleService articleService,
            ISyncCoordinatorService syncCoordinator,
            INotificationService notificationService,
            IModuleService moduleService,
            ISettingsService settingsService,
            ILogger<MainViewModel> logger,
            FeedListViewModel feedListViewModel,
            ArticleListViewModel articleListViewModel,
            ArticleReaderViewModel articleReaderViewModel,
            SettingsViewModel settingsViewModel,
            ModuleManagerViewModel moduleManagerViewModel,
            TagManagerViewModel tagManagerViewModel,
            NotificationCenterViewModel notificationCenterViewModel) : base(logger)
        {
            // Initialize services
            _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _articleService = articleService ?? throw new ArgumentNullException(nameof(articleService));
            _syncCoordinator = syncCoordinator ?? throw new ArgumentNullException(nameof(syncCoordinator));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize child ViewModels
            FeedListViewModel = feedListViewModel ?? throw new ArgumentNullException(nameof(feedListViewModel));
            ArticleListViewModel = articleListViewModel ?? throw new ArgumentNullException(nameof(articleListViewModel));
            ArticleReaderViewModel = articleReaderViewModel ?? throw new ArgumentNullException(nameof(articleReaderViewModel));
            SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
            ModuleManagerViewModel = moduleManagerViewModel ?? throw new ArgumentNullException(nameof(moduleManagerViewModel));
            TagManagerViewModel = tagManagerViewModel ?? throw new ArgumentNullException(nameof(tagManagerViewModel));
            NotificationCenterViewModel = notificationCenterViewModel ?? throw new ArgumentNullException(nameof(notificationCenterViewModel));

            // Set default title and view
            Title = "NeonSuit RSS Reader";
            CurrentViewModel = FeedListViewModel;

            SubscribeToEvents();

            LogInformation("MainViewModel initialized successfully");
        }

        #endregion

        #region Observable Properties - MaterialDesign Specific

        /// <summary>
        /// Indicates whether the navigation drawer (hamburger menu) is open.
        /// </summary>
        [ObservableProperty]
        private bool _isDrawerOpen;

        /// <summary>
        /// Indicates whether a dialog is currently open.
        /// </summary>
        [ObservableProperty]
        private bool _isDialogOpen;

        /// <summary>
        /// Content to display in the current dialog.
        /// </summary>
        [ObservableProperty]
        private object? _dialogContent;

        /// <summary>
        /// Message to display in the snackbar notification.
        /// </summary>
        [ObservableProperty]
        private string _snackbarMessage = string.Empty;

        /// <summary>
        /// Indicates whether the snackbar notification is active.
        /// </summary>
        [ObservableProperty]
        private bool _isSnackbarActive;

        /// <summary>
        /// Currently selected menu item for navigation.
        /// </summary>
        [ObservableProperty]
        private object? _selectedMenuItem;

        /// <summary>
        /// Indicates whether the application is in offline mode.
        /// </summary>
        [ObservableProperty]
        private bool _isOfflineMode;

        /// <summary>
        /// Current theme of the application (Light/Dark/System).
        /// </summary>
        [ObservableProperty]
        private string _currentTheme = "System";

        #endregion

        #region Observable Properties - Application State

        /// <summary>
        /// Total number of unread articles across all feeds.
        /// </summary>
        [ObservableProperty]
        private int _totalUnreadCount;

        /// <summary>
        /// Total number of active feeds in the system.
        /// </summary>
        [ObservableProperty]
        private int _totalFeedsCount;

        /// <summary>
        /// Current synchronization status from the coordinator service.
        /// </summary>
        [ObservableProperty]
        private SyncStatusDto? _syncStatus;

        /// <summary>
        /// Indicates whether the synchronization service is currently paused.
        /// </summary>
        [ObservableProperty]
        private bool _isSyncPaused;

        /// <summary>
        /// Collection of recent notifications to display in the notification center.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<NotificationSummaryDto> _recentNotifications = new();

        /// <summary>
        /// Currently active child ViewModel displayed in the main content area.
        /// </summary>
        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        /// <summary>
        /// Gets a value indicating whether synchronization can be triggered.
        /// </summary>
        public bool CanSync => !IsBusy && !IsSyncPaused;

        #endregion

        #region Child ViewModels

        /// <summary>
        /// ViewModel for managing the feed list and categories.
        /// </summary>
        public FeedListViewModel FeedListViewModel { get; }

        /// <summary>
        /// ViewModel for displaying and managing articles.
        /// </summary>
        public ArticleListViewModel ArticleListViewModel { get; }

        /// <summary>
        /// ViewModel for reading article content with WebView integration.
        /// </summary>
        public ArticleReaderViewModel ArticleReaderViewModel { get; }

        /// <summary>
        /// ViewModel for application settings and preferences.
        /// </summary>
        public SettingsViewModel SettingsViewModel { get; }

        /// <summary>
        /// ViewModel for managing plugable modules.
        /// </summary>
        public ModuleManagerViewModel ModuleManagerViewModel { get; }

        /// <summary>
        /// ViewModel for tag management operations.
        /// </summary>
        public TagManagerViewModel TagManagerViewModel { get; }

        /// <summary>
        /// ViewModel for notification center and history.
        /// </summary>
        public NotificationCenterViewModel NotificationCenterViewModel { get; }

        #endregion

        #region Event Subscriptions

        /// <summary>
        /// Subscribes to events from backend services to provide real-time UI updates.
        /// </summary>
        private void SubscribeToEvents()
        {
            try
            {
                _syncCoordinator.OnStatusChanged += OnSyncStatusChanged;
                _syncCoordinator.OnSyncProgress += OnSyncProgress;
                _syncCoordinator.OnSyncError += OnSyncError;

                _notificationService.OnNotificationCreated += OnNotificationCreated;
                _moduleService.OnModuleLoaded += OnModuleLoaded;

                LogInformation("Successfully subscribed to backend service events");
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to subscribe to backend service events");
            }
        }

        /// <summary>
        /// Handles synchronization status changes from the coordinator.
        /// </summary>
        private void OnSyncStatusChanged(object? sender, SyncStatusDto status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SyncStatus = status;
                IsSyncPaused = status.State == "Paused";

                LogDebug("Sync status changed: {State} - {Message}", status.State, status.Message);
            });
        }

        /// <summary>
        /// Handles progress updates during long-running synchronization tasks.
        /// </summary>
        private void OnSyncProgress(object? sender, SyncProgressDto progress)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = progress.Percentage;
                StatusMessage = progress.Message;

                LogTrace("Sync progress: {Percentage}% - {Message}", progress.Percentage, progress.Message);
            });
        }

        /// <summary>
        /// Handles synchronization errors for UI notification.
        /// </summary>
        private void OnSyncError(object? sender, SyncErrorInfoDto error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowSnackbar($"Error: {error.Message}");
                LogError("Sync error: {ErrorType} - {Message}", error.ErrorType, error.Message);
            });
        }

        /// <summary>
        /// Handles new notification creation for real-time UI updates.
        /// </summary>
        private void OnNotificationCreated(object? sender, NotificationDto notification)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Keep only the last 50 notifications
                if (RecentNotifications.Count >= 50)
                {
                    RecentNotifications.RemoveAt(RecentNotifications.Count - 1);
                }

                RecentNotifications.Insert(0, new NotificationSummaryDto
                {
                    Id = notification.Id,
                    Title = notification.Title,
                    Message = notification.Message,
                    CreatedAt = notification.CreatedAt,
                    Priority = notification.Priority,
                    IsRead = notification.IsRead
                });

                // Show snackbar for high priority notifications
                if (notification.Priority == "High" || notification.Priority == "Critical")
                {
                    ShowSnackbar(notification.Title);
                }

                LogDebug("New notification received: {Title}", notification.Title);
            });
        }

        /// <summary>
        /// Handles module loaded events to update module list.
        /// </summary>
        private void OnModuleLoaded(object? sender, ModuleInfoDto module)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowSnackbar($"Module loaded: {module.Name}");
                LogInformation("Module {ModuleName} loaded successfully", module.Name);
            });
        }

        #endregion

        #region Navigation Commands

        /// <summary>
        /// Command to toggle the navigation drawer open/closed.
        /// </summary>
        [RelayCommand]
        private void ToggleDrawer()
        {
            IsDrawerOpen = !IsDrawerOpen;
            LogDebug("Drawer toggled: {IsDrawerOpen}", IsDrawerOpen);
        }

        /// <summary>
        /// Command to navigate to the feed list view.
        /// </summary>
        [RelayCommand]
        private void NavigateToFeeds()
        {
            CurrentViewModel = FeedListViewModel;
            Title = "Feeds";
            IsDrawerOpen = false;
            StatusMessage = "Viewing feeds";
            LogDebug("Navigated to Feeds view");
        }

        /// <summary>
        /// Command to navigate to the articles list view.
        /// </summary>
        [RelayCommand]
        private void NavigateToArticles()
        {
            CurrentViewModel = ArticleListViewModel;
            Title = "Articles";
            IsDrawerOpen = false;
            StatusMessage = "Viewing articles";
            LogDebug("Navigated to Articles view");
        }

        /// <summary>
        /// Command to navigate to the settings view.
        /// </summary>
        [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentViewModel = SettingsViewModel;
            Title = "Settings";
            IsDrawerOpen = false;
            StatusMessage = "Viewing settings";
            LogDebug("Navigated to Settings view");
        }

        /// <summary>
        /// Command to navigate to the module manager view.
        /// </summary>
        [RelayCommand]
        private void NavigateToModules()
        {
            CurrentViewModel = ModuleManagerViewModel;
            Title = "Modules";
            IsDrawerOpen = false;
            StatusMessage = "Managing modules";
            LogDebug("Navigated to Module Manager view");
        }

        /// <summary>
        /// Command to navigate to the tag manager view.
        /// </summary>
        [RelayCommand]
        private void NavigateToTags()
        {
            CurrentViewModel = TagManagerViewModel;
            Title = "Tags";
            IsDrawerOpen = false;
            StatusMessage = "Managing tags";
            LogDebug("Navigated to Tag Manager view");
        }

        /// <summary>
        /// Command to navigate to the notification center.
        /// </summary>
        [RelayCommand]
        private void NavigateToNotifications()
        {
            CurrentViewModel = NotificationCenterViewModel;
            Title = "Notifications";
            IsDrawerOpen = false;
            StatusMessage = "Viewing notifications";
            LogDebug("Navigated to Notification Center view");
        }

        #endregion

        #region Sync Commands

        /// <summary>
        /// Command to manually trigger a full synchronization of all feeds.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSync))]
        private async Task SyncAllAsync(CancellationToken cancellationToken)
        {
            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Manual full synchronization started");

                var result = await _syncCoordinator.TriggerFullSyncAsync(ct);

                if (result.Success)
                {
                    ShowSnackbar($"Synchronization completed. Processed: {result.ProcessedCount} items");
                    LogInformation("Full synchronization completed: {ProcessedCount} items", result.ProcessedCount);
                }
                else
                {
                    ShowSnackbar($"Synchronization failed: {result.ErrorMessage}");
                    LogError("Full synchronization failed: {ErrorMessage}", result.ErrorMessage);
                }

                // Refresh statistics after sync
                await LoadStatisticsAsync(ct);

            }, "Synchronizing...", cancellationToken);
        }

        /// <summary>
        /// Command to pause all synchronization activities.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSync))]
        private async Task PauseSyncAsync(CancellationToken cancellationToken)
        {
            await ExecuteAsync(async (ct) =>
            {
                await _syncCoordinator.PauseAsync(ct);
                ShowSnackbar("Synchronization paused");
                LogInformation("Synchronization paused by user");
            }, "Pausing...", cancellationToken);
        }

        /// <summary>
        /// Command to resume paused synchronization.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSync))]
        private async Task ResumeSyncAsync(CancellationToken cancellationToken)
        {
            await ExecuteAsync(async (ct) =>
            {
                await _syncCoordinator.ResumeAsync(ct);
                ShowSnackbar("Synchronization resumed");
                LogInformation("Synchronization resumed by user");
            }, "Resuming...", cancellationToken);
        }

        #endregion

        #region Dialog Commands

        /// <summary>
        /// Command to open a dialog with the specified content.
        /// </summary>
        [RelayCommand]
        private void OpenDialog(object? content)
        {
            DialogContent = content;
            IsDialogOpen = true;
            LogDebug("Dialog opened with content type: {ContentType}", content?.GetType().Name ?? "null");
        }

        /// <summary>
        /// Command to close the currently open dialog.
        /// </summary>
        [RelayCommand]
        private void CloseDialog()
        {
            IsDialogOpen = false;
            DialogContent = null;
            LogDebug("Dialog closed");
        }

        #endregion

        #region Settings Commands

        /// <summary>
        /// Command to toggle offline mode.
        /// </summary>
        [RelayCommand]
        private async Task ToggleOfflineModeAsync()
        {
            try
            {
                IsOfflineMode = !IsOfflineMode;
                await _settingsService.SetBoolAsync("IsOfflineMode", IsOfflineMode);

                ShowSnackbar(IsOfflineMode ? "Offline mode enabled" : "Online mode enabled");
                LogInformation("Offline mode toggled: {IsOfflineMode}", IsOfflineMode);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to toggle offline mode");
                ShowSnackbar($"Failed to toggle offline mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Command to toggle application theme.
        /// </summary>
        [RelayCommand]
        private async Task ToggleThemeAsync()
        {
            try
            {
                CurrentTheme = CurrentTheme switch
                {
                    "Light" => "Dark",
                    "Dark" => "System",
                    _ => "Light"
                };

                await _settingsService.SetValueAsync("Theme", CurrentTheme);
                ShowSnackbar($"Theme changed to {CurrentTheme}");

                LogInformation("Theme changed to {Theme}", CurrentTheme);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to change theme");
                ShowSnackbar($"Failed to change theme: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the application asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Starting application initialization");

                // Load user preferences
                await LoadUserPreferencesAsync(ct);

                // Load initial statistics
                await LoadStatisticsAsync(ct);

                // Load recent notifications
                await LoadRecentNotificationsAsync(ct);

                // Start sync coordinator if not already running
                if (_syncCoordinator.CurrentStatus.State == "Stopped")
                {
                    await _syncCoordinator.StartAsync(ct);
                    LogInformation("Sync coordinator started");
                }

                // Initialize child ViewModels
                await FeedListViewModel.InitializeAsync(ct);

                LogInformation("Application initialization completed successfully");

            }, "Initializing application...", cancellationToken);
        }

        /// <summary>
        /// Shows a snackbar notification message.
        /// </summary>
        /// <param name="message">Message to display.</param>
        /// <param name="durationMs">Duration in milliseconds (default: 3000).</param>
        private void ShowSnackbar(string message, int durationMs = 3000)
        {
            SnackbarMessage = message;
            IsSnackbarActive = true;

            // Auto-hide after duration
            Task.Delay(durationMs).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsSnackbarActive = false;
                });
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads user preferences from the settings service.
        /// </summary>
        private async Task LoadUserPreferencesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsOfflineMode = await _settingsService.GetBoolAsync("IsOfflineMode", false, cancellationToken);
                CurrentTheme = await _settingsService.GetValueAsync("Theme", "System", cancellationToken);

                LogDebug("User preferences loaded: OfflineMode={OfflineMode}, Theme={Theme}",
                    IsOfflineMode, CurrentTheme);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load user preferences");
            }
        }

        /// <summary>
        /// Loads application statistics.
        /// </summary>
        private async Task LoadStatisticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var unreadCounts = await _feedService.GetUnreadCountsAsync(cancellationToken);
                TotalUnreadCount = 0;

                foreach (var count in unreadCounts.Values)
                {
                    TotalUnreadCount += count;
                }

                var feeds = await _feedService.GetAllFeedsAsync(false, cancellationToken);
                TotalFeedsCount = feeds.Count;

                LogDebug("Statistics loaded: Unread={UnreadCount}, Feeds={FeedsCount}",
                    TotalUnreadCount, TotalFeedsCount);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load statistics");
            }
        }

        /// <summary>
        /// Loads recent notifications from the notification service.
        /// </summary>
        private async Task LoadRecentNotificationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var notifications = await _notificationService.GetRecentNotificationsAsync(20, cancellationToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RecentNotifications.Clear();
                    foreach (var notification in notifications)
                    {
                        RecentNotifications.Add(notification);
                    }
                });

                LogDebug("Loaded {Count} recent notifications", notifications.Count);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load recent notifications");
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LogInformation("Disposing MainViewModel");

                // Unsubscribe from events
                _syncCoordinator.OnStatusChanged -= OnSyncStatusChanged;
                _syncCoordinator.OnSyncProgress -= OnSyncProgress;
                _syncCoordinator.OnSyncError -= OnSyncError;
                _notificationService.OnNotificationCreated -= OnNotificationCreated;
                _moduleService.OnModuleLoaded -= OnModuleLoaded;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}