using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.DependencyInjection;

public static class ToolServiceCollectionExtensions
{
    public static IServiceCollection AddTool<T>(
        this IServiceCollection services)
        where T : class, ITool
    {
        services.AddSingleton<ITool, T>();

        return services;
    }
}