using NeonSuit.RSSReader.Tests.Integration.Fixtures;

namespace NeonSuit.RSSReader.Tests.Integration.Collections;

/// <summary>
/// Defines a shared test collection for integration tests that require a real database
/// and a mock web server. This collection ensures that all integration tests share the 
/// same instances of DatabaseFixture and FeedWebServerFixture, preventing redundant 
/// setup/teardown operations and maintaining test isolation through database transactions.
/// </summary>
/// <remarks>
/// Usage: Decorate test classes with [Collection("Integration Tests")] to participate
/// in this shared context. The fixtures are created once per test run and disposed
/// after all tests in the collection complete.
/// </remarks>
[CollectionDefinition("Integration Tests")]
public class IntegrationCollection :
    ICollectionFixture<DatabaseFixture>
{
    // No implementation required - XUnit uses this class solely as a marker
    // to group fixtures and control their lifetime scope.
}