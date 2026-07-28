using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.WorkflowPackages.Packaging;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios.Persistence;

internal static class WorkflowPackageScenario
{
    public static async Task RunAsync(IServiceProvider services)
    {
        ShowcaseConsole.Section("Workflow Package Showcase");

         var mapper =
            services.GetRequiredService<IWorkflowMapper>();

        var serializer =
            services.GetRequiredService<IWorkflowSerializer>();

        var deserializer =
            services.GetRequiredService<IWorkflowDeserializer>();

        var validator =
            services.GetRequiredService<IWorkflowValidator>();

        var agentResolver =
            services.GetRequiredService<IAgentResolver>();            

        var builder =
            new ZipWorkflowPackageBuilder(validator, mapper, serializer);

        var reader =
            new ZipWorkflowPackageReader(mapper, deserializer, agentResolver);

        var store =
            services.GetRequiredService<IWorkflowPackageStore>();

        ShowcaseConsole.Info("Creating package...");
        var package = ShowcasePackageFactory.CreatePackage();
        ShowcaseConsole.Success("Package created"); 

        ShowcaseConsole.Info("Building package...");
        using var packageStream =
            await builder.BuildAsync(package);

        ShowcaseConsole.Success("Package built");    

        ShowcaseConsole.Info("Saving package...");
        await store.SaveAsync(
            package.Identity.Id,
            packageStream);
         ShowcaseConsole.Success("Package saved");

        ShowcaseConsole.Info("Loading package...");
        using var loaded =
            await store.LoadAsync(package.Identity.Id);

        ShowcaseConsole.Success("Package loaded");

        ShowcaseConsole.Info("Reading package...");
        var restored =
            await reader.ReadAsync(loaded!);

        ShowcaseConsole.Success("Package restored");    

        // Print summary
        Console.WriteLine();
        Console.WriteLine("Workflow Package");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();

        Console.WriteLine($"Id              : {restored.Identity.Id}");
        Console.WriteLine($"Version         : {restored.Identity.Version}");
        Console.WriteLine($"Title           : {restored.Metadata.Title}");

        // Print Workflow summary 
        Console.WriteLine();
        Console.WriteLine("Workflow");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();

        Console.WriteLine($"Name            : {restored.Workflow.Definition.Name}");
        Console.WriteLine($"Version         : {restored.Workflow.Identity.Version}");
        Console.WriteLine($"Description     : {restored.Workflow.Definition.Description}");
        Console.WriteLine($"Steps           : {restored.Workflow.Steps.Count}");

        Console.WriteLine();

        ShowcaseConsole.Success(
            "Workflow package completed successfully.");

    }
}