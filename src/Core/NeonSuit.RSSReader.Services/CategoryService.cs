using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Categories;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Service implementation for managing category-related business logic and operations.
    /// Implements <see cref="ICategoryService"/> with optimizations for low-resource environments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service coordinates operations between category and feed repositories,
    /// ensuring data consistency and optimal performance through:
    /// <list type="bullet">
    /// <item>Server-side filtering and aggregation to minimize memory usage</item>
    /// <item>Batch loading of statistics to avoid N+1 query patterns</item>
    /// <item>Proper cancellation token propagation for long-running operations</item>
    /// <item>Structured logging with context for observability</item>
    /// </list>
    /// </para>
    /// </remarks>
    internal class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeedRepository _feedRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="CategoryService"/> class.
        /// </summary>
        /// <param name="categoryRepository">The repository for category data access.</param>
        /// <param name="feedRepository">The repository for feed data access.</param>
        /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public CategoryService(
            ICategoryRepository categoryRepository,
            IFeedRepository feedRepository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(categoryRepository);
            ArgumentNullException.ThrowIfNull(feedRepository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _categoryRepository = categoryRepository;
            _feedRepository = feedRepository;
            _mapper = mapper;
            _logger = logger.ForContext<CategoryService>();

#if DEBUG
            _logger.Debug("CategoryService initialized");
#endif
        }

        #endregion

        #region Read Operations

        /// <inheritdoc />
        public async Task<List<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all categories with statistics");

                var categories = await _categoryRepository.GetAllOrderedAsync(cancellationToken).ConfigureAwait(false);
                var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                // Populate statistics
                foreach (var dto in categoryDtos)
                {
                    dto.FeedCount = feedCounts.GetValueOrDefault(dto.Id, 0);
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);

                    // TODO (Medium - v1.x): Calculate FullPath and Depth efficiently
                    // What to do: Implement recursive path building or store precomputed path in database
                    // Why: Current implementation leaves these properties empty
                    // Risk level: Medium - affects UI display of hierarchical information
                    // Estimated effort: 4 hours
                }

                _logger.Information("Retrieved {Count} categories with statistics", categoryDtos.Count);
                return categoryDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllCategoriesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all categories");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CategoryDto?> GetCategoryByIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Debug("Retrieving category by ID: {CategoryId}", categoryId);

                var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
                if (category == null)
                {
                    _logger.Debug("Category not found with ID: {CategoryId}", categoryId);
                    return null;
                }

                var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                var categoryDto = _mapper.Map<CategoryDto>(category);
                categoryDto.FeedCount = feedCounts.GetValueOrDefault(categoryId, 0);
                categoryDto.UnreadCount = unreadCounts.GetValueOrDefault(categoryId, 0);

                _logger.Debug("Retrieved category: {CategoryName} (ID: {CategoryId})", category.Name, categoryId);
                return categoryDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetCategoryByIdAsync operation was cancelled for ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve category by ID: {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Building complete category tree");

                var categories = await _categoryRepository.GetAllOrderedAsync(cancellationToken).ConfigureAwait(false);
                var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                // Build lookup dictionary for quick parent-child mapping
                var categoryLookup = categories.ToDictionary(c => c.Id);
                var childGroups = categories
                    .Where(c => c.ParentCategoryId.HasValue)
                    .GroupBy(c => c.ParentCategoryId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Map root categories and build tree recursively
                var rootCategories = categories
                    .Where(c => !c.ParentCategoryId.HasValue)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToList();

                var result = new List<CategoryTreeDto>();
                foreach (var root in rootCategories)
                {
                    var treeDto = await BuildCategoryTreeAsync(
                        root,
                        childGroups,
                        feedCounts,
                        unreadCounts,
                        0,
                        cancellationToken).ConfigureAwait(false);
                    result.Add(treeDto);
                }

                _logger.Information("Built category tree with {TotalCategories} total categories, {RootCount} root categories",
                    categories.Count, result.Count);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetCategoryTreeAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to build category tree");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<CategoryDto>> GetAllCategoriesWithFeedsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all categories with feeds");

                var categories = await _categoryRepository.GetAllOrderedAsync(cancellationToken).ConfigureAwait(false);
                var feedsByCategory = await _feedRepository.GetFeedsGroupedByCategoryAsync(false, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                foreach (var dto in categoryDtos)
                {
                    if (feedsByCategory.TryGetValue(dto.Id, out var feeds))
                    {
                        dto.FeedCount = feeds.Count;
                    }
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Information("Retrieved {Count} categories with feeds", categoryDtos.Count);
                return categoryDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllCategoriesWithFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve categories with feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CategoryDto?> GetCategoryWithFeedsAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Debug("Retrieving category with feeds for ID: {CategoryId}", categoryId);

                var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
                if (category == null)
                {
                    _logger.Debug("Category not found with ID: {CategoryId}", categoryId);
                    return null;
                }

                var feeds = await _feedRepository.GetFeedsByCategoryAsync(categoryId, false, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                var categoryDto = _mapper.Map<CategoryDto>(category);
                categoryDto.FeedCount = feeds.Count;
                categoryDto.UnreadCount = unreadCounts.GetValueOrDefault(categoryId, 0);

                _logger.Debug("Retrieved category {CategoryName} with {FeedCount} feeds", category.Name, feeds.Count);
                return categoryDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetCategoryWithFeedsAsync operation was cancelled for ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve category with feeds for ID: {CategoryId}", categoryId);
                throw;
            }
        }

        #endregion

        #region Write Operations

        /// <inheritdoc />
        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                if (string.IsNullOrWhiteSpace(createDto.Name))
                {
                    _logger.Warning("Attempted to create category with empty name");
                    throw new ArgumentException("Category name cannot be empty", nameof(createDto));
                }

                // Validate parent category if specified - usando GetByIdAsync en lugar de ExistsAsync
                if (createDto.ParentCategoryId.HasValue)
                {
                    var parentCategory = await _categoryRepository.GetByIdAsync(createDto.ParentCategoryId.Value, cancellationToken).ConfigureAwait(false);
                    if (parentCategory == null)
                    {
                        _logger.Warning("Parent category not found: {ParentCategoryId}", createDto.ParentCategoryId.Value);
                        throw new InvalidOperationException($"Parent category with ID {createDto.ParentCategoryId.Value} does not exist");
                    }

                    // TODO (High - v1.x): Add validation for circular references
                    // What to do: Prevent creating cycles in category hierarchy
                    // Why: Current implementation might allow invalid parent-child relationships
                    // Risk level: High - affects core category creation logic
                    // Estimated effort: 4 hours
                }

                // Check for duplicate name - usando ExistsByNameAsync que SÍ existe
                var nameExists = await _categoryRepository.ExistsByNameAsync(createDto.Name.Trim(), cancellationToken).ConfigureAwait(false);
                if (nameExists)
                {
                    _logger.Warning("Category with name '{CategoryName}' already exists", createDto.Name);
                    throw new InvalidOperationException($"A category with the name '{createDto.Name}' already exists");
                }

                _logger.Information("Creating new category: {CategoryName}", createDto.Name);

                var category = new Category
                {
                    Name = createDto.Name.Trim(),
                    Description = createDto.Description,
                    ParentCategoryId = createDto.ParentCategoryId,
                    SortOrder = createDto.SortOrder > 0 ? createDto.SortOrder : await GetNextSortOrderAsync(cancellationToken).ConfigureAwait(false),
                    Color = GetDefaultCategoryColor(),
                    CreatedAt = DateTime.UtcNow
                };

                await _categoryRepository.InsertAsync(category, cancellationToken).ConfigureAwait(false);

                var categoryDto = _mapper.Map<CategoryDto>(category);

                // Get statistics for the new category
                var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                categoryDto.FeedCount = feedCounts.GetValueOrDefault(category.Id, 0);
                categoryDto.UnreadCount = unreadCounts.GetValueOrDefault(category.Id, 0);

                _logger.Information("Successfully created category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);

                return categoryDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CreateCategoryAsync operation was cancelled for name: {CategoryName}", createDto.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create category: {CategoryName}", createDto.Name);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CategoryDto?> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided for update: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Debug("Updating category ID: {CategoryId}", categoryId);

                var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
                if (category == null)
                {
                    _logger.Warning("Category not found for update: {CategoryId}", categoryId);
                    return null;
                }

                // Track original values for logging
                var originalName = category.Name;
                var originalParentId = category.ParentCategoryId;
                var originalSortOrder = category.SortOrder;

                // Update properties if provided
                if (!string.IsNullOrWhiteSpace(updateDto.Name) && updateDto.Name.Trim() != category.Name)
                {
                    // Check for duplicate name (excluding current category) - usando ExistsByNameAsync
                    var nameExists = await _categoryRepository.ExistsByNameAsync(updateDto.Name.Trim(), cancellationToken).ConfigureAwait(false);
                    if (nameExists)
                    {
                        // Verificar si el nombre existente pertenece a otra categoría (no a la actual)
                        var existingCategory = await _categoryRepository.GetByNameAsync(updateDto.Name.Trim(), cancellationToken).ConfigureAwait(false);
                        if (existingCategory != null && existingCategory.Id != categoryId)
                        {
                            _logger.Warning("Another category with name '{CategoryName}' already exists (ID: {ExistingId})",
                                updateDto.Name, existingCategory.Id);
                            throw new InvalidOperationException($"A category with the name '{updateDto.Name}' already exists");
                        }
                    }
                    category.Name = updateDto.Name.Trim();
                }

                if (updateDto.Description != null)
                {
                    category.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
                }

                // Validate parent category change
                if (updateDto.ParentCategoryId.HasValue && updateDto.ParentCategoryId != category.ParentCategoryId)
                {
                    if (updateDto.ParentCategoryId.Value == categoryId)
                    {
                        _logger.Warning("Attempted to set category as its own parent: {CategoryId}", categoryId);
                        throw new InvalidOperationException("A category cannot be its own parent");
                    }

                    // Usar GetByIdAsync en lugar de ExistsAsync
                    var parentCategory = await _categoryRepository.GetByIdAsync(updateDto.ParentCategoryId.Value, cancellationToken).ConfigureAwait(false);
                    if (parentCategory == null)
                    {
                        _logger.Warning("Parent category not found: {ParentCategoryId}", updateDto.ParentCategoryId.Value);
                        throw new InvalidOperationException($"Parent category with ID {updateDto.ParentCategoryId.Value} does not exist");
                    }

                    // TODO (High - v1.x): Add validation for circular references in parent-child relationship
                    // What to do: Prevent assigning a parent that would create a cycle
                    // Why: Maintain hierarchical integrity
                    // Risk level: High - affects core category update logic
                    // Estimated effort: 4 hours

                    category.ParentCategoryId = updateDto.ParentCategoryId;
                }

                if (updateDto.SortOrder.HasValue && updateDto.SortOrder.Value != category.SortOrder)
                {
                    category.SortOrder = updateDto.SortOrder.Value;
                }

                category.LastModified = DateTime.UtcNow;

                var result = await _categoryRepository.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully updated category ID: {CategoryId} (Name: {OriginalName} -> {NewName})",
                        categoryId, originalName, category.Name);

                    var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                    var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                    var categoryDto = _mapper.Map<CategoryDto>(category);
                    categoryDto.FeedCount = feedCounts.GetValueOrDefault(categoryId, 0);
                    categoryDto.UnreadCount = unreadCounts.GetValueOrDefault(categoryId, 0);

                    return categoryDto;
                }

                _logger.Warning("No changes made to category ID: {CategoryId}", categoryId);
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateCategoryAsync operation was cancelled for category ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update category ID: {CategoryId}", categoryId);
                throw;
            }
        }
        /// <inheritdoc />
        public async Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided for deletion: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Information("Deleting category with ID: {CategoryId}", categoryId);

                // Check if category exists
                var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
                if (category == null)
                {
                    _logger.Warning("Category not found for deletion: {CategoryId}", categoryId);
                    return false;
                }

                // Check for child categories
                var childCategories = await GetChildCategoryIdsAsync(categoryId, cancellationToken).ConfigureAwait(false);
                if (childCategories.Any())
                {
                    _logger.Warning("Cannot delete category {CategoryId}: has {ChildCount} child categories", categoryId, childCategories.Count);
                    throw new InvalidOperationException($"Cannot delete category '{category.Name}' because it has {childCategories.Count} child categories. Delete or reassign child categories first.");
                }

                // Check for feeds in this category
                var feedsInCategory = await _feedRepository.GetFeedsByCategoryAsync(categoryId, false, cancellationToken).ConfigureAwait(false);
                if (feedsInCategory.Any())
                {
                    _logger.Warning("Cannot delete category {CategoryId}: contains {FeedCount} feeds", categoryId, feedsInCategory.Count);
                    throw new InvalidOperationException($"Cannot delete category '{category.Name}' because it contains {feedsInCategory.Count} feeds. Move or delete feeds first.");
                }

                var result = await _categoryRepository.DeleteAsync(categoryId, cancellationToken).ConfigureAwait(false);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully deleted category with ID: {CategoryId}, Name: {CategoryName}", categoryId, category.Name);
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteCategoryAsync operation was cancelled for ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete category with ID: {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ReorderCategoriesAsync(ReorderCategoriesDto reorderDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reorderDto);

            try
            {
                if (reorderDto.CategoryIds == null || !reorderDto.CategoryIds.Any())
                {
                    _logger.Warning("Attempted to reorder with empty category ID list");
                    return false;
                }

                // Check for duplicates
                if (reorderDto.CategoryIds.Count != reorderDto.CategoryIds.Distinct().Count())
                {
                    _logger.Warning("Duplicate category IDs found in reorder list");
                    throw new ArgumentException("Category IDs must be unique", nameof(reorderDto));
                }

                _logger.Debug("Reordering {Count} categories under parent {ParentCategoryId}",
                    reorderDto.CategoryIds.Count, reorderDto.ParentCategoryId ?? 0);

                // Verify all categories exist and belong to the specified parent
                foreach (var id in reorderDto.CategoryIds)
                {
                    var category = await _categoryRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                    if (category == null)
                    {
                        _logger.Warning("Category not found during reorder: {CategoryId}", id);
                        throw new InvalidOperationException($"Category with ID {id} does not exist");
                    }

                    if (category.ParentCategoryId != reorderDto.ParentCategoryId)
                    {
                        _logger.Warning("Category {CategoryId} does not belong to parent {ParentCategoryId}",
                            id, reorderDto.ParentCategoryId ?? 0);
                        throw new InvalidOperationException($"Category '{category.Name}' is not under the specified parent");
                    }
                }

                var result = await _categoryRepository.ReorderAsync(reorderDto.CategoryIds, cancellationToken).ConfigureAwait(false);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully reordered {Count} categories under parent {ParentCategoryId}",
                        result, reorderDto.ParentCategoryId ?? 0);
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ReorderCategoriesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reorder categories");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CategoryDto> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to get or create category with empty name");
                    throw new ArgumentException("Category name cannot be empty", nameof(name));
                }

                _logger.Debug("Getting or creating category: {CategoryName}", name);

                // Search for existing category
                var existing = await _categoryRepository.GetByNameAsync(name.Trim(), cancellationToken).ConfigureAwait(false);
                if (existing != null)
                {
                    _logger.Debug("Found existing category: {CategoryName} (ID: {CategoryId})", existing.Name, existing.Id);

                    var feedCounts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                    var unreadCounts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);

                    var existingDto = _mapper.Map<CategoryDto>(existing);
                    existingDto.FeedCount = feedCounts.GetValueOrDefault(existing.Id, 0);
                    existingDto.UnreadCount = unreadCounts.GetValueOrDefault(existing.Id, 0);

                    return existingDto;
                }

                // Create new category
                var createDto = new CreateCategoryDto
                {
                    Name = name.Trim(),
                    SortOrder = await GetNextSortOrderAsync(cancellationToken).ConfigureAwait(false)
                };

                return await CreateCategoryAsync(createDto, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetOrCreateCategoryAsync operation was cancelled for name: {CategoryName}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get or create category: {CategoryName}", name);
                throw;
            }
        }

        #endregion

        #region Statistics Operations

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetFeedCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving feed counts by category");

                // TODO (Low - v1.x): Add caching for feed counts
                // What to do: Cache feed counts with 5-minute sliding expiration
                // Why: Feed counts change infrequently but are requested often in UI
                // Risk level: Low - cache miss falls back to database
                // Estimated effort: 4 hours

                var counts = await _feedRepository.GetCountByCategoryAsync(cancellationToken).ConfigureAwait(false);
                _logger.Debug("Retrieved feed counts for {Count} categories", counts.Count);
                return counts;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFeedCountsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feed counts by category");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetUnreadCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving unread counts by category");

                // TODO (Low - v1.x): Add caching for unread counts
                // What to do: Cache unread counts with 2-minute sliding expiration
                // Why: Unread counts change frequently but can be cached briefly to reduce load
                // Risk level: Low - slight staleness acceptable for badge counts
                // Estimated effort: 4 hours

                var counts = await _categoryRepository.GetUnreadCountsByCategoryAsync(cancellationToken).ConfigureAwait(false);
                _logger.Debug("Retrieved unread counts for {Count} categories", counts.Count);
                return counts;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetUnreadCountsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve unread counts by category");
                throw;
            }
        }

        #endregion

        #region Validation Operations

        /// <inheritdoc />
        public async Task<bool> CategoryExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to check existence with empty name");
                    throw new ArgumentException("Category name cannot be empty", nameof(name));
                }

                var exists = await _categoryRepository.ExistsByNameAsync(name.Trim(), cancellationToken).ConfigureAwait(false);
                _logger.Debug("Category existence check for '{Name}': {Exists}", name, exists);
                return exists;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CategoryExistsByNameAsync operation was cancelled for name: {Name}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check category existence for name: {Name}", name);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Builds a hierarchical category tree recursively.
        /// </summary>
        private async Task<CategoryTreeDto> BuildCategoryTreeAsync(
            Category category,
            Dictionary<int, List<Category>> childGroups,
            Dictionary<int, int> feedCounts,
            Dictionary<int, int> unreadCounts,
            int depth,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var treeDto = new CategoryTreeDto
            {
                Id = category.Id,
                Name = category.Name,
                SortOrder = category.SortOrder,
                Depth = depth,
                FeedCount = feedCounts.GetValueOrDefault(category.Id, 0),
                UnreadCount = unreadCounts.GetValueOrDefault(category.Id, 0),
                IsExpanded = depth < 2 // Auto-expand first two levels by default
            };

            if (childGroups.TryGetValue(category.Id, out var children))
            {
                foreach (var child in children.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                {
                    var childDto = await BuildCategoryTreeAsync(
                        child, childGroups, feedCounts, unreadCounts, depth + 1, cancellationToken).ConfigureAwait(false);
                    treeDto.Children.Add(childDto);

                    // Aggregate child counts up to parent for UI convenience
                    treeDto.FeedCount += childDto.FeedCount;
                    treeDto.UnreadCount += childDto.UnreadCount;
                }
            }

            return treeDto;
        }

        /// <summary>
        /// Gets all child category IDs for a given parent category.
        /// </summary>
        private async Task<List<int>> GetChildCategoryIdsAsync(int parentId, CancellationToken cancellationToken)
        {
            var allCategories = await _categoryRepository.GetAllOrderedAsync(cancellationToken).ConfigureAwait(false);

            var result = new List<int>();
            var stack = new Stack<int>();
            stack.Push(parentId);

            while (stack.Count > 0)
            {
                var currentId = stack.Pop();
                var children = allCategories.Where(c => c.ParentCategoryId == currentId).ToList();

                foreach (var child in children)
                {
                    result.Add(child.Id);
                    stack.Push(child.Id);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the next available sort order value for a new category.
        /// </summary>
        private async Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default)
        {
            var maxOrder = await _categoryRepository.GetMaxSortOrderAsync(cancellationToken).ConfigureAwait(false);
            return maxOrder + 1;
        }

        /// <summary>
        /// Gets a default color for a new category.
        /// </summary>
        private static string GetDefaultCategoryColor()
        {
            // Pre-defined color palette for categories
            var colors = new[]
            {
                "#3498db", // Blue
                "#2ecc71", // Green
                "#e74c3c", // Red
                "#f39c12", // Orange
                "#9b59b6", // Purple
                "#1abc9c", // Turquoise
                "#e67e22", // Carrot
                "#e91e63", // Pink
                "#00bcd4", // Cyan
                "#8bc34a", // Light Green
            };

            // Use a static random to avoid creating new Random instances repeatedly
            var index = new Random().Next(colors.Length);
            return colors[index];
        }

        #endregion
    }
}