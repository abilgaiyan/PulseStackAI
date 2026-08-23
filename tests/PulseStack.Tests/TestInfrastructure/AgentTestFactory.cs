using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Realization.Binding;
using PulseStack.Agents.Realization.Composition;
using PulseStack.Agents.Runtime;
using PulseStack.Core.Assets;
using PulseStack.Core.Security;

using CoreToolExecutor = PulseStack.Core.Tools.ToolExecutor;

namespace PulseStack.Tests.TestInfrastructure;

internal static class AgentTestFactory
{
    public static Agent Create(
        IChatClient client,
        IToolRegistry? tools = null,
        IConversationMemory? memory = null,
        float? temperature = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var options = new ModelAssetOptions("TestProvider", "test-model");
        var factory = new ModelAssetFactory(new TestModelCatalog(options));

        var modelAsset = factory.Create(options);

        var modelReference = new AssetReference(
            modelAsset.Id,
            modelAsset.Urn);

        var definition = new AgentBuilder("Test Agent")
            .WithGoal("Execute test behavior.")
            .WithRole("Test assistant.")
            .UseModel(modelReference)
            .Build();

        var composition = new AgentComposition
        {
            Definition = definition,
            Model = modelAsset,
            ChatClient = client
        };

        var authorization =
            new AllowAllToolAuthorizationService();

        var executor =
            new CoreToolExecutor(authorization);

        var binding = new AgentBinding
        {
            ToolExecutor = executor,
            Tools = tools,
            Memory = memory,
            Temperature = temperature
        };

        return new Agent(
            composition,
            binding);
    }

    private sealed class TestModelCatalog(params ModelAssetOptions[] models) : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => models
                .Select(model => new ProviderModelDescriptor(model.Provider, model.Model))
                .ToArray();

        public bool Contains(string provider, string model)
            => models.Any(candidate =>
                string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Model, model, StringComparison.OrdinalIgnoreCase));
    }
}