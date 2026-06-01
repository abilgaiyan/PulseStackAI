using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Agents.Runtime.Usage;

public sealed class UsageAggregator
{
    public AIUsage Aggregate(
        IEnumerable<AIUsage?> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);

        var availableUsages =
            usages
                .Where(usage => usage is not null)
                .Select(usage => usage!)
                .ToList();

        if (availableUsages.Count == 0)
        {
            return new AIUsage();
        }

        var providers =
            availableUsages
                .Select(usage => usage.Provider)
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var models =
            availableUsages
                .Select(usage => usage.Model)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new AIUsage
        {
            Provider =
                providers.Count == 1
                    ? providers[0]
                    : providers.Count == 0
                        ? string.Empty
                        : "Multiple",

            Model =
                models.Count == 1
                    ? models[0]
                    : models.Count == 0
                        ? string.Empty
                        : "Multiple",

            PromptTokens =
                availableUsages.Sum(usage => usage.PromptTokens),

            CompletionTokens =
                availableUsages.Sum(usage => usage.CompletionTokens)
        };
    }
}
