using System.Collections.Generic;

namespace MapEditorTool.Executor.PortalAnimation
{
    public sealed class PortalAnimationImportResult
    {
        public PortalAnimationImportResult()
        {
            GeneratedFrameFiles = new List<string>();
            Summary = string.Empty;
            SceneFilePath = string.Empty;
            SourceVideoFilePath = string.Empty;
            OutputDirectoryPath = string.Empty;
            OutputDirectoryResPath = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public string SourceVideoFilePath { get; set; }
        public string OutputDirectoryPath { get; set; }
        public string OutputDirectoryResPath { get; set; }
        public bool ClearedAnimation { get; set; }
        public bool PatchedScene { get; set; }
        public float AppliedFps { get; set; }
        public float AppliedUpscale { get; set; }
        public int DeletedOldFrameCount { get; set; }
        public int GeneratedFrameCount { get; set; }
        public List<string> GeneratedFrameFiles { get; set; }
        public string Summary { get; set; }
    }
}
