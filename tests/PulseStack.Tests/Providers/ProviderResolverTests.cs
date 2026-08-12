using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Providers;
using PulseStack.Core.Providers;
using Xunit;

namespace PulseStack.Tests.Providers;

public sealed class ProviderResolverTests
{
    [Fact]
    public void Resolve_Should_Return_Registered_Provider_Factory()
    {
        var factory = new TestChatClientFactory();
        var registry = new TestFactoryRegistry("OpenAI", factory);
        var resolver = new ProviderResolver(registry);

        resolver.Resolve("OpenAI")
            .Should()
            .BeSameAs(factory);
    }

    [Fact]
    public void Resolve_Should_Reject_Missing_Provider()
    {
        var registry = new TestFactoryRegistry();
        var resolver = new ProviderResolver(registry);

        var action = () => resolver.Resolve("Missing");

        action.Should()
            .Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_Should_Reject_Invalid_Provider(string? provider)
    {
        var resolver = new ProviderResolver(new TestFactoryRegistry());

        var action = () => resolver.Resolve(provider!);

        action.Should()
            .Throw<ArgumentException>();
    }

    private sealed class TestFactoryRegistry : IChatClientFactoryRegistry
    {
        private readonly string? _provider;
        private readonly IChatClientFactory? _factory;

        public TestFactoryRegistry(
            string? provider = null,
            IChatClientFactory? factory = null)
        {
            _provider = provider;
            _factory = factory;
        }

        public void Register(string provider, IChatClientFactory factory)
            => throw new NotSupportedException();

        public IChatClientFactory Resolve(string provider)
        {
            if (_provider is null ||
                !string.Equals(_provider, provider, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"No chat client factory is registered for provider '{provider}'.");
            }

            return _factory!;
        }
    }

    private sealed class TestChatClientFactory : IChatClientFactory
    {
        public IChatClient Create(string model)
            => throw new NotSupportedException();
    }
}
