using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Core.Runtime.Realization.Binding;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class ConditionBindingResolverTests
{
    [Fact]
    public void Resolve_ShouldBindNamedCondition()
    {
        var condition = new StubCondition("requires-approval");
        var resolver = new ConditionBindingResolver(
        [
            new ConditionBindingRegistration(
                "requires-approval",
                condition)
        ]);

        var result = resolver.Resolve(
            new NamedConditionDefinition
            {
                Name = "requires-approval"
            });

        result.Should().BeSameAs(condition);
    }

    [Fact]
    public void Resolve_ShouldRejectUnregisteredCondition()
    {
        var resolver = new ConditionBindingResolver([]);

        var action = () => resolver.Resolve(
            new NamedConditionDefinition
            {
                Name = "missing-condition"
            });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing-condition*not registered*");
    }

    private sealed class StubCondition(string name) : ICondition
    {
        public string Name { get; } = name;

        public ValueTask<bool> EvaluateAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}
