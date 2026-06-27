using System.Collections.Generic;

namespace MapEditorTool.Executor.MapDeletion
{
    public sealed class MapDeletionResult
    {
        public MapDeletionResult()
        {
            DeletedFiles = new List<string>();
            DeletedDirectories = new List<string>();
            SkippedPaths = new List<string>();
            Summary = string.Empty;
        }

        public bool DeletedSceneFile { get; set; }
        public int DeletedFileCount { get; set; }
        public int DeletedDirectoryCount { get; set; }
        public List<string> DeletedFiles { get; set; }
        public List<string> DeletedDirectories { get; set; }
        public List<string> SkippedPaths { get; set; }
        public string Summary { get; set; }
    }
}
