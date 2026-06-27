namespace MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment
{
    public sealed class DeveloperCommentSignalDecision
    {
        public DeveloperCommentSignalDecision(bool openCommentBox, bool sourceSignalConsumed, string sourceDescription, string statusText)
        {
            OpenCommentBox = openCommentBox;
            SourceSignalConsumed = sourceSignalConsumed;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool OpenCommentBox { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
