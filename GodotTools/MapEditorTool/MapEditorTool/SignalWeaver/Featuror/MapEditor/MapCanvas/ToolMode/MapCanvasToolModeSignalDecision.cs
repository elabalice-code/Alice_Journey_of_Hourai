namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode
{
    public sealed class MapCanvasToolModeSignalDecision
    {
        public MapCanvasToolModeSignalDecision(
            bool applyToolMode,
            bool sourceSignalConsumed,
            string viewModeKey,
            string collisionEditModeKey,
            string collisionModeKey,
            string collisionToolKey,
            string collisionTargetKey,
            string sourceDescription,
            string statusText)
        {
            ApplyToolMode = applyToolMode;
            SourceSignalConsumed = sourceSignalConsumed;
            ViewModeKey = viewModeKey ?? string.Empty;
            CollisionEditModeKey = collisionEditModeKey ?? string.Empty;
            CollisionModeKey = collisionModeKey ?? string.Empty;
            CollisionToolKey = collisionToolKey ?? string.Empty;
            CollisionTargetKey = collisionTargetKey ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool ApplyToolMode { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public string ViewModeKey { get; private set; }
        public string CollisionEditModeKey { get; private set; }
        public string CollisionModeKey { get; private set; }
        public string CollisionToolKey { get; private set; }
        public string CollisionTargetKey { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
