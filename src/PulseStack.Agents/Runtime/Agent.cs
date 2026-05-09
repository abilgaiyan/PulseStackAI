using System.Text.Json;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Models;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : IAgent
{
    private readonly IChatClient _client;
    private readonly string? _instructions;
    private readonly float? _temperature;
    private readonly IToolRegistry? _tools;

    public string Name { get; }

    public Agent(
        string name,
        IChatClient client,
        string? instructions,
        float? temperature,
        IToolRegistry? tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        Name = name;
        _client = client;
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
    }

    public async Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        // Main system instructions
        if (!string.IsNullOrWhiteSpace(_instructions))
        {
            messages.Add(new ChatMessage(ChatRole.System, _instructions));
        }

        // Tool instructions
        if (_tools is not null)
        {
            var toolDescriptions = _tools.GetAll()
                .Select(t => $"{t.Name}: {t.Description}");

           messages.Add(new ChatMessage(
                ChatRole.System,
                $$"""
                Available tools:

                {{string.Join("\n", toolDescriptions)}}

                When a tool is needed, respond ONLY with valid JSON:

                {
                "tool": "tool_name",
                "input": "tool input"
                }

                Do not add explanations.
                """));
        }

        // User input
        messages.Add(new ChatMessage(ChatRole.User, input));

        // Tool execution loop (max 3 attempts)
        for (int i = 0; i < 3; i++)
        {
            var options = new ChatOptions();
            if (_temperature.HasValue)
                options.Temperature = _temperature.Value;

            var response = await _client.GetResponseAsync(
                messages,
                options,
                cancellationToken);

            var text = response.Text ?? string.Empty;

            // Try parse tool call
            var toolCall = ParseToolCall(text);

            // No tool requested → return final response
            if (toolCall is null || _tools is null)
                return response;

            // Resolve and execute tool
            var tool = _tools.GetByName(toolCall.Tool);
            if (tool is null)
            {
                messages.Add(new ChatMessage(
                    ChatRole.Tool,
                    $"Tool '{toolCall.Tool}' not found."));
                continue;
            }

            var result = await tool.ExecuteAsync(toolCall.Input, cancellationToken);

            // Add to conversation history
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.Tool, result));
        }

        // Fallback: final response after max attempts
        return await _client.GetResponseAsync(messages, cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_instructions))
        {
            messages.Add(new ChatMessage(
                ChatRole.System,
                _instructions));
        }

        messages.Add(new ChatMessage(
            ChatRole.User,
            input));

        var options = new ChatOptions();

        if (_temperature.HasValue)
            options.Temperature = _temperature.Value;

        await foreach (var update in _client.GetStreamingResponseAsync(
            messages,
            options,
            cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(update.Text))
                yield return update.Text;
        }
    }
    
    private static ToolCall? ParseToolCall(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<ToolCall>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}