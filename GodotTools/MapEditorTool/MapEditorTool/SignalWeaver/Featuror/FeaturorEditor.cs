using MapEditorTool.SignalWeaver.Main;

namespace MapEditorTool.SignalWeaver.Featuror
{
    public static class FeaturorEditor
    {
        // Mature feature families are routed here so ViewModel does not need to know
        // each feature machine or its dispatch order.
        public static void Apply(MapEditorSignalFrame frame, FeaturorMachineSet machines)
        {
            if (frame == null || machines == null)
                return;

            switch (frame.Route)
            {
                case MapEditorSignalRoute.UiClick:
                    frame.Decisions.DeveloperComment = machines.DeveloperComment.ConsumeUiClick(frame.SourceSignal);
                    frame.Decisions.TerminalAction = machines.TerminalAction.ConsumeUiClick(frame.SourceSignal);
                    frame.Decisions.MapProjectMutation = machines.MapProjectMutation.ConsumeUiClick(frame.SourceSignal);
                    frame.Decisions.PersistenceAction = machines.PersistenceAction.ConsumeUiClick(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.DeveloperCommentClick:
                    frame.Decisions.DeveloperComment = machines.DeveloperComment.ConsumeUiClick(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.TerminalActionClick:
                    frame.Decisions.TerminalAction = machines.TerminalAction.ConsumeUiClick(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.MapSelection:
                case MapEditorSignalRoute.MapSelectionById:
                    ConsumeDeveloperCommentForSpecializedSignal(frame, machines);
                    frame.Decisions.MapGraphSelection = machines.MapGraphSelection.ConsumeMapSelection(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.LinkSelection:
                case MapEditorSignalRoute.LinkSelectionByKey:
                    ConsumeDeveloperCommentForSpecializedSignal(frame, machines);
                    frame.Decisions.MapGraphSelection = machines.MapGraphSelection.ConsumeLinkSelection(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.MapCanvasToolMode:
                    ConsumeDeveloperCommentForSpecializedSignal(frame, machines);
                    frame.Decisions.MapCanvasToolMode = machines.MapCanvasToolMode.ConsumeToolMode(frame.SourceSignal);
                    break;

                case MapEditorSignalRoute.DeveloperCommentMode:
                    frame.Decisions.DeveloperComment = machines.DeveloperComment.SetMode(
                        frame.DeveloperCommentModeEnabled.GetValueOrDefault(false),
                        frame.SourceSignal);
                    break;
            }
        }

        private static void ConsumeDeveloperCommentForSpecializedSignal(
            MapEditorSignalFrame frame,
            FeaturorMachineSet machines)
        {
            // DeveloperComment is the developer mailbox. Do not remove it from
            // specialized signal routes when pruning feature-specific signals.
            frame.Decisions.DeveloperComment = machines.DeveloperComment.ConsumeUiClick(frame.SourceSignal);
        }
    }
}
