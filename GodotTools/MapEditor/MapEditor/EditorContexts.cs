using System.Text.Json;
using MapEditor.Models;

namespace MapEditor;

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal static class GodotRootContext
{
    public static string? CurrentRoot { get; set; }
}

internal static class SelectedMapContext
{
    public static MapDefinition? CurrentMap { get; set; }
}

internal static class ProjectContext
{
    public static MapProject? CurrentProject { get; set; }
}

internal static class MainFormContext
{
    public static MainForm? CurrentForm { get; set; }
}

