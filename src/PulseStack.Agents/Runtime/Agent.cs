using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Models;
using PulseStack.Agents.Tools;
using PulseStack.Abstractions.Chat;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : IAgent
{
    private readonly IChatClient? _client;

    private readonly IChatClientFactory?  _clientFactory;
    private readonly string? _instructions;
    private readonly float? _temperature;
    private readonly IToolRegistry? _tools;
    private readonly IConversationMemory? _memory;

    private const int MaxToolIterations = 3;

    public string Name { get; }

    private readonly string? _model;

    public string? Model => _model;
    private readonly IReadOnlyCollection<string>? _fallbackModels;
    public IReadOnlyCollection<string>? FallbackModels => _fallbackModels;

    public Agent(
        string name,
        IChatClient? client,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        string? model = null,
        IReadOnlyCollection<string>? fallbackModels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        Name = name;
        _client = client;
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
        _memory = memory;
        _fallbackModels = fallbackModels ?? [];
        _model = model;
    }

    public Agent(
        string name,
        IChatClientFactory? clientFactory,
        string model,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        IReadOnlyCollection<string>? fallbackModels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        Name = name;

        _clientFactory = clientFactory;

        _model = model;

        _instructions = instructions;

        _temperature = temperature;

        _tools = tools;

        _memory = memory;

        _fallbackModels = fallbackModels ?? [];
    }
    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunAsync(
            context,
            cancellationToken);
    }

    public async Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var messages = BuildMessages(
            context,
            context.CurrentOutput);

        var options = BuildChatOptions();

        return await ExecuteToolLoopAsync(
            context,
            messages,
            options,
            cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(
            null,
            input);

        var options = BuildChatOptions();

        var responseBuilder = new StringBuilder();
        
        var client = ResolveClient();
        await foreach (var update in client.GetStreamingResponseAsync(
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

    private List<ChatMessage> BuildMessages(
        PipelineContext? context,
        string input)
    {
        var messages = new List<ChatMessage>();

        AddSystemInstructions(messages);

        AddToolInstructions(messages);

        AddPipelineContext(context, messages);

        AddMemory(messages);

        var userMessage = new ChatMessage(
            ChatRole.User,
            input);

        messages.Add(userMessage);

        _memory?.Add(userMessage);

        return messages;
    }

    private void AddSystemInstructions(
        List<ChatMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(_instructions))
            return;

        messages.Add(new ChatMessage(
            ChatRole.System,
            _instructions));
    }

    private void AddToolInstructions(
        List<ChatMessage> messages)
    {
        if (_tools is null)
            return;

        var toolPrompt = ToolPromptBuilder.Build(
            _tools.GetAll()
                .Where(t => t.IsEnabled)
                .ToList());

        messages.Add(new ChatMessage(
            ChatRole.System,
            toolPrompt));
    }

    private void AddPipelineContext(
        PipelineContext? context,
        List<ChatMessage> messages)
    {
        if (context is null)
            return;

        if (context.ToolResults.Count == 0)
            return;

        var builder = new StringBuilder();

        builder.AppendLine("Previous tool execution results:");

        foreach (var tool in context.ToolResults)
        {
            builder.AppendLine($"Tool: {tool.ToolName}");

            builder.AppendLine($"Input: {tool.Input}");

            builder.AppendLine($"Result: {FormatToolResult(tool.Result)}");

            builder.AppendLine();
        }

        messages.Add(new ChatMessage(
            ChatRole.System,
            builder.ToString()));
    }

    private void AddMemory(
        List<ChatMessage> messages)
    {
        if (_memory is null)
            return;

        messages.AddRange(_memory.Messages);
    }

    // Execution

    private async Task<ChatResponse> ExecuteToolLoopAsync(
        PipelineContext context,
        List<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken)
    {
         var client = ResolveClient();
         for (int i = 0; i < MaxToolIterations; i++)
         {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await client.GetResponseAsync(
                messages,
                options,
                cancellationToken);

            var text = response.Text ?? string.Empty;

            var toolCalls = ExtractToolCalls(text);

            // Final AI response
            if (toolCalls.Count == 0 || _tools is null)
            {
                context.CurrentOutput = text;

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
                var tool = _tools.GetByName(
                    toolCall.Tool);

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

                IToolExecutionResult result;

                try
                {
                    var toolContext = new ToolExecutionContext
                    {
                        ToolName = tool.Name,
                        Input = toolCall.Input,
                        PipelineContext = context
                    };

                    result = await tool.ExecuteAsync(
                        toolContext,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    result = ToolExecutionResult.Failure($"Tool '{tool.Name}' failed: {ex.Message}");
                }

                var formatted = FormatToolResult(result);
                var toolContent = result.IsSuccess
                    ? $"Tool '{tool.Name}' result:\n{formatted}"
                    : $"Tool '{tool.Name}' failed:\n{formatted}";
                
                var toolMessage = new ChatMessage(ChatRole.Tool, toolContent);

                messages.Add(toolMessage);

                _memory?.Add(toolMessage);
            }

            messages.Add(new ChatMessage(ChatRole.User,
                """
                Use the tool execution results above.

                Do not assume or invent values.

                Provide the final answer using ONLY the tool results.
                """));
        }

        // Fallback response
        var fallback = await client.GetResponseAsync(
            messages,
            options,
            cancellationToken);

        context.CurrentOutput = fallback.Text ?? string.Empty;

        PersistAssistantMessage(fallback.Text ?? string.Empty);

        return fallback;
    }

    // Helpers
    private IChatClient ResolveClient(
        string? model = null)
    {
        if (_client is not null)
        {
            return _client;
        }

        if (_clientFactory is null)
        {
            throw new InvalidOperationException(
                "No chat client factory configured.");
        }

        var resolvedModel =
            model ?? _model;

        if (string.IsNullOrWhiteSpace(
            resolvedModel))
        {
            throw new InvalidOperationException(
                "No model configured.");
        }

        return _clientFactory.Create(
            resolvedModel);
    }

    private ChatOptions BuildChatOptions()
    {
        var options = new ChatOptions();

        if (_temperature.HasValue)
        {
            options.Temperature =
                _temperature.Value;
        }

        return options;
    }

    private void PersistAssistantMessage(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _memory?.Add(new ChatMessage(
            ChatRole.Assistant,
            text));
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

    private static string FormatToolResult(
        IToolExecutionResult result)
    {
        if (!result.IsSuccess)
        {
            return result.ErrorMessage
                ?? "Unknown tool failure.";
        }

        if (result.Value is null)
        {
            return "Tool executed successfully.";
        }

        if (result.Value is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(
            result.Value,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
}
