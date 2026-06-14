using System.Text.Json.Serialization;

namespace EventStudio.Models;

public sealed class EventProject
{
    public string Name { get; set; } = "New Event Project";
    public string Version { get; set; } = "1.0.0";
    public string StartEventId { get; set; } = "";
    public List<EventNode> Events { get; set; } = [];
    public Dictionary<string, string> GlobalVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public EventNode CreateEvent(EventNodeKind nodeKind = EventNodeKind.Task)
    {
        var isGroup = nodeKind == EventNodeKind.TaskGroup;
        var node = new EventNode
        {
            Id = $"evt_{Guid.NewGuid():N}"[..12],
            Title = isGroup ? $"Event Group {Events.Count + 1}" : $"Event {Events.Count + 1}",
            NodeKind = nodeKind,
            Domain = isGroup ? EventDomain.Meta : EventDomain.Story
        };
        Events.Add(node);
        if (!isGroup && string.IsNullOrWhiteSpace(StartEventId))
        {
            StartEventId = node.Id;
        }
        return node;
    }

    public EventNode? Find(string id) => Events.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed class EventNode
{
    public EventNodeKind NodeKind { get; set; } = EventNodeKind.Task;
    public string ParentGroupId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public EventDomain Domain { get; set; } = EventDomain.Story;
    public string InteractionObjectId { get; set; } = "";
    public string StateKey { get; set; } = "";
    public string StateValueOnActivate { get; set; } = "active";
    public string CompletionItemId { get; set; } = "";
    public int CompletionItemCount { get; set; } = 1;
    public string MapScope { get; set; } = "";
    public string DialogueScope { get; set; } = "";
    public string CombatScope { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool OneShot { get; set; }
    public int Priority { get; set; } = 100;
    public float CooldownSeconds { get; set; }
    public List<TriggerRule> Triggers { get; set; } = [];
    public List<DispatchAction> Actions { get; set; } = [];
    public string Notes { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventNodeKind
{
    Task,
    TaskGroup
}

public sealed class TriggerRule
{
    public TriggerType Type { get; set; } = TriggerType.Manual;
    public EventDomain SourceDomain { get; set; } = EventDomain.System;
    public string Signal { get; set; } = "";
    public string ConditionExpr { get; set; } = "";
    public int DebounceMs { get; set; }
}

public sealed class DispatchAction
{
    public DispatchActionType Type { get; set; } = DispatchActionType.StartEvent;
    public string TargetEventId { get; set; } = "";
    public int DelayMs { get; set; }
    public string PayloadJson { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventDomain
{
    System,
    Story,
    Map,
    Combat,
    Dialogue,
    Meta
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    Manual,
    Signal,
    EnterMap,
    LeaveMap,
    BattleWin,
    BattleLose,
    DialogueStart,
    DialogueEnd,
    ChoiceSelected,
    VariableChanged,
    TimerElapsed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DispatchActionType
{
    StartEvent,
    EmitSignal,
    StartDialogue,
    StartCombat,
    ChangeMap,
    SetVariable,
    CompleteQuest,
    CustomScript
}
