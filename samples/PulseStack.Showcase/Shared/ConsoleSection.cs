namespace PulseStack.Showcase.Shared;

internal static class ConsoleSection
{
    public static void Print(string title)
    {
        Console.WriteLine();

        Console.WriteLine(
            new string('=', 60));

        Console.WriteLine(title);

        Console.WriteLine(
            new string('=', 60));

        Console.WriteLine();
    }
}
