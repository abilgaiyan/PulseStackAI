namespace PulseStack.Abstractions.Security;

public sealed class SecurityContext
{
    public string? UserId { get; init; }

    public string? TenantId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public bool IsAuthenticated { get; init; }
}