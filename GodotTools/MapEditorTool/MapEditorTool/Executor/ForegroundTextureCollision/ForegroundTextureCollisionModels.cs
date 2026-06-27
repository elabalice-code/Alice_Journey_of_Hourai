using MapEditorTool.Executor.MapCreation;

namespace MapEditorTool.Executor.ForegroundTextureCollision
{
    public sealed class ForegroundTextureCollisionResult
    {
        public ForegroundTextureCollisionResult()
        {
            TextureFilePath = string.Empty;
            CollisionFilePath = string.Empty;
            CollisionResPath = string.Empty;
            Layout = CollisionLayoutData.Create(1, 1);
            Summary = string.Empty;
        }

        public string TextureFilePath { get; set; }
        public string CollisionFilePath { get; set; }
        public string CollisionResPath { get; set; }
        public CollisionLayoutData Layout { get; set; }
        public bool UsedTextureAlpha { get; set; }
        public bool UsedTileFallback { get; set; }
        public bool TextureHasAlphaChannel { get; set; }
        public bool WroteCollisionFile { get; set; }
        public int SolidTileCount { get; set; }
        public int PolygonCount { get; set; }
        public string Summary { get; set; }
    }
}
