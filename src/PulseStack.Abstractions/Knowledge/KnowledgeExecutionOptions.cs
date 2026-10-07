namespace PulseStack.Abstractions.Knowledge;

/// <summary>Consumer-owned limits for call-local retrieved reference material.</summary>
public sealed record KnowledgeExecutionOptions
{
    /// <summary>Maximum UTF-8 bytes including framing, source labels and separators.</summary>
    public int MaxContributionBytes { get; init; } = 64 * 1024;
}
