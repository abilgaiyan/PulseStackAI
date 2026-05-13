using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly string? _model;
    public string? Model => _model;
    private const int MaxToolIterations = 3;


    public Agent(
        string name,
        IChatClient client,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        string? model = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        Name = name;
        _client = client;
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
        _memory = memory;
        _model = model;
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

            var toolCalls = ExtractToolCalls(text);

            // Final AI response
            if (toolCalls.Count == 0 || _tools is null)
            {
                PersistAssistantMessage(text);

                return response;
            }

            // Persist assistant reasoning/tool request
            var assistantMessage = new ChatMessage(
                ChatRole.Assistant,
                text);

            messages.Add(assistantMessage);

            _memory?.Add(assistantMessage);

            // Execute ALL tools
            foreach (var toolCall in toolCalls)
            {
                var tool = _tools.GetByName(toolCall.Tool);

                if (tool is null)
                {
                    var errorMessage = new ChatMessage(
                        ChatRole.Tool,
                        $"Tool '{toolCall.Tool}' not found.");

                    messages.Add(errorMessage);

                    _memory?.Add(errorMessage);

                    continue;
                }

                if (!tool.IsEnabled)
                {
                    var disabledMessage = new ChatMessage(
                        ChatRole.Tool,
                        $"Tool '{tool.Name}' is disabled.");

                    messages.Add(disabledMessage);

                    _memory?.Add(disabledMessage);

                    continue;
                }

                ToolExecutionResult result;

                try
                {
                    result = await tool.ExecuteAsync(
                        toolCall.Input,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    result = new ToolExecutionResult(
                        Success: false,
                        Output: string.Empty,
                        Error: $"Tool '{tool.Name}' failed: {ex.Message}");
                }

                var toolContent = result.Success
                    ? $"Tool '{tool.Name}' result:\n{result.Output}"
                    : $"Tool '{tool.Name}' failed:\n{result.Error}";

                var toolMessage = new ChatMessage(
                    ChatRole.Tool,
                    toolContent);

                messages.Add(toolMessage);
    
                _memory?.Add(toolMessage);
            }
            messages.Add(new ChatMessage(
                ChatRole.User,
                """
                Use the tool execution results above.

                Do not assume or invent values.

                Provide the final answer using ONLY the tool results.
                """));
        }

        // Fallback response
        var fallback = await _client.GetResponseAsync(
            messages,
            options,
            cancellationToken);

        PersistAssistantMessage(
            fallback.Text ?? string.Empty);

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

    
    private static IReadOnlyCollection<ToolCall> ExtractToolCalls(
        string text)
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
                var toolCall = JsonSerializer.Deserialize<ToolCall>(
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