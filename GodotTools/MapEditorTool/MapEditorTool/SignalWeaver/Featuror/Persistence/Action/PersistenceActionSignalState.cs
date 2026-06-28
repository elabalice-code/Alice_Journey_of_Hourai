namespace MapEditorTool.SignalWeaver.Featuror.Persistence.Action
{
    public sealed class PersistenceActionSignalState
    {
        public PersistenceActionSignalState()
        {
            Phase = PersistenceActionSignalPhase.Idle;
            LastActionKind = PersistenceActionKind.None;
            LastActionKey = string.Empty;
            LastSourceDescription = string.Empty;
            StatusText = "Persistence action pipeline is idle.";
        }

        public PersistenceActionSignalPhase Phase { get; internal set; }
        public PersistenceActionKind LastActionKind { get; internal set; }
        public string LastActionKey { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int ClickSignalCount { get; internal set; }
        public int RequestCount { get; internal set; }
        public int IgnoredCount { get; internal set; }

        public PersistenceActionSignalState Clone()
        {
            return new PersistenceActionSignalState
            {
                Phase = Phase,
                LastActionKind = LastActionKind,
                LastActionKey = LastActionKey,
                LastSourceDescription = LastSourceDescription,
                StatusText = StatusText,
                ClickSignalCount = ClickSignalCount,
                RequestCount = RequestCount,
                IgnoredCount = IgnoredCount
            };
        }
    }
}
