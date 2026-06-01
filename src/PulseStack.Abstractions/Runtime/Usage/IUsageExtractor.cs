using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Runtime.Usage;

public interface IUsageExtractor
{
    AIUsage? Extract(
        ChatResponse response,
        UsageExtractionContext context);
}
