// =======================================================
// FeedListViewModel.cs
// =======================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeonSuit.RSSReader.Core.DTOs.Categories;
using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Desktop.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NeonSuit.RSSReader.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for managing and displaying the list of RSS feeds and their categories.
    /// Provides comprehensive feed management with support for hierarchical categories,
    /// real-time unread counts, and MaterialDesign UI integration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ViewModel handles:
    /// <list type="bullet">
    /// <item><description>Display of feeds organized by categories with expand/collapse support</description></item>
    /// <item><description>Real-time unread count badges for categories and individual feeds</description></item>
    /// <item><description>Add, edit, delete feed operations with validation</description></item>
    /// <item><description>Category management (create, rename, delete, reorder)</description></item>
    /// <item><description>Drag-drop organization of feeds between categories</description></item>
    /// <item><description>Feed refresh operations with progress indication</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Designed for MaterialDesignInXaml with properties for:
    /// <list type="bullet">
    /// <item><description>Expander controls for categories</description></item>
    /// <item><description>Context menus for feed/category operations</description></item>
    /// <item><description>Dialog hosts for add/edit forms</description></item>
    /// <item><description>Progress indicators during refresh</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public partial class FeedListViewModel : BaseViewModel
    {
        private readonly IFeedService _feedService;
        private readonly ICategoryService _categoryService;
        private readonly IArticleService _articleService;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedListViewModel"/> class.
        /// </summary>
        /// <param name="feedService">Service for feed operations.</param>
        /// <param name="categoryService">Service for category operations.</param>
        /// <param name="articleService">Service for article operations (for unread counts).</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency is null.</exception>
        public FeedListViewModel(
            IFeedService feedService,
            ICategoryService categoryService,
            IArticleService articleService,
            ILogger<FeedListViewModel> logger) : base(logger)
        {
            _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _articleService = articleService ?? throw new ArgumentNullException(nameof(articleService));

            Title = "Feeds";

            LogInformation("FeedListViewModel initialized");
        }

        #endregion

        #region Observable Properties - Data Collections

        /// <summary>
        /// Collection of categories with their feeds, organized hierarchically.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CategoryTreeViewModel> _categories = new();

        /// <summary>
        /// Collection of feeds that don't belong to any category.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<FeedItemViewModel> _uncategorizedFeeds = new();

        /// <summary>
        /// Currently selected feed in the list.
        /// </summary>
        [ObservableProperty]
        private FeedItemViewModel? _selectedFeed;

        /// <summary>
        /// Currently selected category.
        /// </summary>
        [ObservableProperty]
        private CategoryTreeViewModel? _selectedCategory;

        /// <summary>
        /// Collection of all tags for quick access.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TagChipViewModel> _popularTags = new();

        #endregion

        #region Observable Properties - UI State (MaterialDesign)

        /// <summary>
        /// Indicates whether the add feed dialog is open.
        /// </summary>
        [ObservableProperty]
        private bool _isAddFeedDialogOpen;

        /// <summary>
        /// Indicates whether the edit feed dialog is open.
        /// </summary>
        [ObservableProperty]
        private bool _isEditFeedDialogOpen;

        /// <summary>
        /// Indicates whether the add category dialog is open.
        /// </summary>
        [ObservableProperty]
        private bool _isAddCategoryDialogOpen;

        /// <summary>
        /// Indicates whether the edit category dialog is open.
        /// </summary>
        [ObservableProperty]
        private bool _isEditCategoryDialogOpen;

        /// <summary>
        /// Indicates whether the delete confirmation dialog is open.
        /// </summary>
        [ObservableProperty]
        private bool _isDeleteConfirmationOpen;

        /// <summary>
        /// Content for the delete confirmation dialog.
        /// </summary>
        [ObservableProperty]
        private ConfirmDeleteViewModel? _deleteConfirmationContent;

        /// <summary>
        /// Search text for filtering feeds.
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// Indicates whether to show only feeds with unread articles.
        /// </summary>
        [ObservableProperty]
        private bool _showOnlyUnread;

        /// <summary>
        /// Currently dragged feed for drag-drop operations.
        /// </summary>
        [ObservableProperty]
        private FeedItemViewModel? _draggedFeed;

        /// <summary>
        /// Currently dragged category for drag-drop operations.
        /// </summary>
        [ObservableProperty]
        private CategoryTreeViewModel? _draggedCategory;

        /// <summary>
        /// Indicates whether a feed refresh operation is in progress.
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// ID of the feed currently being refreshed.
        /// </summary>
        [ObservableProperty]
        private int? _refreshingFeedId;

        #endregion

        #region Observable Properties - Form Data

        /// <summary>
        /// Data for the new feed being added.
        /// </summary>
        [ObservableProperty]
        private AddFeedFormViewModel _newFeed = new();

        /// <summary>
        /// Data for the feed being edited.
        /// </summary>
        [ObservableProperty]
        private EditFeedFormViewModel _editingFeed = new();

        /// <summary>
        /// Data for the new category being added.
        /// </summary>
        [ObservableProperty]
        private AddCategoryFormViewModel _newCategory = new();

        /// <summary>
        /// Data for the category being edited.
        /// </summary>
        [ObservableProperty]
        private EditCategoryFormViewModel _editingCategory = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the ViewModel asynchronously, loading feeds and categories.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Loading feeds and categories");

                await LoadCategoriesAsync(ct);
                await LoadUncategorizedFeedsAsync(ct);
                await LoadPopularTagsAsync(ct);

                LogInformation("Feeds and categories loaded successfully");

            }, "Loading feeds...", cancellationToken);
        }

        /// <summary>
        /// Loads all categories with their feeds and unread counts.
        /// </summary>
        private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var categoryDtos = await _categoryService.GetAllCategoriesAsync(cancellationToken);
                var unreadCounts = await _articleService.GetUnreadCountsByFeedAsync(cancellationToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Categories.Clear();

                    foreach (var categoryDto in categoryDtos.OrderBy(c => c.SortOrder))
                    {
                        var categoryVm = new CategoryTreeViewModel
                        {
                            Id = categoryDto.Id,
                            Name = categoryDto.Name,
                            SortOrder = categoryDto.SortOrder,
                            FeedCount = categoryDto.FeedCount,
                            IsExpanded = true // Default expanded
                        };

                        // Load feeds for this category
                        foreach (var feedDto in categoryDto.Feeds ?? Enumerable.Empty<FeedSummaryDto>())
                        {
                            unreadCounts.TryGetValue(feedDto.Id, out var unreadCount);

                            categoryVm.Feeds.Add(new FeedItemViewModel
                            {
                                Id = feedDto.Id,
                                Title = feedDto.Title,
                                Description = feedDto.Description,
                                Url = feedDto.Url,
                                WebsiteUrl = feedDto.WebsiteUrl,
                                IconUrl = feedDto.IconUrl,
                                CategoryId = categoryDto.Id,
                                UnreadCount = unreadCount,
                                IsActive = feedDto.IsActive,
                                LastUpdated = feedDto.LastUpdated,
                                ErrorCount = feedDto.ErrorCount
                            });
                        }

                        Categories.Add(categoryVm);
                    }

                    LogDebug("Loaded {CategoryCount} categories", Categories.Count);
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load categories");
                throw;
            }
        }

        /// <summary>
        /// Loads feeds that don't belong to any category.
        /// </summary>
        private async Task LoadUncategorizedFeedsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var feedDtos = await _feedService.GetUncategorizedFeedsAsync(false, cancellationToken);
                var unreadCounts = await _articleService.GetUnreadCountsByFeedAsync(cancellationToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    UncategorizedFeeds.Clear();

                    foreach (var feedDto in feedDtos)
                    {
                        unreadCounts.TryGetValue(feedDto.Id, out var unreadCount);

                        UncategorizedFeeds.Add(new FeedItemViewModel
                        {
                            Id = feedDto.Id,
                            Title = feedDto.Title,
                            Description = feedDto.Description,
                            Url = feedDto.Url,
                            WebsiteUrl = feedDto.WebsiteUrl,
                            IconUrl = feedDto.IconUrl,
                            CategoryId = null,
                            UnreadCount = unreadCount,
                            IsActive = feedDto.IsActive,
                            LastUpdated = feedDto.LastUpdated,
                            ErrorCount = feedDto.ErrorCount
                        });
                    }

                    LogDebug("Loaded {FeedCount} uncategorized feeds", UncategorizedFeeds.Count);
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load uncategorized feeds");
                throw;
            }
        }

        /// <summary>
        /// Loads popular tags for quick filtering.
        /// </summary>
        private async Task LoadPopularTagsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // This would come from ITagService in a real implementation
                // For now, we'll leave it empty or with sample data
                LogDebug("Popular tags loaded");
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to load popular tags");
            }
        }

        #endregion

        #region Feed Commands

        /// <summary>
        /// Command to open the add feed dialog.
        /// </summary>
        [RelayCommand]
        private void OpenAddFeedDialog()
        {
            NewFeed = new AddFeedFormViewModel();
            IsAddFeedDialogOpen = true;
            LogDebug("Add feed dialog opened");
        }

        /// <summary>
        /// Command to close the add feed dialog.
        /// </summary>
        [RelayCommand]
        private void CloseAddFeedDialog()
        {
            IsAddFeedDialogOpen = false;
            NewFeed = new AddFeedFormViewModel();
        }

        /// <summary>
        /// Command to add a new feed.
        /// </summary>
        [RelayCommand]
        private async Task AddFeedAsync(CancellationToken cancellationToken)
        {
            if (!NewFeed.Validate())
            {
                ShowSnackbar("Please fill in all required fields");
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Adding new feed: {FeedUrl}", NewFeed.Url);

                var createDto = new CreateFeedDto
                {
                    Url = NewFeed.Url,
                    CategoryId = NewFeed.CategoryId,
                    IsActive = true
                };

                var newFeed = await _feedService.AddFeedAsync(createDto, ct);

                // Refresh the appropriate category or uncategorized list
                if (newFeed.CategoryId.HasValue)
                {
                    await RefreshCategoryAsync(newFeed.CategoryId.Value, ct);
                }
                else
                {
                    await LoadUncategorizedFeedsAsync(ct);
                }

                CloseAddFeedDialog();
                ShowSnackbar($"Feed '{newFeed.Title}' added successfully");

                LogInformation("Feed added successfully: {FeedTitle}", newFeed.Title);

            }, "Adding feed...", cancellationToken);
        }

        /// <summary>
        /// Command to open the edit feed dialog.
        /// </summary>
        [RelayCommand]
        private void OpenEditFeedDialog(FeedItemViewModel? feed)
        {
            if (feed == null) return;

            SelectedFeed = feed;
            EditingFeed = new EditFeedFormViewModel
            {
                Id = feed.Id,
                Title = feed.Title,
                Url = feed.Url,
                CategoryId = feed.CategoryId,
                IsActive = feed.IsActive
            };

            IsEditFeedDialogOpen = true;
            LogDebug("Edit feed dialog opened for: {FeedTitle}", feed.Title);
        }

        /// <summary>
        /// Command to close the edit feed dialog.
        /// </summary>
        [RelayCommand]
        private void CloseEditFeedDialog()
        {
            IsEditFeedDialogOpen = false;
            EditingFeed = new EditFeedFormViewModel();
        }

        /// <summary>
        /// Command to update an existing feed.
        /// </summary>
        [RelayCommand]
        private async Task UpdateFeedAsync(CancellationToken cancellationToken)
        {
            if (!EditingFeed.Validate())
            {
                ShowSnackbar("Please fill in all required fields");
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Updating feed: {FeedId}", EditingFeed.Id);

                var updateDto = new UpdateFeedDto
                {
                    Title = EditingFeed.Title,
                    Url = EditingFeed.Url,
                    CategoryId = EditingFeed.CategoryId,
                    IsActive = EditingFeed.IsActive
                };

                var updatedFeed = await _feedService.UpdateFeedAsync(EditingFeed.Id, updateDto, ct);

                if (updatedFeed != null)
                {
                    // Refresh both old and new categories
                    if (SelectedFeed?.CategoryId != EditingFeed.CategoryId)
                    {
                        if (SelectedFeed?.CategoryId.HasValue == true)
                            await RefreshCategoryAsync(SelectedFeed.CategoryId.Value, ct);

                        if (EditingFeed.CategoryId.HasValue)
                            await RefreshCategoryAsync(EditingFeed.CategoryId.Value, ct);
                        else
                            await LoadUncategorizedFeedsAsync(ct);
                    }
                    else if (EditingFeed.CategoryId.HasValue)
                    {
                        await RefreshCategoryAsync(EditingFeed.CategoryId.Value, ct);
                    }
                    else
                    {
                        await LoadUncategorizedFeedsAsync(ct);
                    }

                    CloseEditFeedDialog();
                    ShowSnackbar("Feed updated successfully");

                    LogInformation("Feed updated successfully: {FeedId}", EditingFeed.Id);
                }

            }, "Updating feed...", cancellationToken);
        }

        /// <summary>
        /// Command to confirm feed deletion.
        /// </summary>
        [RelayCommand]
        private void ConfirmDeleteFeed(FeedItemViewModel? feed)
        {
            if (feed == null) return;

            SelectedFeed = feed;
            DeleteConfirmationContent = new ConfirmDeleteViewModel
            {
                Title = "Delete Feed",
                Message = $"Are you sure you want to delete '{feed.Title}'? This will also delete all associated articles.",
                ItemId = feed.Id,
                ItemType = "Feed"
            };

            IsDeleteConfirmationOpen = true;
        }

        /// <summary>
        /// Command to delete a feed.
        /// </summary>
        [RelayCommand]
        private async Task DeleteFeedAsync(CancellationToken cancellationToken)
        {
            if (DeleteConfirmationContent?.ItemId == null || SelectedFeed == null)
                return;

            await ExecuteAsync(async (ct) =>
            {
                var feedId = DeleteConfirmationContent.ItemId.Value;
                LogInformation("Deleting feed: {FeedId}", feedId);

                var success = await _feedService.DeleteFeedAsync(feedId, ct);

                if (success)
                {
                    // Remove from UI
                    if (SelectedFeed.CategoryId.HasValue)
                    {
                        var category = Categories.FirstOrDefault(c => c.Id == SelectedFeed.CategoryId);
                        category?.Feeds.Remove(SelectedFeed);
                    }
                    else
                    {
                        UncategorizedFeeds.Remove(SelectedFeed);
                    }

                    IsDeleteConfirmationOpen = false;
                    SelectedFeed = null;
                    ShowSnackbar("Feed deleted successfully");

                    LogInformation("Feed deleted successfully: {FeedId}", feedId);
                }

            }, "Deleting feed...", cancellationToken);
        }

        /// <summary>
        /// Command to refresh a single feed.
        /// </summary>
        [RelayCommand]
        private async Task RefreshFeedAsync(FeedItemViewModel? feed)
        {
            if (feed == null) return;

            RefreshingFeedId = feed.Id;

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Refreshing feed: {FeedTitle} ({FeedId})", feed.Title, feed.Id);

                var success = await _feedService.RefreshFeedAsync(feed.Id, ct);

                if (success)
                {
                    // Update unread count
                    var unreadCount = await _articleService.GetUnreadCountByFeedAsync(feed.Id, ct);
                    feed.UnreadCount = unreadCount;
                    feed.LastUpdated = DateTime.UtcNow;

                    ShowSnackbar($"Feed '{feed.Title}' refreshed");
                    LogInformation("Feed refreshed successfully: {FeedId}", feed.Id);
                }
                else
                {
                    ShowSnackbar($"Failed to refresh '{feed.Title}'");
                }

            }, "Refreshing feed...", cancellationToken);

            RefreshingFeedId = null;
        }

        /// <summary>
        /// Command to mark all articles in a feed as read.
        /// </summary>
        [RelayCommand]
        private async Task MarkFeedAsReadAsync(FeedItemViewModel? feed)
        {
            if (feed == null) return;

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Marking feed as read: {FeedId}", feed.Id);

                var count = await _articleService.MarkAllAsReadByFeedAsync(feed.Id, ct);

                feed.UnreadCount = 0;
                ShowSnackbar($"Marked {count} articles as read");

                LogInformation("Feed marked as read: {FeedId}, {Count} articles", feed.Id, count);

            }, "Marking as read...", cancellationToken);
        }

        #endregion

        #region Category Commands

        /// <summary>
        /// Command to open the add category dialog.
        /// </summary>
        [RelayCommand]
        private void OpenAddCategoryDialog()
        {
            NewCategory = new AddCategoryFormViewModel();
            IsAddCategoryDialogOpen = true;
            LogDebug("Add category dialog opened");
        }

        /// <summary>
        /// Command to close the add category dialog.
        /// </summary>
        [RelayCommand]
        private void CloseAddCategoryDialog()
        {
            IsAddCategoryDialogOpen = false;
            NewCategory = new AddCategoryFormViewModel();
        }

        /// <summary>
        /// Command to add a new category.
        /// </summary>
        [RelayCommand]
        private async Task AddCategoryAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(NewCategory.Name))
            {
                ShowSnackbar("Please enter a category name");
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Adding new category: {CategoryName}", NewCategory.Name);

                var createDto = new CreateCategoryDto
                {
                    Name = NewCategory.Name,
                    Description = NewCategory.Description,
                    ParentId = NewCategory.ParentId
                };

                var newCategory = await _categoryService.CreateCategoryAsync(createDto, ct);

                // Add to UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Categories.Add(new CategoryTreeViewModel
                    {
                        Id = newCategory.Id,
                        Name = newCategory.Name,
                        SortOrder = newCategory.SortOrder,
                        IsExpanded = true
                    });
                });

                CloseAddCategoryDialog();
                ShowSnackbar($"Category '{newCategory.Name}' added successfully");

                LogInformation("Category added successfully: {CategoryId}", newCategory.Id);

            }, "Adding category...", cancellationToken);
        }

        /// <summary>
        /// Command to open the edit category dialog.
        /// </summary>
        [RelayCommand]
        private void OpenEditCategoryDialog(CategoryTreeViewModel? category)
        {
            if (category == null) return;

            SelectedCategory = category;
            EditingCategory = new EditCategoryFormViewModel
            {
                Id = category.Id,
                Name = category.Name
            };

            IsEditCategoryDialogOpen = true;
            LogDebug("Edit category dialog opened for: {CategoryName}", category.Name);
        }

        /// <summary>
        /// Command to close the edit category dialog.
        /// </summary>
        [RelayCommand]
        private void CloseEditCategoryDialog()
        {
            IsEditCategoryDialogOpen = false;
            EditingCategory = new EditCategoryFormViewModel();
        }

        /// <summary>
        /// Command to update an existing category.
        /// </summary>
        [RelayCommand]
        private async Task UpdateCategoryAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(EditingCategory.Name) || SelectedCategory == null)
            {
                ShowSnackbar("Please enter a category name");
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Updating category: {CategoryId}", EditingCategory.Id);

                var updateDto = new UpdateCategoryDto
                {
                    Name = EditingCategory.Name
                };

                var updatedCategory = await _categoryService.UpdateCategoryAsync(EditingCategory.Id, updateDto, ct);

                if (updatedCategory != null)
                {
                    SelectedCategory.Name = updatedCategory.Name;

                    CloseEditCategoryDialog();
                    ShowSnackbar("Category updated successfully");

                    LogInformation("Category updated successfully: {CategoryId}", EditingCategory.Id);
                }

            }, "Updating category...", cancellationToken);
        }

        /// <summary>
        /// Command to confirm category deletion.
        /// </summary>
        [RelayCommand]
        private void ConfirmDeleteCategory(CategoryTreeViewModel? category)
        {
            if (category == null) return;

            SelectedCategory = category;
            DeleteConfirmationContent = new ConfirmDeleteViewModel
            {
                Title = "Delete Category",
                Message = $"Are you sure you want to delete '{category.Name}'? Feeds in this category will be moved to Uncategorized.",
                ItemId = category.Id,
                ItemType = "Category"
            };

            IsDeleteConfirmationOpen = true;
        }

        /// <summary>
        /// Command to delete a category.
        /// </summary>
        [RelayCommand]
        private async Task DeleteCategoryAsync(CancellationToken cancellationToken)
        {
            if (DeleteConfirmationContent?.ItemId == null || SelectedCategory == null)
                return;

            await ExecuteAsync(async (ct) =>
            {
                var categoryId = DeleteConfirmationContent.ItemId.Value;
                LogInformation("Deleting category: {CategoryId}", categoryId);

                var success = await _categoryService.DeleteCategoryAsync(categoryId, ct);

                if (success)
                {
                    // Remove from UI
                    Categories.Remove(SelectedCategory);

                    // Reload uncategorized feeds (feeds from deleted category move here)
                    await LoadUncategorizedFeedsAsync(ct);

                    IsDeleteConfirmationOpen = false;
                    SelectedCategory = null;
                    ShowSnackbar("Category deleted successfully");

                    LogInformation("Category deleted successfully: {CategoryId}", categoryId);
                }

            }, "Deleting category...", cancellationToken);
        }

        /// <summary>
        /// Command to toggle category expand/collapse.
        /// </summary>
        [RelayCommand]
        private void ToggleCategoryExpanded(CategoryTreeViewModel? category)
        {
            if (category != null)
            {
                category.IsExpanded = !category.IsExpanded;
            }
        }

        /// <summary>
        /// Command to expand all categories.
        /// </summary>
        [RelayCommand]
        private void ExpandAllCategories()
        {
            foreach (var category in Categories)
            {
                category.IsExpanded = true;
            }
            LogDebug("All categories expanded");
        }

        /// <summary>
        /// Command to collapse all categories.
        /// </summary>
        [RelayCommand]
        private void CollapseAllCategories()
        {
            foreach (var category in Categories)
            {
                category.IsExpanded = false;
            }
            LogDebug("All categories collapsed");
        }

        #endregion

        #region Drag-Drop Commands

        /// <summary>
        /// Command called when drag operation starts on a feed.
        /// </summary>
        [RelayCommand]
        private void StartDragFeed(FeedItemViewModel? feed)
        {
            DraggedFeed = feed;
            LogDebug("Drag started on feed: {FeedTitle}", feed?.Title);
        }

        /// <summary>
        /// Command called when a feed is dropped onto a category.
        /// </summary>
        [RelayCommand]
        private async Task DropFeedOnCategoryAsync(CategoryTreeViewModel? targetCategory)
        {
            if (DraggedFeed == null || targetCategory == null) return;

            if (DraggedFeed.CategoryId == targetCategory.Id)
            {
                DraggedFeed = null;
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Moving feed {FeedId} to category {CategoryId}",
                    DraggedFeed.Id, targetCategory.Id);

                var success = await _feedService.UpdateFeedCategoryAsync(
                    DraggedFeed.Id, targetCategory.Id, ct);

                if (success)
                {
                    // Remove from source
                    if (DraggedFeed.CategoryId.HasValue)
                    {
                        var sourceCategory = Categories.FirstOrDefault(c => c.Id == DraggedFeed.CategoryId);
                        sourceCategory?.Feeds.Remove(DraggedFeed);
                    }
                    else
                    {
                        UncategorizedFeeds.Remove(DraggedFeed);
                    }

                    // Add to target
                    DraggedFeed.CategoryId = targetCategory.Id;
                    targetCategory.Feeds.Add(DraggedFeed);

                    ShowSnackbar($"Feed moved to '{targetCategory.Name}'");
                    LogInformation("Feed moved successfully");
                }

            }, "Moving feed...", cancellationToken);

            DraggedFeed = null;
        }

        /// <summary>
        /// Command called when a feed is dropped onto uncategorized area.
        /// </summary>
        [RelayCommand]
        private async Task DropFeedOnUncategorizedAsync()
        {
            if (DraggedFeed == null) return;

            if (DraggedFeed.CategoryId == null)
            {
                DraggedFeed = null;
                return;
            }

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Moving feed {FeedId} to Uncategorized", DraggedFeed.Id);

                var success = await _feedService.UpdateFeedCategoryAsync(
                    DraggedFeed.Id, null, ct);

                if (success)
                {
                    // Remove from source category
                    var sourceCategory = Categories.FirstOrDefault(c => c.Id == DraggedFeed.CategoryId);
                    sourceCategory?.Feeds.Remove(DraggedFeed);

                    // Add to uncategorized
                    DraggedFeed.CategoryId = null;
                    UncategorizedFeeds.Add(DraggedFeed);

                    ShowSnackbar("Feed moved to Uncategorized");
                    LogInformation("Feed moved to Uncategorized successfully");
                }

            }, "Moving feed...", cancellationToken);

            DraggedFeed = null;
        }

        #endregion

        #region Filter Commands

        /// <summary>
        /// Command to clear the search filter.
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
            LogDebug("Search cleared");
        }

        /// <summary>
        /// Command to toggle the unread only filter.
        /// </summary>
        [RelayCommand]
        private void ToggleUnreadOnly()
        {
            ShowOnlyUnread = !ShowOnlyUnread;
            LogDebug("Unread only filter toggled: {ShowOnlyUnread}", ShowOnlyUnread);
        }

        #endregion

        #region Refresh Commands

        /// <summary>
        /// Command to refresh all feeds.
        /// </summary>
        [RelayCommand]
        private async Task RefreshAllFeedsAsync(CancellationToken cancellationToken)
        {
            IsRefreshing = true;

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Refreshing all feeds");

                var count = await _feedService.RefreshAllFeedsAsync(ct);

                // Reload all data
                await LoadCategoriesAsync(ct);
                await LoadUncategorizedFeedsAsync(ct);

                ShowSnackbar($"Refreshed {count} feeds");
                LogInformation("All feeds refreshed: {Count} feeds", count);

            }, "Refreshing all feeds...", cancellationToken);

            IsRefreshing = false;
        }

        /// <summary>
        /// Command to refresh feeds in a specific category.
        /// </summary>
        [RelayCommand]
        private async Task RefreshCategoryFeedsAsync(CategoryTreeViewModel? category, CancellationToken cancellationToken)
        {
            if (category == null) return;

            await ExecuteAsync(async (ct) =>
            {
                LogInformation("Refreshing feeds in category: {CategoryName}", category.Name);

                var count = await _feedService.RefreshFeedsByCategoryAsync(category.Id, ct);

                // Refresh this category
                await RefreshCategoryAsync(category.Id, ct);

                ShowSnackbar($"Refreshed {count} feeds in '{category.Name}'");
                LogInformation("Category feeds refreshed: {Count} feeds", count);

            }, "Refreshing category...", cancellationToken);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Refreshes a specific category's feed list.
        /// </summary>
        private async Task RefreshCategoryAsync(int categoryId, CancellationToken cancellationToken)
        {
            try
            {
                var categoryDto = await _categoryService.GetCategoryByIdAsync(categoryId, cancellationToken);
                var unreadCounts = await _articleService.GetUnreadCountsByFeedAsync(cancellationToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var category = Categories.FirstOrDefault(c => c.Id == categoryId);
                    if (category != null && categoryDto != null)
                    {
                        category.Feeds.Clear();

                        foreach (var feedDto in categoryDto.Feeds ?? Enumerable.Empty<FeedSummaryDto>())
                        {
                            unreadCounts.TryGetValue(feedDto.Id, out var unreadCount);

                            category.Feeds.Add(new FeedItemViewModel
                            {
                                Id = feedDto.Id,
                                Title = feedDto.Title,
                                Description = feedDto.Description,
                                Url = feedDto.Url,
                                WebsiteUrl = feedDto.WebsiteUrl,
                                IconUrl = feedDto.IconUrl,
                                CategoryId = categoryId,
                                UnreadCount = unreadCount,
                                IsActive = feedDto.IsActive,
                                LastUpdated = feedDto.LastUpdated,
                                ErrorCount = feedDto.ErrorCount
                            });
                        }

                        category.FeedCount = category.Feeds.Count;
                    }
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to refresh category {CategoryId}", categoryId);
            }
        }

        /// <summary>
        /// Shows a snackbar notification.
        /// </summary>
        private void ShowSnackbar(string message, int durationMs = 3000)
        {
            // This will be handled by the parent MainViewModel
            // We'll raise an event or use a service in real implementation
            LogDebug("Snackbar: {Message}", message);
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
                LogInformation("Disposing FeedListViewModel");

                // Clear collections
                Categories.Clear();
                UncategorizedFeeds.Clear();
                PopularTags.Clear();
            }

            base.Dispose(disposing);
        }

        #endregion
    }

    #region Supporting ViewModels

    /// <summary>
    /// ViewModel representing a category in the tree view with its feeds.
    /// </summary>
    public partial class CategoryTreeViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _sortOrder;

        [ObservableProperty]
        private int _feedCount;

        [ObservableProperty]
        private int _unreadCount;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ObservableCollection<FeedItemViewModel> _feeds = new();
    }

    /// <summary>
    /// ViewModel representing a single feed item in the list.
    /// </summary>
    public partial class FeedItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private string _websiteUrl = string.Empty;

        [ObservableProperty]
        private string _iconUrl = string.Empty;

        [ObservableProperty]
        private int? _categoryId;

        [ObservableProperty]
        private int _unreadCount;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private DateTime? _lastUpdated;

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// Gets the display text for the feed (used in filtering).
        /// </summary>
        public string DisplayText => Title;
    }

    /// <summary>
    /// ViewModel for tag chips in the filter area.
    /// </summary>
    public partial class TagChipViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _color = "#FF5733";

        [ObservableProperty]
        private int _usageCount;

        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>
    /// ViewModel for add feed form.
    /// </summary>
    public partial class AddFeedFormViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private int? _categoryId;

        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(Url) && Uri.IsWellFormedUriString(Url, UriKind.Absolute);
        }
    }

    /// <summary>
    /// ViewModel for edit feed form.
    /// </summary>
    public partial class EditFeedFormViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private int? _categoryId;

        [ObservableProperty]
        private bool _isActive = true;

        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrWhiteSpace(Url) &&
                   Uri.IsWellFormedUriString(Url, UriKind.Absolute);
        }
    }

    /// <summary>
    /// ViewModel for add category form.
    /// </summary>
    public partial class AddCategoryFormViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private int? _parentId;
    }

    /// <summary>
    /// ViewModel for edit category form.
    /// </summary>
    public partial class EditCategoryFormViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;
    }

    /// <summary>
    /// ViewModel for delete confirmation dialog.
    /// </summary>
    public partial class ConfirmDeleteViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private int? _itemId;

        [ObservableProperty]
        private string _itemType = string.Empty;
    }

    #endregion
}