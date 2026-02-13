using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.FeedParser;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;

namespace NeonSuit.RSSReader.Tests.Integration.Factories;

public class ServiceFactory
{
    private readonly DatabaseFixture _dbFixture;
    private RssReaderDbContext? _currentDbContext;

    public ServiceFactory(DatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    /// <summary>
    /// Obtiene el DbContext actual o crea uno nuevo si no existe.
    /// </summary>
    public RssReaderDbContext GetDbContext()
    {
        _currentDbContext ??= _dbFixture.CreateNewDbContext();
        return _currentDbContext;
    }

    /// <summary>
    /// Crea un DbContext fresco explícitamente.
    /// </summary>
    public RssReaderDbContext CreateFreshDbContext()
    {
        _currentDbContext = _dbFixture.CreateNewDbContext();
        return _currentDbContext;
    }

    public IFeedService CreateFeedService()
    {
        var dbContext = GetDbContext();

        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);
        var articleRepo = new ArticleRepository(dbContext, _dbFixture.Logger);
        var parser = new RssFeedParser(_dbFixture.Logger);

        return new FeedService(feedRepo, articleRepo, parser, _dbFixture.Logger);
    }

    public IArticleService CreateArticleService()
    {
        var dbContext = GetDbContext();

        var articleRepo = new ArticleRepository(dbContext, _dbFixture.Logger);
        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);

        return new ArticleService(articleRepo, feedRepo, _dbFixture.Logger);
    }

    public ICategoryService CreateCategoryService()
    {
        var dbContext = GetDbContext();

        var categoryRepo = new CategoryRepository(dbContext, _dbFixture.Logger);
        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);

        return new CategoryService(categoryRepo, feedRepo, _dbFixture.Logger);
    }
}