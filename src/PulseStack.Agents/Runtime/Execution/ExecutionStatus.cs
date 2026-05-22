namespace PulseStack.Agents.Runtime.Execution;

public enum ExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    PartialSuccess = 3,
    Failed = 4,
    Cancelled = 5
}
