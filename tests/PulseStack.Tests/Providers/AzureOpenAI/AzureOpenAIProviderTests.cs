using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.AzureOpenAI.DependencyInjection;
using PulseStack.Providers.AzureOpenAI.Factories;
using PulseStack.Providers.AzureOpenAI.Models;
using PulseStack.Providers.AzureOpenAI.Options;
using Xunit;

namespace PulseStack.Tests.Providers.AzureOpenAI;

public sealed class AzureOpenAIProviderTests
{
    [Fact]
    public void Catalog_Should_Return_Default_Deployment_When_None_Are_Configured()
    {
        var services = new ServiceCollection();
        services.Configure<AzureOpenAIOptions>(options =>
        {
            options.Deployment = "gpt-4o-mini";
        });

        using var provider = services.BuildServiceProvider();
        var source = new AzureOpenAIModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("AzureOpenAI", "gpt-4o-mini")
        ]);
    }

    [Fact]
    public void Catalog_Should_Return_Configured_Deployments()
    {
        var services = new ServiceCollection();
        services.Configure<AzureOpenAIOptions>(options =>
        {
            options.Deployment = "gpt-4o-mini";
            options.AvailableDeployments =
            [
                "gpt-4o-mini",
                "production-chat",
                "   "
            ];
        });

        using var provider = services.BuildServiceProvider();
        var source = new AzureOpenAIModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("AzureOpenAI", "gpt-4o-mini"),
            new ProviderModelDescriptor("AzureOpenAI", "production-chat")
        ]);
    }

    [Fact]
    public void UseAzureOpenAI_Should_Register_Provider_Catalog_Source()
    {
        var services = new ServiceCollection();

        services.UseAzureOpenAI(
            "https://test-resource.openai.azure.com/",
            "test-api-key",
            "gpt-4o-mini");

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IModelCatalogSource>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<AzureOpenAIModelCatalogSource>();
    }

    [Fact]
    public void UseAzureOpenAI_Should_Register_Chat_Client_Factory()
    {
        var services = new ServiceCollection();

        services.UseAzureOpenAI(
            "https://test-resource.openai.azure.com/",
            "test-api-key",
            "gpt-4o-mini");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<AzureOpenAIChatClientFactory>()
            .Should().NotBeNull();

        provider.GetServices<ChatClientFactoryRegistration>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatClientFactoryRegistration>(registration =>
                registration.Provider == "AzureOpenAI" &&
                registration.Factory is AzureOpenAIChatClientFactory);
    }

    [Fact]
    public void Factory_Should_Create_Chat_Client_Without_Executing_A_Request()
    {
        var services = new ServiceCollection();

        services.UseAzureOpenAI(
            "https://test-resource.openai.azure.com/",
            "test-api-key",
            "gpt-4o-mini");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<AzureOpenAIChatClientFactory>();

        factory.Create("gpt-4o-mini")
            .Should().BeAssignableTo<IChatClient>();
    }
}
