using Microsoft.Extensions.AI;

namespace PulseStack.Core.Chat;

public static class ChatClientExtensions
{
    public static async Task<string> AskAsync(
        this IChatClient client,
        string prompt,
        CancellationToken ct = default)
    {
        var response = await client.GetResponseAsync(
            prompt,
            cancellationToken: ct);

        return response.Text ?? string.Empty;
    }

    public static async IAsyncEnumerable<string> StreamAskAsync(
        this IChatClient client,
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        await foreach (var update in client.GetStreamingResponseAsync(
            messages,
            cancellationToken: ct))
        {
            if (!string.IsNullOrWhiteSpace(update.Text))
                yield return update.Text;
        }
    }
}