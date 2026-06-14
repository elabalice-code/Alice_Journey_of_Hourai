using System.Text.Json;
using EventStudio.Models;

namespace EventStudio.IO;

public static class EventProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static EventProject Load(string path)
    {
        var json = File.ReadAllText(path);
        var project = JsonSerializer.Deserialize<EventProject>(json, JsonOptions) ?? new EventProject();
        project.Events ??= [];
        project.GlobalVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return project;
    }

    public static void Save(string path, EventProject project)
    {
        var json = JsonSerializer.Serialize(project, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, json);
    }

    public static RuntimeEventGraph BuildRuntimeGraph(EventProject project)
    {
        var nodes = new List<RuntimeEventNode>(project.Events.Count);
        foreach (var evt in project.Events)
        {
            var links = evt.Actions
                .Where(x => x.Type == DispatchActionType.StartEvent && !string.IsNullOrWhiteSpace(x.TargetEventId))
                .Select(x => x.TargetEventId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            nodes.Add(new RuntimeEventNode
            {
                Id = evt.Id,
                Domain = evt.Domain,
                Scope = new RuntimeScope
                {
                    Map = evt.MapScope,
                    Dialogue = evt.DialogueScope,
                    Combat = evt.CombatScope
                },
                TriggerCount = evt.Triggers.Count,
                NextEvents = links
            });
        }

        return new RuntimeEventGraph
        {
            Version = project.Version,
            StartEventId = project.StartEventId,
            Nodes = nodes
        };
    }

    public static List<ValidationIssue> ValidateProject(EventProject project)
    {
        var issues = new List<ValidationIssue>();
        if (project.Events.Count == 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "PROJECT_EMPTY", "工程中没有任何事件。"));
            return issues;
        }

        var idLookup = new Dictionary<string, EventNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in project.Events)
        {
            if (string.IsNullOrWhiteSpace(evt.Id))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "EVENT_ID_EMPTY", $"事件“{evt.Title}”缺少 Id。"));
                continue;
            }

            if (!idLookup.TryAdd(evt.Id.Trim(), evt))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "EVENT_ID_DUP", $"事件 Id 重复：{evt.Id}"));
            }

            if (!evt.Enabled)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "EVENT_DISABLED", $"事件“{evt.Id}”被禁用，可能导致流程断链。"));
            }
        }

        if (string.IsNullOrWhiteSpace(project.StartEventId))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "START_EMPTY", "StartEventId 为空。"));
        }
        else if (!idLookup.ContainsKey(project.StartEventId.Trim()))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "START_MISSING", $"StartEventId 未找到：{project.StartEventId}"));
        }

        foreach (var evt in project.Events)
        {
            for (var i = 0; i < evt.Actions.Count; i++)
            {
                var action = evt.Actions[i];
                if (action.Type != DispatchActionType.StartEvent)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(action.TargetEventId))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "ACTION_TARGET_EMPTY", $"事件“{evt.Id}”的第 {i + 1} 个 StartEvent 动作缺少 TargetEventId。"));
                    continue;
                }

                var target = action.TargetEventId.Trim();
                if (!idLookup.ContainsKey(target))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "ACTION_TARGET_MISSING", $"事件“{evt.Id}”引用了不存在的目标事件：{target}"));
                }
                else if (string.Equals(target, evt.Id, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ACTION_SELF_LOOP", $"事件“{evt.Id}”存在自触发回路。"));
                }
            }
        }

        return issues;
    }
}

public enum ValidationSeverity
{
    Warning,
    Error
}

public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Message);

public sealed class RuntimeEventGraph
{
    public string Version { get; set; } = "1.0.0";
    public string StartEventId { get; set; } = "";
    public List<RuntimeEventNode> Nodes { get; set; } = [];
}

public sealed class RuntimeEventNode
{
    public string Id { get; set; } = "";
    public EventDomain Domain { get; set; } = EventDomain.Story;
    public RuntimeScope Scope { get; set; } = new();
    public int TriggerCount { get; set; }
    public List<string> NextEvents { get; set; } = [];
}

public sealed class RuntimeScope
{
    public string Map { get; set; } = "";
    public string Dialogue { get; set; } = "";
    public string Combat { get; set; } = "";
}
