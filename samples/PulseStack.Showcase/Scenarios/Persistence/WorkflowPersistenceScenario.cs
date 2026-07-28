using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Abstractions.Workflows;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios.Persistence;

internal static class WorkflowPersistenceScenario
{
    public static async Task RunAsync(IServiceProvider services)
    {
        ShowcaseConsole.Section("Workflow Persistence Showcase");


        // Resolve services

        var mapper =
            services.GetRequiredService<IWorkflowMapper>();

        var serializer =
            services.GetRequiredService<IWorkflowSerializer>();

        var deserializer =
            services.GetRequiredService<IWorkflowDeserializer>();

        var validator =
            services.GetRequiredService<IWorkflowValidator>();

        var store =
            services.GetRequiredService<IWorkflowStore>();

        var agentResolver =
            services.GetRequiredService<IAgentResolver>();

        // Create workflow
        
        ShowcaseConsole.Info("Creating workflow...");
        var workflow = ShowcaseWorkflowFactory.CreateCustomerApprovalWorkflow();
        ShowcaseConsole.Success("Workflow created"); 


        // Map
        ShowcaseConsole.Info("Mapping workflow...");
        var document = mapper.ToDocument(workflow);
        ShowcaseConsole.Success("Workflow mapped");

        // Validate
        ShowcaseConsole.Info("Validating workflow...");
        var validation = await validator.ValidateAsync(document);
        
        if (!validation.IsValid)
        {
            ShowcaseConsole.Error("Validation failed");

            foreach (var error in validation.Errors)
            {
               ShowcaseConsole.Error(error.Message);
            }

            return;
        }

        ShowcaseConsole.Success("Validation succeeded");

        // Serialize
        ShowcaseConsole.Info("Serializing workflow...");
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(document, stream);
        ShowcaseConsole.Success("Workflow serialized");

        // Store
        ShowcaseConsole.Info("Saving workflow...");
        await store.SaveAsync(workflow.Identity.Id, stream);
        ShowcaseConsole.Success("Workflow saved");

        // Load
        ShowcaseConsole.Info("Loading workflow...");
        using var loaded = await store.LoadAsync(workflow.Identity.Id);
        if (loaded is null)
        {
            ShowcaseConsole.Error("Workflow not found");
            return;
        }
        
        ShowcaseConsole.Success("Workflow loaded");

        // Deserialize
        ShowcaseConsole.Info("Deserializing workflow...");
        var loadedDocument =
            await deserializer.DeserializeAsync(loaded!);
        ShowcaseConsole.Success("Workflow deserialized");

        // Reconstruct
        ShowcaseConsole.Info("Reconstructing workflow...");
        var reconstructed =
            mapper.FromDocument(
                loadedDocument,
                agentResolver);

        ShowcaseConsole.Success("Workflow reconstructed");        

        // Print summary
        Console.WriteLine();
        Console.WriteLine("Workflow Summary");
        Console.WriteLine(new string('-', 60));

        Console.WriteLine($"Name        : {reconstructed.Definition.Name}");
        Console.WriteLine($"Version     : {reconstructed.Identity.Version}");
        Console.WriteLine($"Description : {reconstructed.Definition.Description}");
        Console.WriteLine($"Steps       : {reconstructed.Steps.Count}");

        Console.WriteLine();

        ShowcaseConsole.Success(
            "Workflow persistence completed successfully.");
    }
     
}