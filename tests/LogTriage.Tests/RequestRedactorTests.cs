using LogTriage.Configuration;
using LogTriage.Redaction;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class RequestRedactorTests
{
    private readonly RequestRedactor redactor = new(Options.Create(new IssueOptions()));

    [Fact]
    public void Masks_sensitive_json_fields_at_any_depth()
    {
        const string body = """{"email":"demo@example.com","password":"hunter2","profile":{"apiKey":"abc","tags":[{"refresh_token":"xyz","name":"kept"}]}}""";

        var result = redactor.Redact("", body);

        Assert.False(result.Withheld);
        Assert.DoesNotContain("hunter2", result.Body);
        Assert.DoesNotContain("abc", result.Body);
        Assert.DoesNotContain("xyz", result.Body);
        Assert.Contains("demo@example.com", result.Body);
        Assert.Contains("kept", result.Body);
        Assert.Equal(3, result.Body.Split("\"***\"").Length - 1);
    }

    [Fact]
    public void Masks_sensitive_query_parameters_and_keeps_the_rest()
    {
        var result = redactor.Redact("?page=2&access_token=abc123&Session=s1&flag", "");

        Assert.Equal("page=2&access_token=***&Session=***&flag", result.Query);
    }

    [Theory]
    [InlineData("""{"user":"demo","password":"hunter2","note":"cut off he""", "hunter2")]
    [InlineData("user=demo&password=hunter2&remember=true", "hunter2")]
    [InlineData("Authorization: Bearer abc.def", "abc.def")]
    public void Masks_bodies_that_are_not_valid_json(string body, string secret)
    {
        var result = redactor.Redact("", body);

        Assert.DoesNotContain(secret, result.Body);
        Assert.Contains("***", result.Body);
        Assert.Contains("demo", redactor.Redact("", "user=demo&password=hunter2").Body);
    }

    [Theory]
    [InlineData("""{"cardNumber":"4111111111111111"}""")]
    [InlineData("""{"note":"paid with 4111 1111 1111 1111 today"}""")]
    [InlineData("""{"number":"5500-0000-0000-0004"}""")]
    [InlineData("""{"account":"GB82WEST12345698765432"}""")]
    public void Withholds_requests_that_carry_payment_details(string body)
    {
        var result = redactor.Redact("ref=1", body);

        Assert.True(result.Withheld);
        Assert.Equal("", result.Body);
        Assert.Equal("", result.Query);
    }

    [Fact]
    public void Withholds_when_the_card_number_is_in_the_query()
    {
        Assert.True(redactor.Redact("card=4111111111111111", "").Withheld);
    }

    [Theory]
    [InlineData("""{"orderId":4111111111111112}""")]
    [InlineData("""{"createdAtMs":1768478400000}""")]
    [InlineData("""{"phone":"+1 555 010 0199"}""")]
    [InlineData("""{"reference":"GB00WEST12345698765432"}""")]
    public void Keeps_numbers_that_are_not_payment_details(string body)
    {
        Assert.False(redactor.Redact("", body).Withheld);
    }

    [Fact]
    public void Leaves_data_untouched_when_no_sensitive_keys_are_configured()
    {
        var open = new RequestRedactor(Options.Create(new IssueOptions { SensitiveKeys = "" }));

        Assert.Equal("password=hunter2", open.Redact("password=hunter2", "").Query);
        Assert.Equal("password=hunter2", open.Redact("", "password=hunter2").Body);
    }
}
