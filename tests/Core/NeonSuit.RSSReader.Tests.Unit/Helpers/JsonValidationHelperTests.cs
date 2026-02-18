using FluentAssertions;
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
        // Arrange
        var invalidJson = "{ invalid json }";
        var fieldName = "testField";

        // Act
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson(invalidJson, fieldName));

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentException>();
        exception.Message.Should().Contain($"{fieldName} contains invalid JSON");
        ((ArgumentException)exception).ParamName.Should().Be(fieldName);
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
        // Arrange
        var jsonArrayOfStrings = "[\"string1\", \"string2\"]";
        var fieldName = "test";

        // Act
        var exception = Record.Exception(() =>
            JsonValidationHelper.EnsureValidJson(jsonArrayOfStrings, fieldName, expectIntArray: true));

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentException>();
        exception.Message.Should().Contain($"{fieldName} contains invalid JSON");
        ((ArgumentException)exception).ParamName.Should().Be(fieldName);
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
        // Arrange
        var invalidJson = "{ invalid json }";
        var fieldName = "test";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            JsonValidationHelper.EnsureValidJson<List<int>>(invalidJson, fieldName));

       
        exception.Message.Should().Contain("test contains invalid JSON for type List");
        exception.ParamName.Should().Be("test");
    }
}