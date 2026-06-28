namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation
{
    public sealed class MapProjectMutationSignalState
    {
        public MapProjectMutationSignalState()
        {
            Phase = MapProjectMutationSignalPhase.Idle;
            LastMutationKind = MapProjectMutationKind.None;
            LastActionKey = string.Empty;
            LastSourceDescription = string.Empty;
            StatusText = "Map project mutation pipeline is idle.";
        }

        public MapProjectMutationSignalPhase Phase { get; internal set; }
        public MapProjectMutationKind LastMutationKind { get; internal set; }
        public string LastActionKey { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int ClickSignalCount { get; internal set; }
        public int RequestCount { get; internal set; }
        public int IgnoredCount { get; internal set; }

        public MapProjectMutationSignalState Clone()
        {
            return new MapProjectMutationSignalState
            {
                Phase = Phase,
                LastMutationKind = LastMutationKind,
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
