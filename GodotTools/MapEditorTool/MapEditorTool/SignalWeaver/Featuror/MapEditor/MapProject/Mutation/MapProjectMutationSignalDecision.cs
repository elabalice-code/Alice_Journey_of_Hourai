namespace MapEditorTool.SignalWeaver.Featuror.MapEditor.MapProject.Mutation
{
    public sealed class MapProjectMutationSignalDecision
    {
        public MapProjectMutationSignalDecision(
            bool requestMutation,
            bool sourceSignalConsumed,
            MapProjectMutationKind mutationKind,
            string actionKey,
            string sourceDescription,
            string statusText)
        {
            RequestMutation = requestMutation;
            SourceSignalConsumed = sourceSignalConsumed;
            MutationKind = mutationKind;
            ActionKey = actionKey ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool RequestMutation { get; private set; }
        public bool SourceSignalConsumed { get; private set; }
        public MapProjectMutationKind MutationKind { get; private set; }
        public string ActionKey { get; private set; }
        public string SourceDescription { get; private set; }
        public string StatusText { get; private set; }
    }
}
