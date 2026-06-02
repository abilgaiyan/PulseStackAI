namespace PulseStack.Abstractions.Runtime.Usage;

public static class ProviderModelParser
{
    public static string ExtractProvider(
        string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return string.Empty;
        }

        var separator =
            model.IndexOf('/');

        if (separator <= 0)
        {
            return string.Empty;
        }

        return model[..separator];
    }
}
