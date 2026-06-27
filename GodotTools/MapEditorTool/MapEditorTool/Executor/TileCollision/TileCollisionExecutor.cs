using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.TileCollision
{
    public sealed class TileCollisionExecutor
    {
        public TileCollisionPatchResult ApplyTileCollisionEdits(string godotRoot, MapDefinition map, IList<TileCollisionCommit> edits)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (edits == null || edits.Count == 0)
                return CreateEmptyResult(map);

            var sceneAbsPath = ResolveScenePath(godotRoot, map);
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var byPath = BuildNodePathIndex(scene);
            var tileSetCache = new Dictionary<string, GodotTileSet>(StringComparer.OrdinalIgnoreCase);
            var tileSetPatchCount = 0;
            var layerPatchCount = 0;
            var newAlternativeCount = 0;

            foreach (var edit in edits)
            {
                var tilesetAbsPath = ToAbsoluteGodotPath(godotRoot, edit.TileSetResPath);
                GodotTileSet tileSet;
                if (!tileSetCache.TryGetValue(tilesetAbsPath, out tileSet))
                {
                    tileSet = GodotTileSetLoader.Load(tilesetAbsPath);
                    tileSetCache[tilesetAbsPath] = tileSet;
                }

                GodotTileAtlasSource source;
                if (!tileSet.Sources.TryGetValue(edit.SourceId, out source))
                    throw new InvalidOperationException("TileSet source_id not found: " + edit.SourceId.ToString(CultureInfo.InvariantCulture));

                var newAlternative = GodotTileSetLoader.CreateAtlasPhysicsPolygonAlternative(
                    tilesetAbsPath,
                    source.SubResourceId,
                    edit.AtlasX,
                    edit.AtlasY,
                    edit.OneWay,
                    edit.ToPoints);

                tileSetPatchCount++;
                newAlternativeCount++;
                PatchLayerAlternative(map, byPath, edit.LayerNodePath, edit.CellX, edit.CellY, newAlternative);
                layerPatchCount++;
            }

            var scenePatched = TscnWriter.PatchFile(sceneAbsPath, scene, new[] { "tile_map_data" });
            return new TileCollisionPatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneAbsPath),
                Patched = scenePatched || tileSetPatchCount > 0,
                TileSetPatchCount = tileSetPatchCount,
                SceneLayerPatchCount = layerPatchCount,
                NewAlternativeCount = newAlternativeCount,
                Summary = "tileSetPatches=" + tileSetPatchCount +
                    "; sceneLayerPatches=" + layerPatchCount +
                    "; newAlternatives=" + newAlternativeCount
            };
        }

        public TileCollisionPatchResult ApplyTileCollisionAlternativeEdits(string godotRoot, MapDefinition map, IList<TileCollisionAlternativeCommit> edits)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (edits == null || edits.Count == 0)
                return CreateEmptyResult(map);

            var sceneAbsPath = ResolveScenePath(godotRoot, map);
            var scene = TscnParser.ParseFile(sceneAbsPath);
            var byPath = BuildNodePathIndex(scene);
            var layerPatchCount = 0;

            foreach (var edit in edits)
            {
                PatchLayerAlternative(map, byPath, edit.LayerNodePath, edit.CellX, edit.CellY, edit.ToAlternative);
                layerPatchCount++;
            }

            var scenePatched = TscnWriter.PatchFile(sceneAbsPath, scene, new[] { "tile_map_data" });
            return new TileCollisionPatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneAbsPath),
                Patched = scenePatched,
                TileSetPatchCount = 0,
                SceneLayerPatchCount = layerPatchCount,
                NewAlternativeCount = 0,
                Summary = "tileSetPatches=0; sceneLayerPatches=" + layerPatchCount + "; newAlternatives=0"
            };
        }

        public static string PatchTileMapDataAlternative(string raw, int cellX, int cellY, int newAlternative)
        {
            raw = (raw ?? string.Empty).Trim();
            if (!raw.StartsWith("PackedByteArray", StringComparison.Ordinal))
                throw new InvalidOperationException("tile_map_data is not a PackedByteArray.");

            var bytes = DecodePackedByteArray(raw);
            if (bytes.Length < 2)
                throw new InvalidOperationException("Invalid tile_map_data buffer.");

            var values = new ushort[bytes.Length / 2];
            for (var i = 0; i < values.Length; i++)
                values[i] = BitConverter.ToUInt16(bytes, i * 2);

            var found = false;
            for (var i = 1; i + 5 < values.Length; i += 6)
            {
                if ((int)values[i] == cellX && (int)values[i + 1] == cellY)
                {
                    values[i + 5] = (ushort)newAlternative;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new InvalidOperationException("Cell not found in tile_map_data: (" + cellX.ToString(CultureInfo.InvariantCulture) + ", " + cellY.ToString(CultureInfo.InvariantCulture) + ")");

            var outBytes = new byte[values.Length * 2];
            for (var i = 0; i < values.Length; i++)
            {
                var valueBytes = BitConverter.GetBytes(values[i]);
                outBytes[i * 2] = valueBytes[0];
                outBytes[i * 2 + 1] = valueBytes[1];
            }

            return "PackedByteArray(\"" + Convert.ToBase64String(outBytes) + "\")";
        }

        private static void PatchLayerAlternative(MapDefinition map, Dictionary<string, TscnNode> byPath, string layerNodePath, int cellX, int cellY, int newAlternative)
        {
            TscnNode layerNode;
            if (!byPath.TryGetValue(layerNodePath, out layerNode))
                throw new InvalidOperationException("TileMapLayer node not found: " + layerNodePath);

            string tileMapData;
            if (!layerNode.RawProps.TryGetValue("tile_map_data", out tileMapData))
                throw new InvalidOperationException("tile_map_data not found on node: " + layerNodePath);

            layerNode.RawProps["tile_map_data"] = PatchTileMapDataAlternative(tileMapData, cellX, cellY, newAlternative);

            var layerModel = (map.TileLayers ?? new List<TileLayer>())
                .FirstOrDefault(x => string.Equals(x.NodePath, layerNodePath, StringComparison.Ordinal));
            if (layerModel == null)
                return;

            var cellModel = (layerModel.Cells ?? new List<TileCell>())
                .FirstOrDefault(x => x.X == cellX && x.Y == cellY);
            if (cellModel != null)
                cellModel.Alternative = newAlternative;
        }

        private static byte[] DecodePackedByteArray(string raw)
        {
            var firstQuote = raw.IndexOf('"');
            if (firstQuote >= 0)
            {
                var secondQuote = raw.IndexOf('"', firstQuote + 1);
                if (secondQuote <= firstQuote)
                    throw new InvalidOperationException("Invalid PackedByteArray base64 string.");

                return Convert.FromBase64String(raw.Substring(firstQuote + 1, secondQuote - firstQuote - 1));
            }

            var open = raw.IndexOf('(');
            var close = raw.LastIndexOf(')');
            if (open < 0 || close <= open)
                throw new InvalidOperationException("Invalid PackedByteArray.");

            var parts = raw.Substring(open + 1, close - open - 1)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            var bytes = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                byte value;
                if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    throw new InvalidOperationException("Invalid PackedByteArray.");
                bytes[i] = value;
            }

            return bytes;
        }

        private static Dictionary<string, TscnNode> BuildNodePathIndex(TscnScene scene)
        {
            var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
            foreach (var node in scene.Nodes)
            {
                var path = string.IsNullOrWhiteSpace(node.Parent) || node.Parent == "."
                    ? node.Name
                    : node.Parent.Trim('/') + "/" + node.Name;
                byPath[path] = node;
            }

            return byPath;
        }

        private static string ResolveScenePath(string godotRoot, MapDefinition map)
        {
            if (string.IsNullOrWhiteSpace(map.ScenePath))
                throw new FileNotFoundException("Map scene path is empty.");

            return ToAbsoluteGodotPath(godotRoot, map.ScenePath);
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");
            if (string.IsNullOrWhiteSpace(resPath))
                throw new FileNotFoundException("Godot resource path is empty.");

            var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath.Substring("res://".Length) : resPath;
            rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, rel);
        }

        private static TileCollisionPatchResult CreateEmptyResult(MapDefinition map)
        {
            return new TileCollisionPatchResult
            {
                SceneFilePath = map == null ? string.Empty : map.ScenePath,
                Patched = false,
                TileSetPatchCount = 0,
                SceneLayerPatchCount = 0,
                NewAlternativeCount = 0,
                Summary = "No tile collision edits were supplied."
            };
        }
    }
}
