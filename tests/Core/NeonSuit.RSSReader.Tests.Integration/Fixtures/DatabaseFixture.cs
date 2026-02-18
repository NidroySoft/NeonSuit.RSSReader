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
            var connectionString = $"DataSource=file:memdb-{Guid.NewGuid():N}?mode=memory&cache=private";

            Logger.Debug("Creating new in-memory database with connection: {Connection}", connectionString);

            var connection = new SqliteConnection(connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new RssReaderDbContext(options, Logger);

            // ✅ FORZAR eliminación y recreación para aplicar configuraciones
            context.Database.EnsureDeleted(); // Asegura que no haya tablas viejas
            context.Database.EnsureCreated(); // Recrea con la configuración ACTUAL

            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

            return context;
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask; // ✅ NADA que limpiar global
    }
}