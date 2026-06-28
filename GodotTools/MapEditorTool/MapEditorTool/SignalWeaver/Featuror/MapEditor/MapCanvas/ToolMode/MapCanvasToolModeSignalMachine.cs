using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode
{
    public sealed class MapCanvasToolModeSignalMachine
    {
        private readonly MapCanvasToolModeSignalState _state = new MapCanvasToolModeSignalState();

        public MapCanvasToolModeSignalState State
        {
            get { return _state.Clone(); }
        }

        public MapCanvasToolModeSignalDecision ConsumeToolMode(UiSignal signal)
        {
            _state.ToolModeSignalCount++;
            _state.LastSourceDescription = signal == null ? "(unknown source)" : signal.SourceDescription;

            if (signal == null || signal.Kind != UiSignalKind.ValueChanged || string.IsNullOrWhiteSpace(signal.StringValue))
            {
                _state.Phase = MapCanvasToolModeSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "Map canvas tool mode signal ignored: missing mode facts.";
                return BuildDecision(false, true);
            }

            _state.ViewModeKey = SafeKey(signal.StringValue, _state.ViewModeKey);
            var incomingStyleKey = SafeKey(signal.StringValue2, _state.CollisionEditStyleKey);
            _state.CollisionEditStyleKey = NormalizeCollisionEditStyleKey(incomingStyleKey);
            _state.CollisionEditModeKey = ToCollisionEditorModeKey(_state.CollisionEditStyleKey);
            _state.CollisionModeKey = ToCollisionModeKey(_state.CollisionEditStyleKey);
            _state.CollisionToolKey = NormalizeCollisionToolKey(signal.StringValue3, _state.CollisionToolKey);
            _state.CollisionTargetKey = ToCollisionTargetKey(_state.CollisionEditStyleKey);
            _state.Phase = string.Equals(incomingStyleKey, _state.CollisionEditStyleKey, System.StringComparison.Ordinal)
                ? MapCanvasToolModeSignalPhase.Updated
                : MapCanvasToolModeSignalPhase.Coerced;
            _state.ApplyCount++;
            if (_state.Phase == MapCanvasToolModeSignalPhase.Coerced)
                _state.CoercedCount++;
            _state.StatusText = "Map canvas tool mode updated: view=" + _state.ViewModeKey + "; edit=" + _state.CollisionEditStyleKey + "; tool=" + _state.CollisionToolKey;
            return BuildDecision(true, true);
        }

        private MapCanvasToolModeSignalDecision BuildDecision(bool applyToolMode, bool sourceSignalConsumed)
        {
            return new MapCanvasToolModeSignalDecision(
                applyToolMode,
                sourceSignalConsumed,
                _state.ViewModeKey,
                _state.CollisionEditModeKey,
                _state.CollisionModeKey,
                _state.CollisionToolKey,
                _state.CollisionTargetKey,
                _state.LastSourceDescription,
                _state.StatusText);
        }

        private static string SafeKey(string value, string fallback)
        {
            value = (value ?? string.Empty).Trim();
            return value.Length == 0 ? fallback : value;
        }

        private static string NormalizeCollisionEditStyleKey(string value)
        {
            value = SafeKey(value, "layout");
            if (string.Equals(value, "tile", System.StringComparison.Ordinal) ||
                string.Equals(value, "tile-set-collision", System.StringComparison.Ordinal))
                return "tile";

            return "layout";
        }

        private static string ToCollisionEditorModeKey(string styleKey)
        {
            return string.Equals(styleKey, "tile", System.StringComparison.Ordinal)
                ? "tile-set-collision"
                : "collision-layout";
        }

        private static string ToCollisionModeKey(string styleKey)
        {
            if (string.Equals(styleKey, "tile", System.StringComparison.Ordinal))
                return "tile-foreground";

            return "foreground-texture";
        }

        private static string ToCollisionTargetKey(string styleKey)
        {
            if (string.Equals(styleKey, "tile", System.StringComparison.Ordinal))
                return "tile";

            return "foreground-texture";
        }

        private static string NormalizeCollisionToolKey(string value, string fallback)
        {
            value = SafeKey(value, fallback);
            if (string.Equals(value, "vertex", System.StringComparison.Ordinal) ||
                string.Equals(value, "move", System.StringComparison.Ordinal) ||
                string.Equals(value, "rotate", System.StringComparison.Ordinal) ||
                string.Equals(value, "scale", System.StringComparison.Ordinal) ||
                string.Equals(value, "add-box", System.StringComparison.Ordinal) ||
                string.Equals(value, "remove", System.StringComparison.Ordinal))
                return value;

            return "select";
        }
    }
}
