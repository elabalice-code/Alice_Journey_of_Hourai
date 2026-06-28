namespace MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction
{
    public sealed class TerminalActionSignalDecision
    {
        public TerminalActionSignalDecision(bool executeAction, bool sourceSignalConsumed, string actionKey, string sourceDescription, string statusText)
        {
            ExecuteAction = executeAction;
            SourceSignalConsumed = sourceSignalConsumed;
            ActionKey = actionKey ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool ExecuteAction { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public string ActionKey { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
