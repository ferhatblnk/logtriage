using LogTriage.Configuration;
using LogTriage.Issues;
using LogTriage.Redaction;
using LogTriage.Tests.Support;
using Microsoft.Extensions.Options;

namespace LogTriage.Tests;

public class IssueComposerTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromMinutes(60);

    [Fact]
    public void Title_follows_the_template()
    {
        var draft = Composer().Compose(Samples.Group(), Hour);

        Assert.Equal("[orders-api] InvalidOperationException: GET /orders/{id}", draft.Title);
    }

    [Fact]
    public void Title_does_not_treat_route_parameters_as_placeholders()
    {
        var draft = Composer().Compose(Samples.Group(route: "/jobs/{status}/{service}"), Hour);

        Assert.Equal("[orders-api] InvalidOperationException: GET /jobs/{status}/{service}", draft.Title);
    }

    [Fact]
    public void Title_drops_dangling_separators_and_respects_the_length_limit()
    {
        var composer = Composer(new IssueOptions { TitleTemplate = "{exceptionFullType} | {status} {message} - {environment} {unknown}:" });
        var draft = composer.Compose(Samples.Group(), Hour);

        Assert.Equal("System.InvalidOperationException | 500 Order 41 has no shipping address. - demo {unknown}", draft.Title);

        var longTitle = Composer(new IssueOptions { TitleTemplate = new string('x', 400) }).Compose(Samples.Group(), Hour).Title;
        Assert.Equal(255, longTitle.Length);
        Assert.EndsWith("…", longTitle);
    }

    [Fact]
    public void Body_lists_the_facts_of_the_group()
    {
        var draft = Composer().Compose(Samples.Group(count: 3), TimeSpan.FromHours(24));
        var facts = Assert.Single(draft.Body.OfType<FactsBlock>()).Facts.ToDictionary(fact => fact.Label, fact => fact.Value);

        Assert.Equal("orders-api", facts["Service"]);
        Assert.Equal("demo", facts["Environment"]);
        Assert.Equal("System.InvalidOperationException", facts["Exception"]);
        Assert.Equal("GET /orders/{id}", facts["Request"]);
        Assert.Equal("500", facts["Status"]);
        Assert.Equal("3 in the last 24 hours", facts["Occurrences"]);
        Assert.Equal("2026-01-15 11:58:00 UTC", facts["First seen"]);
        Assert.Equal("2026-01-15 12:00:00 UTC", facts["Last seen"]);
        Assert.Equal(draft.Fingerprint, facts["Fingerprint"]);
    }

    [Fact]
    public void Body_carries_message_stack_trace_and_a_loki_query()
    {
        var draft = Composer(new IssueOptions { MaxStackTraceLength = 10 }).Compose(Samples.Group(), Hour);
        var text = Text(draft);

        Assert.Contains("Order 41 has no shipping address.", text);
        Assert.Contains("   at Ord…", text);
        Assert.Contains("{service_name=\"orders-api\"} |= `System.InvalidOperationException`", text);
    }

    [Fact]
    public void Request_data_is_left_out_by_default()
    {
        var draft = Composer().Compose(Samples.Group(query: "page=2", body: """{"password":"hunter2"}"""), Hour);

        Assert.DoesNotContain("Latest request", Text(draft));
        Assert.DoesNotContain("hunter2", Text(draft));
        Assert.DoesNotContain("page=2", Text(draft));
    }

    [Fact]
    public void Request_data_is_redacted_when_included()
    {
        var composer = Composer(new IssueOptions { IncludeRequestData = true });
        var text = Text(composer.Compose(Samples.Group(query: "page=2&token=tok-secret-1", body: """{"password":"hunter2"}"""), Hour));

        Assert.Contains("GET /orders/41?page=2&token=***", text);
        Assert.Contains("\"password\": \"***\"", text);
        Assert.DoesNotContain("hunter2", text);
        Assert.DoesNotContain("tok-secret-1", text);
    }

    [Fact]
    public void Request_data_is_withheld_when_it_looks_like_payment_details()
    {
        var composer = Composer(new IssueOptions { IncludeRequestData = true });
        var text = Text(composer.Compose(Samples.Group(body: """{"cardNumber":"4111111111111111"}"""), Hour));

        Assert.Contains("Request data was withheld", text);
        Assert.DoesNotContain("4111111111111111", text);
    }

    private static IssueComposer Composer(IssueOptions? options = null)
    {
        var issue = Options.Create(options ?? new IssueOptions());
        return new IssueComposer(issue, Options.Create(new LokiOptions()), new RequestRedactor(issue));
    }

    private static string Text(IssueDraft draft) => string.Join('\n', draft.Body.Select(static block => block switch
    {
        HeadingBlock heading => heading.Text,
        ParagraphBlock paragraph => paragraph.Text,
        CodeBlock code => code.Text,
        FactsBlock facts => string.Join('\n', facts.Facts.Select(static fact => $"{fact.Label}: {fact.Value}")),
        _ => "",
    }));
}
