namespace PulseStack.Core.Resilience;

public sealed class ResilienceOptions
{
    public int RetryCount { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 30;

    public int CircuitBreakerFailureThreshold
        { get; set; } = 5;

    public int CircuitBreakerDurationSeconds
        { get; set; } = 30;
}
