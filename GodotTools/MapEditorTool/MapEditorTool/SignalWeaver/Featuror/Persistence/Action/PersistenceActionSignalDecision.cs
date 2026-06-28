namespace MapEditorTool.SignalWeaver.Featuror.Persistence.Action
{
    public sealed class PersistenceActionSignalDecision
    {
        public PersistenceActionSignalDecision(
            bool requestAction,
            bool sourceSignalConsumed,
            PersistenceActionKind actionKind,
            string actionKey,
            string sourceDescription,
            string statusText)
        {
            RequestAction = requestAction;
            SourceSignalConsumed = sourceSignalConsumed;
            ActionKind = actionKind;
            ActionKey = actionKey ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool RequestAction { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public PersistenceActionKind ActionKind { get; private set; }
        public string ActionKey { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
