using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Runtime.Usage;

public sealed class UsageExtractorRegistry
{
    private readonly List<IUsageExtractor> _extractors = [];

    public UsageExtractorRegistry()
    {
    }

    public UsageExtractorRegistry(
        IEnumerable<IUsageExtractor> extractors)
    {
        ArgumentNullException.ThrowIfNull(extractors);

        foreach (var extractor in extractors)
        {
            Register(extractor);
        }
    }

    public static UsageExtractorRegistry CreateDefault()
    {
        var registry = new UsageExtractorRegistry();

        registry.Register(
            new ChatResponseUsageExtractor());

        return registry;
    }

    public void Register(
        IUsageExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);

        _extractors.Add(extractor);
    }

    public AIUsage? Extract(
        ChatResponse response,
        UsageExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var extractor in _extractors)
        {
            var usage =
                extractor.Extract(
                    response,
                    context);

            if (usage is not null)
            {
                return usage;
            }
        }

        return null;
    }
}
