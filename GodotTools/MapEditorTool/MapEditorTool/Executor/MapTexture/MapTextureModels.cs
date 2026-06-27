using System.Collections.Generic;

namespace MapEditorTool.Executor.MapTexture
{
    public sealed class MapTexturePatchResult
    {
        public MapTexturePatchResult()
        {
            SceneFilePath = string.Empty;
            PatchedKeys = new List<string>();
            Summary = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public bool Patched { get; set; }
        public bool IsTemplateMap { get; set; }
        public int AddedExtResourceCount { get; set; }
        public List<string> PatchedKeys { get; set; }
        public string Summary { get; set; }
    }
}
