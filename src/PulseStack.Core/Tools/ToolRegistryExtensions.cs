using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Core.Tools;

public static class ToolRegistryExtensions
{
    public static ToolRegistry PopulateFromServices(
        this ToolRegistry registry,
        IServiceProvider serviceProvider)
    {
        var tools = serviceProvider.GetServices<ITool>();

        foreach (var tool in tools)
        {
            registry.Register(tool);
        }

        return registry;
    }
}