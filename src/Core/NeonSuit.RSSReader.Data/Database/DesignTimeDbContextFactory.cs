using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Serilog;

namespace NeonSuit.RSSReader.Data.Database;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RSSReaderDbContext>
{
    public RSSReaderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RSSReaderDbContext>()
            .UseSqlite("Data Source=neonsuit-design.db").Options;
        return new RSSReaderDbContext(options, new LoggerConfiguration().CreateLogger());
    }
}
