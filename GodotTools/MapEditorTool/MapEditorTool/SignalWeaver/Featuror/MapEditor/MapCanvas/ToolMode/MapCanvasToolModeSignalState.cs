namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode
{
    public sealed class MapCanvasToolModeSignalState
    {
        public MapCanvasToolModeSignalState()
        {
            Phase = MapCanvasToolModeSignalPhase.Idle;
            ViewModeKey = "map";
            CollisionEditStyleKey = "tile";
            CollisionEditModeKey = "tile-set-collision";
            CollisionModeKey = "tile-foreground";
            CollisionToolKey = "vertex";
            CollisionTargetKey = "tile";
            LastSourceDescription = string.Empty;
            StatusText = "Map canvas tool mode pipeline is idle.";
        }

        public MapCanvasToolModeSignalPhase Phase { get; internal set; }
        public string ViewModeKey { get; internal set; }
        public string CollisionEditStyleKey { get; internal set; }
        public string CollisionEditModeKey { get; internal set; }
        public string CollisionModeKey { get; internal set; }
        public string CollisionToolKey { get; internal set; }
        public string CollisionTargetKey { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int ToolModeSignalCount { get; internal set; }
        public int ApplyCount { get; internal set; }
        public int IgnoredCount { get; internal set; }
        public int CoercedCount { get; internal set; }

        public MapCanvasToolModeSignalState Clone()
        {
            return new MapCanvasToolModeSignalState
            {
                Phase = Phase,
                ViewModeKey = ViewModeKey,
                CollisionEditStyleKey = CollisionEditStyleKey,
                CollisionEditModeKey = CollisionEditModeKey,
                CollisionModeKey = CollisionModeKey,
                CollisionToolKey = CollisionToolKey,
                CollisionTargetKey = CollisionTargetKey,
                LastSourceDescription = LastSourceDescription,
                StatusText = StatusText,
                ToolModeSignalCount = ToolModeSignalCount,
                ApplyCount = ApplyCount,
                IgnoredCount = IgnoredCount,
                CoercedCount = CoercedCount
            };
        }
    }
}
