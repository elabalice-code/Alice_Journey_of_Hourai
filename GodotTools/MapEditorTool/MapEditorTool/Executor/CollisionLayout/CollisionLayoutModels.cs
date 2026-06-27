using MapEditorTool.Executor.MapCreation;

namespace MapEditorTool.Executor.CollisionLayout
{
    public enum CollisionLayoutTarget
    {
        Tile = 0,
        ForegroundTexture = 1
    }

    public sealed class CollisionLayoutFileResult
    {
        public CollisionLayoutFileResult()
        {
            CollisionResPath = string.Empty;
            CollisionFilePath = string.Empty;
            Layout = CollisionLayoutData.Create(1, 1);
            Summary = string.Empty;
        }

        public string CollisionResPath { get; set; }
        public string CollisionFilePath { get; set; }
        public CollisionLayoutData Layout { get; set; }
        public bool FileExists { get; set; }
        public bool CreatedDefaultPath { get; set; }
        public bool ResizedLayout { get; set; }
        public bool FixedSolidBuffer { get; set; }
        public bool WroteFile { get; set; }
        public string Summary { get; set; }
    }
}
