using NeonSuit.RSSReader.Core.Helpers;
using System.Text.Json;

namespace NeonSuit.RSSReader.Tests.Unit.Helpers;

public class JsonValidationHelperTests
{
    [Fact]
    public void EnsureValidJson_NullOrEmpty_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson(null, "test"));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("", "test"));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("   ", "test"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidJson_ValidJson_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("[1,2,3]", "test"));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("{\"key\": \"value\"}", "test"));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("42", "test"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidJson_InvalidJson_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            JsonValidationHelper.EnsureValidJson("[1,2,", "testField"));

        Assert.Equal("testField", ex.ParamName);
        Assert.Contains("JSON inválido", ex.Message);
        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public void EnsureValidJson_ExpectIntArray_Valid_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("[1,2,3]", "test", expectIntArray: true));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson("[]", "test", expectIntArray: true));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidJson_ExpectIntArray_InvalidType_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            JsonValidationHelper.EnsureValidJson("{\"key\":1}", "test", expectIntArray: true));

        Assert.Equal("test", ex.ParamName);
        Assert.Contains("JSON inválido", ex.Message);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void EnsureValidJson_Generic_Valid_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson<List<int>>("[1,2,3]", "test"));

        Assert.Null(exception);

        exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson<Dictionary<string, string>>(
                "{\"key\":\"value\"}", "test"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValidJson_Generic_InvalidJson_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            JsonValidationHelper.EnsureValidJson<List<int>>("[1,2,", "test"));

        Assert.Equal("test", ex.ParamName);
        Assert.Contains("JSON inválido para tipo List", ex.Message);
    }
}