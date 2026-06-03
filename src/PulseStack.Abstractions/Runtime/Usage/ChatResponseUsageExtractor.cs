using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Runtime.Usage;

public sealed class ChatResponseUsageExtractor : IUsageExtractor
{
    public AIUsage? Extract(
        ChatResponse response,
        UsageExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(context);

        if (response.Usage is null)
        {
            return null;
        }

        var promptTokens =
            response.Usage.InputTokenCount ?? 0;

        var completionTokens =
            response.Usage.OutputTokenCount ?? 0;

        if (promptTokens == 0
            && completionTokens == 0
            && (response.Usage.TotalTokenCount ?? 0) == 0)
        {
            return null;
        }

        return new AIUsage
        {
            Provider =
                context.Provider
                ?? string.Empty,

            Model =
                context.Model
                ?? response.ModelId
                ?? string.Empty,

            PromptTokens =
                promptTokens,

            CompletionTokens =
                completionTokens
        };
    }
}
