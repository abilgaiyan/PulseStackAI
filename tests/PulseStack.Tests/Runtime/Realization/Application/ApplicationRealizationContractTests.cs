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
        var authorizedVariants = new[]
        {
            typeof(ApplicationRealizationResult.EntryWorkflowTypeIncoherent),
            typeof(ApplicationRealizationResult.EntryWorkflowUnresolved),
            typeof(ApplicationRealizationResult.Success),
            typeof(ApplicationRealizationResult.UnsupportedRoot)
        }
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToArray();

        var publicNestedVariants = baseType
            .GetNestedTypes(BindingFlags.Public)
            .Where(type => type.BaseType == baseType)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        publicNestedVariants.Should().Equal(authorizedVariants);
        authorizedVariants.Should().OnlyContain(static type => type.IsSealed);

        var allDerivedTypes = baseType.Assembly
            .GetTypes()
            .Where(type => type != baseType && baseType.IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        allDerivedTypes.Should().Equal(authorizedVariants);

        var constructors = baseType.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic);

        constructors.Should().ContainSingle();
        constructors[0].IsPrivate.Should().BeTrue();
    }

    [Fact]
    public void Success_ShouldRequireAndPreserveRealizedApplicationAsSolePositivePayload()
    {
        var workflow = new Workflow("entry");
        var application = CreateRealizedApplication(workflow);

        var result = new ApplicationRealizationResult.Success(application);

        result.Application.Should().BeSameAs(application);
        result.Workflow.Should().BeSameAs(application.Workflow);
        result.Workflow.Should().BeSameAs(workflow);

        var constructors = typeof(ApplicationRealizationResult.Success)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        constructors.Should().ContainSingle();
        constructors[0].GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(RealizedApplication));
        constructors.Should().NotContain(constructor =>
            constructor.GetParameters().Select(static parameter => parameter.ParameterType)
                .SequenceEqual(new[] { typeof(Workflow) }));

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

    [Fact]
    public void UnsupportedRootContext_ShouldRejectEveryUnauthorizedRootType()
    {
        var rejectedTypes = Enum.GetValues<AssetType>()
            .Where(type => type is not (AssetType.Library or AssetType.Package))
            .Append((AssetType)int.MaxValue);

        foreach (var type in rejectedTypes)
        {
            var action = () => new ApplicationRealizationUnsupportedRootContext(
                DefinitionKey(type));

            action.Should().Throw<ArgumentException>();
        }
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

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectEveryNonProjectRootType()
    {
        var entryWorkflow = Reference(AssetType.Workflow);
        var rejectedRootTypes = Enum.GetValues<AssetType>()
            .Where(type => type != AssetType.Project)
            .Append((AssetType)int.MaxValue);

        foreach (var rootType in rejectedRootTypes)
        {
            AssertEntryContextRejected(
                DefinitionKey(rootType),
                entryWorkflow);
        }
    }

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectStructurallyInvalidProjectRoot()
    {
        var entryWorkflow = Reference(AssetType.Workflow);
        var invalidRoots = new[]
        {
            new AssetDefinitionKey(
                AssetType.Project,
                AssetId.Empty,
                new AssetVersion("1.0.0")),
            new AssetDefinitionKey(
                AssetType.Project,
                AssetId.New(),
                null!),
            new AssetDefinitionKey(
                AssetType.Project,
                AssetId.New(),
                new AssetVersion(" "))
        };

        foreach (var rootKey in invalidRoots)
        {
            AssertEntryContextRejected(rootKey, entryWorkflow);
        }
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

    [Fact]
    public void EntryWorkflowContexts_ShouldRejectEveryNonWorkflowReferenceType()
    {
        var rootKey = DefinitionKey(AssetType.Project);
        var rejectedReferenceTypes = Enum.GetValues<AssetType>()
            .Where(type => type != AssetType.Workflow)
            .Append((AssetType)int.MaxValue);

        foreach (var type in rejectedReferenceTypes)
        {
            AssertEntryContextRejected(rootKey, Reference(type));
        }
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
        var factoryType = typeof(IApplicationRealizationChainFactory);
        var method = factoryType.GetMethod(
            nameof(IApplicationRealizationChainFactory.Create));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IWorkflowComposer));
        method.GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(IAssetResolver));

        var forbiddenTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            typeof(IServiceProvider).FullName!,
            "Microsoft.Extensions.DependencyInjection.IServiceCollection",
            "Microsoft.Extensions.DependencyInjection.IServiceScope"
        };

        var exposedTypes = GetCompletePublicSurfaceTypes(factoryType)
            .SelectMany(FlattenTypeShape)
            .Where(static type => type.FullName is not null)
            .ToArray();

        exposedTypes.Should().NotContain(
            type => forbiddenTypeNames.Contains(type.FullName!),
            "portable realization-chain factory APIs must not expose DI/container types");
    }

    private static IEnumerable<Type> GetCompletePublicSurfaceTypes(Type interfaceType)
    {
        foreach (var contractType in new[] { interfaceType }.Concat(interfaceType.GetInterfaces()))
        {
            foreach (var method in contractType.GetMethods())
            {
                yield return method.ReturnType;

                foreach (var parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }

            foreach (var property in contractType.GetProperties())
            {
                yield return property.PropertyType;

                foreach (var parameter in property.GetIndexParameters())
                {
                    yield return parameter.ParameterType;
                }
            }

            foreach (var @event in contractType.GetEvents())
            {
                if (@event.EventHandlerType is not null)
                {
                    yield return @event.EventHandlerType;
                }
            }
        }
    }

    private static IEnumerable<Type> FlattenTypeShape(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nestedType in FlattenTypeShape(elementType))
            {
                yield return nestedType;
            }
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var nestedType in FlattenTypeShape(genericArgument))
            {
                yield return nestedType;
            }
        }
    }

    private static AssetDefinitionKey DefinitionKey(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0.0"));

    private static AssetReference Reference(AssetType type) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test"),
            new AssetVersion("1.0.0"));

    private static RealizedApplication CreateRealizedApplication(Workflow workflow)
    {
        var constructor = typeof(RealizedApplication)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();

        return (RealizedApplication)constructor.Invoke(
            [Reference(AssetType.Project), Reference(AssetType.Workflow), workflow]);
    }

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
