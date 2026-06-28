using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation
{
    public sealed class MapProjectMutationSignalMachine
    {
        private readonly MapProjectMutationSignalState _state = new MapProjectMutationSignalState();

        public MapProjectMutationSignalState State
        {
            get { return _state.Clone(); }
        }

        public MapProjectMutationSignalDecision ConsumeUiClick(UiSignal signal)
        {
            _state.ClickSignalCount++;
            _state.LastActionKey = signal == null ? string.Empty : signal.ActionKey;
            _state.LastSourceDescription = signal == null ? "(unknown source)" : signal.SourceDescription;
            _state.LastMutationKind = ToMutationKind(_state.LastActionKey);

            if (signal == null || signal.Kind != UiSignalKind.Click || _state.LastMutationKind == MapProjectMutationKind.None)
            {
                _state.Phase = MapProjectMutationSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "UI click has no map project mutation intent.";
                return BuildDecision(false, true);
            }

            _state.Phase = MapProjectMutationSignalPhase.Requested;
            _state.RequestCount++;
            _state.StatusText = "Map project mutation requested: " + _state.LastMutationKind;
            return BuildDecision(true, true);
        }

        private MapProjectMutationSignalDecision BuildDecision(bool requestMutation, bool sourceSignalConsumed)
        {
            return new MapProjectMutationSignalDecision(
                requestMutation,
                sourceSignalConsumed,
                _state.LastMutationKind,
                _state.LastActionKey,
                _state.LastSourceDescription,
                _state.StatusText);
        }

        private static MapProjectMutationKind ToMutationKind(string actionKey)
        {
            switch (actionKey ?? string.Empty)
            {
                case "context.maps.add":
                    return MapProjectMutationKind.AddMap;
                case "context.maps.delete":
                    return MapProjectMutationKind.DeleteMap;
                case "context.maps.pin":
                    return MapProjectMutationKind.PinMap;
                case "context.links.add":
                    return MapProjectMutationKind.AddLink;
                case "context.links.delete":
                    return MapProjectMutationKind.DeleteLink;
                default:
                    return MapProjectMutationKind.None;
            }
        }
    }
}
