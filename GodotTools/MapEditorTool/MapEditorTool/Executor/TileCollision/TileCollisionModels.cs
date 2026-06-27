using System.Collections.Generic;

namespace MapEditorTool.Executor.TileCollision
{
    public struct GodotVector2
    {
        public GodotVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
    }

    public sealed class TileCollisionCommit
    {
        public TileCollisionCommit()
        {
            TileSetResPath = string.Empty;
            LayerNodePath = string.Empty;
            FromPoints = new List<GodotVector2>();
            ToPoints = new List<GodotVector2>();
        }

        public string TileSetResPath { get; set; }
        public string LayerNodePath { get; set; }
        public int SourceId { get; set; }
        public int AtlasX { get; set; }
        public int AtlasY { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public bool OneWay { get; set; }
        public List<GodotVector2> FromPoints { get; set; }
        public List<GodotVector2> ToPoints { get; set; }
    }

    public sealed class TileCollisionAlternativeCommit
    {
        public TileCollisionAlternativeCommit()
        {
            LayerNodePath = string.Empty;
        }

        public string LayerNodePath { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public int FromAlternative { get; set; }
        public int ToAlternative { get; set; }
    }

    public sealed class TileCollisionPatchResult
    {
        public TileCollisionPatchResult()
        {
            SceneFilePath = string.Empty;
            Summary = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public bool Patched { get; set; }
        public int TileSetPatchCount { get; set; }
        public int SceneLayerPatchCount { get; set; }
        public int NewAlternativeCount { get; set; }
        public string Summary { get; set; }
    }
}
