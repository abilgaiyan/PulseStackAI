namespace PulseStack.Abstractions.Tools;

public interface IToolRegistry
{
    IReadOnlyCollection<ITool> GetAll();

    ITool? GetByName(string name);

    IReadOnlyCollection<ITool> GetByTag(string tag);

    void Register(ITool tool);
}