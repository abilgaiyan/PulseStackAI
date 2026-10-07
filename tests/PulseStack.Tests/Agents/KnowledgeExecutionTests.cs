using System.Text;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Core.Memory;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;
using PulseStack.Tools.BuiltIn;
using Xunit;

namespace PulseStack.Tests.Agents;

public sealed class KnowledgeExecutionTests
{
    [Fact]
    public async Task Run_SnapshotsPresentedInput_PreservesOrder_AndDoesNotPersistKnowledge()
    {
        var context = new PipelineContext { Input = "original", CurrentOutput = "presented" };
        var order = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var first = new Source("first", (query, token) =>
        {
            Assert.Equal("presented", query.Text);
            Assert.Equal(cancellation.Token, token);
            order.Add("first");
            context.CurrentOutput = "changed during retrieval";
            return new KnowledgeResult { Items = ["one", "two"] };
        });
        var second = new Source("second", (query, token) =>
        {
            Assert.Equal("presented", query.Text);
            order.Add("second");
            return new KnowledgeResult { Items = ["three"] };
        });
        var memory = new ConversationMemory();
        memory.Add(new ChatMessage(ChatRole.Assistant, "history"));
        var client = new Client("answer");
        await Runtime(client, [first, second], memory).RunAsync(context, cancellation.Token);
        Assert.Equal(new[] { "first", "second" }, order);
        Assert.Equal(new[] { "history", "Retrieved reference material\n\nSource: first\none\n\ntwo\n\nSource: second\nthree\n", "presented" },
            client.Calls.Single().Select(message => message.Text));
        Assert.Equal(ChatRole.User, client.Calls[0][1].Role);
        Assert.Equal(new[] { "history", "presented", "answer" }, memory.Messages.Select(message => message.Text));
    }

    [Fact]
    public async Task Run_RetrievesOncePerCall_AndReusesContributionAcrossToolLoop()
    {
        var source = new Source("reference", (_, _) => new KnowledgeResult { Items = ["material"] });
        var client = new Client("{\"tool\":\"calculator\",\"input\":\"5 * 5\"}", "25", "next");
        var tools = new ToolRegistry();
        tools.Register(new CalculatorTool());
        var runtime = Runtime(client, [source], tools: tools);
        await runtime.RunAsync(new PipelineContext { Input = "first", CurrentOutput = "first" });
        Assert.Equal(1, source.Calls);
        Assert.Equal(2, client.Calls.Count);
        foreach (var call in client.Calls)
            Assert.Single(call.Where(message => message.Text.StartsWith("Retrieved reference material")));
        await runtime.RunAsync(new PipelineContext { Input = "second", CurrentOutput = "second" });
        Assert.Equal(2, source.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task MalformedResults_FailBeforeProviderOrMemoryMutation(int kind)
    {
        var source = new Source("bad", (_, _) => kind switch
        {
            0 => null!,
            1 => new KnowledgeResult { Items = null! },
            2 => new KnowledgeResult { Items = [null!] },
            3 => new KnowledgeResult { Items = [""] },
            _ => new KnowledgeResult { Items = ["valid", "  "] }
        });
        var memory = new ConversationMemory();
        var client = new Client("unused");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(client, [source], memory).RunAsync(Context()));
        Assert.Empty(client.Calls);
        Assert.Empty(memory.Messages);
    }

    [Fact]
    public async Task EmptyResults_AndNoKnowledge_PreserveNormalMessages()
    {
        foreach (var sources in new IReadOnlyCollection<IKnowledgeSource>[]
        {
            [], [new Source("empty", (_, _) => new KnowledgeResult { Items = [] })]
        })
        {
            var client = new Client("answer");
            await Runtime(client, sources).RunAsync(Context());
            Assert.Equal("input", Assert.Single(client.Calls.Single()).Text);
        }
    }

    [Fact]
    public async Task Budget_IncludesUtf8FramingAndLabels_ExactLimitSucceeds_OneByteLessFails()
    {
        const string contribution = "Retrieved reference material\n\nSource: référence\névidence\n";
        var bytes = Encoding.UTF8.GetByteCount(contribution);
        var source = new Source("référence", (_, _) => new KnowledgeResult { Items = ["évidence"] });
        var client = new Client("answer");
        await Runtime(client, [source], limit: bytes).RunAsync(Context());
        Assert.Equal(contribution, client.Calls.Single()[0].Text);
        var failingClient = new Client("unused");
        var memory = new ConversationMemory();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(failingClient, [source], memory, bytes - 1).RunAsync(Context()));
        Assert.Empty(failingClient.Calls);
        Assert.Empty(memory.Messages);
    }

