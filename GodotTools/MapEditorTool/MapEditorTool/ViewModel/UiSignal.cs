using System;

namespace MapEditorTool.ViewModel
{
    public sealed class UiSignal
    {
        public UiSignal(UiSignalKind kind, string sourceDescription, string eventName, DateTimeOffset createdAt)
        {
            Kind = kind;
            SourceDescription = sourceDescription ?? string.Empty;
            EventName = eventName ?? string.Empty;
            CreatedAt = createdAt;
        }

        public UiSignalKind Kind { get; private set; }
        public string SourceDescription { get; private set; }
        public string EventName { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        public string ToSummary()
        {
            return string.Format("{0:HH:mm:ss} {1} {2} from {3}", CreatedAt, Kind, EventName, SourceDescription);
        }
    }
}
