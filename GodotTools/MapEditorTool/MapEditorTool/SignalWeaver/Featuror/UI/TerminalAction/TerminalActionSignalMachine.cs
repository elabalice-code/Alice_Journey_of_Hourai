using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction
{
    public sealed class TerminalActionSignalMachine
    {
        private readonly TerminalActionSignalState _state = new TerminalActionSignalState();

        public TerminalActionSignalState State
        {
            get { return _state.Clone(); }
        }

        public TerminalActionSignalDecision ConsumeUiClick(UiSignal signal)
        {
            _state.ClickSignalCount++;
            _state.LastActionKey = SafeActionKey(signal);
            _state.LastSourceDescription = SafeSource(signal);

            if (signal == null || signal.Kind != UiSignalKind.Click || string.IsNullOrWhiteSpace(signal.ActionKey))
            {
                _state.Phase = TerminalActionSignalPhase.Ignored;
                _state.IgnoredCount++;
                _state.StatusText = "UI click has no terminal action intent.";
                return new TerminalActionSignalDecision(false, true, _state.LastActionKey, _state.LastSourceDescription, _state.StatusText);
            }

            _state.Phase = TerminalActionSignalPhase.Requested;
            _state.RequestCount++;
            _state.StatusText = "Terminal UI action requested: " + _state.LastActionKey;
            return new TerminalActionSignalDecision(true, true, _state.LastActionKey, _state.LastSourceDescription, _state.StatusText);
        }

        private static string SafeActionKey(UiSignal signal)
        {
            return signal == null ? string.Empty : signal.ActionKey;
        }

        private static string SafeSource(UiSignal signal)
        {
            return signal == null ? "(unknown source)" : signal.SourceDescription;
        }
    }
}
