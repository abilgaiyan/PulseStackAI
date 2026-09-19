using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.OpenRouter.DependencyInjection;
using PulseStack.Providers.OpenRouter.Factories;
using Xunit;

namespace PulseStack.Tests.Providers.OpenRouter;

public sealed class OpenRouterProviderTests
{
    [Fact]
    public void UseOpenRouter_ShouldRegisterChatClientFactory()
    {
        var services = new ServiceCollection();

        services.UseOpenRouter(
            apiKey: "test-api-key",
            model: "deepseek/deepseek-chat-v3-0324");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<OpenRouterChatClientFactory>()
            .Should().NotBeNull();

        provider.GetServices<ChatClientFactoryRegistration>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatClientFactoryRegistration>(registration =>
                registration.Provider == "OpenRouter" &&
                registration.Factory is OpenRouterChatClientFactory);
    }

    [Fact]
    public void Factory_ShouldCreateChatClientWithoutExecutingRequest()
    {
        var services = new ServiceCollection();

        services.UseOpenRouter(
            apiKey: "test-api-key",
            model: "deepseek/deepseek-chat-v3-0324");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<OpenRouterChatClientFactory>()
            .Create("deepseek/deepseek-chat-v3-0324")
            .Should()
            .BeAssignableTo<IChatClient>();
    }
}
