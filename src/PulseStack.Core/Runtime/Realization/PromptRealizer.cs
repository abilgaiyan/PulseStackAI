using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization;

namespace PulseStack.Core.Runtime.Realization;

public sealed class PromptRealizer
{
    public RuntimePrompt Realize(PromptAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        asset.Id.EnsureValid();
        ArgumentException.ThrowIfNullOrWhiteSpace(asset.Options.SystemInstructions);

        return new RuntimePrompt
        {
            SystemInstructions = asset.Options.SystemInstructions
        };
    }
}
