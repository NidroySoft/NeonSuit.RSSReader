using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Database;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Integration.Fixtures
{
    public class DatabaseFixture : IAsyncLifetime
    {
        // ❌ ELIMINAR - No compartir el mismo path
        // private readonly string _dbPath;

        public ILogger Logger { get; } = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger()
            .ForContext<DatabaseFixture>();

        // ✅ NUEVO - CADA LLAMADA genera su PROPIO archivo único
        public RssReaderDbContext CreateNewDbContext()
        {
            var dbPath = $"testdb_{Guid.NewGuid():N}.db"; // ✅ NUEVO GUID CADA VEZ
            Logger.Debug("Creating new database: {Path}", dbPath);

            var connection = new SqliteConnection($"DataSource={dbPath}");
            connection.Open();

            var options = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new RssReaderDbContext(options, Logger);
            context.Database.EnsureCreated();
            context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;").Wait();

            return context;
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask; // ✅ NADA que limpiar global
    }
}