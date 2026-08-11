using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Groq.DependencyInjection;
using PulseStack.Providers.Groq.Factories;
using PulseStack.Providers.Groq.Models;
using PulseStack.Providers.Groq.Options;
using Xunit;

namespace PulseStack.Tests.Providers.Groq;

public sealed class GroqProviderTests
{
    [Fact]
    public void Catalog_Should_Return_Default_Model_When_No_Models_Are_Configured()
    {
        var services = new ServiceCollection();
        services.Configure<GroqOptions>(options =>
        {
            options.Model = "llama-3.3-70b-versatile";
        });

        using var provider = services.BuildServiceProvider();
        var source = new GroqModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GroqOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("Groq", "llama-3.3-70b-versatile")
        ]);
    }

    [Fact]
    public void Catalog_Should_Return_Configured_Models()
    {
        var services = new ServiceCollection();
        services.Configure<GroqOptions>(options =>
        {
            options.Model = "llama-3.3-70b-versatile";
            options.AvailableModels =
            [
                "llama-3.3-70b-versatile",
                "meta-llama/llama-4-scout-17b-16e-instruct",
                "   "
            ];
        });

        using var provider = services.BuildServiceProvider();
        var source = new GroqModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GroqOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("Groq", "llama-3.3-70b-versatile"),
            new ProviderModelDescriptor("Groq", "meta-llama/llama-4-scout-17b-16e-instruct")
        ]);
    }

    [Fact]
    public void UseGroq_Should_Register_Provider_Catalog_Source()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<GroqOptions>>(
            Microsoft.Extensions.Options.Options.Create(new GroqOptions()));
        services.UseGroq("test-api-key");

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IModelCatalogSource>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<GroqModelCatalogSource>();
    }

    [Fact]
    public void UseGroq_Should_Register_Chat_Client_Factory()
    {
        var services = new ServiceCollection();

        services.UseGroq("test-api-key");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<GroqChatClientFactory>()
            .Should().NotBeNull();

        provider.GetServices<ChatClientFactoryRegistration>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatClientFactoryRegistration>(registration =>
                registration.Provider == "Groq" &&
                registration.Factory is GroqChatClientFactory);
    }

    [Fact]
    public void Factory_Should_Create_Chat_Client_Without_Executing_A_Request()
    {
        var services = new ServiceCollection();
        services.Configure<GroqOptions>(options =>
        {
            options.ApiKey = "test-api-key";
            options.Endpoint = "https://api.groq.com/openai/v1";
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<GroqChatClientFactory>();

        factory.Create("llama-3.3-70b-versatile")
            .Should().BeAssignableTo<IChatClient>();
    }
}
