using System.Net.Http;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class HttpTool : ITool
{
    private readonly HttpClient _httpClient;

    public HttpTool(
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(
            httpClientFactory);

        _httpClient = httpClientFactory
            .CreateClient("PulseStack");
    }

    public string Name => "http";

    public string Description =>
        "Fetches content from HTTP endpoints.";

    public string Category => "Web";

    public bool IsEnabled => true;

    public IReadOnlyCollection<string> Tags =>
        ["http", "web", "api"];

    public async Task<ToolExecutionResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(
            input,
            UriKind.Absolute,
            out var uri))
        {
            return new ToolExecutionResult(
                Success: false,
                Output: string.Empty,
                Error: "Invalid URL.");
        }

        try
        {
            var response = await _httpClient.GetAsync(
                uri,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return new ToolExecutionResult(
                Success: true,
                Output: content);
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult(
                Success: false,
                Output: string.Empty,
                Error: $"HTTP request failed: {ex.Message}");
        }
    }
}