namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection
{
    public sealed class MapGraphSelectionSignalState
    {
        public MapGraphSelectionSignalState()
        {
            Phase = MapGraphSelectionSignalPhase.Idle;
            LastTarget = MapGraphSelectionTarget.None;
            LastSourceDescription = string.Empty;
            LastSelectedKey = string.Empty;
            StatusText = "Map graph selection pipeline is idle.";
            LastSelectedIndex = -1;
        }

        public MapGraphSelectionSignalPhase Phase { get; internal set; }
        public MapGraphSelectionTarget LastTarget { get; internal set; }
        public int LastSelectedIndex { get; internal set; }
        public string LastSelectedKey { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int SelectionSignalCount { get; internal set; }
        public int ApplyCount { get; internal set; }
        public int IgnoredCount { get; internal set; }

        public MapGraphSelectionSignalState Clone()
        {
            return new MapGraphSelectionSignalState
            {
                Phase = Phase,
                LastTarget = LastTarget,
                LastSelectedIndex = LastSelectedIndex,
                LastSelectedKey = LastSelectedKey,
                LastSourceDescription = LastSourceDescription,
                StatusText = StatusText,
                SelectionSignalCount = SelectionSignalCount,
                ApplyCount = ApplyCount,
                IgnoredCount = IgnoredCount
            };
        }
    }
}
