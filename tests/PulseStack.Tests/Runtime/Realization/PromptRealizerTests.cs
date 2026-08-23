using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class PromptRealizerTests
{
    [Fact]
    public void Realize_ShouldCreateRuntimePrompt_FromPromptAsset()
    {
        var asset = new PromptAssetFactory().Create(
            new PromptAssetOptions
            {
                Name = "System Prompt",
                SystemInstructions = "You are concise and helpful."
            });

        var realizer = new PromptRealizer();

        var prompt = realizer.Realize(asset);

        prompt.SystemInstructions.Should().Be("You are concise and helpful.");
    }
}
