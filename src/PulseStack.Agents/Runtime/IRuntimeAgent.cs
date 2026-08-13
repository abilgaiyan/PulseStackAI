namespace PulseStack.Agents.Runtime;

/// <summary>
/// Compatibility bridge for the existing AgentRuntime execution path.
/// New runtime composition uses <see cref="IRuntimeAgentExecutor"/> directly.
/// </summary>
[Obsolete("Use IRuntimeAgentExecutor instead.")]
internal interface IRuntimeAgent : IRuntimeAgentExecutor
{
}
