using System;

namespace MapEditorTool.ViewModel
{
    public sealed class UiSignal
    {
        public UiSignal(UiSignalKind kind, string sourceDescription, string eventName, DateTimeOffset createdAt)
            : this(kind, sourceDescription, eventName, createdAt, string.Empty)
        {
        }

        public UiSignal(UiSignalKind kind, string sourceDescription, string eventName, DateTimeOffset createdAt, string actionKey)
            : this(kind, sourceDescription, eventName, createdAt, actionKey, false, 0)
        {
        }

        public UiSignal(
            UiSignalKind kind,
            string sourceDescription,
            string eventName,
            DateTimeOffset createdAt,
            string actionKey,
            bool hasNumericValue,
            int numericValue)
            : this(kind, sourceDescription, eventName, createdAt, actionKey, hasNumericValue, numericValue, string.Empty)
        {
        }

        public UiSignal(
            UiSignalKind kind,
            string sourceDescription,
            string eventName,
            DateTimeOffset createdAt,
            string actionKey,
            bool hasNumericValue,
            int numericValue,
            string stringValue)
            : this(kind, sourceDescription, eventName, createdAt, actionKey, hasNumericValue, numericValue, stringValue, string.Empty, string.Empty, string.Empty, string.Empty)
        {
        }

        public UiSignal(
            UiSignalKind kind,
            string sourceDescription,
            string eventName,
            DateTimeOffset createdAt,
            string actionKey,
            bool hasNumericValue,
            int numericValue,
            string stringValue,
            string stringValue2,
            string stringValue3,
            string stringValue4,
            string stringValue5)
        {
            Kind = kind;
            SourceDescription = sourceDescription ?? string.Empty;
            EventName = eventName ?? string.Empty;
            CreatedAt = createdAt;
            ActionKey = actionKey ?? string.Empty;
            HasNumericValue = hasNumericValue;
            NumericValue = numericValue;
            StringValue = stringValue ?? string.Empty;
            StringValue2 = stringValue2 ?? string.Empty;
            StringValue3 = stringValue3 ?? string.Empty;
            StringValue4 = stringValue4 ?? string.Empty;
            StringValue5 = stringValue5 ?? string.Empty;
        }

        public UiSignalKind Kind { get; private set; }
        public string SourceDescription { get; private set; }
        public string EventName { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public string ActionKey { get; private set; }
        public bool HasNumericValue { get; private set; }
        public int NumericValue { get; private set; }
        public string StringValue { get; private set; }
        public string StringValue2 { get; private set; }
        public string StringValue3 { get; private set; }
        public string StringValue4 { get; private set; }
        public string StringValue5 { get; private set; }

        public string ToSummary()
        {
            var actionText = string.IsNullOrWhiteSpace(ActionKey) ? string.Empty : " action=" + ActionKey;
            var numericText = HasNumericValue ? " value=" + NumericValue : string.Empty;
            var stringText = string.IsNullOrWhiteSpace(StringValue) ? string.Empty : " text=" + StringValue;
            return string.Format("{0:HH:mm:ss} {1} {2}{3}{4}{5} from {6}", CreatedAt, Kind, EventName, actionText, numericText, stringText, SourceDescription);
        }
    }
}
