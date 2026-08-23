namespace PulseStack.Abstractions.Models;

public interface IModelCatalogSource
{
    IReadOnlyCollection<ProviderModelDescriptor> GetModels();
}