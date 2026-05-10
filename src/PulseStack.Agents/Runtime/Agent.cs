using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Models;
using PulseStack.Agents.Tools;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : IAgent
{
    private readonly IChatClient _client;
    private readonly string? _instructions;
    private readonly float? _temperature;
    private readonly IToolRegistry? _tools;
    private readonly IConversationMemory? _memory;

    public string Name { get; }
    private const int MaxToolIterations = 3;

    public Agent(
        string name,
        IChatClient client,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        Name = name;
        _client = client;
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
        _memory = memory;
    }

    public async Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(input);

        var options = BuildChatOptions();

        return await ExecuteToolLoopAsync(
            messages,
            options,
            cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(input);

        var options = BuildChatOptions();

        var responseBuilder = new StringBuilder();

        await foreach (var update in _client.GetStreamingResponseAsync(
            messages,
            options,
            cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(update.Text))
                continue;

            responseBuilder.Append(update.Text);

            yield return update.Text;
        }

        PersistAssistantMessage(responseBuilder.ToString());
    }

    // Message Construction
    private List<ChatMessage> BuildMessages(string input)
    {
        var messages = new List<ChatMessage>();

        AddSystemInstructions(messages);

        AddToolInstructions(messages);

        AddMemory(messages);

        var userMessage = new ChatMessage(ChatRole.User, input);

        messages.Add(userMessage);

        _memory?.Add(userMessage);

        return messages;
    }

    private void AddSystemInstructions(List<ChatMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(_instructions))
            return;

        messages.Add(new ChatMessage(
            ChatRole.System,
            _instructions));
    }

    private void AddToolInstructions(List<ChatMessage> messages)
    {
        if (_tools is null)
            return;

       var toolPrompt = ToolPromptBuilder.Build(
                _tools.GetAll()
                    .Where(t => t.IsEnabled)
                    .ToList());

        messages.Add(new ChatMessage(ChatRole.System, toolPrompt));
    }

    private void AddMemory(List<ChatMessage> messages)
    {
        if (_memory is null)
            return;

        messages.AddRange(_memory.Messages);
    }

    // Execution
    private async Task<ChatResponse> ExecuteToolLoopAsync(
        List<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < MaxToolIterations; i++)
        {
            var response = await _client.GetResponseAsync(
                messages,
                options,
                cancellationToken);

            var text = response.Text ?? string.Empty;

            var toolCall = ParseToolCall(text);

            // Final AI response
            if (toolCall is null || _tools is null)
            {
                PersistAssistantMessage(text);

                return response;
            }

            // Resolve tool
            var tool = _tools.GetByName(toolCall.Tool);

            if (tool is null)
            {
                messages.Add(new ChatMessage(
                    ChatRole.Tool,
                    $"Tool '{toolCall.Tool}' not found."));

                continue;
            }

            if (!tool.IsEnabled)
            {
                messages.Add(new ChatMessage(
                    ChatRole.Tool,
                    $"Tool '{tool.Name}' is disabled."));

                continue;
            }

            // Execute tool
            string result;

            try
            {
                result = await tool.ExecuteAsync(
                    toolCall.Input,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                result = $"Tool '{tool.Name}' failed: {ex.Message}";
            }

            // Continue conversation
            var assistantMessage = new ChatMessage(
                ChatRole.Assistant,
                text);

            var toolMessage = new ChatMessage(
                ChatRole.Tool,
                result);

            messages.Add(assistantMessage);
            messages.Add(toolMessage);

            _memory?.Add(assistantMessage);
            _memory?.Add(toolMessage);
        }

        // Fallback final response
        var fallback = await _client.GetResponseAsync(
            messages,
            options,
            cancellationToken);

        PersistAssistantMessage(fallback.Text ?? string.Empty);

        return fallback;
    }

    // Helpers
    private ChatOptions BuildChatOptions()
    {
        var options = new ChatOptions();

        if (_temperature.HasValue)
        {
            options.Temperature = _temperature.Value;
        }

        return options;
    }

    private void PersistAssistantMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _memory?.Add(new ChatMessage(
            ChatRole.Assistant,
            text));
    }

    private static ToolCall? ParseToolCall(string text)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ToolCall>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (string.IsNullOrWhiteSpace(result?.Tool))
                return null;

            return result;
        }
        catch
        {
            return null;
        }
    }
}