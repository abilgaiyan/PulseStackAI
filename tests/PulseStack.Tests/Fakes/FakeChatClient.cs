using Microsoft.Extensions.AI;

namespace PulseStack.Tests.Fakes;

internal sealed class FakeChatClient : IChatClient
{
    private readonly Queue<string> _responses;

    public FakeChatClient(IEnumerable<string> responses)
    {
        _responses = new Queue<string>(responses);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var text = _responses.Dequeue();

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    text)));
    }

   public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken cancellationToken = default)
    {
        while (_responses.Count > 0)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, _responses.Dequeue());
        }
        await Task.CompletedTask;
    }

    public object? GetService(
        Type serviceType,
        object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
    }
}