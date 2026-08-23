using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Chat;
using PulseStack.Core.Chat;
using Xunit;

namespace PulseStack.Tests.Chat;

public sealed class ChatClientFactoryRegistryTests
{
    [Fact]
    public void Registry_ShouldRegisterFactory()
    {
        var factory = new FakeChatClientFactory();
        var registry = CreateRegistry();

        registry.Register("TestProvider", factory);

        registry.Resolve("TestProvider").Should().BeSameAs(factory);
    }

    [Fact]
    public void Registry_ShouldResolveFactory()
    {
        var factory = new FakeChatClientFactory();
        var registry = CreateRegistry(("TestProvider", factory));

        var resolved = registry.Resolve("TestProvider");

        resolved.Should().BeSameAs(factory);
    }

    [Fact]
    public void Registry_ShouldResolveProviderCaseInsensitively()
    {
        var factory = new FakeChatClientFactory();
        var registry = CreateRegistry(("TestProvider", factory));

        registry.Resolve("testprovider").Should().BeSameAs(factory);
    }

    [Fact]
    public void Registry_ShouldRejectDuplicateProvider()
    {
        var registry = CreateRegistry(("TestProvider", new FakeChatClientFactory()));

        var action = () => registry.Register("testprovider", new FakeChatClientFactory());

        action.Should().Throw<ArgumentException>()
            .WithMessage("*TestProvider*");
    }

    [Fact]
    public void Registry_ShouldRejectUnknownProvider()
    {
        var registry = CreateRegistry();

        var action = () => registry.Resolve("UnknownProvider");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnknownProvider*");
    }

    private static ChatClientFactoryRegistry CreateRegistry(
        params (string Provider, IChatClientFactory Factory)[] registrations)
        => new(registrations.Select(registration =>
            new ChatClientFactoryRegistration(
                registration.Provider,
                registration.Factory)));

    internal sealed class FakeChatClientFactory : IChatClientFactory
    {
        public string? CreatedModel { get; private set; }

        public IChatClient Create(string model)
        {
            CreatedModel = model;
            return new FakeChatClient();
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => null;

        public void Dispose()
        {
        }
    }
}
