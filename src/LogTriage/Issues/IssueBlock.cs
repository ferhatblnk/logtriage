namespace LogTriage.Issues;

/// <summary>A piece of an issue description, independent of any tracker's markup.</summary>
public abstract record IssueBlock;

public sealed record HeadingBlock(string Text) : IssueBlock;

public sealed record ParagraphBlock(string Text) : IssueBlock;

public sealed record CodeBlock(string Text) : IssueBlock;

public sealed record FactsBlock(IReadOnlyList<Fact> Facts) : IssueBlock;

public sealed record Fact(string Label, string Value);
