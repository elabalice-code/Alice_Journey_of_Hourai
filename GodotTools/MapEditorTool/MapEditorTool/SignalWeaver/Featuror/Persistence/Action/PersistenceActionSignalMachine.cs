using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.Persistence.Action
{
    public sealed class PersistenceActionSignalMachine
    {
        private readonly PersistenceActionSignalState _state = new PersistenceActionSignalState();

        public PersistenceActionSignalState State
        {
            get { return _state.Clone(); }
        }

        public PersistenceActionSignalDecision ConsumeUiClick(UiSignal signal)
        {
            _state.ClickSignalCount++;
            _state.LastActionKey = signal == null ? string.Empty : signal.ActionKey;
            _state.LastSourceDescription = signal == null ? "(unknown source)" : signal.SourceDescription;
            _state.LastActionKind = ToActionKind(_state.LastActionKey);

            if (signal == null || signal.Kind != UiSignalKind.Click || _state.LastActionKind == PersistenceActionKind.None)
            {
                _state.Phase = PersistenceActionSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "UI click has no persistence action intent.";
                return BuildDecision(false, true);
            }

            _state.Phase = PersistenceActionSignalPhase.Requested;
            _state.RequestCount++;
            _state.StatusText = "Persistence action requested: " + _state.LastActionKind;
            return BuildDecision(true, true);
        }

        private PersistenceActionSignalDecision BuildDecision(bool requestAction, bool sourceSignalConsumed)
        {
            return new PersistenceActionSignalDecision(
                requestAction,
                sourceSignalConsumed,
                _state.LastActionKind,
                _state.LastActionKey,
                _state.LastSourceDescription,
                _state.StatusText);
        }

        private static PersistenceActionKind ToActionKind(string actionKey)
        {
            switch (actionKey ?? string.Empty)
            {
                case "menu.file.newProject":
                    return PersistenceActionKind.NewProject;
                case "menu.file.openProject":
                    return PersistenceActionKind.OpenProject;
                case "menu.file.saveProject":
                    return PersistenceActionKind.SaveProject;
                case "menu.file.saveProjectAs":
                    return PersistenceActionKind.SaveProjectAs;
                case "menu.file.importFromGodot":
                    return PersistenceActionKind.ImportFromGodot;
                case "menu.file.applySelectedMapToGodot":
                    return PersistenceActionKind.ApplySelectedMapToGodot;
                case "menu.file.exit":
                    return PersistenceActionKind.Exit;
                default:
                    return PersistenceActionKind.None;
            }
        }
    }
}
