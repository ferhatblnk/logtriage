using LogTriage.Configuration;
using LogTriage.Grouping;
using LogTriage.Issues;
using LogTriage.Loki;
using Microsoft.Extensions.Options;

namespace LogTriage.Api;

public static class GroupsApi
{
    private const int MaxWindowMinutes = 7 * 24 * 60;
    private const int MessageLimit = 500;

    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api").AddEndpointFilter<ApiKeyFilter>().MapGet("/groups", GetGroupsAsync);
        return app;
    }

    private static async Task<IResult> GetGroupsAsync(
        int? minutes,
        ErrorReader reader,
        IssueRegistry registry,
        IOptions<SyncOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var window = TimeSpan.FromMinutes(Math.Clamp(minutes ?? settings.LookbackMinutes, 1, MaxWindowMinutes));
        var now = time.GetUtcNow();

        ErrorReadResult read;
        try
        {
            read = await reader.ReadAsync(now - window, now, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or LokiException or TaskCanceledException)
        {
            return Results.Problem("Loki could not be queried.", statusCode: StatusCodes.Status502BadGateway);
        }

        var groups = ErrorGrouper.Group(read.Events).Select(group => ToDto(group, settings, registry)).ToList();
        return Results.Ok(new GroupsResponse((int)window.TotalMinutes, settings.DryRun, read.Truncated, groups));
    }

    private static GroupDto ToDto(ErrorGroup group, SyncOptions settings, IssueRegistry registry)
    {
        registry.TryGet(group.Fingerprint, out var issue);
        var message = group.Latest.Message;
        return new GroupDto(
            group.Fingerprint,
            group.Service,
            group.Environment,
            group.ExceptionType,
            group.Method,
            group.Route,
            group.StatusCode,
            group.Count,
            group.FirstSeen,
            group.LastSeen,
            message.Length <= MessageLimit ? message : message[..MessageLimit],
            IssuePolicy.IsEligible(group, settings),
            issue?.Key,
            issue?.Url);
    }
}
