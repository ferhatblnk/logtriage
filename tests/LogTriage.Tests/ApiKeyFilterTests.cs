using LogTriage.Api;
using LogTriage.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class ApiKeyFilterTests
{
    [Fact]
    public async Task Api_does_not_exist_while_no_key_is_configured()
    {
        Assert.IsType<NotFound>(await InvokeAsync(configured: "", provided: "anything"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData("secret-key-and-more")]
    public async Task Rejects_a_missing_or_wrong_key(string? provided)
    {
        Assert.IsType<UnauthorizedHttpResult>(await InvokeAsync(configured: "secret-key", provided));
    }

    [Fact]
    public async Task Lets_the_request_through_with_the_right_key()
    {
        Assert.Equal("reached", await InvokeAsync(configured: "secret-key", provided: "secret-key"));
    }

    private static async Task<object?> InvokeAsync(string configured, string? provided)
    {
        var context = new DefaultHttpContext();
        if (provided is not null)
            context.Request.Headers[ApiKeyFilter.Header] = provided;

        var filter = new ApiKeyFilter(Options.Create(new ApiOptions { Key = configured }));
        return await filter.InvokeAsync(EndpointFilterInvocationContext.Create(context), _ => ValueTask.FromResult<object?>("reached"));
    }
}
