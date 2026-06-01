using System.Text;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Tools;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Tools;
using RuntimeToolExecutor = PulseStack.Agents.Runtime.Tools.ToolExecutor;

namespace PulseStack.Agents.Runtime;

public sealed class AgentRuntime : IAgentRuntime
{
    private const int MaxToolIterations = 3;

    private readonly IChatClient? _client;
    private readonly IChatClientFactory? _clientFactory;
    private readonly ToolExecutionLoop? _toolExecutionLoop;
    private readonly string? _instructions;
    private readonly float? _temperature;
    private readonly IToolRegistry? _tools;
    private readonly IConversationMemory? _memory;
    private readonly string? _model;
    private readonly IAgent? _agent;
    private readonly IRuntimeEventDispatcher _eventDispatcher;
    private readonly UsageExtractorRegistry _usageExtractors;

    internal AgentRuntime(
        IRuntimeEventDispatcher eventDispatcher)
    {
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _usageExtractors = UsageExtractorRegistry.CreateDefault();
    }

    public AgentRuntime(
        IChatClient client,
        IToolExecutor toolExecutor,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        string? model = null,
        IAgent? agent = null)
        : this(
            client,
            toolExecutor,
            instructions,
            temperature,
            tools,
            memory,
            model,
            agent,
            new RuntimeEventDispatcher())
    {
    }

    internal AgentRuntime(
        IChatClient client,
        IToolExecutor toolExecutor,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory,
        string? model,
        IAgent? agent,
        IRuntimeEventDispatcher eventDispatcher,
        UsageExtractorRegistry? usageExtractors = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
        _memory = memory;
        _model = model;
        _agent = agent;
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _usageExtractors =
            usageExtractors
            ?? UsageExtractorRegistry.CreateDefault();
        _toolExecutionLoop = new ToolExecutionLoop(
            tools,
            new RuntimeToolExecutor(toolExecutor));
    }

    public AgentRuntime(
        IChatClientFactory clientFactory,
        IToolExecutor toolExecutor,
        string model,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        IAgent? agent = null)
        : this(
            clientFactory,
            toolExecutor,
            model,
            instructions,
            temperature,
            tools,
            memory,
            agent,
            new RuntimeEventDispatcher())
    {
    }

    internal AgentRuntime(
        IChatClientFactory clientFactory,
        IToolExecutor toolExecutor,
        string model,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory,
        IAgent? agent,
        IRuntimeEventDispatcher eventDispatcher,
        UsageExtractorRegistry? usageExtractors = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("Model is required.", nameof(model))
            : model;
        _instructions = instructions;
        _temperature = temperature;
        _tools = tools;
        _memory = memory;
        _agent = agent;
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _usageExtractors =
            usageExtractors
            ?? UsageExtractorRegistry.CreateDefault();
        _toolExecutionLoop = new ToolExecutionLoop(
            tools,
            new RuntimeToolExecutor(toolExecutor));
    }

