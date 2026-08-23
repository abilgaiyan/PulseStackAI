using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Models;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Models;
using Xunit;

namespace PulseStack.Tests.Models;

public class ModelCatalogTests
{
    [Fact]
    public void GetModels_Should_Aggregate_Models_From_All_Sources()
    {
        var catalog = new ModelCatalog(
        [
            new TestModelCatalogSource(
            [
                new ProviderModelDescriptor("OpenRouter", "openai/gpt-4.1-mini")
            ]),
            new TestModelCatalogSource(
            [
                new ProviderModelDescriptor("AzureOpenAI", "gpt-4.1")
            ])
        ]);

        catalog.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("OpenRouter", "openai/gpt-4.1-mini"),
            new ProviderModelDescriptor("AzureOpenAI", "gpt-4.1")
        ]);
    }

    [Fact]
    public void GetModels_Should_Remove_Duplicate_Provider_Model_Entries()
    {
        var catalog = new ModelCatalog(
        [
            new TestModelCatalogSource(
            [
                new ProviderModelDescriptor("OpenRouter", "openai/gpt-4.1-mini")
            ]),
            new TestModelCatalogSource(
            [
                new ProviderModelDescriptor("openrouter", "OPENAI/GPT-4.1-MINI")
            ])
        ]);

        catalog.GetModels().Should().ContainSingle()
            .Which.Should().Be(
                new ProviderModelDescriptor("OpenRouter", "openai/gpt-4.1-mini"));
    }

    [Fact]
    public void Contains_Should_Return_True_For_A_Registered_Model()
    {
        var catalog = new ModelCatalog(
        [
            new TestModelCatalogSource(
            [
                new ProviderModelDescriptor("OpenRouter", "openai/gpt-4.1-mini")
            ])
        ]);

        catalog.Contains("openrouter", "OPENAI/GPT-4.1-MINI")
            .Should().BeTrue();
    }

    [Fact]
    public void Contains_Should_Return_False_For_An_Unregistered_Model()
    {
        var catalog = new ModelCatalog([]);

        catalog.Contains("OpenRouter", "openai/gpt-4.1-mini")
            .Should().BeFalse();
    }

    [Fact]
    public void GetModels_Should_Return_Empty_When_Sources_Are_Empty()
    {
        var catalog = new ModelCatalog(
        [
            new TestModelCatalogSource([]),
            new TestModelCatalogSource([])
        ]);

        catalog.GetModels().Should().BeEmpty();
    }

    [Fact]
    public void AddPulseStack_Should_Register_ModelCatalog()
    {
        var services = new ServiceCollection();

        services.AddPulseStack();

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IModelCatalog>()
            .Should().BeOfType<ModelCatalog>();
    }

    private sealed class TestModelCatalogSource(
        IReadOnlyCollection<ProviderModelDescriptor> models)
        : IModelCatalogSource
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => models;
    }
}
