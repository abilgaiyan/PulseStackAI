using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Tools;

namespace PulseStack.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStack(
        this IServiceCollection services)
    {
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        return services;
    }
}