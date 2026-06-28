namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection
{
    public sealed class MapGraphSelectionSignalDecision
    {
        public MapGraphSelectionSignalDecision(
            bool applySelection,
            bool sourceSignalConsumed,
            MapGraphSelectionTarget target,
            int selectedIndex,
            string selectedKey,
            string sourceDescription,
            string statusText)
        {
            ApplySelection = applySelection;
            SourceSignalConsumed = sourceSignalConsumed;
            Target = target;
            SelectedIndex = selectedIndex;
            SelectedKey = selectedKey ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool ApplySelection { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public MapGraphSelectionTarget Target { get; private set; }
        public int SelectedIndex { get; private set; }
        public string SelectedKey { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
