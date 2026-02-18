using NeonSuit.RSSReader.Core.Interfaces.Database;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.RssFeedParser;
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
    public IRssReaderDbContext CreateFreshDbContext()
    {
        _currentDbContext = _dbFixture.CreateNewDbContext();
        return _currentDbContext;
    }

    /// <summary>
    /// Crea un FeedService con DbContext NUEVO en cada llamada.
    /// Útil para pruebas que requieren aislamiento total.
    /// </summary>
    public IFeedService CreateFreshFeedService()
    {
        var dbContext = _dbFixture.CreateNewDbContext(); // ✅ SIEMPRE NUEVO

        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);
        var articleRepo = new ArticleRepository(dbContext, _dbFixture.Logger);
        var parser = new RssFeedParser(_dbFixture.Logger);

        return new FeedService(feedRepo, articleRepo, parser, _dbFixture.Logger);
    }

    /// <summary>
    /// Crea un CategoryService con DbContext NUEVO en cada llamada.
    /// Útil para pruebas que requieren aislamiento total.
    /// </summary>
    public ICategoryService CreateFreshCategoryService()
    {
        var dbContext = _dbFixture.CreateNewDbContext(); // ✅ SIEMPRE NUEVO

        var categoryRepo = new CategoryRepository(dbContext, _dbFixture.Logger);
        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);

        return new CategoryService(categoryRepo, feedRepo, _dbFixture.Logger);
    }

    /// <summary>
    /// Crea un ArticleService con DbContext NUEVO en cada llamada.
    /// </summary>
    public IArticleService CreateFreshArticleService()
    {
        var dbContext = _dbFixture.CreateNewDbContext(); // ✅ SIEMPRE NUEVO

        var articleRepo = new ArticleRepository(dbContext, _dbFixture.Logger);
        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);

        return new ArticleService(articleRepo, feedRepo, _dbFixture.Logger);
    }

    /// <summary>
    /// Crea un OpmlService con servicios FRESCOS (DbContext nuevos).
    /// </summary>
    public IOpmlService CreateFreshOpmlService()
    {
        return new OpmlService(
            CreateFreshFeedService(),
            CreateFreshCategoryService(),
            _dbFixture.Logger);
    }

    // ===== MÉTODOS ORIGINALES (con DbContext compartido) =====
    // Se mantienen para compatibilidad con pruebas existentes
    // pero las nuevas pruebas deberían usar los métodos Fresh

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

    public IRuleService CreateRuleService()
    {
        var dbContext = GetDbContext();

        var ruleRepo = new RuleRepository(dbContext, _dbFixture.Logger);
        var articleRepo = new ArticleRepository(dbContext, _dbFixture.Logger);
        var feedRepo = new FeedRepository(dbContext, _dbFixture.Logger);

        return new RuleService(ruleRepo, articleRepo, feedRepo, _dbFixture.Logger);
    }

    /// <summary>
    /// Creates a fresh instance of ISettingsService with a new DbContext.
    /// </summary>
    public ISettingsService CreateSettingsService()
    {
        var dbContext = GetDbContext();
        var settingsRepo = new UserPreferencesRepository(dbContext, _dbFixture.Logger);
        return new SettingsService(settingsRepo, _dbFixture.Logger);
    }

    /// <summary>
    /// Creates a fresh instance of ITagService with a new DbContext.
    /// </summary>
    public ITagService CreateTagService()
    {
        var dbContext = _dbFixture.CreateNewDbContext();
        var tagRepo = new TagRepository(dbContext, _dbFixture.Logger);
        var articleTagRepo = new ArticleTagRepository(dbContext, _dbFixture.Logger);
        return new TagService(tagRepo, _dbFixture.Logger);
    }

    /// <summary>
    /// Creates a fresh instance of ITagService with a NEW DbContext each time.
    /// Único método que debe usarse en pruebas para evitar conflictos.
    /// </summary>
    public ITagService CreateFreshTagService()
    {
        var dbContext = _dbFixture.CreateNewDbContext(); // ✅ SIEMPRE NUEVO
        var tagRepo = new TagRepository(dbContext, _dbFixture.Logger);
        return new TagService(tagRepo, _dbFixture.Logger);
    }
}