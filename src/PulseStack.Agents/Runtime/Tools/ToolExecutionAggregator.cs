using PulseStack.Abstractions.Runtime.Tools;
using ToolResultRecord = PulseStack.Abstractions.Tools.ToolExecutionRecord;

namespace PulseStack.Agents.Runtime.Tools;

public sealed class ToolExecutionAggregator
{
    public ToolExecutionSummary Aggregate(
        IEnumerable<ToolResultRecord> toolResults)
    {
        ArgumentNullException.ThrowIfNull(toolResults);

        var executions =
            toolResults
                .Select(toolResult => new ToolExecutionRecord
                {
                    ToolName =
                        string.IsNullOrWhiteSpace(toolResult.Result.Metadata.ToolName)
                            ? toolResult.ToolName
                            : toolResult.Result.Metadata.ToolName,

                    Category =
                        toolResult.Result.Metadata.Category,

                    Success =
                        toolResult.Result.Metadata.Success
                        || toolResult.Result.IsSuccess,

                    Duration =
                        toolResult.Result.Metadata.Duration
                })
                .ToList();

        return new ToolExecutionSummary
        {
            TotalInvocations =
                executions.Count,

            SuccessfulExecutions =
                executions.Count(execution => execution.Success),

            FailedExecutions =
                executions.Count(execution => !execution.Success),

            TotalDuration =
                TimeSpan.FromTicks(
                    executions.Sum(execution => execution.Duration.Ticks)),

            Executions =
                executions
        };
    }
}
