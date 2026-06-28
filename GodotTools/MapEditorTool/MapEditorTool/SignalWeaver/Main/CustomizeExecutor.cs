using MapEditorTool.SignalWeaver.Featuror;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapCanvas.ToolMode;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapGraph.Selection;
using MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation;
using MapEditorTool.SignalWeaver.Featuror.Persistence.Action;
using MapEditorTool.SignalWeaver.Featuror.UI.DeveloperComment;
using MapEditorTool.SignalWeaver.Featuror.UI.TerminalAction;
using MapEditorTool.ViewModel;

namespace MapEditorTool.SignalWeaver.Main
{
    // Central cable harness for MapEditorTool UI producer signals.
    public sealed class MapEditorSignalWeaverHost
    {
        private readonly FeaturorMachineSet _machines = new FeaturorMachineSet();

        public MapEditorSignalStates States
        {
            get { return _machines.CreateStateSnapshot(); }
        }

        public MapEditorSignalFrame Submit(MapEditorSignalRoute route, UiSignal signal)
        {
            var frame = new MapEditorSignalFrame(route, signal);
            FeaturorEditor.Apply(frame, _machines);
            return frame;
        }

        public MapEditorSignalFrame SetDeveloperCommentMode(bool enabled, UiSignal signal)
        {
            var frame = new MapEditorSignalFrame(MapEditorSignalRoute.DeveloperCommentMode, signal)
            {
                DeveloperCommentModeEnabled = enabled
            };
            FeaturorEditor.Apply(frame, _machines);
            return frame;
        }
    }

    // Compatibility facade for the old file name. New code should depend on MapEditorSignalWeaverHost.
    public static class CustomizeExecutor
    {
        public static MapEditorSignalFrame Execute(MapEditorSignalWeaverHost host, MapEditorSignalRoute route, UiSignal signal)
        {
            if (host == null)
                return new MapEditorSignalFrame(MapEditorSignalRoute.None, signal);

            return host.Submit(route, signal);
        }
    }

    public sealed class FeaturorMachineSet
    {
        public FeaturorMachineSet()
        {
            DeveloperComment = new DeveloperCommentSignalMachine();
            TerminalAction = new TerminalActionSignalMachine();
            MapGraphSelection = new MapGraphSelectionSignalMachine();
            MapCanvasToolMode = new MapCanvasToolModeSignalMachine();
            MapProjectMutation = new MapProjectMutationSignalMachine();
            PersistenceAction = new PersistenceActionSignalMachine();
        }

        public DeveloperCommentSignalMachine DeveloperComment { get; private set; }
        public TerminalActionSignalMachine TerminalAction { get; private set; }
        public MapGraphSelectionSignalMachine MapGraphSelection { get; private set; }
        public MapCanvasToolModeSignalMachine MapCanvasToolMode { get; private set; }
        public MapProjectMutationSignalMachine MapProjectMutation { get; private set; }
        public PersistenceActionSignalMachine PersistenceAction { get; private set; }

        public MapEditorSignalStates CreateStateSnapshot()
        {
            return new MapEditorSignalStates(
                DeveloperComment.State,
                TerminalAction.State,
                MapGraphSelection.State,
                MapCanvasToolMode.State,
                MapProjectMutation.State,
                PersistenceAction.State);
        }
    }
}
