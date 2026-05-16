namespace PulseStack.Abstractions.Chat;

public sealed class ModelExecutionOptions
{
    public string PrimaryModel { get; set; }
        = string.Empty;

    public IList<string> FallbackModels
        { get; } =
            new List<string>();
}
