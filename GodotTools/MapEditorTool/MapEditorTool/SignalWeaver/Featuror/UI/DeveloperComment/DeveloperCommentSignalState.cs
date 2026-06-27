namespace MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment
{
    public sealed class DeveloperCommentSignalState
    {
        public DeveloperCommentSignalState()
        {
            PendingSourceDescription = string.Empty;
            LastSourceDescription = string.Empty;
            StatusText = "Developer Comment Mode is OFF. UI clicks will not open comment input.";
            Phase = DeveloperCommentSignalPhase.Disabled;
        }

        public bool ModeEnabled { get; internal set; }
        public bool CommentBoxRequested { get; internal set; }
        public DeveloperCommentSignalPhase Phase { get; internal set; }
        public string PendingSourceDescription { get; internal set; }
        public string LastSourceDescription { get; internal set; }
        public string StatusText { get; internal set; }
        public int ClickSignalCount { get; internal set; }
        public int RequestCount { get; internal set; }

        public DeveloperCommentSignalState Clone()
        {
            return new DeveloperCommentSignalState
            {
                ModeEnabled = ModeEnabled,
                CommentBoxRequested = CommentBoxRequested,
                Phase = Phase,
                PendingSourceDescription = PendingSourceDescription,
                LastSourceDescription = LastSourceDescription,
                StatusText = StatusText,
                ClickSignalCount = ClickSignalCount,
                RequestCount = RequestCount
            };
        }
    }
}
