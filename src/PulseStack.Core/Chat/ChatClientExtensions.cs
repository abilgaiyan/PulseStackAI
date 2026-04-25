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
}