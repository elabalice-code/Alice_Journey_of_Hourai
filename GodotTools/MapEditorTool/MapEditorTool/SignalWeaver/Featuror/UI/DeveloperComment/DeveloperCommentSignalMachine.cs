using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment
{
    public sealed class DeveloperCommentSignalMachine
    {
        private readonly DeveloperCommentSignalState _state = new DeveloperCommentSignalState();

        public DeveloperCommentSignalState State
        {
            get { return _state.Clone(); }
        }

        public DeveloperCommentSignalDecision SetMode(bool enabled, UiSignal signal)
        {
            _state.ModeEnabled = enabled;
            _state.CommentBoxRequested = false;
            _state.PendingSourceDescription = string.Empty;
            _state.Phase = enabled ? DeveloperCommentSignalPhase.Enabled : DeveloperCommentSignalPhase.Disabled;
            _state.LastSourceDescription = SafeSource(signal);
            _state.StatusText = enabled
                ? "Developer Comment Mode is ON. Click a UI element to leave a comment."
                : "Developer Comment Mode is OFF. UI clicks will not open comment input.";

            return new DeveloperCommentSignalDecision(false, true, _state.LastSourceDescription, _state.StatusText);
        }

        public DeveloperCommentSignalDecision ConsumeUiClick(UiSignal signal)
        {
            _state.ClickSignalCount++;
            _state.LastSourceDescription = SafeSource(signal);

            if (!_state.ModeEnabled)
            {
                _state.CommentBoxRequested = false;
                _state.PendingSourceDescription = string.Empty;
                _state.Phase = DeveloperCommentSignalPhase.Disabled;
                _state.StatusText = "Developer Comment Mode is OFF. UI clicks will not open comment input.";
                return new DeveloperCommentSignalDecision(false, true, _state.LastSourceDescription, _state.StatusText);
            }

            _state.CommentBoxRequested = true;
            _state.PendingSourceDescription = _state.LastSourceDescription;
            _state.RequestCount++;
            _state.Phase = DeveloperCommentSignalPhase.AwaitingInput;
            _state.StatusText = "Developer comment requested: " + _state.PendingSourceDescription;

            return new DeveloperCommentSignalDecision(true, true, _state.PendingSourceDescription, _state.StatusText);
        }

        private static string SafeSource(UiSignal signal)
        {
            return signal == null ? "(unknown source)" : signal.SourceDescription;
        }
    }
}
