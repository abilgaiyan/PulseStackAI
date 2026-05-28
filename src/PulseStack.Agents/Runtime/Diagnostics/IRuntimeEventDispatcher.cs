namespace PulseStack.Agents.Runtime.Diagnostics;

public interface IRuntimeEventDispatcher
{
    void Dispatch(
        IRuntimeEvent runtimeEvent);
}
