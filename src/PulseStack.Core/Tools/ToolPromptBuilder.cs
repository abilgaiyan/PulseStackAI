using System.Text;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Agents.Tools;

public static class ToolPromptBuilder
{
    public static string Build(
        IReadOnlyCollection<ITool> tools)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Available tools:");

        foreach (var tool in tools)
        {
            sb.AppendLine();
            sb.AppendLine($"Tool: {tool.Name}");
            sb.AppendLine($"Category: {tool.Category}");
            sb.AppendLine($"Description: {tool.Description}");

            if (tool.Tags.Count > 0)
            {
                sb.AppendLine(
                    $"Tags: {string.Join(", ", tool.Tags)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("""
        To use a tool, respond ONLY with JSON:

        {
          "tool": "tool_name",
          "input": "tool input"
        }
        """);

        return sb.ToString();
    }
}