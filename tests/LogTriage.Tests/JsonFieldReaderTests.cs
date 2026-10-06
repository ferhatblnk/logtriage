using System.Text.Json;
using LogTriage.Loki;

namespace LogTriage.Tests;

public class JsonFieldReaderTests
{
    [Theory]
    [InlineData("""{"exception.type":"System.TimeoutException"}""")]
    [InlineData("""{"exception":{"type":"System.TimeoutException"}}""")]
    [InlineData("""{"State":{"exception.type":"System.TimeoutException"}}""", "State.exception.type")]
    [InlineData("""{"log":{"exception":{"type":"System.TimeoutException"}}}""", "log.exception.type")]
    public void Reads_flat_nested_and_mixed_names(string json, string name = "exception.type")
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal("System.TimeoutException", JsonFieldReader.Read(document.RootElement, name));
    }

    [Fact]
    public void Returns_numbers_and_objects_as_text()
    {
        using var document = JsonDocument.Parse("""{"http.response.status_code":503,"http.request.body":{"id":7}}""");

        Assert.Equal("503", JsonFieldReader.Read(document.RootElement, "http.response.status_code"));
        Assert.Equal("""{"id":7}""", JsonFieldReader.Read(document.RootElement, "http.request.body"));
    }

    [Theory]
    [InlineData("""{"other":"value"}""")]
    [InlineData("""{"exception.type":null}""")]
    [InlineData("""{"exception":"not an object"}""")]
    public void Returns_null_when_the_field_is_missing(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Null(JsonFieldReader.Read(document.RootElement, "exception.type"));
    }
}
