using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapCreation
{
    public sealed class MapCreationExecutor
    {
        private const string DefaultTileSetPath = "res://CoreEngine/Resources/Tileset.tres";
        private const string DefaultRoomInstancePath = "res://addons/MetroidvaniaSystem/Nodes/RoomInstance.tscn";

        public MapCreationResult CreateMap(string godotRoot, string displayName, IEnumerable<MapDefinition> existingMaps)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            var mapsAbsDir = Path.Combine(godotRoot, "CoreEngine", "Maps");
            if (!Directory.Exists(mapsAbsDir))
                Directory.CreateDirectory(mapsAbsDir);

            displayName = NormalizeDisplayName(displayName, "NewMap");
            var safeBase = SanitizeFolderName(displayName);
            if (safeBase.Length == 0)
                safeBase = "NewMap";

            var fileBase = MakeUniqueMapFileBase(mapsAbsDir, safeBase, existingMaps);
            var sceneResPath = "res://CoreEngine/Maps/" + fileBase + ".tscn";
            var tileCollisionResPath = "res://CoreEngine/Maps/Resources/" + fileBase + "/collision_tile.json";
            var foregroundCollisionResPath = "res://CoreEngine/Maps/Resources/" + fileBase + "/collision_fgtex.json";

            var sceneAbsPath = ToAbsoluteGodotPath(godotRoot, sceneResPath);
            var tileCollisionAbsPath = ToAbsoluteGodotPath(godotRoot, tileCollisionResPath);
            var foregroundCollisionAbsPath = ToAbsoluteGodotPath(godotRoot, foregroundCollisionResPath);

            Directory.CreateDirectory(Path.GetDirectoryName(sceneAbsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(tileCollisionAbsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(foregroundCollisionAbsPath));

            var layout = CollisionLayoutData.Create(27, 15);
            WriteCollisionLayout(tileCollisionAbsPath, layout, overwrite: true);
            WriteCollisionLayout(foregroundCollisionAbsPath, layout, overwrite: true);
            File.WriteAllText(sceneAbsPath, BuildDefaultSceneText(tileCollisionResPath, foregroundCollisionResPath), Encoding.UTF8);

            return new MapCreationResult
            {
                CreatedScene = true,
                CreatedTileCollisionFile = true,
                CreatedForegroundTextureCollisionFile = true,
                SceneFilePath = Path.GetFullPath(sceneAbsPath),
                SceneResPath = sceneResPath,
                TileCollisionFilePath = Path.GetFullPath(tileCollisionAbsPath),
                ForegroundTextureCollisionFilePath = Path.GetFullPath(foregroundCollisionAbsPath),
                CreatedMap = CreateMapDefinition(displayName, sceneResPath, tileCollisionResPath, foregroundCollisionResPath),
                Summary = "createdMap=" + sceneResPath + "; collisionFiles=2"
            };
        }

        public MapCreationResult EnsureMapSceneExists(string godotRoot, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            var sceneResPath = (map.ScenePath ?? string.Empty).Trim();
            var mapId = (map.Id ?? string.Empty).Trim();
            if (sceneResPath.Length == 0 && mapId.StartsWith("res://", StringComparison.Ordinal))
            {
                sceneResPath = mapId;
                map.ScenePath = sceneResPath;
            }

            if (sceneResPath.Length == 0 || !sceneResPath.StartsWith("res://", StringComparison.Ordinal))
                throw new FileNotFoundException("Map scene path must be a res:// path.");

            var sceneAbsPath = ToAbsoluteGodotPath(godotRoot, sceneResPath);
            var baseName = Path.GetFileNameWithoutExtension(sceneResPath);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "NewMap";

            if (string.IsNullOrWhiteSpace(map.TileCollisionDataPath))
                map.TileCollisionDataPath = "res://CoreEngine/Maps/Resources/" + baseName + "/collision_tile.json";
            if (string.IsNullOrWhiteSpace(map.ForegroundTextureCollisionDataPath))
                map.ForegroundTextureCollisionDataPath = "res://CoreEngine/Maps/Resources/" + baseName + "/collision_fgtex.json";

            var tileCollisionAbsPath = ToAbsoluteGodotPath(godotRoot, map.TileCollisionDataPath);
            var foregroundCollisionAbsPath = ToAbsoluteGodotPath(godotRoot, map.ForegroundTextureCollisionDataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sceneAbsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(tileCollisionAbsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(foregroundCollisionAbsPath));

            var layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);
            var createdTileCollision = WriteCollisionLayout(tileCollisionAbsPath, layout, overwrite: false);
            var createdForegroundCollision = WriteCollisionLayout(foregroundCollisionAbsPath, layout, overwrite: false);
            var createdScene = false;
            if (!File.Exists(sceneAbsPath))
            {
                File.WriteAllText(sceneAbsPath, BuildDefaultSceneText(map.TileCollisionDataPath, map.ForegroundTextureCollisionDataPath), Encoding.UTF8);
                createdScene = true;
            }

            EnsureDefaultTileLayers(map);

            return new MapCreationResult
            {
                CreatedScene = createdScene,
                CreatedTileCollisionFile = createdTileCollision,
                CreatedForegroundTextureCollisionFile = createdForegroundCollision,
                SceneFilePath = Path.GetFullPath(sceneAbsPath),
                SceneResPath = sceneResPath,
                TileCollisionFilePath = Path.GetFullPath(tileCollisionAbsPath),
                ForegroundTextureCollisionFilePath = Path.GetFullPath(foregroundCollisionAbsPath),
                CreatedMap = map,
                Summary = "ensuredMap=" + sceneResPath +
                    "; createdScene=" + createdScene +
                    "; createdCollisionFiles=" + CountTrue(createdTileCollision, createdForegroundCollision)
            };
        }

        private static MapDefinition CreateMapDefinition(string displayName, string sceneResPath, string tileCollisionResPath, string foregroundCollisionResPath)
        {
            var map = new MapDefinition
            {
                Id = sceneResPath,
                DisplayName = displayName,
                ScenePath = sceneResPath,
                Kind = MapKind.Vertical,
                RoomWidth = 27,
                RoomHeight = 15,
                CollisionUsed = CollisionMode.TileForeground,
                TileCollisionDataPath = tileCollisionResPath,
                ForegroundTextureCollisionDataPath = foregroundCollisionResPath
            };
            EnsureDefaultTileLayers(map);
            return map;
        }

        private static void EnsureDefaultTileLayers(MapDefinition map)
        {
            if (map.TileLayers == null)
                map.TileLayers = new List<TileLayer>();
            if (map.TileLayers.Count > 0)
                return;

            map.TileLayers.Add(new TileLayer
            {
                Name = "Foreground",
                NodePath = "TileMap/Foreground",
                TileSetPath = DefaultTileSetPath,
                Visible = true,
                ZIndex = 0
            });
            map.TileLayers.Add(new TileLayer
            {
                Name = "Background",
                NodePath = "TileMap/Background",
                TileSetPath = DefaultTileSetPath,
                Visible = false,
                ZIndex = -1
            });
        }

        private static string MakeUniqueMapFileBase(string mapsAbsDir, string safeBase, IEnumerable<MapDefinition> existingMaps)
        {
            var existing = existingMaps ?? Enumerable.Empty<MapDefinition>();
            var fileBase = safeBase;
            var index = 1;
            while (File.Exists(Path.Combine(mapsAbsDir, fileBase + ".tscn")) ||
                existing.Any(map => string.Equals(Path.GetFileNameWithoutExtension(map.ScenePath ?? string.Empty), fileBase, StringComparison.OrdinalIgnoreCase)))
            {
                index++;
                fileBase = safeBase + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return fileBase;
        }

        private static bool WriteCollisionLayout(string filePath, CollisionLayoutData layout, bool overwrite)
        {
            if (!overwrite && File.Exists(filePath))
                return false;

            var json = JsonSerializer.Serialize(layout, CollisionLayoutJson.Options);
            File.WriteAllText(filePath, json);

            return true;
        }

        private static string BuildDefaultSceneText(string tileCollisionResPath, string foregroundCollisionResPath)
        {
            return string.Join("\n", new[]
            {
                "[gd_scene load_steps=3 format=4]",
                string.Empty,
                "[ext_resource type=\"TileSet\" path=\"" + DefaultTileSetPath + "\" id=\"1_tileset\"]",
                "[ext_resource type=\"PackedScene\" path=\"" + DefaultRoomInstancePath + "\" id=\"2_room\"]",
                string.Empty,
                "[node name=\"Map\" type=\"Node2D\"]",
                "metadata/collision_mode = \"tile_foreground\"",
                "metadata/collision_tile_path = \"" + tileCollisionResPath + "\"",
                "metadata/collision_fgtex_path = \"" + foregroundCollisionResPath + "\"",
                string.Empty,
                "[node name=\"TileMap\" type=\"Node2D\" parent=\".\"]",
                string.Empty,
                "[node name=\"Foreground\" type=\"TileMapLayer\" parent=\"TileMap\"]",
                "use_parent_material = true",
                "tile_set = ExtResource(\"1_tileset\")",
                "tile_map_data = PackedByteArray()",
                string.Empty,
                "[node name=\"Background\" type=\"TileMapLayer\" parent=\"TileMap\"]",
                "visible = false",
                "z_index = -1",
                "use_parent_material = true",
                "tile_set = ExtResource(\"1_tileset\")",
                "tile_map_data = PackedByteArray()",
                string.Empty,
                "[node name=\"RoomInstance\" parent=\".\" instance=ExtResource(\"2_room\")]",
                string.Empty
            });
        }

        private static string SanitizeFolderName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (invalid.Contains(ch) || char.IsControl(ch))
                    builder.Append('_');
                else
                    builder.Append(ch);
            }

            return builder.ToString().Trim(' ', '.');
        }

        private static string NormalizeDisplayName(string value, string fallback)
        {
            value = (value ?? string.Empty).Trim();
            return value.Length == 0 ? fallback : value;
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            if (string.IsNullOrWhiteSpace(resPath))
                throw new FileNotFoundException("Godot resource path is empty.");

            var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath.Substring("res://".Length) : resPath;
            rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, rel);
        }

        private static int CountTrue(params bool[] values)
        {
            return values.Count(value => value);
        }
    }
}
