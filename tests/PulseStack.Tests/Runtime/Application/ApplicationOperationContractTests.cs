using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Application;

public sealed class ApplicationOperationContractTests
{
    [Fact]
    public void OperationContract_ShouldExposeExactlyTheFrozenExecuteAsyncSignature()
    {
        var methods = typeof(IApplicationOperation).GetMethods();

        methods.Should().ContainSingle();
        var method = methods[0];
        method.Name.Should().Be("ExecuteAsync");
        method.ReturnType.Should().Be<Task<ApplicationOperationResult>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be<AssetDefinitionKey>();
        parameters[0].Name.Should().Be("projectKey");
        parameters[1].ParameterType.Should().Be<ApplicationInvocationRequest>();
        parameters[1].Name.Should().Be("request");
        parameters[2].ParameterType.Should().Be<CancellationToken>();
        parameters[2].Name.Should().Be("cancellationToken");
        parameters[2].HasDefaultValue.Should().BeTrue();
        parameters[2].DefaultValue.Should().BeNull();
    }

    [Fact]
    public void ResultAlgebra_ShouldExposeExactlyTheFrozenClosedVariants()
    {
        var baseType = typeof(ApplicationOperationResult);
        var authorizedVariants = new[]
        {
            typeof(ApplicationOperationResult.InvocationOutcome),
            typeof(ApplicationOperationResult.LoadOutcome),
            typeof(ApplicationOperationResult.RealizationOutcome)
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
    public void LoadOutcome_ShouldRejectNullAndSuccessfulLoadResult()
    {
        new Action(() => new ApplicationOperationResult.LoadOutcome(null!))
            .Should().Throw<ArgumentNullException>();

        var success = CreateLoadSuccess();
        new Action(() => new ApplicationOperationResult.LoadOutcome(success))
            .Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("result");
    }

    [Fact]
    public void LoadOutcome_ShouldPreserveExactNonSuccessResult()
    {
        AIAssetGraphLoadResult result = new AIAssetGraphLoadResult.RootDefinitionUnavailable(
            new AIAssetGraphRootDefinitionUnavailableContext(Key(AssetType.Project)));

        var outcome = new ApplicationOperationResult.LoadOutcome(result);

        outcome.Result.Should().BeSameAs(result);
    }

    [Fact]
    public void RealizationOutcome_ShouldRejectNullAndSuccessfulRealizationResult()
    {
        new Action(() => new ApplicationOperationResult.RealizationOutcome(null!))
            .Should().Throw<ArgumentNullException>();

        var success = CreateRealizationSuccess();
        new Action(() => new ApplicationOperationResult.RealizationOutcome(success))
            .Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("result");
    }

    [Fact]
    public void RealizationOutcome_ShouldPreserveExactNonSuccessResult()
    {
        ApplicationRealizationResult result = new ApplicationRealizationResult.UnsupportedRoot(
            new ApplicationRealizationUnsupportedRootContext(Key(AssetType.Library)));

        var outcome = new ApplicationOperationResult.RealizationOutcome(result);

        outcome.Result.Should().BeSameAs(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvocationOutcome_ShouldAcceptAndPreserveResultRegardlessOfSuccess(bool success)
    {
        var result = new ApplicationInvocationResult(
            Reference(AssetType.Project),
            Reference(AssetType.Workflow),
            success,
            "output",
            Array.Empty<PulseStack.Abstractions.Workflows.Steps.StepExecutionResult>());

        var outcome = new ApplicationOperationResult.InvocationOutcome(result);

        outcome.Result.Should().BeSameAs(result);
        outcome.Result.Success.Should().Be(success);
    }

    [Fact]
    public void InvocationOutcome_ShouldRejectNull()
    {
        new Action(() => new ApplicationOperationResult.InvocationOutcome(null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static AIAssetGraphLoadResult.Success CreateLoadSuccess()
    {
        var graphType = typeof(AIAssetGraph);
        var constructor = graphType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();
        var parameters = constructor.GetParameters();
        var arguments = parameters
            .Select(parameter => CreateArgument(parameter.ParameterType))
            .ToArray();
        var graph = (AIAssetGraph)constructor.Invoke(arguments);
        return new AIAssetGraphLoadResult.Success(graph);
    }

    private static ApplicationRealizationResult.Success CreateRealizationSuccess()
    {
        var realizedType = typeof(RealizedApplication);
        var constructor = realizedType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();
        var parameters = constructor.GetParameters();
        var arguments = parameters
            .Select(parameter => CreateArgument(parameter.ParameterType))
            .ToArray();
        var application = (RealizedApplication)constructor.Invoke(arguments);
        return new ApplicationRealizationResult.Success(application);
    }

    private static object? CreateArgument(Type type)
    {
        if (type == typeof(AssetDefinitionKey))
        {
            return Key(AssetType.Project);
        }

        if (type == typeof(AssetReference))
        {
            return Reference(AssetType.Workflow);
        }

        if (type == typeof(PulseStack.Abstractions.Workflows.Workflow))
        {
            return new PulseStack.Abstractions.Workflows.Workflow("entry");
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()));
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        }

        if (type == typeof(string))
        {
            return "value";
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        throw new InvalidOperationException($"No contract-test argument factory exists for {type}.");
    }

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), AssetVersion.Initial);

    private static AssetReference Reference(AssetType type) =>
        new(type, AssetId.New(), new AssetUrn($"urn:pulsestack:test:{Guid.NewGuid():N}"), AssetVersion.Initial);
}
