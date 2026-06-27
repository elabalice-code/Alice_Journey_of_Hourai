namespace MapEditorTool.Executor.ScenePatch
{
    public sealed class ScenePatchResult
    {
        public ScenePatchResult()
        {
            SceneFilePath = string.Empty;
            NodePath = string.Empty;
            PatchedKey = string.Empty;
            NewRawValue = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public string NodePath { get; set; }
        public bool Patched { get; set; }
        public string PatchedKey { get; set; }
        public string NewRawValue { get; set; }
    }
}
