namespace PulseStack.Showcase.Shared;
internal static class ShowcaseConsole
{
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();
    }

    public static void Success(string message)
    {
        Console.WriteLine($"✓ {message}");
    }

    public static void Info(string message)
    {
        Console.WriteLine($"• {message}");
    }

    public static void Error(string message)
    {
        Console.WriteLine($"✗ {message}");
    }
}