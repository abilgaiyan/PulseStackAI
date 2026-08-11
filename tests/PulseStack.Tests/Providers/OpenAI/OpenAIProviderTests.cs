using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.OpenAI.DependencyInjection;
using PulseStack.Providers.OpenAI.Factories;
using PulseStack.Providers.OpenAI.Models;
using PulseStack.Providers.OpenAI.Options;
using Xunit;

namespace PulseStack.Tests.Providers.OpenAI;

public sealed class OpenAIProviderTests
{
    [Fact]
    public void Catalog_Should_Return_Default_Model_When_No_Models_Are_Configured()
    {
        var options = Options.Create(new OpenAIOptions
        {
            Model = "gpt-4o-mini"
        });

        var source = new OpenAIModelCatalogSource(options);

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("OpenAI", "gpt-4o-mini")
        ]);
    }

    [Fact]
    public void Catalog_Should_Return_Configured_Models()
    {
        var options = Options.Create(new OpenAIOptions
        {
            Model = "gpt-4o-mini",
            AvailableModels =
            [
                "gpt-4o-mini",
                "gpt-4.1",
                "   "
            ]
        });

        var source = new OpenAIModelCatalogSource(options);

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("OpenAI", "gpt-4o-mini"),
            new ProviderModelDescriptor("OpenAI", "gpt-4.1")
        ]);
    }

    [Fact]
    public void UseOpenAI_Should_Register_Provider_Catalog_Source()
    {
        var services = new ServiceCollection();

        services.UseOpenAI("test-api-key");

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IModelCatalogSource>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<OpenAIModelCatalogSource>();
    }

    [Fact]
    public void UseOpenAI_Should_Register_Chat_Client_Factory()
    {
        var services = new ServiceCollection();

        services.UseOpenAI("test-api-key");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<OpenAIChatClientFactory>()
            .Should().NotBeNull();

        provider.GetServices<ChatClientFactoryRegistration>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatClientFactoryRegistration>(registration =>
                registration.Provider == "OpenAI" &&
                registration.Factory is OpenAIChatClientFactory);
    }

    [Fact]
    public void Factory_Should_Create_Chat_Client_Without_Executing_A_Request()
    {
        var services = new ServiceCollection();

        services.UseOpenAI("test-api-key");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<OpenAIChatClientFactory>();

        factory.Create("gpt-4o-mini")
            .Should().BeAssignableTo<IChatClient>();
    }
}
