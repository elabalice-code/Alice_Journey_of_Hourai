using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapImport
{
    public static class GodotMapImporter
    {
        private static readonly Regex Vector2Regex = new Regex("Vector2\\((?<x>-?\\d+(?:\\.\\d+)?),\\s*(?<y>-?\\d+(?:\\.\\d+)?)\\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExtResourceValueRegex = new Regex("ExtResource\\(\\\"(?<id>[^\\\"]+)\\\"\\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static MapProject ImportFromGodot(string godotRootDir)
        {
            var project = new MapProject();
            var mapRootDir = ResolveMapRootDirectory(godotRootDir);
            var scenes = EnumerateTscnFiles(mapRootDir)
                .Select(p => new SceneFile(p, GetRelativePath(godotRootDir, p).Replace('\\', '/')))
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var byScenePath = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
            var byUid = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var scene in scenes)
            {
                var scenePath = "res://" + scene.RelativePath;
                var tscn = TscnParser.ParseFile(scene.AbsolutePath);

                if (!LooksLikeMapScene(tscn))
                    continue;

                var definition = BuildMapDefinition(scenePath, tscn);
                project.Maps.Add(definition);
                byScenePath[scenePath] = definition;

                if (!string.IsNullOrWhiteSpace(tscn.SceneUid))
                    byUid[tscn.SceneUid] = scenePath;
            }

            BuildLinks(project, byScenePath, byUid);

            if (project.Maps.Count == 0)
                project.ResetToDefault();

            return project;
        }

        private static string ResolveMapRootDirectory(string godotRootDir)
        {
            // Default UX import must show game maps only. Do not scan BuildLogs or tool-generated verification scenes.
            var mapRootDir = Path.Combine(godotRootDir ?? string.Empty, "CoreEngine", "Maps");
            return Directory.Exists(mapRootDir) ? mapRootDir : godotRootDir;
        }

        private static MapDefinition BuildMapDefinition(string scenePath, TscnScene tscn)
        {
            var fileName = scenePath.Split('/').LastOrDefault() ?? scenePath;
            var baseName = fileName.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 5)
                : fileName;

            var map = new MapDefinition
            {
                Id = scenePath,
                ScenePath = scenePath,
                DisplayName = baseName
            };

            var nodePathByNode = new Dictionary<TscnNode, string>();
            foreach (var node in tscn.Nodes)
                nodePathByNode[node] = ComputeNodePath(node);

            foreach (var node in tscn.Nodes)
            {
                var nodePath = nodePathByNode[node];

                if (IsMapRootNode(node))
                    ReadMapRootProperties(map, node, tscn);

                if (string.Equals(node.Type, "TileMapLayer", StringComparison.Ordinal))
                    ReadTileLayer(map, node, nodePath, tscn);

                ReadTextureNodeProperties(map, node, nodePath, tscn);
                ReadPositionedNode(map, node, nodePath, tscn);
            }

            NormalizeMapSize(map);

            map.BackgroundTextureEnabled = map.BackgroundTexturePath.Length > 0;
            map.ForegroundTextureEnabled = map.ForegroundTexturePath.Length > 0;
            map.BackgroundTileLayerVisible = map.TileLayers.Any(l => IsBackgroundTileLayerName(l.Name) && l.Visible);

            return map;
        }

        private static bool IsMapRootNode(TscnNode node)
        {
            return string.Equals(node.Name, "Map", StringComparison.Ordinal)
                && (string.Equals(node.Type, "Node2D", StringComparison.Ordinal) || node.Type.Length == 0);
        }

        private static void ReadMapRootProperties(MapDefinition map, TscnNode node, TscnScene tscn)
        {
            ReadExtResourcePath(node, tscn, "template", path => map.TemplateTexturePath = path);
            ReadExtResourcePath(node, tscn, "foreground_texture", path => map.ForegroundTexturePath = path);
            ReadExtResourcePath(node, tscn, "background_texture", path => map.BackgroundTexturePath = path);

            string value;
            if (node.RawProps.TryGetValue("metadata/collision_mode", out value))
            {
                var mode = Unquote(value).Trim();
                if (StringEqualsAny(mode, "foreground_texture", "fgtex"))
                    map.CollisionUsed = CollisionMode.ForegroundTexture;
                else if (StringEqualsAny(mode, "tile", "tile_foreground"))
                    map.CollisionUsed = CollisionMode.TileForeground;
            }

            if (node.RawProps.TryGetValue("metadata/collision_tile_path", out value))
            {
                var path = Unquote(value).Trim();
                if (IsValidResPath(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    map.TileCollisionDataPath = path;
            }

            if (node.RawProps.TryGetValue("metadata/collision_fgtex_path", out value))
            {
                var path = Unquote(value).Trim();
                if (IsValidResPath(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    map.ForegroundTextureCollisionDataPath = path;
            }

            if (node.RawProps.TryGetValue("metadata/foreground_texture_anchor", out value))
            {
                TextureAnchor anchor;
                if (TryParseTextureAnchor(Unquote(value).Trim(), out anchor))
                    map.ForegroundTextureAnchor = anchor;
            }

            if (node.RawProps.TryGetValue("metadata/foreground_texture_upscale", out value))
            {
                float upscale;
                if (float.TryParse(Unquote(value).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out upscale) && upscale > 0)
                    map.ForegroundTextureUpscale = upscale;
            }

            if (node.RawProps.TryGetValue("metadata/background_texture_anchor", out value))
            {
                TextureAnchor anchor;
                if (TryParseTextureAnchor(Unquote(value).Trim(), out anchor))
                    map.BackgroundTextureAnchor = anchor;
            }

            if (node.RawProps.TryGetValue("metadata/background_texture_upscale", out value))
            {
                float upscale;
                if (float.TryParse(Unquote(value).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out upscale) && upscale > 0)
                    map.BackgroundTextureUpscale = upscale;
            }
        }

        private static void ReadTileLayer(MapDefinition map, TscnNode node, string nodePath, TscnScene tscn)
        {
            var layer = new TileLayer
            {
                Name = node.Name,
                NodePath = nodePath
            };

            ReadExtResourcePath(node, tscn, "tile_set", path => layer.TileSetPath = path);

            string value;
            if (node.RawProps.TryGetValue("visible", out value))
            {
                bool visible;
                if (bool.TryParse(value.Trim(), out visible))
                    layer.Visible = visible;
            }

            if (node.RawProps.TryGetValue("z_index", out value))
            {
                int z;
                if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out z))
                    layer.ZIndex = z;
            }

            if (node.RawProps.TryGetValue("tile_map_data", out value))
                layer.Cells = DecodeTileMapData(value);

            map.TileLayers.Add(layer);

            if (string.Equals(node.Name, "Foreground", StringComparison.OrdinalIgnoreCase))
                map.Skin.TileSetId = layer.TileSetPath.Length > 0 ? layer.TileSetPath : map.Skin.TileSetId;
        }

        private static void ReadTextureNodeProperties(MapDefinition map, TscnNode node, string nodePath, TscnScene tscn)
        {
            if (!string.Equals(node.Type, "TextureRect", StringComparison.Ordinal) && !string.Equals(node.Type, "Sprite2D", StringComparison.Ordinal))
                return;

            if (nodePath.EndsWith("BackgroundLayer/BackgroundTexture", StringComparison.Ordinal))
            {
                map.BackgroundNodePath = nodePath;
                ReadExtResourcePath(node, tscn, "texture", path => map.BackgroundTexturePath = path);
                return;
            }

            if (nodePath.EndsWith("ForegroundTextureLayer/ForegroundTexture", StringComparison.Ordinal))
            {
                map.ForegroundTextureNodePath = nodePath;
                ReadExtResourcePath(node, tscn, "texture", path => map.ForegroundTexturePath = path);
            }
        }

        private static void ReadPositionedNode(MapDefinition map, TscnNode node, string nodePath, TscnScene tscn)
        {
            string positionValue;
            if (!node.RawProps.TryGetValue("position", out positionValue))
                return;

            var position = ParseVector2(positionValue);

            string targetMapValue;
            if (node.RawProps.TryGetValue("target_map", out targetMapValue))
            {
                map.Portals.Add(CreatePortal(node, nodePath, position, Unquote(targetMapValue), ReadTargetPortal(node)));
                return;
            }

            if (string.IsNullOrWhiteSpace(node.InstanceExtResourceId))
                return;

            var prefab = tscn.FindExtResourcePathById(node.InstanceExtResourceId) ?? string.Empty;
            if (prefab.Length == 0)
                return;

            if (ContainsOrdinalIgnoreCase(prefab, "/Portal.tscn"))
            {
                var target = node.RawProps.TryGetValue("target_map", out targetMapValue) ? Unquote(targetMapValue) : string.Empty;
                map.Portals.Add(CreatePortal(node, nodePath, position, target, ReadTargetPortal(node)));
                return;
            }

            var isCollectible = ContainsOrdinalIgnoreCase(prefab, "/Collectible.tscn");
            var isElevator = ContainsOrdinalIgnoreCase(prefab, "/Elevator.tscn");

            map.Entities.Add(new PlacedEntity
            {
                Type = isCollectible ? "Collectible" : isElevator ? "Elevator" : "Entity",
                Prefab = prefab,
                NodePath = nodePath,
                X = position.X,
                Y = position.Y,
                Pushable = !isCollectible && !isElevator
            });
        }

        private static Portal CreatePortal(TscnNode node, string nodePath, GodotVector2 position, string targetMapId, string targetPortalId)
        {
            return new Portal
            {
                Id = nodePath,
                Name = node.Name,
                NodePath = nodePath,
                X = position.X,
                Y = position.Y,
                TargetMapId = targetMapId,
                TargetPortalId = targetPortalId
            };
        }

        private static string ReadTargetPortal(TscnNode node)
        {
            string value;
            if (node.RawProps.TryGetValue("target_area", out value))
            {
                var targetArea = UnquoteStringName(value);
                if (targetArea.Length > 0)
                    return targetArea;
            }

            return node.RawProps.TryGetValue("target_portal", out value) ? Unquote(value) : string.Empty;
        }

        private static void NormalizeMapSize(MapDefinition map)
        {
            var maxTileX = -1;
            var maxTileY = -1;

            foreach (var layer in map.TileLayers)
            {
                foreach (var cell in layer.Cells)
                {
                    maxTileX = Math.Max(maxTileX, cell.X);
                    maxTileY = Math.Max(maxTileY, cell.Y);
                }
            }

            if (maxTileX >= 0 || maxTileY >= 0)
            {
                if (maxTileX >= 0)
                    map.RoomWidth = Math.Max(map.RoomWidth, maxTileX + 1);
                if (maxTileY >= 0)
                    map.RoomHeight = Math.Max(map.RoomHeight, maxTileY + 1);
                return;
            }

            var maxPxX = 0f;
            var maxPxY = 0f;
            foreach (var portal in map.Portals)
            {
                maxPxX = Math.Max(maxPxX, portal.X);
                maxPxY = Math.Max(maxPxY, portal.Y);
            }

            foreach (var entity in map.Entities)
            {
                maxPxX = Math.Max(maxPxX, entity.X);
                maxPxY = Math.Max(maxPxY, entity.Y);
            }

            if (maxPxX > 0 || maxPxY > 0)
            {
                map.RoomWidth = Math.Max(map.RoomWidth, (int)Math.Ceiling(maxPxX / 32f) + 4);
                map.RoomHeight = Math.Max(map.RoomHeight, (int)Math.Ceiling(maxPxY / 32f) + 4);
            }
        }

        private static void BuildLinks(MapProject project, Dictionary<string, MapDefinition> byScenePath, Dictionary<string, string> byUid)
        {
            foreach (var map in project.Maps)
            {
                foreach (var portal in map.Portals)
                {
                    if (string.IsNullOrWhiteSpace(portal.TargetMapId))
                        continue;

                    var normalized = NormalizeScenePath(portal.TargetMapId, byUid);
                    if (normalized == null || !byScenePath.ContainsKey(normalized))
                        continue;

                    project.Links.Add(new MapLink
                    {
                        From = new LinkEndpoint { MapId = map.Id, PortalId = portal.Id },
                        To = new LinkEndpoint { MapId = normalized, PortalId = portal.TargetPortalId ?? string.Empty }
                    });
                }
            }
        }

        private static void ReadExtResourcePath(TscnNode node, TscnScene tscn, string key, Action<string> apply)
        {
            string raw;
            if (!node.RawProps.TryGetValue(key, out raw))
                return;

            var match = ExtResourceValueRegex.Match(raw);
            if (!match.Success)
                return;

            var path = tscn.FindExtResourcePathById(match.Groups["id"].Value);
            if (IsValidResPath(path))
                apply(path);
        }

        private static bool TryParseTextureAnchor(string raw, out TextureAnchor anchor)
        {
            var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (StringEqualsAny(value, "topleft", "top_left", "top-left", "lt"))
            {
                anchor = TextureAnchor.TopLeft;
                return true;
            }
            if (StringEqualsAny(value, "topright", "top_right", "top-right", "rt"))
            {
                anchor = TextureAnchor.TopRight;
                return true;
            }
            if (StringEqualsAny(value, "bottomleft", "bottom_left", "bottom-left", "lb"))
            {
                anchor = TextureAnchor.BottomLeft;
                return true;
            }
            if (StringEqualsAny(value, "bottomright", "bottom_right", "bottom-right", "rb"))
            {
                anchor = TextureAnchor.BottomRight;
                return true;
            }
            if (StringEqualsAny(value, "center", "centre", "c"))
            {
                anchor = TextureAnchor.Center;
                return true;
            }

            anchor = TextureAnchor.TopLeft;
            return false;
        }

        private static bool IsBackgroundTileLayerName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return false;
            if (string.Equals(name, "Foreground", StringComparison.OrdinalIgnoreCase))
                return false;
            return ContainsOrdinalIgnoreCase(name, "back");
        }

        private static bool IsValidResPath(string path)
        {
            path = (path ?? string.Empty).Trim();
            return path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("uid://", StringComparison.Ordinal);
        }

        private static List<TileCell> DecodeTileMapData(string raw)
        {
            raw = raw.Trim();
            if (!raw.StartsWith("PackedByteArray", StringComparison.Ordinal))
                return new List<TileCell>();

            byte[] bytes;
            var q1 = raw.IndexOf('"');
            if (q1 >= 0)
            {
                var q2 = raw.IndexOf('"', q1 + 1);
                if (q2 <= q1)
                    return new List<TileCell>();
                bytes = Convert.FromBase64String(raw.Substring(q1 + 1, q2 - q1 - 1));
            }
            else
            {
                var p1 = raw.IndexOf('(');
                var p2 = raw.LastIndexOf(')');
                if (p1 < 0 || p2 <= p1)
                    return new List<TileCell>();

                var content = raw.Substring(p1 + 1, p2 - p1 - 1);
                var parts = SplitCsv(content);
                bytes = new byte[parts.Count];
                for (var i = 0; i < parts.Count; i++)
                {
                    byte b;
                    if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out b))
                        return new List<TileCell>();
                    bytes[i] = b;
                }
            }

            if (bytes.Length < 2)
                return new List<TileCell>();

            var u16Count = bytes.Length / 2;
            var values = new ushort[u16Count];
            for (var i = 0; i < u16Count; i++)
                values[i] = BitConverter.ToUInt16(bytes, i * 2);

            if (values.Length < 7)
                return new List<TileCell>();

            var cells = new List<TileCell>();
            for (var i = 1; i + 5 < values.Length; i += 6)
            {
                cells.Add(new TileCell
                {
                    X = values[i],
                    Y = values[i + 1],
                    SourceId = values[i + 2],
                    AtlasX = values[i + 3],
                    AtlasY = values[i + 4],
                    Alternative = values[i + 5]
                });
            }

            return cells;
        }

        private static string ComputeNodePath(TscnNode node)
        {
            if (string.IsNullOrWhiteSpace(node.Parent) || node.Parent == ".")
                return node.Name;

            return node.Parent.Trim('/') + "/" + node.Name;
        }

        private static GodotVector2 ParseVector2(string raw)
        {
            var match = Vector2Regex.Match(raw);
            if (!match.Success)
                return new GodotVector2(0, 0);

            return new GodotVector2(
                float.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture));
        }

        private static string Unquote(string raw)
        {
            raw = raw.Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static string UnquoteStringName(string raw)
        {
            raw = raw.Trim();
            if (raw.StartsWith("&\"", StringComparison.Ordinal) && raw.EndsWith("\"", StringComparison.Ordinal))
                return raw.Substring(2, raw.Length - 3);
            return Unquote(raw);
        }

        private static string NormalizeScenePath(string targetMapValue, Dictionary<string, string> byUid)
        {
            var target = targetMapValue.Trim();
            if (target.StartsWith("ExtResource(", StringComparison.Ordinal))
                return null;

            if (target.StartsWith("uid://", StringComparison.Ordinal))
            {
                string resolved;
                return byUid.TryGetValue(target, out resolved) ? resolved : null;
            }

            return target.StartsWith("res://", StringComparison.Ordinal) ? target : null;
        }

        private static IEnumerable<string> EnumerateTscnFiles(string rootDir)
        {
            var stack = new Stack<string>();
            stack.Push(rootDir);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*.tscn", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                    yield return file;

                IEnumerable<string> subDirs;
                try
                {
                    subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    var name = Path.GetFileName(subDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (!IsIgnoredDirName(name))
                        stack.Push(subDir);
                }
            }
        }

        private static bool IsIgnoredDirName(string name)
        {
            return string.Equals(name, ".godot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ".vs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "GodotTools", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeMapScene(TscnScene tscn)
        {
            return tscn.Nodes.Any(node => string.Equals(node.Type, "TileMapLayer", StringComparison.Ordinal));
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string fragment)
        {
            return value != null && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool StringEqualsAny(string value, params string[] options)
        {
            foreach (var option in options)
            {
                if (string.Equals(value, option, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> SplitCsv(string content)
        {
            return content
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }

        private static string GetRelativePath(string rootDir, string path)
        {
            var rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(rootDir)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private sealed class SceneFile
        {
            public SceneFile(string absolutePath, string relativePath)
            {
                AbsolutePath = absolutePath;
                RelativePath = relativePath;
            }

            public string AbsolutePath { get; private set; }
            public string RelativePath { get; private set; }
        }

        private struct GodotVector2
        {
            public GodotVector2(float x, float y)
            {
                X = x;
                Y = y;
            }

            public float X { get; private set; }
            public float Y { get; private set; }
        }
    }
}
