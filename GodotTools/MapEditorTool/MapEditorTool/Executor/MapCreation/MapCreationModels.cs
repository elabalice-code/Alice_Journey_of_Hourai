using System.Collections.Generic;
using System.Text.Json;
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

    // Serialized with System.Text.Json + CamelCase (see CollisionLayoutJson.Options) so collision JSON
    // stays wire-compatible with files written by the legacy editor and with the game side.
    public sealed class CollisionLayoutData
    {
        public CollisionLayoutData()
        {
            Solid = new bool[0];
            Polygons = new List<List<GodotVector2Data>>();
        }

        public int RoomWidth { get; set; }

        public int RoomHeight { get; set; }

        public bool[] Solid { get; set; }

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

    public sealed class GodotVector2Data
    {
        public float X { get; set; }

        public float Y { get; set; }
    }

    // CamelCase options mirroring the legacy editor's JsonOptions.Default (System.Text.Json +
    // JsonNamingPolicy.CamelCase, indented). Keeps collision .json files readable by both the
    // old tool and the Godot side, and lets the new tool load legacy camelCase files.
    internal static class CollisionLayoutJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}
