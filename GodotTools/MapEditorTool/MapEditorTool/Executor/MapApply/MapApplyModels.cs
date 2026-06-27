using System.Collections.Generic;

namespace MapEditorTool.Executor.MapApply
{
    public sealed class MapApplyResult
    {
        public MapApplyResult()
        {
            SceneFilePath = string.Empty;
            Steps = new List<string>();
            Summary = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public bool CreatedScene { get; set; }
        public bool CreatedCollisionFiles { get; set; }
        public bool PatchedRuntimeNodes { get; set; }
        public bool PatchedTextures { get; set; }
        public bool PatchedTextureMetadata { get; set; }
        public bool PatchedBackgroundTileLayerVisibility { get; set; }
        public bool PatchedCollisionMetadata { get; set; }
        public List<string> Steps { get; set; }
        public string Summary { get; set; }
    }
}
