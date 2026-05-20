using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Models;

namespace PulseStack.Agents.Runtime.Tools;

internal sealed class ToolExecutionLoop
{
    private readonly IToolRegistry? _tools;
    private readonly ToolExecutor _toolExecutor;

    public ToolExecutionLoop(
        IToolRegistry? tools,
        ToolExecutor toolExecutor)
    {
        _tools = tools;
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    public async Task<ToolExecutionLoopResult> ExecuteAsync(
        string responseText,
        AgentExecutionContext context)
    {
        var toolCalls = ExtractToolCalls(responseText);

        if (toolCalls.Count == 0 || _tools is null)
        {
            return ToolExecutionLoopResult.NoToolCalls();
        }

        var messages = new List<ChatMessage>();

        foreach (var toolCall in toolCalls)
        {
            messages.Add(await _toolExecutor.ExecuteAsync(
                toolCall,
                _tools,
                context));
        }

        foreach (var message in messages)
        {
            context.ToolExecutionResults.Add(message);
        }

        return ToolExecutionLoopResult.Executed(messages);
    }

    private static IReadOnlyCollection<ToolCall>
        ExtractToolCalls(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var matches = Regex.Matches(
            text,
            @"\{[\s\S]*?""tool""[\s\S]*?""input""[\s\S]*?\}",
            RegexOptions.IgnoreCase);

        var results = new List<ToolCall>();

        foreach (Match match in matches)
        {
            try
            {
                var toolCall =
                    JsonSerializer.Deserialize<ToolCall>(
                        match.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (toolCall is not null)
                {
                    results.Add(toolCall);
                }
            }
            catch
            {
                // Ignore invalid tool JSON
            }
        }

        return results;
    }
}

internal sealed class ToolExecutionLoopResult
{
    private ToolExecutionLoopResult(
        bool hasToolCalls,
        IReadOnlyCollection<ChatMessage> messages)
    {
        HasToolCalls = hasToolCalls;
        Messages = messages;
    }

    public bool HasToolCalls { get; }

    public IReadOnlyCollection<ChatMessage> Messages { get; }

    public static ToolExecutionLoopResult NoToolCalls()
        => new(false, []);

    public static ToolExecutionLoopResult Executed(
        IReadOnlyCollection<ChatMessage> messages)
        => new(true, messages);
}
