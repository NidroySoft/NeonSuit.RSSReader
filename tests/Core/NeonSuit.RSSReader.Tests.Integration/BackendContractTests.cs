using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeonSuit.RSSReader.Core.DTOs.Categories;
using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.RssFeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Setup;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Integration;

public sealed class BackendContractTests
{
    [Fact]
    public async Task Startup_ValidatesScopesMappingsAndMigratesAllTables()
    {
        await using var host = new BackendHost();
        host.Provider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
        using var scope = host.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.SyncTaskConfigs.ToListAsync());
        host.Provider.UseNeonSuitDatabase(); // startup is idempotent
    }

    [Fact]
    public async Task Category_CreateReadUpdateDelete_InSameScope()
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var category = await service.CreateCategoryAsync(new() { Name = "News" });
        Assert.Equal("News", (await service.GetCategoryByIdAsync(category.Id))!.Name);
        await service.UpdateCategoryAsync(category.Id, new() { Name = "Science" });
        Assert.Equal("Science", (await service.GetCategoryByIdAsync(category.Id))!.Name);
        Assert.True(await service.DeleteCategoryAsync(category.Id));
        Assert.Null(await service.GetCategoryByIdAsync(category.Id));
    }

    [Fact]
    public async Task Category_HierarchyHasPathsAndDepthAcrossScopes()
    {
        await using var host = new BackendHost();
        int childId;
        using (var scope = host.Provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            var parent = await service.CreateCategoryAsync(new() { Name = "News" });
            childId = (await service.CreateCategoryAsync(new() { Name = "Science", ParentCategoryId = parent.Id })).Id;
        }
        using (var scope = host.Provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            var child = (await service.GetAllCategoriesAsync()).Single(c => c.Id == childId);
            Assert.Equal(1, child.Depth);
            Assert.Equal("News / Science", child.FullPath);
            Assert.Equal(child.FullPath, (await service.GetCategoryByIdAsync(childId))!.FullPath);
            Assert.Equal(childId, Assert.Single(Assert.Single(await service.GetCategoryTreeAsync()).Children).Id);
        }
    }

    [Fact]
    public async Task Category_RejectsDescendantAsParent()
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var parent = await service.CreateCategoryAsync(new() { Name = "Parent" });
        var child = await service.CreateCategoryAsync(new() { Name = "Child", ParentCategoryId = parent.Id });
        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateCategoryAsync(parent.Id, new() { ParentCategoryId = child.Id }));
        Assert.Null((await service.GetCategoryByIdAsync(parent.Id))!.ParentCategoryId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Category_RejectsBlankNames(string name)
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<ICategoryService>().CreateCategoryAsync(new() { Name = name }));
    }

    [Fact]
    public async Task Articles_StateChangesPersistAcrossScopes_AndPagingWorks()
    {
        await using var host = new BackendHost();
        var id = await host.SeedArticle();
        using (var scope = host.Provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
            Assert.Equal(1, await service.GetUnreadCountAsync());
            Assert.True(await service.MarkAsReadAsync(id, true));
            Assert.True(await service.ToggleFavoriteAsync(id));
            Assert.True(await service.ToggleStarredAsync(id));
        }
        using (var scope = host.Provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IArticleService>();
            Assert.Equal(0, await service.GetUnreadCountAsync());
            Assert.Single(await service.GetFavoriteArticlesAsync());
            Assert.Single(await service.GetStarredArticlesAsync());
            Assert.Single(await service.GetPagedArticlesAsync(1, 1));
            Assert.Empty(await service.GetPagedArticlesAsync(2, 1));
            Assert.Single(await service.SearchArticlesAsync("Science"));
        }
    }

    [Fact]
    public async Task Tags_CreateApplyDeduplicateAndRemove()
    {
        await using var host = new BackendHost();
        var id = await host.SeedArticle();
        using var scope = host.Provider.CreateScope();
        var tags = scope.ServiceProvider.GetRequiredService<ITagService>();
        var links = scope.ServiceProvider.GetRequiredService<IArticleTagService>();
        var tag = await tags.CreateTagAsync(new() { Name = "Research", Color = "#123456" });
        Assert.True(await links.TagArticleAsync(id, tag.Id));
        await links.TagArticleAsync(id, tag.Id);
        Assert.Single(await links.GetTagsForArticleAsync(id));
        Assert.True(await links.UntagArticleAsync(id, tag.Id));
        Assert.Empty(await links.GetTagsForArticleAsync(id));
    }

    [Theory]
    [InlineData(RuleActionType.MarkAsRead)]
    [InlineData(RuleActionType.MarkAsStarred)]
    [InlineData(RuleActionType.MarkAsFavorite)]
    [InlineData(RuleActionType.HighlightArticle)]
    [InlineData(RuleActionType.ApplyTags)]
    public async Task Rules_ExecuteMatchingActions_AndMarkProcessed(RuleActionType action)
    {
        await using var host = new BackendHost();
        var id = await host.SeedArticle();
        using (var scope = host.Provider.CreateScope())
        {
            var tag = await scope.ServiceProvider.GetRequiredService<ITagService>().CreateTagAsync(new() { Name = "Auto" });
            var rule = await scope.ServiceProvider.GetRequiredService<IRuleService>().CreateRuleAsync(new()
            {
                Name = "Science rule", Target = RuleFieldTarget.Title, Operator = RuleOperator.Contains,
                Value = "Science", ActionType = action, HighlightColor = "#123456", TagIds = [tag.Id]
            });
            var result = await scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>().ExecuteAsync(SyncTaskType.RuleProcessing, null, default);
            Assert.Equal(1, result["articles_processed"]);
            Assert.Equal(1, result["rules_applied"]);
        }
        using (var scope = host.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
            var article = await db.Articles.SingleAsync(a => a.Id == id);
            Assert.True(article.ProcessedByRules);
            switch(action)
            {
                case RuleActionType.MarkAsRead: Assert.Equal(ArticleStatus.Read, article.Status); break;
                case RuleActionType.MarkAsStarred: Assert.True(article.IsStarred); break;
                case RuleActionType.MarkAsFavorite: Assert.True(article.IsFavorite); break;
                case RuleActionType.HighlightArticle: Assert.Equal("#123456", article.HighlightColor); break;
                case RuleActionType.ApplyTags: Assert.Single(await db.ArticleTags.ToListAsync()); break;
            }
        }
    }

    [Fact]
    public async Task Notifications_EmitEventAndSuppressDuplicates()
    {
        await using var host = new BackendHost();
        var id = await host.SeedArticle();
        using var scope = host.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        int events = 0, globalEvents = 0;
        host.Provider.GetRequiredService<IBackendEvents>().NotificationCreated += (_, _) => globalEvents++;
        service.OnNotificationCreated += (_, _) => events++;
        Assert.NotNull(await service.SendNotificationAsync(new() { ArticleId = id, Title = "New article" }));
        Assert.Null(await service.SendNotificationAsync(new() { ArticleId = id }));
        Assert.Equal(1, events);
        Assert.Equal(1, globalEvents);
        Assert.Single(await service.GetArticleNotificationHistoryAsync(id));
    }

    [Fact]
    public async Task Preferences_PersistAndExportImport()
    {
        await using var host = new BackendHost();
        var path = Path.Combine(host.Directory, "preferences.json");
        using (var scope = host.Provider.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settings.SetIntAsync("probe.number", 42);
            await settings.SetBoolAsync("probe.enabled", true);
            await settings.ExportToFileAsync(path);
        }
        using (var scope = host.Provider.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            Assert.Equal(42, await settings.GetIntAsync("probe.number"));
            Assert.True(await settings.GetBoolAsync("probe.enabled"));
            await settings.SetIntAsync("probe.number", 9);
            await settings.ImportFromFileAsync(path);
            Assert.Equal(42, await settings.GetIntAsync("probe.number"));
        }
    }

    [Fact]
    public async Task FeedRefresh_ReallyDownloadsAndDeduplicatesFixtureArticles()
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFeedService>();
        var feed = await service.AddFeedAsync(new() { Url = "https://fixture.test/rss" });
        Assert.True(await service.RefreshFeedAsync(feed.Id));
        Assert.True(await service.RefreshFeedAsync(feed.Id));
        Assert.Single(await scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>().Articles.ToListAsync());
        Assert.True(host.Parser.ParseCount >= 2);
    }

    [Fact]
    public async Task Opml_ExportValidateAndImportWithoutDuplicates()
    {
        await using var host = new BackendHost();
        await host.SeedArticle();
        using var scope = host.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOpmlService>();
        var xml = await service.ExportAsync();
        Assert.Contains("https://fixture.test/rss", xml);
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.True((await service.ValidateAsync(input)).IsValid);
        input.Position = 0;
        await service.ImportAsync(input);
        Assert.Single(await scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>().Feeds.ToListAsync());
    }

    [Fact]
    public async Task Backup_IsRealReadableSQLite_WithCommittedData()
    {
        await using var host = new BackendHost();
        await host.SeedArticle();
        using var scope = host.Provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>().ExecuteAsync(SyncTaskType.BackupCreation, null, default);
        var path = (string)result["backup_path"];
        Assert.True(File.Exists(path));
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Articles";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Statistics_ContainRealDatabaseCounts()
    {
        await using var host = new BackendHost();
        await host.SeedArticle();
        using var scope = host.Provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>().ExecuteAsync(SyncTaskType.StatisticsUpdate, null, default);
        Assert.Equal(1, result["feeds_count"]);
        Assert.Equal(1, result["articles_count"]);
        Assert.Equal(0, result["tags_count"]);
    }

    [Fact]
    public async Task Coordinator_StartTriggerBackupStopAndRestart_PersistsHistory()
    {
        await using var host = new BackendHost();
        await host.SeedArticle();
        var sync = host.Provider.GetRequiredService<ISyncCoordinatorService>();
        var completed = new TaskCompletionSource<NeonSuit.RSSReader.Core.DTOs.Sync.SyncTaskExecutionInfoDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.OnTaskCompleted += (_, value) => { if(value.TaskType == SyncTaskType.BackupCreation.ToString()) completed.TrySetResult(value); };
        await sync.StartAsync();
        await sync.TriggerBackupSyncAsync();
        var execution = await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(execution.LastRunSuccessful);
        await sync.StopAsync();
        using (var scope = host.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
            Assert.Contains(await db.SyncTaskExecutions.ToListAsync(), row => row.TaskType == "BackupCreation" && row.Success);
        }
        await sync.StartAsync();
        await sync.StopAsync();
    }

    [Fact]
    public async Task FeedUpdate_FailedHttpDoesNotReportSuccessfulSync()
    {
        await using var host = new BackendHost();
        await host.SeedArticle();
        host.Parser.Failure = new HttpRequestException("Feed unavailable");
        using var scope = host.Provider.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>()
            .ExecuteAsync(SyncTaskType.FeedUpdate, null, default));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>().Articles.CountAsync());
    }

    [Fact]
    public async Task Executor_CancellationDoesNotProduceSuccess()
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>().ExecuteAsync(SyncTaskType.BackupCreation, null, cts.Token));
        Assert.False(System.IO.Directory.Exists(Path.Combine(host.Directory, "backups")));
    }
}