    [Fact]
    public async Task RetrievalFailure_AndCancellation_PreventProviderAndMemoryMutation()
    {
        var client = new Client("unused");
        var memory = new ConversationMemory();
        var failure = new InvalidOperationException("source failure");
        var throwing = new Source("throws", (_, _) => throw failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(client, [throwing], memory).RunAsync(Context())));
        using var cancellation = new CancellationTokenSource();
        var cancelling = new Source("cancel", (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return new KnowledgeResult { Items = ["material"] };
        });
        var later = new Source("later", (_, _) => new KnowledgeResult());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runtime(client, [cancelling, later], memory).RunAsync(Context(), cancellation.Token));
        Assert.Equal(0, later.Calls);
        var preCancelled = new Source("never", (_, _) => new KnowledgeResult());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runtime(client, [preCancelled], memory).RunAsync(Context(), cancellation.Token));
        Assert.Equal(0, preCancelled.Calls);
        Assert.Empty(client.Calls);
        Assert.Empty(memory.Messages);
    }

    [Fact]
    public async Task Streaming_PreparesKnowledgeBeforeProvider_AndRejectsMalformedMaterialBeforeYield()
    {
        var source = new Source("stream", (query, _) =>
        {
            Assert.Equal("stream input", query.Text);
            return new KnowledgeResult { Items = ["material"] };
        });
        var client = new Client("chunk");
        var memory = new ConversationMemory();
        var chunks = new List<string>();
        await foreach (var chunk in Runtime(client, [source], memory).StreamAsync("stream input"))
            chunks.Add(chunk);
        Assert.Equal(new[] { "chunk" }, chunks);
        Assert.Equal(1, source.Calls);
        Assert.Equal("stream input", client.Calls.Single().Last().Text);
        Assert.DoesNotContain(memory.Messages, message => message.Text.StartsWith("Retrieved reference material"));
        var invalid = new Source("invalid", (_, _) => new KnowledgeResult { Items = [" "] });
        var unused = new Client("unused");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var chunk in Runtime(unused, [invalid]).StreamAsync("input"))
                Assert.True(false, "No chunk should be yielded.");
        });
        Assert.Empty(unused.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidBudget_FailsAtRuntimeComposition(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Runtime(new Client(), [], limit: limit));
    }

    [Fact]
    public async Task AggregateBudget_RejectsWholeContributionWithoutPersistingEarlierSource()
    {
        var first = new Source("first", (_, _) => new KnowledgeResult { Items = ["one"] });
        var second = new Source("second", (_, _) => new KnowledgeResult { Items = ["two"] });
        var firstBytes = Encoding.UTF8.GetByteCount("Retrieved reference material\n\nSource: first\none\n");
        var client = new Client("unused");
        var memory = new ConversationMemory();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(client, [first, second], memory, firstBytes).RunAsync(Context()));
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.Empty(memory.Messages);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task StreamingCancellation_DuringRetrieval_PreventsProviderAndMemoryMutation()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new Source("cancel", (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return new KnowledgeResult { Items = ["material"] };
        });
        var client = new Client("unused");
        var memory = new ConversationMemory();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in Runtime(client, [source], memory).StreamAsync("input", cancellation.Token))
                Assert.True(false, "No chunk should be yielded.");
        });
        Assert.Empty(client.Calls);
        Assert.Empty(memory.Messages);
    }

    private static PipelineContext Context() => new() { Input = "input", CurrentOutput = "input" };

    private static AgentRuntime Runtime(
        Client client,
        IReadOnlyCollection<IKnowledgeSource> sources,
        ConversationMemory? memory = null,
        int limit = 65536,
        ToolRegistry? tools = null) => new(
            client, new ToolExecutor(new AllowAllToolAuthorizationService()),
            null, null, tools, memory, null, null, new RuntimeEventDispatcher(),
            knowledge: sources,
            knowledgeOptions: new KnowledgeExecutionOptions { MaxContributionBytes = limit });

    private sealed class Source(string name, Func<KnowledgeQuery, CancellationToken, KnowledgeResult> retrieve) : IKnowledgeSource
    {
        public string Name => name;
        public int Calls { get; private set; }
        public Task<KnowledgeResult> RetrieveAsync(KnowledgeQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(retrieve(query, cancellationToken));
        }
    }

    private sealed class Client(params string[] responses) : IChatClient
    {
        private readonly Queue<string> _responses = new(responses);
        public List<ChatMessage[]> Calls { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(messages.ToArray());
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _responses.Dequeue())));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(messages.ToArray());
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, _responses.Dequeue());
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
