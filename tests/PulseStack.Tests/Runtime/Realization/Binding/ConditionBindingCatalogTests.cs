using System.Collections;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Runtime.Realization.Binding;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Binding;

public sealed class ConditionBindingCatalogTests
{
    [Fact]
    public void Catalog_ShouldAllowZeroRegistrations()
    {
        var catalog = new ConditionBindingCatalog([]);

        catalog.Contains("missing").Should().BeFalse();
    }

    [Fact]
    public void Contains_ShouldUseCaseInsensitiveBindingIdentity()
    {
        var condition = new TrackingCondition("ready");
        var catalog = CreateCatalog("Ready", condition);

        catalog.Contains("Ready").Should().BeTrue();
        catalog.Contains("READY").Should().BeTrue();
        catalog.Contains("ready").Should().BeTrue();
        condition.EvaluationCount.Should().Be(0);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Contains_ShouldReturnFalseForUnknownOrBlankName(string? name)
    {
        var catalog = CreateCatalog("ready", new TrackingCondition("ready"));

        catalog.Contains(name!).Should().BeFalse();
    }

    [Fact]
    public void Resolver_ShouldReturnExactRegisteredConditionInstance()
    {
        var condition = new TrackingCondition("ready");
        var catalog = CreateCatalog("Ready", condition);
        var resolver = new ConditionBindingResolver(catalog);

        var resolved = resolver.Resolve(new NamedConditionDefinition { Name = "READY" });

        resolved.Should().BeSameAs(condition);
        condition.EvaluationCount.Should().Be(0);
    }

    [Fact]
    public void Catalog_ShouldRejectDuplicateNamesIgnoringCase()
    {
        var registrations = new[]
        {
            new ConditionBindingRegistration("Ready", new TrackingCondition("first")),
            new ConditionBindingRegistration("READY", new TrackingCondition("second"))
        };

        var act = () => new ConditionBindingCatalog(registrations);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'READY'*more than once*case-insensitive*");
    }

    [Fact]
    public void Catalog_ShouldRejectNullRegistrationDeterministically()
    {
        ConditionBindingRegistration[] registrations = [null!];

        var act = () => new ConditionBindingCatalog(registrations);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*index 0*cannot be null*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Catalog_ShouldRejectBlankRegistrationName(string name)
    {
        var registrations = new[]
        {
            new ConditionBindingRegistration(name, new TrackingCondition("condition"))
        };

        var act = () => new ConditionBindingCatalog(registrations);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*index 0*non-blank name*");
    }

    [Fact]
    public void Catalog_ShouldRejectNullConditionDeterministically()
    {
        var registrations = new[]
        {
            new ConditionBindingRegistration("ready", null!)
        };

        var act = () => new ConditionBindingCatalog(registrations);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*'ready'*condition instance*");
    }

    [Fact]
    public void Catalog_ShouldMaterializeRegistrationEnumerationExactlyOnce()
    {
        var source = new SingleEnumerationRegistrations(
            new ConditionBindingRegistration("ready", new TrackingCondition("ready")));

        var catalog = new ConditionBindingCatalog(source);

        source.EnumerationCount.Should().Be(1);
        catalog.Contains("ready").Should().BeTrue();
        catalog.Contains("READY").Should().BeTrue();
        source.EnumerationCount.Should().Be(1);
    }

    [Fact]
    public void Catalog_ShouldRemainDetachedFromLaterSourceMutation()
    {
        var first = new TrackingCondition("first");
        var registrations = new List<ConditionBindingRegistration>
        {
            new("first", first)
        };
        var catalog = new ConditionBindingCatalog(registrations);

        registrations.Clear();
        registrations.Add(new ConditionBindingRegistration("second", new TrackingCondition("second")));

        catalog.Contains("first").Should().BeTrue();
        catalog.Contains("second").Should().BeFalse();
        new ConditionBindingResolver(catalog)
            .Resolve(new NamedConditionDefinition { Name = "FIRST" })
            .Should().BeSameAs(first);
    }

    [Fact]
    public void ResolverAndAvailabilityInterface_ShouldShareConcreteCatalogAuthority()
    {
        var catalog = CreateCatalog("ready", new TrackingCondition("ready"));
        IConditionBindingCatalog availability = catalog;
        var resolver = new ConditionBindingResolver(catalog);

        availability.Should().BeSameAs(catalog);
        GetResolverCatalog(resolver).Should().BeSameAs(catalog);
    }

    [Fact]
    public void AddPulseStack_ShouldResolveOneSingletonCatalogSnapshotForBothContracts()
    {
        var condition = new TrackingCondition("ready");
        var services = new ServiceCollection();
        services.AddSingleton(new ConditionBindingRegistration("Ready", condition));
        services.AddPulseStack();
        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<ConditionBindingCatalog>();
        var availability = provider.GetRequiredService<IConditionBindingCatalog>();
        var resolver = provider.GetRequiredService<IConditionBindingResolver>();
        var concreteAgain = provider.GetRequiredService<ConditionBindingCatalog>();

        availability.Should().BeSameAs(concrete);
        concreteAgain.Should().BeSameAs(concrete);
        resolver.Should().BeOfType<ConditionBindingResolver>();
        GetResolverCatalog((ConditionBindingResolver)resolver).Should().BeSameAs(concrete);
        availability.Contains("READY").Should().BeTrue();
        resolver.Resolve(new NamedConditionDefinition { Name = "ready" })
            .Should().BeSameAs(condition);
        condition.EvaluationCount.Should().Be(0);
    }

    [Fact]
    public void Resolver_ShouldPreserveMissingAndBlankNameBehavior()
    {
        var resolver = new ConditionBindingResolver(new ConditionBindingCatalog([]));

        var missing = () => resolver.Resolve(new NamedConditionDefinition { Name = "missing" });
        var blank = () => resolver.Resolve(new NamedConditionDefinition { Name = " " });

        missing.Should().Throw<InvalidOperationException>()
            .WithMessage("Runtime condition 'missing' is not registered.");
        blank.Should().Throw<ArgumentException>();
    }

    private static ConditionBindingCatalog CreateCatalog(string name, ICondition condition)
        => new([new ConditionBindingRegistration(name, condition)]);

    private static ConditionBindingCatalog GetResolverCatalog(ConditionBindingResolver resolver)
        => (ConditionBindingCatalog)typeof(ConditionBindingResolver)
            .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(resolver)!;

    private sealed class TrackingCondition(string name) : ICondition
    {
        public string Name { get; } = name;

        public int EvaluationCount { get; private set; }

        public ValueTask<bool> EvaluateAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            EvaluationCount++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class SingleEnumerationRegistrations(
        params ConditionBindingRegistration[] registrations)
        : IEnumerable<ConditionBindingRegistration>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<ConditionBindingRegistration> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount > 1)
            {
                throw new InvalidOperationException("Registration source was enumerated more than once.");
            }

            return ((IEnumerable<ConditionBindingRegistration>)registrations).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
