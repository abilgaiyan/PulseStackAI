using System.Net.Http;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Tools.BuiltIn;

public sealed class HttpTool : ITool
{
    private readonly HttpClient _httpClient;

    public HttpTool(
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _httpClient = httpClientFactory.CreateClient("PulseStack");
    }

    public string Name => "http";

    public string Description => "Fetches content from HTTP endpoints.";

    public string Category => "Web";

    public bool IsEnabled => true;

    public IReadOnlyCollection<string> Tags => ["http", "web", "api"];

    public ToolDescriptor Descriptor => new ToolDescriptor
    {
        Name = Name,
        Description = Description,
        ActionType = ToolActionType.Read,
        RequiredRoles = [],
        RequiredPermissions = [],
        AllowedScopes = [],
        IsDestructive = false,
        RequiresConfirmation = false
    };

    public async Task<IToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var input = context.Input?.ToString();

        if (string.IsNullOrWhiteSpace(input))
        {
            return ToolExecutionResult<string>.Failure("Input is required.");
        }

        if (!Uri.TryCreate(
            input,
            UriKind.Absolute,
            out var uri))
        {
            return ToolExecutionResult<string>.Failure("Invalid URL.");
        }

        try
        {
            var response = await SendRequestAsync(
                new HttpRequestMessage(HttpMethod.Get, uri),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return ToolExecutionResult<string>.Success(content);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult<string>.Failure($"HTTP request failed: {ex.Message}");
        }
    }
    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return await _httpClient.SendAsync(
            request,
            cancellationToken);
    }
}