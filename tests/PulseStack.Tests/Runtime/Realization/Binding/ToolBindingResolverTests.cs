using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Binding;
using PulseStack.Core.Tools;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Binding;

public sealed class ToolBindingResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnBoundRuntimeTool()
    {
        var asset = CreateToolAsset();
        var tool = new StubTool("calculator");
        var registry = new ToolRegistry();
        registry.Register(tool);

        var resolver = new ToolBindingResolver(
            [new ToolBindingRegistration(Reference(asset), tool.Name)],
            registry);

        resolver.Resolve(asset).Should().BeSameAs(tool);
    }

    [Fact]
    public void Resolve_ShouldRejectUnboundToolAsset()
    {
        var asset = CreateToolAsset();
        var resolver = new ToolBindingResolver([], new ToolRegistry());

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetType()
    {
        var asset = CreateToolAsset();
        var tool = new StubTool("calculator");
        var registry = new ToolRegistry();
        registry.Register(tool);
        var resolver = new ToolBindingResolver(
            [new ToolBindingRegistration(
                new AssetReference(AssetType.Knowledge, asset.Id, asset.Urn, asset.Version),
                tool.Name)],
            registry);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetVersion()
    {
        var asset = CreateToolAsset();
        var tool = new StubTool("calculator");
        var registry = new ToolRegistry();
        registry.Register(tool);
        var resolver = new ToolBindingResolver(
            [new ToolBindingRegistration(
                new AssetReference(asset.Type, asset.Id, asset.Urn, new AssetVersion("2.0.0")),
                tool.Name)],
            registry);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static ToolAsset CreateToolAsset() =>
        new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = "Calculator",
                Description = "Performs calculations.",
                Category = "Utilities"
            });

    private sealed class StubTool(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => "Stub tool";
        public string Category => "Tests";
        public IReadOnlyCollection<string> Tags => [];
        public ToolDescriptor Descriptor => new()
        {
            Name = Name,
            Description = Description
        };

        public Task<IToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
