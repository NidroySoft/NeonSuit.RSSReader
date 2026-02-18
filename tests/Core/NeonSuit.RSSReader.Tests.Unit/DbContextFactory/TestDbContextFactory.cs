using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.DbContextFactory
{
    /// <summary>
    /// Factory para crear DbContext de prueba con SQLite en memoria.
    /// CADA llamada crea una base de datos independiente.
    /// </summary>
    public class TestDbContextFactory : IDisposable
    {
        private readonly List<SqliteConnection> _connections = new();
        private readonly ILogger _logger;
        private bool _disposed;

        public TestDbContextFactory()
        {
            _logger = Log.Logger.ForContext<TestDbContextFactory>();
        }

        /// <summary>
        /// Crea un nuevo DbContext con su propia base de datos en memoria.
        /// </summary>
        public RssReaderDbContext CreateContext()
        {
            // Cada contexto tiene SU propia conexion
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            _connections.Add(connection);

            var options = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite(connection)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.AmbientTransactionWarning))
                .Options;

            var context = new RssReaderDbContext(options, _logger);

            // Dejar que EF Core cree el esquema (mantiene consistencia con migraciones)
            context.Database.EnsureCreated();

            _logger.Debug("DbContext creado con SQLite en memoria (ID: {Id})", connection.GetHashCode());
            return context;
        }

        /// <summary>
        /// Crea contexto y ejecuta seed opcional.
        /// </summary>
        public RssReaderDbContext CreateContextWithSeed(Action<RssReaderDbContext> seedAction)
        {
            var context = CreateContext();
            seedAction(context);
            context.SaveChanges();
            return context;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var conn in _connections)
            {
                try
                {
                    conn.Close();
                    conn.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error cerrando conexion SQLite");
                }
            }

            _connections.Clear();
            _disposed = true;
        }
    }
}