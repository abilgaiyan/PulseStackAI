using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class ApplicationRealizationContractTests
{
    [Fact]
    public void ResultAlgebra_ShouldExposeExactlyTheFrozenClosedVariants()
    {
        var baseType = typeof(ApplicationRealizationResult);
        var variants = baseType
            .GetNestedTypes(BindingFlags.Public)
            .Where(type => type.BaseType == baseType)
            .Select(type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        variants.Should().Equal(
            nameof(ApplicationRealizationResult.EntryWorkflowTypeIncoherent),
            nameof(ApplicationRealizationResult.EntryWorkflowUnresolved),
            nameof(ApplicationRealizationResult.Success),
            nameof(ApplicationRealizationResult.UnsupportedRoot));

        var constructors = baseType.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic);

        constructors.Should().ContainSingle();
        constructors[0].IsPrivate.Should().BeTrue();
    }

    [Fact]
    public void Success_ShouldRequireAndPreserveExistingWorkflow()
    {
        var workflow = new Workflow("entry");

        var result = new ApplicationRealizationResult.Success(workflow);

        result.Workflow.Should().BeSameAs(workflow);

        var action = () => new ApplicationRealizationResult.Success(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    public void UnsupportedRootContext_ShouldAcceptOnlyCurrentlyUnsupportedAggregateRoots(
        AssetType type)
    {
        var rootKey = DefinitionKey(type);

        var context = new ApplicationRealizationUnsupportedRootContext(rootKey);

        context.RootKey.Should().Be(rootKey);
    }

    [Theory]
    [InlineData(AssetType.Project)]
    [InlineData(AssetType.Workflow)]
    [InlineData(AssetType.Agent)]
    [InlineData(AssetType.Provider)]
    public void UnsupportedRootContext_ShouldRejectOtherRootTypes(AssetType type)
    {
        var action = () => new ApplicationRealizationUnsupportedRootContext(
            DefinitionKey(type));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnsupportedRootContext_ShouldRejectStructurallyInvalidRootKey()
    {
        var emptyId = new AssetDefinitionKey(
            AssetType.Library,
            AssetId.Empty,
            new AssetVersion("1.0.0"));
        var missingVersion = new AssetDefinitionKey(
            AssetType.Library,
            AssetId.New(),
            null!);
        var blankVersion = new AssetDefinitionKey(
            AssetType.Library,
            AssetId.New(),
            new AssetVersion(" "));
        var undefinedType = new AssetDefinitionKey(
            (AssetType)int.MaxValue,
            AssetId.New(),
            new AssetVersion("1.0.0"));

        new Action(() => new ApplicationRealizationUnsupportedRootContext(emptyId))
            .Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationUnsupportedRootContext(missingVersion))
            .Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationUnsupportedRootContext(blankVersion))
            .Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationUnsupportedRootContext(undefinedType))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntryWorkflowContexts_ShouldPreserveExactAuthoredReference()
    {
        var rootKey = DefinitionKey(AssetType.Project);
        var entryWorkflow = Reference(AssetType.Workflow);

        var unresolved = new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            entryWorkflow);
        var incoherent = new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            entryWorkflow);

        unresolved.RootKey.Should().Be(rootKey);
        unresolved.EntryWorkflow.Should().BeSameAs(entryWorkflow);
        incoherent.RootKey.Should().Be(rootKey);
        incoherent.EntryWorkflow.Should().BeSameAs(entryWorkflow);
    }

    [Theory]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    [InlineData(AssetType.Workflow)]
    public void EntryWorkflowContexts_ShouldRejectNonProjectRoot(AssetType rootType)
    {
        var rootKey = DefinitionKey(rootType);
        var entryWorkflow = Reference(AssetType.Workflow);

        new Action(() => new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            entryWorkflow)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            entryWorkflow)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectStructurallyInvalidProjectRoot()
    {
        var rootKey = new AssetDefinitionKey(
            AssetType.Project,
            AssetId.Empty,
            new AssetVersion("1.0.0"));
        var entryWorkflow = Reference(AssetType.Workflow);

        new Action(() => new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            entryWorkflow)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            entryWorkflow)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectNullReference()
    {
        var rootKey = DefinitionKey(AssetType.Project);

        new Action(() => new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            null!)).Should().Throw<ArgumentNullException>();
        new Action(() => new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            null!)).Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(AssetType.Agent)]
    [InlineData(AssetType.Project)]
    [InlineData(AssetType.Provider)]
    public void EntryWorkflowContexts_ShouldRejectNonWorkflowReference(AssetType type)
    {
        var rootKey = DefinitionKey(AssetType.Project);
        var reference = Reference(type);

        new Action(() => new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            reference)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            reference)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectMalformedWorkflowReference()
    {
        var rootKey = DefinitionKey(AssetType.Project);
        var emptyId = new AssetReference(
            AssetType.Workflow,
            AssetId.Empty,
            new AssetUrn("urn:pulsestack:workflow:entry"),
            new AssetVersion("1.0.0"));
        var missingUrn = new AssetReference(
            AssetType.Workflow,
            AssetId.New(),
            null!,
            new AssetVersion("1.0.0"));
        var blankUrn = new AssetReference(
            AssetType.Workflow,
            AssetId.New(),
            new AssetUrn(" "),
            new AssetVersion("1.0.0"));
        var missingVersion = new AssetReference(
            AssetType.Workflow,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:workflow:entry"),
            null!);
        var blankVersion = new AssetReference(
            AssetType.Workflow,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:workflow:entry"),
            new AssetVersion(" "));

        AssertEntryContextRejected(rootKey, emptyId);
        AssertEntryContextRejected(rootKey, missingUrn);
        AssertEntryContextRejected(rootKey, blankUrn);
        AssertEntryContextRejected(rootKey, missingVersion);
        AssertEntryContextRejected(rootKey, blankVersion);
    }

    [Fact]
    public void FailureResults_ShouldRequireAndPreserveTheirExactContext()
    {
        var rootKey = DefinitionKey(AssetType.Project);
        var entryWorkflow = Reference(AssetType.Workflow);
        var unsupportedContext = new ApplicationRealizationUnsupportedRootContext(
            DefinitionKey(AssetType.Library));
        var unresolvedContext = new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            entryWorkflow);
        var incoherentContext = new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            entryWorkflow);

        new ApplicationRealizationResult.UnsupportedRoot(unsupportedContext)
            .Context.Should().BeSameAs(unsupportedContext);
        new ApplicationRealizationResult.EntryWorkflowUnresolved(unresolvedContext)
            .Context.Should().BeSameAs(unresolvedContext);
        new ApplicationRealizationResult.EntryWorkflowTypeIncoherent(incoherentContext)
            .Context.Should().BeSameAs(incoherentContext);

        new Action(() => new ApplicationRealizationResult.UnsupportedRoot(null!))
            .Should().Throw<ArgumentNullException>();
        new Action(() => new ApplicationRealizationResult.EntryWorkflowUnresolved(null!))
            .Should().Throw<ArgumentNullException>();
        new Action(() => new ApplicationRealizationResult.EntryWorkflowTypeIncoherent(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OperationContract_ShouldExposeGraphCancellationAndClosedResultOnly()
    {
        var method = typeof(IApplicationRealizer).GetMethod(
            nameof(IApplicationRealizer.RealizeAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ApplicationRealizationResult>));
        method.GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(AIAssetGraph), typeof(CancellationToken));
    }

    [Fact]
    public void ChainFactoryContract_ShouldRequireExplicitResolverAndExposeNoContainerTypes()
    {
        var method = typeof(IApplicationRealizationChainFactory).GetMethod(
            nameof(IApplicationRealizationChainFactory.Create));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IWorkflowComposer));
        method.GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(IAssetResolver));

        typeof(IApplicationRealizationChainFactory).AssemblyQualifiedName
            .Should().NotContain("Microsoft.Extensions.DependencyInjection");
    }

    private static AssetDefinitionKey DefinitionKey(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0.0"));

    private static AssetReference Reference(AssetType type) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test"),
            new AssetVersion("1.0.0"));

    private static void AssertEntryContextRejected(
        AssetDefinitionKey rootKey,
        AssetReference reference)
    {
        new Action(() => new ApplicationRealizationEntryWorkflowUnresolvedContext(
            rootKey,
            reference)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
            rootKey,
            reference)).Should().Throw<ArgumentException>();
    }
}