    public async Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsAgentLifecycleManaged(context))
        {
            var managedEventDispatcher =
                ResolveEventDispatcher(context);

            var managedExecutionContext = new AgentExecutionContext(
                context,
                new List<ChatMessage>(),
                cancellationToken,
                _agent,
                executionId: ResolveExecutionId(context),
                branchId: ResolveBranchId(context),
                metadata: SnapshotPipelineMetadata(context.Items),
                eventDispatcher: managedEventDispatcher);

            return await RunCoreAsync(
                context,
                managedExecutionContext,
                cancellationToken);
        }

        var messages = BuildMessages(
            context,
            context.CurrentOutput);

        var eventDispatcher =
            ResolveEventDispatcher(context);

        var executionContext = new AgentExecutionContext(
            context,
            messages,
            cancellationToken,
            _agent,
            executionId: ResolveExecutionId(context),
            branchId: ResolveBranchId(context),
            metadata: SnapshotPipelineMetadata(context.Items),
            eventDispatcher: eventDispatcher);

        var options = BuildChatOptions();

        executionContext.EventDispatcher.Dispatch(
            new AgentStartedEvent(
                executionContext.ExecutionId,
                DateTimeOffset.UtcNow,
                _agent?.Name,
                _model,
                executionContext.BranchId,
                SnapshotMetadata(executionContext.Metadata)));

        try
        {
            var response = await ExecuteToolLoopAsync(
                executionContext,
                options);

            executionContext.EventDispatcher.Dispatch(
                new AgentCompletedEvent(
                    executionContext.ExecutionId,
                    DateTimeOffset.UtcNow,
                    _agent?.Name,
                    _model,
                    executionContext.BranchId,
                    true,
                    null,
                    SnapshotMetadata(executionContext.Metadata)));

            return response;
        }
        catch (Exception ex)
        {
            executionContext.EventDispatcher.Dispatch(
                new AgentCompletedEvent(
                    executionContext.ExecutionId,
                    DateTimeOffset.UtcNow,
                    _agent?.Name,
                    _model,
                    executionContext.BranchId,
                    false,
                    ex.Message,
                    SnapshotMetadata(executionContext.Metadata)));

            throw;
        }
    }

    internal async Task<AgentExecutionResult> ExecuteAsync(
        IAgent agent,
        PipelineContext context,
        AgentExecutionContext executionContext,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(policy);

        var startedAt =
            DateTimeOffset.UtcNow;

        var retryCount = 0;

        executionContext.EventDispatcher.Dispatch(
            new AgentStartedEvent(
                executionContext.ExecutionId,
                startedAt,
                agent.Name,
                agent.Model,
                executionContext.BranchId,
                SnapshotPipelineMetadata(context.Items)));

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response =
                    await ExecuteAgentAsync(
                        agent,
                        context,
                        executionContext,
                        cancellationToken);

                var output =
                    response.Text
                    ?? context.CurrentOutput
                    ?? string.Empty;

                context.CurrentOutput =
                    output;

                var usage =
                    _usageExtractors.Extract(
                        response,
                        new UsageExtractionContext
                        {
                            Model = agent.Model
                        });

                var completedAt =
                    DateTimeOffset.UtcNow;

                executionContext.EventDispatcher.Dispatch(
                    new AgentCompletedEvent(
                        executionContext.ExecutionId,
                        completedAt,
                        agent.Name,
                        agent.Model,
                        executionContext.BranchId,
                        true,
                        null,
                        SnapshotPipelineMetadata(context.Items)));

                return new AgentExecutionResult
                {
                    Success = true,
                    Output = output,
                    RetryCount = retryCount,
                    Usage = usage,
                    StartedAt = startedAt,
                    CompletedAt = completedAt
                };
            }
            catch (OperationCanceledException ex)
                when (cancellationToken.IsCancellationRequested)
            {
                var completedAt =
                    DateTimeOffset.UtcNow;

                executionContext.EventDispatcher.Dispatch(
                    new AgentCompletedEvent(
                        executionContext.ExecutionId,
                        completedAt,
                        agent.Name,
                        agent.Model,
                        executionContext.BranchId,
                        false,
                        ex.Message,
                        SnapshotPipelineMetadata(context.Items)));

                throw;
            }
            catch (Exception ex)
            {
                if (retryCount < policy.MaxRetries)
                {
                    retryCount++;

                    executionContext.EventDispatcher.Dispatch(
                        new AgentRetryEvent(
                            executionContext.ExecutionId,
                            DateTimeOffset.UtcNow,
                            agent.Name,
                            retryCount,
                            ex.Message));

                    continue;
                }

                var completedAt =
                    DateTimeOffset.UtcNow;

                executionContext.EventDispatcher.Dispatch(
                    new AgentCompletedEvent(
                        executionContext.ExecutionId,
                        completedAt,
                        agent.Name,
                        agent.Model,
                        executionContext.BranchId,
                        false,
                        ex.Message,
                        SnapshotPipelineMetadata(context.Items)));

                return new AgentExecutionResult
                {
                    Success = false,
                    Output = context.CurrentOutput ?? string.Empty,
                    RetryCount = retryCount,
                    Exception = ex,
                    StartedAt = startedAt,
                    CompletedAt = completedAt
                };
            }
        }
    }

    internal async Task<ChatResponse> RunCoreAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionContext);

        var messages = BuildMessages(
            context,
            context.CurrentOutput);

        var agentExecutionContext = new AgentExecutionContext(
            context,
            messages,
            cancellationToken,
            _agent,
            executionId: executionContext.ExecutionId,
            branchId: executionContext.BranchId,
            metadata: SnapshotPipelineMetadata(context.Items),
            eventDispatcher: executionContext.EventDispatcher);

        var options = BuildChatOptions();

        return await ExecuteToolLoopAsync(
            agentExecutionContext,
            options);
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

            builder.AppendLine($"Result: {RuntimeToolExecutor.FormatResult(tool.Result)}");

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

    private async Task<ChatResponse> ExecuteToolLoopAsync(
        AgentExecutionContext executionContext,
        ChatOptions options)
    {
        if (_toolExecutionLoop is null)
        {
            throw new InvalidOperationException(
                "This runtime instance is not configured for direct agent execution.");
        }

        var client = ResolveClient();
        for (int i = 0; i < MaxToolIterations; i++)
        {
            executionContext.CancellationToken.ThrowIfCancellationRequested();

            var response = await client.GetResponseAsync(
                executionContext.Messages,
                options,
                executionContext.CancellationToken);

            var text = response.Text ?? string.Empty;

            var toolExecution = await _toolExecutionLoop.ExecuteAsync(
                text,
                executionContext);

            if (!toolExecution.HasToolCalls)
            {
                executionContext.PipelineContext.CurrentOutput = text;

                PersistAssistantMessage(text);

                return response;
            }

            var assistantMessage = new ChatMessage(
                ChatRole.Assistant,
                text);

            executionContext.Messages.Add(assistantMessage);

            _memory?.Add(assistantMessage);

            foreach (var toolMessage in toolExecution.Messages)
            {
                executionContext.Messages.Add(toolMessage);

                _memory?.Add(toolMessage);
            }

            executionContext.Messages.Add(new ChatMessage(ChatRole.User,
                """
                Use the tool execution results above.

                Do not assume or invent values.

                Provide the final answer using ONLY the tool results.
                """));
        }

        var fallback = await client.GetResponseAsync(
            executionContext.Messages,
            options,
            executionContext.CancellationToken);

        executionContext.PipelineContext.CurrentOutput = fallback.Text ?? string.Empty;

        PersistAssistantMessage(fallback.Text ?? string.Empty);

        return fallback;
    }

    private static async Task<ChatResponse> ExecuteAgentAsync(
        IAgent agent,
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (agent is Agent runtimeAgent)
        {
            return await runtimeAgent.RunCoreAsync(
                context,
                executionContext,
                cancellationToken);
        }

        var hadLifecycleMarker =
            context.Items.TryGetValue(
                PipelineContextKeys.RuntimeAgentLifecycleManaged,
                out var previousLifecycleMarker);

        context.Items[PipelineContextKeys.RuntimeAgentLifecycleManaged] =
            true;

        try
        {
            return await agent.RunAsync(
                context,
                cancellationToken);
        }
        finally
        {
            if (hadLifecycleMarker)
            {
                context.Items[PipelineContextKeys.RuntimeAgentLifecycleManaged] =
                    previousLifecycleMarker;
            }
            else
            {
                context.Items.Remove(
                    PipelineContextKeys.RuntimeAgentLifecycleManaged);
            }
        }
    }

    private static bool IsAgentLifecycleManaged(
        PipelineContext context)
        => context.Items.TryGetValue(
                PipelineContextKeys.RuntimeAgentLifecycleManaged,
                out var lifecycleManaged)
            && lifecycleManaged is true;

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

    private static IReadOnlyDictionary<string, object?> SnapshotMetadata(
        IDictionary<string, object?> metadata)
        => new Dictionary<string, object?>(metadata);

    private static Dictionary<string, object?> SnapshotPipelineMetadata(
        IDictionary<string, object?> metadata)
    {
        var snapshot = new Dictionary<string, object?>();

        foreach (var item in metadata)
        {
            if (item.Key == PipelineContextKeys.RuntimeEventDispatcher)
            {
                continue;
            }

            if (item.Key == PipelineContextKeys.RuntimeAgentLifecycleManaged)
            {
                continue;
            }

            snapshot[item.Key] = item.Value;
        }

        return snapshot;
    }

    private IRuntimeEventDispatcher ResolveEventDispatcher(
        PipelineContext context)
    {
        if (context.Items.TryGetValue(
                PipelineContextKeys.RuntimeEventDispatcher,
                out var dispatcher)
            && dispatcher is IRuntimeEventDispatcher runtimeEventDispatcher)
        {
            return runtimeEventDispatcher;
        }

        return _eventDispatcher;
    }

    private static Guid? ResolveExecutionId(
        PipelineContext context)
    {
        if (context.Items.TryGetValue(
                PipelineContextKeys.RuntimeExecutionId,
                out var executionId)
            && executionId is Guid value)
        {
            return value;
        }

        return null;
    }

    private static Guid? ResolveBranchId(
        PipelineContext context)
    {
        if (context.Items.TryGetValue(
                PipelineContextKeys.RuntimeBranchId,
                out var branchId)
            && branchId is Guid value)
        {
            return value;
        }

        return null;
    }

}
