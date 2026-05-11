using Microsoft.Extensions.AI;

namespace PulseStack.Providers.Gemini.Internal;

internal sealed class GeminiChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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