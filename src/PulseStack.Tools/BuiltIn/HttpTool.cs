using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class HttpTool : ITool
{
    private readonly HttpClient _httpClient;

    public HttpTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

            return new ToolExecutionResult(
                Success: true,
                Output: await response.Content.ReadAsStringAsync(cancellationToken));
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