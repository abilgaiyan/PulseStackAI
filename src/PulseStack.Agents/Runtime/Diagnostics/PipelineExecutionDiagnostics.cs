namespace PulseStack.Abstractions.Runtime.Diagnostics;
public sealed class PipelineExecutionDiagnostics
{
    public List<ConditionExecutionRecord> Conditions { get; } = [];
}