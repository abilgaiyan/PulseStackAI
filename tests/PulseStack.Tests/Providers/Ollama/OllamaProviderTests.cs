using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Ollama.DependencyInjection;
using PulseStack.Providers.Ollama.Factories;
using PulseStack.Providers.Ollama.Options;
using PulseStack.Providers.Ollama.Models;
using Xunit;

namespace PulseStack.Tests.Providers.Ollama;

public sealed class OllamaProviderTests
{
    [Fact]
    public void Catalog_Should_Return_Default_Model()
    {
        var services = new ServiceCollection();
        services.UseOllama(
            "http://localhost:11434",
            "llama3");

        using var provider = services.BuildServiceProvider();
        var source = provider
            .GetRequiredService<IEnumerable<IModelCatalogSource>>()
            .Single(source => source.GetModels().Any(model => model.Provider == "Ollama"));

        source.GetModels()
            .Should()
            .ContainSingle(model =>
                model.Provider == "Ollama" &&
                model.Model == "llama3");
    }

    [Fact]
    public void Catalog_Should_Return_Configured_Models()
    {
        var services = new ServiceCollection();
        services.Configure<OllamaOptions>(options =>
        {
            options.Model = "llama3";
            options.AvailableModels =
            [
                "llama3",
                "qwen3",
                " ",
                ""
            ];
        });
        services.AddSingleton<OllamaModelCatalogSource>();

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<OllamaModelCatalogSource>();

        source.GetModels()
            .Should()
            .BeEquivalentTo(
            [
                new ProviderModelDescriptor("Ollama", "llama3"),
                new ProviderModelDescriptor("Ollama", "qwen3")
            ]);
    }

    [Fact]
    public void UseOllama_Should_Register_Catalog_Source()
    {
        var services = new ServiceCollection();

        services.UseOllama(
            "http://localhost:11434",
            "llama3");

        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<IModelCatalogSource>()
            .Should()
            .ContainSingle(source => source is OllamaModelCatalogSource);
    }

    [Fact]
    public void UseOllama_Should_Register_Factory()
    {
        var services = new ServiceCollection();

        services.UseOllama(
            "http://localhost:11434",
            "llama3");

        using var provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<OllamaChatClientFactory>()
            .Should()
            .NotBeNull();
    }

    [Fact]
    public void Factory_Should_Create_Chat_Client_Without_Executing_A_Request()
    {
        var services = new ServiceCollection();

        services.UseOllama(
            "http://localhost:11434",
            "llama3");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<OllamaChatClientFactory>();

        factory.Create("llama3")
            .Should()
            .BeAssignableTo<IChatClient>();
    }

    [Fact]
    public void UseOllama_Should_Register_Default_Chat_Client()
    {
        var services = new ServiceCollection();

        services.UseOllama(
            "http://localhost:11434",
            "llama3");

        using var provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<IChatClient>()
            .Should()
            .BeAssignableTo<IChatClient>();
    }
}
