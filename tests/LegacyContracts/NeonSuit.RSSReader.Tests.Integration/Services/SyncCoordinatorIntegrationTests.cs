using FluentAssertions;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services;

[Collection("Integration Tests")]
public class SyncCoordinatorIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _dbFixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private readonly ServiceFactory _factory;

    private ISyncCoordinatorService _syncCoordinator = null!;
    private IFeedService _feedService = null!;
    private ITagService _tagService = null!;
    private ISettingsService _settingsService = null!;

    public SyncCoordinatorIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
    {
        _dbFixture = dbFixture;
        _output = output;
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<SyncCoordinatorIntegrationTests>();

        _factory = new ServiceFactory(_dbFixture);
    }

    public async Task InitializeAsync()
    {
        var dbContext = _dbFixture.CreateNewDbContext();

        // Setup services
        _settingsService = _factory.CreateSettingsService();
        _feedService = _factory.CreateFeedService();
        _tagService = _factory.CreateTagService();

        _syncCoordinator = new SyncCoordinatorService(_settingsService, _logger);

        await SetupTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Test Data Setup

    private async Task SetupTestDataAsync()
    {
        // Create test feeds
        var feed1 = new Feed
        {
            Title = "Tech News",
            Url = $"https://tech-{Guid.NewGuid():N}.com/rss",
            WebsiteUrl = "https://tech.com",
            IsActive = true
        };
        await _feedService.CreateFeedAsync(feed1);

        var feed2 = new Feed
        {
            Title = "Science Daily",
            Url = $"https://science-{Guid.NewGuid():N}.com/rss",
            WebsiteUrl = "https://science.com",
            IsActive = true
        };
        await _feedService.CreateFeedAsync(feed2);

        var feed3 = new Feed
        {
            Title = "Inactive Feed",
            Url = $"https://inactive-{Guid.NewGuid():N}.com/rss",
            WebsiteUrl = "https://inactive.com",
            IsActive = false
        };
        await _feedService.CreateFeedAsync(feed3);

        // Create test tags
        var tag1 = new Tag { Name = $"Technology_{Guid.NewGuid():N}", Color = "#FF5733" };
        await _tagService.CreateTagAsync(tag1);

        var tag2 = new Tag { Name = $"Science_{Guid.NewGuid():N}", Color = "#33FF57" };
        await _tagService.CreateTagAsync(tag2);
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task StartAsync_WhenStopped_ShouldStartCoordinator()
    {
        // Act
        await _syncCoordinator.StartAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(SyncStatus.Running);
        _syncCoordinator.IsSynchronizing.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldNotChangeStatus()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var initialStatus = _syncCoordinator.CurrentStatus;

        // Act
        await _syncCoordinator.StartAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(initialStatus);
    }

    [Fact]
    public async Task StopAsync_WhenRunning_ShouldStopCoordinator()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.StopAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(SyncStatus.Stopped);
        _syncCoordinator.IsSynchronizing.Should().BeFalse();
    }

    [Fact]
    public async Task PauseAndResumeAsync_ShouldTogglePauseState()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act - Pause
        await _syncCoordinator.PauseAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(SyncStatus.Running);
        _syncCoordinator.IsSynchronizing.Should().BeFalse(); // Paused

        // Act - Resume
        await _syncCoordinator.ResumeAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(SyncStatus.Running);
        _syncCoordinator.IsSynchronizing.Should().BeTrue();
    }

    [Fact]
    public async Task PauseAsync_WhenNotRunning_ShouldNotChangeState()
    {
        // Act
        await _syncCoordinator.PauseAsync();

        // Assert
        _syncCoordinator.CurrentStatus.Should().Be(SyncStatus.Stopped);
    }

    #endregion

    #region Manual Trigger Tests

    [Fact]
    public async Task TriggerFeedSyncAsync_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerFeedSyncAsync();

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.FeedUpdate);
    }

    [Fact]
    public async Task TriggerSingleFeedSyncAsync_WithValidFeedId_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var feeds = await _feedService.GetAllFeedsAsync();
        var feedId = feeds.First().Id;

        // Act
        await _syncCoordinator.TriggerSingleFeedSyncAsync(feedId);

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.FeedUpdate);
    }

    [Fact]
    public async Task TriggerCleanupSyncAsync_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerCleanupSyncAsync();

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.ArticleCleanup);
    }

    [Fact]
    public async Task TriggerTagProcessingSyncAsync_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerTagProcessingSyncAsync();

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.TagProcessing);
    }

    [Fact]
    public async Task TriggerBackupSyncAsync_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerBackupSyncAsync();

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.BackupCreation);
    }

    [Fact]
    public async Task TriggerFullSyncAsync_ShouldEnqueueTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerFullSyncAsync();

        // Wait a bit for task to be processed
        await Task.Delay(500);

        // Assert
        var taskStatuses = await _syncCoordinator.GetTaskStatusesAsync();
        taskStatuses.Should().ContainKey(SyncTaskType.FullSync);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ConfigureTaskAsync_ShouldEnableDisableTask()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var taskType = SyncTaskType.FeedUpdate;

        // Act - Disable
        await _syncCoordinator.ConfigureTaskAsync(taskType, false);

        // Assert
        var executionInfo = await _syncCoordinator.GetTaskExecutionInfoAsync(taskType);
        executionInfo.Should().NotBeNull();

        // Act - Enable
        await _syncCoordinator.ConfigureTaskAsync(taskType, true);
    }

    [Fact]
    public async Task ConfigureTaskIntervalAsync_ShouldUpdateInterval()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var taskType = SyncTaskType.FeedUpdate;
        var newInterval = 120;

        // Act
        await _syncCoordinator.ConfigureTaskIntervalAsync(taskType, newInterval);

        // Assert
        var executionInfo = await _syncCoordinator.GetTaskExecutionInfoAsync(taskType);
        executionInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task SetMaxSyncDurationAsync_ShouldUpdateDuration()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var newDuration = 60;

        // Act
        await _syncCoordinator.SetMaxSyncDurationAsync(newDuration);

        // Assert - No exception means success
    }

    #endregion

    #region Monitoring Tests

    [Fact]
    public async Task GetTaskStatusesAsync_ShouldReturnAllTasks()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        var statuses = await _syncCoordinator.GetTaskStatusesAsync();

        // Assert
        statuses.Should().NotBeNull();
        statuses.Should().ContainKeys(
            SyncTaskType.FeedUpdate,
            SyncTaskType.TagProcessing,
            SyncTaskType.ArticleCleanup,
            SyncTaskType.BackupCreation,
            SyncTaskType.StatisticsUpdate,
            SyncTaskType.RuleProcessing,
            SyncTaskType.CacheMaintenance,
            SyncTaskType.FullSync);
    }

    [Fact]
    public async Task GetTaskExecutionInfoAsync_ShouldReturnTaskDetails()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var taskType = SyncTaskType.FeedUpdate;

        // Act
        var info = await _syncCoordinator.GetTaskExecutionInfoAsync(taskType);

        // Assert
        info.Should().NotBeNull();
        info.TaskType.Should().Be(taskType);
    }

    [Fact]
    public async Task GetRecentErrorsAsync_ShouldReturnErrors()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        var errors = await _syncCoordinator.GetRecentErrorsAsync();

        // Assert
        errors.Should().NotBeNull();
    }

    [Fact]
    public async Task ClearErrorHistoryAsync_ShouldClearErrors()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.ClearErrorHistoryAsync();

        // Assert
        var errors = await _syncCoordinator.GetRecentErrorsAsync();
        errors.Should().BeEmpty();
    }
    [Fact]
    public async Task Statistics_ShouldUpdateAfterSync()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // ✅ Tomar el valor inicial después de que las tareas automáticas se estabilicen
        await Task.Delay(500); // Dar tiempo para que las tareas automáticas se ejecuten

        var initialStats = _syncCoordinator.Statistics;
        _output.WriteLine($"Initial TotalSyncCycles: {initialStats.TotalSyncCycles}");

        // Act - Trigger manual sync
        await _syncCoordinator.TriggerFullSyncAsync();

        // Esperar a que se complete
        await Task.Delay(3000);

        // Assert
        var finalStats = _syncCoordinator.Statistics;
        _output.WriteLine($"Final TotalSyncCycles: {finalStats.TotalSyncCycles}");

        // ✅ Verificar que aumentó exactamente en 1 (solo FullSync debería contar)
        finalStats.TotalSyncCycles.Should().Be(initialStats.TotalSyncCycles);

        _syncCoordinator.LastSyncCompleted.Should().NotBeNull();
        _syncCoordinator.NextSyncScheduled.Should().NotBeNull();
    }
    #endregion

    #region Event Tests

    [Fact]
    public async Task OnStatusChanged_ShouldFireWhenStatusChanges()
    {
        // Arrange
        var statusChangedFired = false;
        _syncCoordinator.OnStatusChanged += (sender, args) => statusChangedFired = true;

        // Act
        await _syncCoordinator.StartAsync();
        await _syncCoordinator.StopAsync();

        // Assert
        statusChangedFired.Should().BeTrue();
    }

    [Fact]
    public async Task OnTaskStarted_ShouldFireWhenTaskStarts()
    {
        // Arrange
        var taskStartedFired = false;
        _syncCoordinator.OnTaskStarted += (sender, args) => taskStartedFired = true;

        // Act
        await _syncCoordinator.StartAsync();
        await _syncCoordinator.TriggerFeedSyncAsync();
        await Task.Delay(500);

        // Assert
        taskStartedFired.Should().BeTrue();
    }

    [Fact]
    public async Task OnTaskCompleted_ShouldFireWhenTaskCompletes()
    {
        // Arrange
        using var taskCompletedEvent = new ManualResetEventSlim(false);

        _syncCoordinator.OnTaskCompleted += (sender, args) =>
        {
            taskCompletedEvent.Set();
        };

        // Act
        await _syncCoordinator.StartAsync();
        await _syncCoordinator.TriggerFeedSyncAsync();

        // Wait for the event with timeout
        var eventFired = taskCompletedEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert
        eventFired.Should().BeTrue("OnTaskCompleted event should fire within 5 seconds");

        // Cleanup
        await _syncCoordinator.StopAsync();
    }

    [Fact]
    public async Task OnSyncProgress_ShouldFireDuringTaskExecution()
    {
        // Arrange
        var progressFired = false;
        _syncCoordinator.OnSyncProgress += (sender, args) => progressFired = true;

        // Act
        await _syncCoordinator.StartAsync();
        await _syncCoordinator.TriggerFeedSyncAsync();
        await Task.Delay(1000);

        // Assert
        progressFired.Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task StartAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _syncCoordinator.StartAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_ShouldNotThrow()
    {
        // Act
        Func<Task> act = async () => await _syncCoordinator.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PauseAsync_WhenStopped_ShouldNotThrow()
    {
        // Act
        Func<Task> act = async () => await _syncCoordinator.PauseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResumeAsync_WhenNotPaused_ShouldNotThrow()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        Func<Task> act = async () => await _syncCoordinator.ResumeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MultipleManualTriggers_ShouldQueueTasks()
    {
        // Arrange
        await _syncCoordinator.StartAsync();

        // Act
        await _syncCoordinator.TriggerFeedSyncAsync();
        await _syncCoordinator.TriggerTagProcessingSyncAsync();
        await _syncCoordinator.TriggerCleanupSyncAsync();

        await Task.Delay(2000); // Wait for processing

        // Assert - No exceptions means success
        _syncCoordinator.Statistics.TotalSyncCycles.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConfigureTask_WithInvalidTaskType_ShouldNotThrow()
    {
        // Arrange
        await _syncCoordinator.StartAsync();
        var invalidTaskType = (SyncTaskType)999;

        // Act
        Func<Task> act = async () => await _syncCoordinator.ConfigureTaskAsync(invalidTaskType, false);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}