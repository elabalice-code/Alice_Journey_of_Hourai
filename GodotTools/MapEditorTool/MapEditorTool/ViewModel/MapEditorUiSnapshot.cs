using System;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;

namespace MapEditorTool.ViewModel
{
    public sealed class MapEditorUiSnapshot
    {
        public MapEditorUiSnapshot()
        {
            MapNames = new string[0];
            LinkNames = new string[0];
            MapState = new MapShellState();
            LinkState = new LinkShellState();
            StatusText = string.Empty;
            LastSignalSummary = string.Empty;
            LastDeveloperComment = string.Empty;
            LastReportSummary = string.Empty;
            CurrentProjectPath = string.Empty;
            DeveloperCommentRequestSource = string.Empty;
            DeveloperCommentState = new DeveloperCommentSignalState();
        }

        public bool DeveloperCommentModeEnabled { get; set; }
        public bool DeveloperCommentOpenRequested { get; set; }
        public bool DeveloperCommentSourceSignalConsumed { get; set; }
        public string DeveloperCommentRequestSource { get; set; }
        public DeveloperCommentSignalState DeveloperCommentState { get; set; }
        public string StatusText { get; set; }
        public string LastSignalSummary { get; set; }
        public string LastDeveloperComment { get; set; }
        public string LastReportSummary { get; set; }
        public string CurrentProjectPath { get; set; }
        public bool ProjectDirty { get; set; }
        public int SignalCount { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; }
        public int SelectedMapIndex { get; set; }
        public string[] MapNames { get; set; }
        public string[] LinkNames { get; set; }
        public MapShellState MapState { get; set; }
        public LinkShellState LinkState { get; set; }
    }
}
