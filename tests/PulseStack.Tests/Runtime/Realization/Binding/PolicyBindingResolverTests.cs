using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Policies;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Binding;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Binding;

public sealed class PolicyBindingResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnBoundRuntimePolicy()
    {
        var asset = CreatePolicyAsset();
        var policy = new StubRuntimePolicy("restricted-tools");
        var resolver = new PolicyBindingResolver(
            [new PolicyBindingRegistration(Reference(asset), policy.Name)],
            [policy]);

        resolver.Resolve(asset).Should().BeSameAs(policy);
    }

    [Fact]
    public void Resolve_ShouldRejectUnboundPolicyAsset()
    {
        var asset = CreatePolicyAsset();
        var resolver = new PolicyBindingResolver([], []);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetType()
    {
        var asset = CreatePolicyAsset();
        var policy = new StubRuntimePolicy("restricted-tools");
        var resolver = new PolicyBindingResolver(
            [new PolicyBindingRegistration(
                new AssetReference(AssetType.Memory, asset.Id, asset.Urn, asset.Version),
                policy.Name)],
            [policy]);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetVersion()
    {
        var asset = CreatePolicyAsset();
        var policy = new StubRuntimePolicy("restricted-tools");
        var resolver = new PolicyBindingResolver(
            [new PolicyBindingRegistration(
                new AssetReference(asset.Type, asset.Id, asset.Urn, new AssetVersion("2.0.0")),
                policy.Name)],
            [policy]);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static PolicyAsset CreatePolicyAsset() =>
        new PolicyAssetFactory().Create(
            new PolicyAssetOptions
            {
                Name = "Restricted Tools",
                Description = "Restricts sensitive tool usage."
            });

    private sealed class StubRuntimePolicy(string name) : IRuntimePolicy
    {
        public string Name { get; } = name;
    }
}
