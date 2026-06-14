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
        foreach (var evt in project.Events.Where(x => x.NodeKind != EventNodeKind.TaskGroup))
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
                InteractionObjectId = evt.InteractionObjectId,
                StateKey = evt.StateKey,
                StateValueOnActivate = evt.StateValueOnActivate,
                CompletionItemId = evt.CompletionItemId,
                CompletionItemCount = evt.CompletionItemCount,
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
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "PROJECT_EMPTY", "Project has no events."));
            return issues;
        }

        var idLookup = new Dictionary<string, EventNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var evt in project.Events)
        {
            if (string.IsNullOrWhiteSpace(evt.Id))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "EVENT_ID_EMPTY", $"Event '{evt.Title}' is missing Id."));
                continue;
            }

            if (!idLookup.TryAdd(evt.Id.Trim(), evt))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "EVENT_ID_DUP", $"Duplicate task/event Id: {evt.Id}"));
            }

            if (!evt.Enabled && evt.NodeKind != EventNodeKind.TaskGroup)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "EVENT_DISABLED", $"Event '{evt.Id}' is disabled."));
            }
        }

        if (string.IsNullOrWhiteSpace(project.StartEventId))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "START_EMPTY", "StartEventId is empty."));
        }
        else if (!idLookup.TryGetValue(project.StartEventId.Trim(), out var startEvent))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "START_MISSING", $"StartEventId not found: {project.StartEventId}"));
        }
        else if (startEvent.NodeKind == EventNodeKind.TaskGroup)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "START_IS_GROUP", $"StartEventId points to display-only event group: {project.StartEventId}"));
        }

        foreach (var evt in project.Events)
        {
            if (evt.NodeKind == EventNodeKind.TaskGroup)
            {
                if (!string.IsNullOrWhiteSpace(evt.ParentGroupId) &&
                    (!idLookup.TryGetValue(evt.ParentGroupId.Trim(), out var parentGroup) || parentGroup.NodeKind != EventNodeKind.TaskGroup))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "GROUP_PARENT_MISSING", $"Event group {evt.Id} references missing parent group: {evt.ParentGroupId}"));
                }
                else if (HasGroupParentCycle(evt, idLookup))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "GROUP_PARENT_CYCLE", $"Event group {evt.Id} has a parent folder cycle."));
                }

                if (evt.Triggers.Count > 0 || evt.Actions.Count > 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "GROUP_HAS_FLOW", $"Event group {evt.Id} is display-only; triggers/actions are ignored."));
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(evt.ParentGroupId) &&
                (!idLookup.TryGetValue(evt.ParentGroupId.Trim(), out var group) || group.NodeKind != EventNodeKind.TaskGroup))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "GROUP_MISSING", $"Event {evt.Id} references missing event group: {evt.ParentGroupId}"));
            }

            for (var i = 0; i < evt.Actions.Count; i++)
            {
                var action = evt.Actions[i];
                if (action.Type != DispatchActionType.StartEvent)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(action.TargetEventId))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "ACTION_TARGET_EMPTY", $"Event {evt.Id} StartEvent action #{i + 1} is missing TargetEventId."));
                    continue;
                }

                var target = action.TargetEventId.Trim();
                if (!idLookup.TryGetValue(target, out var targetEvent))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "ACTION_TARGET_MISSING", $"Event {evt.Id} references missing target event: {target}"));
                }
                else if (targetEvent.NodeKind == EventNodeKind.TaskGroup)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "ACTION_TARGET_GROUP", $"Event {evt.Id} points to display-only event group: {target}"));
                }
                else if (string.Equals(target, evt.Id, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ACTION_SELF_LOOP", $"Event {evt.Id} has a self loop."));
                }
            }
        }

        return issues;
    }

    private static bool HasGroupParentCycle(EventNode group, Dictionary<string, EventNode> idLookup)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { group.Id };
        var parentId = group.ParentGroupId;
        while (!string.IsNullOrWhiteSpace(parentId) && idLookup.TryGetValue(parentId.Trim(), out var parent))
        {
            if (parent.NodeKind != EventNodeKind.TaskGroup)
            {
                return false;
            }

            if (!seen.Add(parent.Id))
            {
                return true;
            }

            parentId = parent.ParentGroupId;
        }

        return false;
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
    public string InteractionObjectId { get; set; } = "";
    public string StateKey { get; set; } = "";
    public string StateValueOnActivate { get; set; } = "";
    public string CompletionItemId { get; set; } = "";
    public int CompletionItemCount { get; set; }
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
