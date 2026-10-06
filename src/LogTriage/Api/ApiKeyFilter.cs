using System.Security.Cryptography;
using System.Text;
using LogTriage.Configuration;
using Microsoft.Extensions.Options;

namespace LogTriage.Api;

/// <summary>Guards the API with a shared key. Without a configured key the API does not exist (404).</summary>
public sealed class ApiKeyFilter(IOptions<ApiOptions> options) : IEndpointFilter
{
    public const string Header = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.Key;
        if (expected.Length == 0)
            return Results.NotFound();

        var provided = context.HttpContext.Request.Headers[Header].ToString();
        var matches = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
        return matches ? await next(context) : Results.Unauthorized();
    }
}
