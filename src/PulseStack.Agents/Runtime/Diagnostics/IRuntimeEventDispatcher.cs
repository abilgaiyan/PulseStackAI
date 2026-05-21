namespace PulseStack.Agents.Runtime.Diagnostics;

internal interface IRuntimeEventDispatcher
{
    void Dispatch(
        IRuntimeEvent runtimeEvent);
}
