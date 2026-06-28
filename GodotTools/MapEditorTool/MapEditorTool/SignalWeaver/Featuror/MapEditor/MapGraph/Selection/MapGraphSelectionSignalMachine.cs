using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection
{
    public sealed class MapGraphSelectionSignalMachine
    {
        private readonly MapGraphSelectionSignalState _state = new MapGraphSelectionSignalState();

        public MapGraphSelectionSignalState State
        {
            get { return _state.Clone(); }
        }

        public MapGraphSelectionSignalDecision ConsumeMapSelection(UiSignal signal)
        {
            return ConsumeSelection(signal, MapGraphSelectionTarget.Map);
        }

        public MapGraphSelectionSignalDecision ConsumeLinkSelection(UiSignal signal)
        {
            return ConsumeSelection(signal, MapGraphSelectionTarget.Link);
        }

        private MapGraphSelectionSignalDecision ConsumeSelection(UiSignal signal, MapGraphSelectionTarget target)
        {
            _state.SelectionSignalCount++;
            _state.LastTarget = target;
            _state.LastSelectedIndex = signal == null || !signal.HasNumericValue ? -1 : signal.NumericValue;
            _state.LastSelectedKey = signal == null ? string.Empty : signal.StringValue;
            _state.LastSourceDescription = signal == null ? "(unknown source)" : signal.SourceDescription;

            if (signal == null || signal.Kind != UiSignalKind.SelectionChanged)
            {
                _state.Phase = MapGraphSelectionSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "Selection signal ignored: wrong signal kind.";
                return new MapGraphSelectionSignalDecision(false, true, target, _state.LastSelectedIndex, _state.LastSelectedKey, _state.LastSourceDescription, _state.StatusText);
            }

            if (!signal.HasNumericValue && string.IsNullOrWhiteSpace(signal.StringValue))
            {
                _state.Phase = MapGraphSelectionSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "Selection signal ignored: missing selected target.";
                return new MapGraphSelectionSignalDecision(false, true, target, _state.LastSelectedIndex, _state.LastSelectedKey, _state.LastSourceDescription, _state.StatusText);
            }

            _state.Phase = MapGraphSelectionSignalPhase.Selected;
            _state.ApplyCount++;
            _state.StatusText = signal.HasNumericValue
                ? target + " selection requested: index=" + _state.LastSelectedIndex
                : target + " selection requested: key=" + _state.LastSelectedKey;
            return new MapGraphSelectionSignalDecision(true, true, target, _state.LastSelectedIndex, _state.LastSelectedKey, _state.LastSourceDescription, _state.StatusText);
        }
    }
}
