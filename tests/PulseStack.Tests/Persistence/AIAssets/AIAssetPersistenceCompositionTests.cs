using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetPersistenceCompositionTests
{
    [Fact]
    public void AddPulseStack_ShouldRegisterAIAssetPersistenceServices()
    {
        var services = new ServiceCollection();

        services.AddPulseStack();

        using var provider = services.BuildServiceProvider();

        var validator = provider.GetRequiredService<IAIAssetDocumentValidator>();
        var mapper = provider.GetRequiredService<IAIAssetDocumentMapper>();

        validator.Should().BeOfType<AIAssetDocumentValidator>();
        mapper.Should().BeOfType<AIAssetDocumentMapper>();
        provider.GetRequiredService<IAIAssetDocumentValidator>().Should().BeSameAs(validator);
        provider.GetRequiredService<IAIAssetDocumentMapper>().Should().BeSameAs(mapper);
    }
}
