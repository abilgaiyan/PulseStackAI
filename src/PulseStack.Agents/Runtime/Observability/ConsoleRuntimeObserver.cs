using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime.Observability;

public sealed class ConsoleRuntimeObserver
    : IRuntimeObserver
{
    public Task OnEventAsync(
        IRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        switch (runtimeEvent)
        {
            case PipelineStartedEvent pipelineStarted:
                WriteHeader("Pipeline Started");
                WriteLine("Pipeline", pipelineStarted.PipelineName);
                WriteLine("ExecutionId", pipelineStarted.ExecutionId);
                WriteLine("Agents", pipelineStarted.AgentCount);
                break;

            case PipelineCompletedEvent pipelineCompleted:
                WriteHeader("Pipeline Completed");
                WriteLine("Pipeline", pipelineCompleted.PipelineName);
                WriteLine("ExecutionId", pipelineCompleted.ExecutionId);
                WriteLine("SuccessfulAgents", pipelineCompleted.SuccessfulAgentCount);
                WriteLine("FailedAgents", pipelineCompleted.FailedAgentCount);
                break;

            case AgentStartedEvent agentStarted:
                WriteHeader("Agent Started");
                WriteLine("Agent", agentStarted.AgentName ?? "Unnamed");
                WriteOptionalLine("Model", agentStarted.Model);
                WriteOptionalLine("BranchId", agentStarted.BranchId);
                break;

            case AgentCompletedEvent agentCompleted:
                WriteHeader("Agent Completed");
                WriteLine("Agent", agentCompleted.AgentName ?? "Unnamed");
                WriteLine("Success", agentCompleted.IsSuccess);
                WriteOptionalLine("Error", agentCompleted.ErrorMessage);
                break;

            case ToolExecutingEvent toolExecuting:
                WriteHeader("Tool Executing");
                WriteLine("Tool", toolExecuting.ToolName);
                WriteOptionalLine("Agent", toolExecuting.AgentName);
                break;

            case ToolExecutedEvent toolExecuted:
                WriteHeader("Tool Executed");
                WriteLine("Tool", toolExecuted.ToolName);
                WriteLine("Success", toolExecuted.IsSuccess);
                WriteOptionalLine("Error", toolExecuted.ErrorMessage);
                break;

            case AgentRetryEvent retry:
                Console.WriteLine();
                Console.WriteLine("[Agent Retry]");
                Console.WriteLine($"Agent : {retry.AgentName}");
                Console.WriteLine($"Attempt : {retry.Attempt}");
                Console.WriteLine($"Reason : {retry.Error}");
                break;    

            case ConditionEvaluatedEvent conditionEvaluated:
                WriteHeader("Condition Evaluated");
                WriteLine("Condition", conditionEvaluated.ConditionName);
                WriteLine("Result",    conditionEvaluated.Result);
                break;                            
        }

        return Task.CompletedTask;
    }

    private static void WriteHeader(
        string text)
    {
        Console.WriteLine();
        Console.WriteLine($"[{text}]");
    }

    private static void WriteLine(
        string name,
        object? value)
        => Console.WriteLine($"{name} : {value}");

    private static void WriteOptionalLine(
        string name,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        WriteLine(name, value);
    }
}
