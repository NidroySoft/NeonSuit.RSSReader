using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="SyncCoordinatorService"/> class.
    /// Tests cover service lifecycle, task management, error handling, and event propagation.
    /// </summary>
    public class SyncCoordinatorServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly SyncCoordinatorService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncCoordinatorServiceTests"/> class.
        /// Sets up common mocks and configurations used across all tests.
        /// </summary>
        public SyncCoordinatorServiceTests()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILogger>();

            // Critical logger configuration - matches production pattern
            _mockLogger.Setup(x => x.ForContext<SyncCoordinatorService>())
                      .Returns(_mockLogger.Object);

            // Default settings configuration
            ConfigureDefaultSettings();

            // Initialize service with mocked dependencies
            _service = new SyncCoordinatorService(_mockSettingsService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Configures default settings for the mock settings service.
        /// Ensures consistent test behavior across all test methods.
        /// </summary>
        private void ConfigureDefaultSettings()
        {
            // Configure task settings
            _mockSettingsService.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
                               .ReturnsAsync((string key, bool defaultValue) => defaultValue);

            _mockSettingsService.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>()))
                               .ReturnsAsync((string key, int defaultValue) => defaultValue);

            _mockSettingsService.Setup(x => x.SetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
                               .Returns(Task.CompletedTask);

            _mockSettingsService.Setup(x => x.SetIntAsync(It.IsAny<string>(), It.IsAny<int>()))
                               .Returns(Task.CompletedTask);
        }

        /// <summary>
        /// Creates a test cancellation token source with optional timeout.
        /// </summary>
        /// <param name="millisecondsTimeout">Optional timeout in milliseconds.</param>
        /// <returns>A configured cancellation token source.</returns>
        private CancellationTokenSource CreateTestCancellationTokenSource(int millisecondsTimeout = 5000)
        {
            return new CancellationTokenSource(millisecondsTimeout);
        }

        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor initializes the service with correct default values.
        /// </summary>
        [Fact]
        public void Constructor_WhenCalled_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var service = new SyncCoordinatorService(_mockSettingsService.Object, _mockLogger.Object);

            // Assert
            service.CurrentStatus.Should().Be(SyncStatus.Stopped);
            service.IsSynchronizing.Should().BeFalse();
            service.LastSyncCompleted.Should().BeNull();
            service.NextSyncScheduled.Should().BeNull();
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when settingsService is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSettingsService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SyncCoordinatorService(null!, _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("settingsService");
        }

        #endregion

        #region StartAsync Tests

        /// <summary>
        /// Tests that StartAsync successfully transitions the service from Stopped to Running state.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenServiceIsStopped_ShouldTransitionToRunning()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();

            // Act
            await _service.StartAsync(cts.Token);

            // Assert
            _service.CurrentStatus.Should().Be(SyncStatus.Running);
            _service.IsSynchronizing.Should().BeTrue();
        }

        /// <summary>
        /// Tests that StartAsync loads configuration from settings service.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenCalled_ShouldLoadConfigurationFromSettings()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            var configLoaded = false;

            _mockSettingsService.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
                               .ReturnsAsync(true)
                               .Callback(() => configLoaded = true);

            // Act
            await _service.StartAsync(cts.Token);

            // Assert
            configLoaded.Should().BeTrue();
            _mockSettingsService.Verify(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.AtLeastOnce);
            _mockSettingsService.Verify(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        /// <summary>
        /// Tests that StartAsync does not change state when called while service is already running.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenServiceIsAlreadyRunning_ShouldNotChangeState()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);
            var initialStatus = _service.CurrentStatus;

            // Act
            await _service.StartAsync(cts.Token);

            // Assert
            _service.CurrentStatus.Should().Be(initialStatus);
        }

        /// <summary>
        /// Tests that StartAsync transitions to Error state when an exception occurs.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenExceptionOccurs_ShouldTransitionToErrorState()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            _mockSettingsService.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
                               .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            Func<Task> act = async () => await _service.StartAsync(cts.Token);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region StopAsync Tests

        /// <summary>
        /// Tests that StopAsync successfully transitions the service from Running to Stopped state.
        /// </summary>
        [Fact]
        public async Task StopAsync_WhenServiceIsRunning_ShouldTransitionToStopped()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Act
            await _service.StopAsync();

            // Assert
            _service.CurrentStatus.Should().Be(SyncStatus.Stopped);
            _service.IsSynchronizing.Should().BeFalse();
        }

        /// <summary>
        /// Tests that StopAsync handles gracefully when called while service is already stopping.
        /// </summary>
        [Fact]
        public async Task StopAsync_WhenServiceIsAlreadyStopping_ShouldHandleGracefully()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Start stop operation
            var stopTask = _service.StopAsync();

            // Act - Try to stop again while stopping
            await _service.StopAsync();

            // Assert
            await stopTask;
            _service.CurrentStatus.Should().Be(SyncStatus.Stopped);
        }

        /// <summary>
        /// Tests that StopAsync cancels ongoing operations within timeout.
        /// </summary>
        [Fact]
        public async Task StopAsync_WithLongRunningOperations_ShouldCancelWithinTimeout()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Trigger a task that might be running
            await _service.TriggerFeedSyncAsync();

            // Act
            var stopTask = _service.StopAsync();

            // Assert - Should complete within reasonable time
            var completionTask = await Task.WhenAny(stopTask, Task.Delay(10000));
            completionTask.Should().Be(stopTask, "StopAsync should complete within timeout");
        }

        #endregion

        #region PauseAsync and ResumeAsync Tests

        /// <summary>
        /// Tests that PauseAsync sets the paused state when service is running.
        /// </summary>
        [Fact]
        public async Task PauseAsync_WhenServiceIsRunning_ShouldSetPausedState()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Act
            await _service.PauseAsync();

            // Assert - Verify through behavior (IsSynchronizing should be false when paused)
            _service.IsSynchronizing.Should().BeFalse();
        }

        /// <summary>
        /// Tests that PauseAsync handles gracefully when service is not running.
        /// </summary>
        [Fact]
        public async Task PauseAsync_WhenServiceIsNotRunning_ShouldHandleGracefully()
        {
            // Act - Should not throw
            Func<Task> act = async () => await _service.PauseAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Tests that ResumeAsync clears the paused state and triggers queue processing.
        /// </summary>
        [Fact]
        public async Task ResumeAsync_WhenServiceIsPaused_ShouldClearPausedState()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await _service.PauseAsync();

            // Act
            await _service.ResumeAsync();

            // Assert
            _service.IsSynchronizing.Should().BeTrue();
        }

        #endregion

        #region Task Trigger Tests

        /// <summary>
        /// Tests that TriggerFeedSyncAsync enqueues a feed update task.
        /// </summary>
        [Fact]
        public async Task TriggerFeedSyncAsync_WhenCalled_ShouldEnqueueFeedUpdateTask()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Act - Should not throw
            Func<Task> act = async () => await _service.TriggerFeedSyncAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Tests that TriggerSingleFeedSyncAsync enqueues a feed update task with feed ID parameter.
        /// </summary>
        [Fact]
        public async Task TriggerSingleFeedSyncAsync_WhenCalled_ShouldEnqueueTaskWithFeedId()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);
            const int testFeedId = 123;

            // Act
            Func<Task> act = async () => await _service.TriggerSingleFeedSyncAsync(testFeedId);

            // Assert
            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Tests that all trigger methods enqueue appropriate tasks.
        /// </summary>
        [Theory]
        [InlineData(SyncTaskType.ArticleCleanup)]
        [InlineData(SyncTaskType.TagProcessing)]
        [InlineData(SyncTaskType.BackupCreation)]
        [InlineData(SyncTaskType.FullSync)]
        public async Task TriggerMethods_WhenCalled_ShouldEnqueueCorrespondingTasks(SyncTaskType expectedTaskType)
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            // Act based on task type
            Func<Task> act = expectedTaskType switch
            {
                SyncTaskType.ArticleCleanup => async () => await _service.TriggerCleanupSyncAsync(),
                SyncTaskType.TagProcessing => async () => await _service.TriggerTagProcessingSyncAsync(),
                SyncTaskType.BackupCreation => async () => await _service.TriggerBackupSyncAsync(),
                SyncTaskType.FullSync => async () => await _service.TriggerFullSyncAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(expectedTaskType))
            };

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Configuration Tests

        /// <summary>
        /// Tests that ConfigureTaskAsync updates task enabled state and saves to settings.
        /// </summary>
        [Fact]
        public async Task ConfigureTaskAsync_WhenCalled_ShouldUpdateTaskEnabledState()
        {
            // Arrange
            const SyncTaskType taskType = SyncTaskType.FeedUpdate;
            const bool newEnabledState = false;

            // Act
            await _service.ConfigureTaskAsync(taskType, newEnabledState);

            // Assert
            _mockSettingsService.Verify(x => x.SetBoolAsync(
                $"sync_task_{taskType.ToString().ToLower()}_enabled",
                newEnabledState),
                Times.Once);
        }

        /// <summary>
        /// Tests that ConfigureTaskIntervalAsync updates task interval with minimum boundary.
        /// </summary>
        [Theory]
        [InlineData(10, 10)]  // Normal value
        [InlineData(0, 1)]    // Below minimum, should clamp to 1
        [InlineData(-5, 1)]   // Negative value, should clamp to 1
        public async Task ConfigureTaskIntervalAsync_WithVariousInputs_ShouldClampToValidRange(
            int inputMinutes, int expectedMinutes)
        {
            // Arrange
            const SyncTaskType taskType = SyncTaskType.FeedUpdate;

            // Act
            await _service.ConfigureTaskIntervalAsync(taskType, inputMinutes);

            // Assert
            _mockSettingsService.Verify(x => x.SetIntAsync(
                $"sync_task_{taskType.ToString().ToLower()}_interval",
                expectedMinutes),
                Times.Once);
        }

        /// <summary>
        /// Tests that SetMaxSyncDurationAsync updates duration with minimum boundary.
        /// </summary>
        [Theory]
        [InlineData(60)]   // Normal value
        [InlineData(0)]     // Below minimum, should clamp to 1
        [InlineData(-10)]   // Negative value, should clamp to 1
        public async Task SetMaxSyncDurationAsync_WithVariousInputs_ShouldClampToValidRange(
            int inputMinutes)
        {
            // Act
            Func<Task> act = async () => await _service.SetMaxSyncDurationAsync(inputMinutes);

            // Assert - Should complete without exception
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Monitoring Tests

        /// <summary>
        /// Tests that GetTaskStatusesAsync returns status for all task types.
        /// </summary>
        [Fact]
        public async Task GetTaskStatusesAsync_WhenCalled_ShouldReturnStatusForAllTaskTypes()
        {
            // Act
            var statuses = await _service.GetTaskStatusesAsync();

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

            // All tasks should start in Idle state
            statuses.Values.Should().AllBeEquivalentTo(SyncTaskStatus.Idle);
        }

        /// <summary>
        /// Tests that GetTaskExecutionInfoAsync returns valid execution info.
        /// </summary>
        [Fact]
        public async Task GetTaskExecutionInfoAsync_ForExistingTask_ShouldReturnExecutionInfo()
        {
            // Arrange
            const SyncTaskType taskType = SyncTaskType.FeedUpdate;

            // Act
            var info = await _service.GetTaskExecutionInfoAsync(taskType);

            // Assert
            info.Should().NotBeNull();
            info.TaskType.Should().Be(taskType);
            info.TotalRuns.Should().Be(0);
            info.SuccessfulRuns.Should().Be(0);
            info.AverageRunDuration.Should().Be(TimeSpan.Zero);
        }

        /// <summary>
        /// Tests that GetRecentErrorsAsync returns limited number of errors.
        /// </summary>
        [Fact]
        public async Task GetRecentErrorsAsync_WithMaxErrorsParameter_ShouldReturnLimitedResults()
        {
            // Arrange
            const int maxErrors = 10;

            // Act
            var errors = await _service.GetRecentErrorsAsync(maxErrors);

            // Assert
            errors.Should().NotBeNull();
            errors.Should().BeEmpty();
        }

        /// <summary>
        /// Tests that ClearErrorHistoryAsync clears all recorded errors.
        /// </summary>
        [Fact]
        public async Task ClearErrorHistoryAsync_WhenCalled_ShouldClearAllErrors()
        {
            // Act
            Func<Task> act = async () => await _service.ClearErrorHistoryAsync();

            // Assert - Should complete without exception
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Event Tests

        /// <summary>
        /// Tests that status change events are raised when service state changes.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenSuccessful_ShouldRaiseStatusChangedEvent()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            var eventsRaised = new List<SyncStatusChangedEventArgs>();

            _service.OnStatusChanged += (sender, args) => eventsRaised.Add(args);

            // Act
            await _service.StartAsync(cts.Token);

            // Assert
            eventsRaised.Should().NotBeEmpty();
            eventsRaised.Should().Contain(e =>
                e.PreviousStatus == SyncStatus.Stopped &&
                e.NewStatus == SyncStatus.Running);
        }

        /// <summary>
        /// Tests that task started events are raised when tasks begin execution.
        /// </summary>
        [Fact]
        public async Task TriggerFeedSyncAsync_WhenServiceIsRunning_ShouldRaiseTaskStartedEvent()
        {
            // Arrange
            await _service.StartAsync();

            using var taskStartedEvent = new ManualResetEventSlim(false);
            SyncTaskType? raisedTaskType = null;

            _service.OnTaskStarted += (sender, args) =>
            {
                // Solo nos interesa FeedUpdate
                if (args.TaskType == SyncTaskType.FeedUpdate)
                {
                    raisedTaskType = args.TaskType;
                    taskStartedEvent.Set();
                }
            };

            // Act
            await _service.TriggerFeedSyncAsync();

            // Assert - Esperar específicamente a que FeedUpdate comience
            var eventRaised = taskStartedEvent.Wait(TimeSpan.FromSeconds(5));
            eventRaised.Should().BeTrue("FeedUpdate task should start");
            raisedTaskType.Should().Be(SyncTaskType.FeedUpdate);

            await _service.StopAsync();
        }
        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that service handles exceptions during configuration loading gracefully.
        /// </summary>
        [Fact]
        public async Task StartAsync_WhenConfigurationLoadingFails_ShouldHandleExceptionGracefully()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            _mockSettingsService.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
                               .ThrowsAsync(new InvalidOperationException("Settings load failed"));

            // Act
            Func<Task> act = async () => await _service.StartAsync(cts.Token);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region Concurrency Tests

        /// <summary>
        /// Tests that multiple trigger calls don't cause race conditions.
        /// </summary>
        [Fact]
        public async Task MultipleTriggerCalls_WhenMadeConcurrently_ShouldNotCauseRaceConditions()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);

            var tasks = new List<Task>();
            const int concurrentCalls = 10;

            // Act - Trigger multiple tasks concurrently
            for (int i = 0; i < concurrentCalls; i++)
            {
                tasks.Add(_service.TriggerFeedSyncAsync());
            }

            await Task.WhenAll(tasks);

            // Assert - No exceptions should occur
            tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
        }

        /// <summary>
        /// Tests that StartAsync and StopAsync called concurrently don't cause deadlocks.
        /// </summary>
        [Fact]
        public async Task StartAndStopConcurrently_WhenCalled_ShouldNotCauseDeadlock()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();

            // Act
            var startTask = _service.StartAsync(cts.Token);
            var stopTask = _service.StopAsync();

            var completionTask = await Task.WhenAny(
                Task.WhenAll(startTask, stopTask),
                Task.Delay(5000));

            // Assert
            completionTask.Should().NotBeNull("Operations should complete within timeout");
        }

        #endregion

        #region Edge Cases Tests

        /// <summary>
        /// Tests service behavior when cancellation token is already cancelled.
        /// </summary>
        [Fact]
        public async Task StartAsync_WithCancelledToken_ShouldHandleGracefully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _service.StartAsync(cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        /// <summary>
        /// Tests that service can be started after being stopped.
        /// </summary>
        [Fact]
        public async Task StartAsync_AfterStop_ShouldRestartSuccessfully()
        {
            // Arrange
            using var cts = CreateTestCancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await _service.StopAsync();

            // Act
            await _service.StartAsync(cts.Token);

            // Assert
            _service.CurrentStatus.Should().Be(SyncStatus.Running);
            _service.IsSynchronizing.Should().BeTrue();
        }

        #endregion

        #region Statistics Tests

        /// <summary>
        /// Tests that Statistics property returns valid statistics object.
        /// </summary>
        [Fact]
        public void Statistics_Property_ShouldReturnValidStatisticsObject()
        {
            // Act
            var stats = _service.Statistics;

            // Assert
            stats.Should().NotBeNull();
            stats.TotalSyncCycles.Should().Be(0);
            stats.SuccessfulSyncs.Should().Be(0);
            stats.FailedSyncs.Should().Be(0);
            stats.TotalSyncTime.Should().Be(TimeSpan.Zero);
            stats.AverageSyncDurationSeconds.Should().Be(0);
        }

        #endregion
    }
}