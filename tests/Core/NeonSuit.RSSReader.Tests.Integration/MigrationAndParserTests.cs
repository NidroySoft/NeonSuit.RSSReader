using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Setup;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Integration;

public sealed class MigrationAndParserTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnversionedDatabase_IsAdoptedAndDataAndBackupArePreserved(bool oldSchema)
    {
        await using var host = new BackendHost();
        var id = await host.SeedArticle();
        using (var scope = host.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
            if (oldSchema)
                await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
            await db.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
        }
        await host.Provider.UseNeonSuitDatabaseAsync();
        using (var scope = host.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
            Assert.Equal("Science news", (await db.Articles.SingleAsync(a => a.Id == id)).Title);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Empty(await db.SyncTaskConfigs.ToListAsync());
        }
        var backup = Assert.Single(Directory.GetFiles(host.Directory, "*.pre-net10-*.db"));
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backup};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Articles";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task IncompatibleUnversionedDatabase_IsRejectedBeforeBaselineWrites()
    {
        await using var host = new BackendHost();
        using var scope = host.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RSSReaderDbContext>();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Categories RENAME COLUMN Name TO UnexpectedName");
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Provider.UseNeonSuitDatabaseAsync());
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(Directory.GetFiles(host.Directory, "*.pre-net10-*.db"));
    }

    [Fact]
    public async Task SoundRule_ReachesApplicationEventAcrossScopes_AndMissingHandlerFails()
    {
        await using var host = new BackendHost();
        var articleId = await host.SeedArticle();
        using (var scope = host.Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRuleService>().CreateRuleAsync(new()
            {
                Name = "Sound", Target = RuleFieldTarget.Title, Operator = RuleOperator.Contains,
                Value = "Science", ActionType = RuleActionType.PlaySound, SoundPath = "notification.wav"
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>()
                .ExecuteAsync(SyncTaskType.RuleProcessing, null, default));
        }
        var received = new List<int>();
        host.Provider.GetRequiredService<IBackendEvents>().RuleActionRequested += (_, action) =>
        {
            Assert.Equal("notification.wav", action.Value);
            received.Add(action.ArticleId);
        };
        using (var scope = host.Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SyncTaskExecutor>().ExecuteAsync(SyncTaskType.RuleProcessing, null, default);
        }
        Assert.Equal(new[] { articleId }, received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionParser_ParsesRssAndAtomOverHttp(bool atom)
    {
        var xml = atom
            ? """
              <?xml version="1.0" encoding="utf-8"?>
              <feed xmlns="http://www.w3.org/2005/Atom"><title>Fixture feed</title><id>urn:fixture</id><updated>2026-10-05T12:00:00Z</updated>
              <entry><title>Fixture article</title><id>urn:article:1</id><link href="https://example.test/1"/><updated>2026-10-05T12:00:00Z</updated><content type="html">&lt;p&gt;Article body&lt;/p&gt;</content></entry></feed>
              """
            : """
              <?xml version="1.0" encoding="utf-8"?>
              <rss version="2.0"><channel><title>Fixture feed</title><link>https://example.test</link><description>Fixture</description>
              <item><title>Fixture article</title><guid>urn:article:1</guid><link>https://example.test/1</link><pubDate>Mon, 05 Oct 2026 12:00:00 GMT</pubDate><description><![CDATA[<p>Article body</p>]]></description></item></channel></rss>
              """;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cts.Token))) { }
            var body = Encoding.UTF8.GetBytes(xml);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/xml; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cts.Token);
            await stream.WriteAsync(body, cts.Token);
        }, cts.Token);
        using var parser = new NeonSuit.RSSReader.Services.RssFeedParser.RssFeedParser(new LoggerConfiguration().CreateLogger());
        var parsed = await parser.ParseFeedAsync($"http://127.0.0.1:{port}/feed", cts.Token);
        await server;
        Assert.Equal("Fixture feed", parsed.feed.Title);
        var article = Assert.Single(parsed.articles);
        Assert.Equal("Fixture article", article.Title);
        Assert.Equal("https://example.test/1", article.Link);
        Assert.Contains("Article body", article.Content ?? article.Summary);
    }
}
