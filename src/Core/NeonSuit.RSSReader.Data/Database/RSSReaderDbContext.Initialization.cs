using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace NeonSuit.RSSReader.Data.Database;

internal partial class RSSReaderDbContext
{
    /// <summary>Initializes new files and adopts a compatible pre-migrations database without deleting data.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var applied = (await Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var assembly = this.GetService<IMigrationsAssembly>();
        if (applied.Count == 0)
        {
            await Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%'";
                var tables = new HashSet<string>(StringComparer.Ordinal);
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
                if (tables.Count > 0)
                {
                    var initialPair = assembly.Migrations.First();
                    var initial = assembly.CreateMigration(initialPair.Value, Database.ProviderName!);
                    foreach (var table in initial.UpOperations.OfType<CreateTableOperation>())
                    {
                        if (!tables.Contains(table.Name))
                            throw new InvalidOperationException($"Existing database is missing {table.Name}; automatic baseline is unsafe. The file has not been changed.");
                        var columns = await GetExistingColumnsAsync(table.Name, cancellationToken);
                        foreach (var column in table.Columns)
                            if (!columns.TryGetValue(column.Name, out var storeType) || !storeType.Equals(column.ColumnType, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException($"Existing database column {table.Name}.{column.Name} does not match the baseline; the file has not been changed.");
                    }
                    var baseline = new List<string> { initialPair.Key };
                    var articleColumns = await GetExistingColumnsAsync("Articles", cancellationToken);
                    foreach (var pair in assembly.Migrations.Skip(1))
                    {
                        var migration = assembly.CreateMigration(pair.Value, Database.ProviderName!);
                        var created = migration.UpOperations.OfType<CreateTableOperation>().ToList();
                        if (created.Count > 0 && created.Any(t => tables.Contains(t.Name)))
                        {
                            foreach (var table in created)
                            {
                                if (!tables.Contains(table.Name))
                                    throw new InvalidOperationException($"Existing database has an incomplete {pair.Key} schema; automatic baseline is unsafe.");
                                var columns = await GetExistingColumnsAsync(table.Name, cancellationToken);
                                foreach (var column in table.Columns)
                                    if (!columns.TryGetValue(column.Name, out var type) || !type.Equals(column.ColumnType, StringComparison.OrdinalIgnoreCase))
                                        throw new InvalidOperationException($"Existing database column {table.Name}.{column.Name} does not match {pair.Key}.");
                            }
                            baseline.Add(pair.Key);
                        }
                        else if (pair.Key.EndsWith("_ArticleHighlights", StringComparison.Ordinal) && articleColumns.ContainsKey("HighlightColor"))
                            baseline.Add(pair.Key);
                        else if (pair.Key.EndsWith("_ArticleTagIdentity", StringComparison.Ordinal))
                        {
                            await using var keyCommand = Database.GetDbConnection().CreateCommand();
                            keyCommand.CommandText = "SELECT name FROM pragma_table_info('ArticleTags') WHERE pk > 0 ORDER BY pk";
                            var keys = new List<string>();
                            await using (var reader = await keyCommand.ExecuteReaderAsync(cancellationToken))
                                while (await reader.ReadAsync(cancellationToken)) keys.Add(reader.GetString(0));
                            if (keys.SequenceEqual(new[] { "Id" })) baseline.Add(pair.Key);
                            else if (!keys.SequenceEqual(new[] { "ArticleId", "TagId" }))
                                throw new InvalidOperationException("Existing ArticleTags primary key is incompatible; automatic baseline is unsafe.");
                        }
                    }
                    var backup = DatabasePath + ".pre-net10-" + Guid.NewGuid().ToString("N") + ".db";
                    await BackupAsync(backup, cancellationToken);
                    var history = this.GetService<IHistoryRepository>();
                    await Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
                    foreach (var migrationId in baseline)
                        await Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migrationId, "10.0.12")), cancellationToken);
                    _logger.Information("Adopted existing schema; recovery backup saved to {Backup}", backup);
                }
            }
            finally { await Database.CloseConnectionAsync(); }
        }
        await Database.MigrateAsync(cancellationToken);
        await ApplySqliteOptimizationsAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> GetExistingColumnsAsync(string table, CancellationToken cancellationToken)
    {
        await using var command = Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info(\"" + table.Replace("\"", "\"\"") + "\")";
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1), reader.GetString(2));
        return columns;
    }
}
