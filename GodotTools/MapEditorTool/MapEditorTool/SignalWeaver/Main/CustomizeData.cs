using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation;
using MapEditorTool.SignalWeaver.Featuror.Persistence.Action;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;
using MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction;
using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Main
{
    public enum MapEditorSignalRoute
    {
        None = 0,
        General = 1,
        UiClick = 2,
        DeveloperCommentClick = 3,
        TerminalActionClick = 4,
        MapSelection = 5,
        MapSelectionById = 6,
        LinkSelection = 7,
        LinkSelectionByKey = 8,
        MapCanvasToolMode = 9,
        DeveloperCommentMode = 10
    }

    // One UI producer enters this frame, then Featuror folds it into pure consumer decisions.
    public sealed class MapEditorSignalFrame
    {
        public MapEditorSignalFrame(MapEditorSignalRoute route, UiSignal sourceSignal)
        {
            Route = route;
            SourceSignal = sourceSignal;
        }

        public MapEditorSignalRoute Route { get; private set; }
        public UiSignal SourceSignal { get; private set; }
        public bool? DeveloperCommentModeEnabled { get; set; }
        public MapEditorSignalDecisions Decisions { get; private set; } = new MapEditorSignalDecisions();
    }

    public sealed class MapEditorSignalDecisions
    {
        public DeveloperCommentSignalDecision DeveloperComment { get; set; }
        public TerminalActionSignalDecision TerminalAction { get; set; }
        public MapGraphSelectionSignalDecision MapGraphSelection { get; set; }
        public MapCanvasToolModeSignalDecision MapCanvasToolMode { get; set; }
        public MapProjectMutationSignalDecision MapProjectMutation { get; set; }
        public PersistenceActionSignalDecision PersistenceAction { get; set; }
    }

    public sealed class MapEditorSignalStates
    {
        public MapEditorSignalStates(
            DeveloperCommentSignalState developerComment,
            TerminalActionSignalState terminalAction,
            MapGraphSelectionSignalState mapGraphSelection,
            MapCanvasToolModeSignalState mapCanvasToolMode,
            MapProjectMutationSignalState mapProjectMutation,
            PersistenceActionSignalState persistenceAction)
        {
            DeveloperComment = developerComment;
            TerminalAction = terminalAction;
            MapGraphSelection = mapGraphSelection;
            MapCanvasToolMode = mapCanvasToolMode;
            MapProjectMutation = mapProjectMutation;
            PersistenceAction = persistenceAction;
        }

        public DeveloperCommentSignalState DeveloperComment { get; private set; }
        public TerminalActionSignalState TerminalAction { get; private set; }
        public MapGraphSelectionSignalState MapGraphSelection { get; private set; }
        public MapCanvasToolModeSignalState MapCanvasToolMode { get; private set; }
        public MapProjectMutationSignalState MapProjectMutation { get; private set; }
        public PersistenceActionSignalState PersistenceAction { get; private set; }
    }
}