internal sealed class BackendHost : IAsyncDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "neonsuit-tests-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Provider { get; }
    public FixtureParser Parser { get; } = new();
    public BackendHost()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var services = new ServiceCollection();
        services.AddSingleton<ILogger>(new LoggerConfiguration().CreateLogger());
        services.AddNeonSuitBackend(Path.Combine(Directory, "reader.db"));
        services.RemoveAll<IRssFeedParser>();
        services.AddSingleton<IRssFeedParser>(Parser);
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Provider.UseNeonSuitDatabase();
    }
    public async Task<int> SeedArticle()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
        var feed = new Feed { Title = "Fixture", Url = "https://fixture.test/rss" };
        db.Feeds.Add(feed); await db.SaveChangesAsync();
        var article = new Article { FeedId = feed.Id, Guid = "fixture-1", Title = "Science news", Link = "https://fixture.test/1", Content = "Science article", PublishedDate = DateTime.UtcNow };
        db.Articles.Add(article); await db.SaveChangesAsync(); return article.Id;
    }
    public async ValueTask DisposeAsync()
    {
        await Provider.GetRequiredService<ISyncCoordinatorService>().StopAsync();
        await Provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        System.IO.Directory.Delete(Directory, true);
    }
}

internal sealed class FixtureParser : IRssFeedParser
{
    public int ParseCount { get; private set; }
    public Exception? Failure { get; set; }
    public Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ParseCount++;
        if (Failure != null) throw Failure;
        return Task.FromResult((new Feed { Title = "Fixture", Url = url }, new List<Article>
        {
            new() { Guid = "fixture-1", Title = "Science news", Link = "https://fixture.test/1", Content = "Science article", PublishedDate = DateTime.UtcNow }
        }));
    }
    public async Task<List<Article>> ParseArticlesAsync(string url, int feedId, CancellationToken cancellationToken = default)
    {
        var parsed = await ParseFeedAsync(url, cancellationToken);
        foreach(var article in parsed.articles) article.FeedId = feedId;
        return parsed.articles;
    }
    public Task<string> GenerateReaderViewHtmlAsync(Article article, CancellationToken cancellationToken = default)
        => Task.FromResult(article.Content ?? "");
}
