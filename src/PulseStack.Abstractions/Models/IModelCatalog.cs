namespace PulseStack.Abstractions.Models;

public interface IModelCatalog
{
    IReadOnlyCollection<ProviderModelDescriptor> GetModels();

    bool Contains(
        string provider,
        string model);
}
