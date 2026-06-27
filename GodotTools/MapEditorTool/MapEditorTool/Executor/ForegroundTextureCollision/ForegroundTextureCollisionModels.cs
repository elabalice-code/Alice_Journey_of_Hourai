using System.Collections.Generic;
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

    public sealed class ForegroundTextureAlphaTraceReport
    {
        public ForegroundTextureAlphaTraceReport()
        {
            ImageFilePath = string.Empty;
            Polygons = new List<List<GodotVector2Data>>();
            SamplePoints = new List<GodotVector2Data>();
            Summary = string.Empty;
        }

        public string ImageFilePath { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int WorldWidth { get; set; }
        public int WorldHeight { get; set; }
        public int AlphaThreshold { get; set; }
        public bool HasAlphaChannel { get; set; }
        public int PolygonCount { get; set; }
        public int FirstPolygonPointCount { get; set; }
        public List<List<GodotVector2Data>> Polygons { get; set; }
        public List<GodotVector2Data> SamplePoints { get; set; }
        public string Summary { get; set; }
    }
}
