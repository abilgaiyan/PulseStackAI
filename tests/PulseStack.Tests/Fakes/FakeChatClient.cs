using Microsoft.Extensions.AI;

namespace PulseStack.Tests.Fakes;

internal sealed class FakeChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses;

    public FakeChatClient(IEnumerable<string> responses)
    {
        _responses = new Queue<ChatResponse>(
            responses.Select(response =>
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        response))));
    }

    public FakeChatClient(IEnumerable<ChatResponse> responses)
    {
        _responses = new Queue<ChatResponse>(responses);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _responses.Dequeue());
    }

   public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken cancellationToken = default)
    {
        while (_responses.Count > 0)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, _responses.Dequeue().Text);
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
