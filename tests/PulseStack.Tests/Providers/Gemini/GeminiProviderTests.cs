using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Gemini.DependencyInjection;
using PulseStack.Providers.Gemini.Factories;
using PulseStack.Providers.Gemini.Models;
using PulseStack.Providers.Gemini.Options;
using Xunit;

namespace PulseStack.Tests.Providers.Gemini;

public sealed class GeminiProviderTests
{
    [Fact]
    public void Catalog_Should_Return_Default_Model_When_No_Models_Are_Configured()
    {
        var services = new ServiceCollection();
        services.Configure<GeminiOptions>(options =>
        {
            options.Model = "gemini-2.5-flash";
        });

        using var provider = services.BuildServiceProvider();
        var source = new GeminiModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("Gemini", "gemini-2.5-flash")
        ]);
    }

    [Fact]
    public void Catalog_Should_Return_Configured_Models()
    {
        var services = new ServiceCollection();
        services.Configure<GeminiOptions>(options =>
        {
            options.Model = "gemini-2.5-flash";
            options.AvailableModels =
            [
                "gemini-2.5-flash",
                "gemini-2.5-pro",
                "   "
            ];
        });

        using var provider = services.BuildServiceProvider();
        var source = new GeminiModelCatalogSource(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>());

        source.GetModels().Should().BeEquivalentTo(
        [
            new ProviderModelDescriptor("Gemini", "gemini-2.5-flash"),
            new ProviderModelDescriptor("Gemini", "gemini-2.5-pro")
        ]);
    }

    [Fact]
    public void UseGemini_Should_Register_Provider_Catalog_Source()
    {
        var services = new ServiceCollection();

        services.UseGemini("test-api-key", "gemini-2.5-flash");

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IModelCatalogSource>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<GeminiModelCatalogSource>();
    }

    [Fact]
    public void UseGemini_Should_Register_Chat_Client_Factory()
    {
        var services = new ServiceCollection();

        services.UseGemini("test-api-key", "gemini-2.5-flash");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<GeminiChatClientFactory>()
            .Should().NotBeNull();

        provider.GetServices<ChatClientFactoryRegistration>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatClientFactoryRegistration>(registration =>
                registration.Provider == "Gemini" &&
                registration.Factory is GeminiChatClientFactory);
    }

    [Fact]
    public void Factory_Should_Create_Native_Gemini_Chat_Client_Without_Executing_A_Request()
    {
        var services = new ServiceCollection();
        services.Configure<GeminiOptions>(options =>
        {
            options.ApiKey = "test-api-key";
            options.Model = "gemini-2.5-flash";
        });

        using var provider = services.BuildServiceProvider();
        var factory = new GeminiChatClientFactory(
            provider,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>());

        factory.Create("gemini-2.5-flash")
            .Should().BeAssignableTo<IChatClient>();
    }
}
