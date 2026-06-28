namespace MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction
{
    public sealed class TerminalActionSignalState
    {
        public TerminalActionSignalState()
        {
            LastActionKey = string.Empty;
            LastSourceDescription = string.Empty;
            StatusText = "Terminal UI action pipeline is idle.";
            Phase = TerminalActionSignalPhase.Idle;
        }

        public TerminalActionSignalPhase Phase { get; internal set; }
        public string LastActionKey { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int ClickSignalCount { get; internal set; }
        public int RequestCount { get; internal set; }
        public int IgnoredCount { get; internal set; }

        public TerminalActionSignalState Clone()
        {
            return new TerminalActionSignalState
            {
                Phase = Phase,
                LastActionKey = LastActionKey,
                LastSourceDescription = LastSourceDescription,
                StatusText = StatusText,
                ClickSignalCount = ClickSignalCount,
                RequestCount = RequestCount,
                IgnoredCount = IgnoredCount
            };
        }
    }
}
