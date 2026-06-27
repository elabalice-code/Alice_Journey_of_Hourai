using System.Collections.Generic;
using System.Runtime.Serialization;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapCreation
{
    public sealed class MapCreationResult
    {
        public MapCreationResult()
        {
            SceneFilePath = string.Empty;
            SceneResPath = string.Empty;
            TileCollisionFilePath = string.Empty;
            ForegroundTextureCollisionFilePath = string.Empty;
            CreatedMap = new MapDefinition();
            Summary = string.Empty;
        }

        public bool CreatedScene { get; set; }
        public bool CreatedTileCollisionFile { get; set; }
        public bool CreatedForegroundTextureCollisionFile { get; set; }
        public string SceneFilePath { get; set; }
        public string SceneResPath { get; set; }
        public string TileCollisionFilePath { get; set; }
        public string ForegroundTextureCollisionFilePath { get; set; }
        public MapDefinition CreatedMap { get; set; }
        public string Summary { get; set; }
    }

    [DataContract]
    public sealed class CollisionLayoutData
    {
        public CollisionLayoutData()
        {
            Solid = new bool[0];
            Polygons = new List<List<GodotVector2Data>>();
        }

        [DataMember(Order = 0)]
        public int RoomWidth { get; set; }

        [DataMember(Order = 1)]
        public int RoomHeight { get; set; }

        [DataMember(Order = 2)]
        public bool[] Solid { get; set; }

        [DataMember(Order = 3)]
        public List<List<GodotVector2Data>> Polygons { get; set; }

        public static CollisionLayoutData Create(int roomWidth, int roomHeight)
        {
            roomWidth = roomWidth < 1 ? 1 : roomWidth;
            roomHeight = roomHeight < 1 ? 1 : roomHeight;
            return new CollisionLayoutData
            {
                RoomWidth = roomWidth,
                RoomHeight = roomHeight,
                Solid = new bool[roomWidth * roomHeight],
                Polygons = new List<List<GodotVector2Data>>()
            };
        }
    }

    [DataContract]
    public sealed class GodotVector2Data
    {
        [DataMember(Order = 0)]
        public float X { get; set; }

        [DataMember(Order = 1)]
        public float Y { get; set; }
    }
}
