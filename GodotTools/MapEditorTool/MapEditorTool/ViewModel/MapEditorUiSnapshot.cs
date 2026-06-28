using System;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation;
using MapEditorTool.SignalWeaver.Featuror.Persistence.Action;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;
using MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction;

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
            PinnedStartingMapPath = string.Empty;
            DeveloperCommentRequestSource = string.Empty;
            DeveloperCommentState = new DeveloperCommentSignalState();
            TerminalActionKey = string.Empty;
            TerminalActionRequestSource = string.Empty;
            TerminalActionState = new TerminalActionSignalState();
            MapGraphSelectionState = new MapGraphSelectionSignalState();
            MapCanvasToolModeState = new MapCanvasToolModeSignalState();
            MapProjectMutationActionKey = string.Empty;
            MapProjectMutationRequestSource = string.Empty;
            MapProjectMutationState = new MapProjectMutationSignalState();
            PersistenceActionKey = string.Empty;
            PersistenceActionRequestSource = string.Empty;
            PersistenceActionState = new PersistenceActionSignalState();
        }

        public bool DeveloperCommentModeEnabled { get; set; }
        public bool DeveloperCommentOpenRequested { get; set; }
        public bool DeveloperCommentSourceSignalConsumed { get; set; }
        public string DeveloperCommentRequestSource { get; set; }
        public DeveloperCommentSignalState DeveloperCommentState { get; set; }
        public bool TerminalActionRequested { get; set; }
        public bool TerminalActionSourceSignalConsumed { get; set; }
        public string TerminalActionKey { get; set; }
        public string TerminalActionRequestSource { get; set; }
        public TerminalActionSignalState TerminalActionState { get; set; }
        public MapGraphSelectionSignalState MapGraphSelectionState { get; set; }
        public MapCanvasToolModeSignalState MapCanvasToolModeState { get; set; }
        public bool MapProjectMutationRequested { get; set; }
        public bool MapProjectMutationSourceSignalConsumed { get; set; }
        public MapProjectMutationKind MapProjectMutationKind { get; set; }
        public string MapProjectMutationActionKey { get; set; }
        public string MapProjectMutationRequestSource { get; set; }
        public MapProjectMutationSignalState MapProjectMutationState { get; set; }
        public bool PersistenceActionRequested { get; set; }
        public bool PersistenceActionSourceSignalConsumed { get; set; }
        public PersistenceActionKind PersistenceActionKind { get; set; }
        public string PersistenceActionKey { get; set; }
        public string PersistenceActionRequestSource { get; set; }
        public PersistenceActionSignalState PersistenceActionState { get; set; }
        public string StatusText { get; set; }
        public string LastSignalSummary { get; set; }
        public string LastDeveloperComment { get; set; }
        public string LastReportSummary { get; set; }
        public string CurrentProjectPath { get; set; }
        public string PinnedStartingMapPath { get; set; }
        public bool ProjectDirty { get; set; }
        public int SignalCount { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; }
        public int SelectedMapIndex { get; set; }
        public int SelectedLinkIndex { get; set; }
        public string[] MapNames { get; set; }
        public string[] LinkNames { get; set; }
        public MapShellState MapState { get; set; }
        public LinkShellState LinkState { get; set; }
    }
}
