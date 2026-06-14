using System.Text.RegularExpressions;
using MapEditor.Godot.Tscn;
using MapEditor.Models;

namespace MapEditor.Godot;

public static partial class GodotMapImporter
{
    public static MapProject ImportFromGodot(string godotRootDir)
    {
        var project = new MapProject();
        project.Maps.Clear();
        project.Links.Clear();

        var scenes = EnumerateTscnFiles(godotRootDir)
            .Select(p => new { Abs = p, Rel = Path.GetRelativePath(godotRootDir, p).Replace('\\', '/') })
            .OrderBy(x => x.Rel, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var byScenePath = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
        var byUid = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var scene in scenes)
        {
            var scenePath = "res://" + scene.Rel;

            var tscn = TscnParser.ParseFile(scene.Abs);
            if (!LooksLikeMapScene(tscn))
                continue;
            var def = BuildMapDefinition(scenePath, tscn);
            project.Maps.Add(def);
            byScenePath[scenePath] = def;
            if (!string.IsNullOrWhiteSpace(tscn.SceneUid))
                byUid[tscn.SceneUid] = scenePath;
        }

        BuildLinks(project, byScenePath, byUid);
        if (project.Maps.Count == 0)
            project.ResetToDefault();
        return project;
    }

    private static MapDefinition BuildMapDefinition(string scenePath, TscnScene tscn)
    {
        var fileName = scenePath.Split('/').LastOrDefault() ?? scenePath;
        var baseName = fileName.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^5]
            : fileName;

        var map = new MapDefinition
        {
            Id = scenePath,
            ScenePath = scenePath,
            DisplayName = baseName
        };

        var nodePathByNode = new Dictionary<TscnNode, string>();
        foreach (var node in tscn.Nodes)
        {
            nodePathByNode[node] = ComputeNodePath(node);
        }

        foreach (var node in tscn.Nodes)
        {
            var nodePath = nodePathByNode[node];

            if (string.Equals(node.Name, "Map", StringComparison.Ordinal) && (string.Equals(node.Type, "Node2D", StringComparison.Ordinal) || node.Type.Length == 0))
            {
                if (node.RawProps.TryGetValue("template", out var templateValue))
                {
                    var m = ExtResourceValueRegex().Match(templateValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                            map.TemplateTexturePath = extPath ?? "";
                    }
                }

                if (node.RawProps.TryGetValue("foreground_texture", out var fgValue))
                {
                    var m = ExtResourceValueRegex().Match(fgValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                            map.ForegroundTexturePath = extPath ?? "";
                    }
                }

                if (node.RawProps.TryGetValue("background_texture", out var bgValue))
                {
                    var m = ExtResourceValueRegex().Match(bgValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                            map.BackgroundTexturePath = extPath ?? "";
                    }
                }

                if (node.RawProps.TryGetValue("metadata/collision_mode", out var modeValue))
                {
                    var m = Unquote(modeValue).Trim();
                    if (string.Equals(m, "foreground_texture", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "fgtex", StringComparison.OrdinalIgnoreCase))
                        map.CollisionUsed = CollisionMode.ForegroundTexture;
                    else if (string.Equals(m, "tile", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "tile_foreground", StringComparison.OrdinalIgnoreCase))
                        map.CollisionUsed = CollisionMode.TileForeground;
                }
                if (node.RawProps.TryGetValue("metadata/collision_tile_path", out var tileColPath))
                {
                    var v = Unquote(tileColPath).Trim();
                    if (IsValidResPath(v) && v.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        map.TileCollisionDataPath = v;
                }
                if (node.RawProps.TryGetValue("metadata/collision_fgtex_path", out var fgColPath))
                {
                    var v = Unquote(fgColPath).Trim();
                    if (IsValidResPath(v) && v.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        map.ForegroundTextureCollisionDataPath = v;
                }

                if (node.RawProps.TryGetValue("metadata/foreground_texture_anchor", out var fgAnchorValue))
                {
                    var a = Unquote(fgAnchorValue).Trim();
                    if (TryParseTextureAnchor(a, out var anchor))
                        map.ForegroundTextureAnchor = anchor;
                }
                if (node.RawProps.TryGetValue("metadata/foreground_texture_upscale", out var fgUpscaleValue))
                {
                    var u = Unquote(fgUpscaleValue).Trim();
                    if (float.TryParse(u, out var f) && f > 0)
                        map.ForegroundTextureUpscale = f;
                }
                if (node.RawProps.TryGetValue("metadata/background_texture_anchor", out var bgAnchorValue))
                {
                    var a = Unquote(bgAnchorValue).Trim();
                    if (TryParseTextureAnchor(a, out var anchor))
                        map.BackgroundTextureAnchor = anchor;
                }
                if (node.RawProps.TryGetValue("metadata/background_texture_upscale", out var bgUpscaleValue))
                {
                    var u = Unquote(bgUpscaleValue).Trim();
                    if (float.TryParse(u, out var f) && f > 0)
                        map.BackgroundTextureUpscale = f;
                }
            }

            if (string.Equals(node.Type, "TileMapLayer", StringComparison.Ordinal))
            {
                var layer = new TileLayer
                {
                    Name = node.Name,
                    NodePath = nodePath
                };

                if (node.RawProps.TryGetValue("tile_set", out var tileSetValue))
                {
                    var m = ExtResourceValueRegex().Match(tileSetValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                            layer.TileSetPath = extPath ?? "";
                    }
                }

                if (node.RawProps.TryGetValue("visible", out var visibleValue))
                {
                    if (bool.TryParse(visibleValue.Trim(), out var v))
                        layer.Visible = v;
                }
                if (node.RawProps.TryGetValue("z_index", out var zIndexValue))
                {
                    if (int.TryParse(zIndexValue.Trim(), out var z))
                        layer.ZIndex = z;
                }
                if (node.RawProps.TryGetValue("tile_map_data", out var tmdValue))
                {
                    layer.Cells = DecodeTileMapData(tmdValue);
                }

                map.TileLayers.Add(layer);
                if (string.Equals(node.Name, "Foreground", StringComparison.OrdinalIgnoreCase))
                {
                    map.Skin.TileSetId = layer.TileSetPath.Length > 0 ? layer.TileSetPath : map.Skin.TileSetId;
                }
            }

            if ((string.Equals(node.Type, "TextureRect", StringComparison.Ordinal) || string.Equals(node.Type, "Sprite2D", StringComparison.Ordinal))
                && nodePath.EndsWith("BackgroundLayer/BackgroundTexture", StringComparison.Ordinal))
            {
                if (node.RawProps.TryGetValue("texture", out var texValue))
                {
                    var m = ExtResourceValueRegex().Match(texValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                        {
                            map.BackgroundTexturePath = extPath ?? "";
                            map.BackgroundNodePath = nodePath;
                        }
                    }
                }
                else
                {
                    map.BackgroundNodePath = nodePath;
                }
            }

            if ((string.Equals(node.Type, "TextureRect", StringComparison.Ordinal) || string.Equals(node.Type, "Sprite2D", StringComparison.Ordinal))
                && nodePath.EndsWith("ForegroundTextureLayer/ForegroundTexture", StringComparison.Ordinal))
            {
                if (node.RawProps.TryGetValue("texture", out var texValue))
                {
                    var m = ExtResourceValueRegex().Match(texValue);
                    if (m.Success)
                    {
                        var extId = m.Groups["id"].Value;
                        var extPath = tscn.FindExtResourcePathById(extId);
                        if (IsValidResPath(extPath))
                        {
                            map.ForegroundTexturePath = extPath ?? "";
                            map.ForegroundTextureNodePath = nodePath;
                        }
                    }
                }
                else
                {
                    map.ForegroundTextureNodePath = nodePath;
                }
            }

            if (node.RawProps.TryGetValue("position", out var posValue))
            {
                var pos = ParseVector2(posValue);

                if (node.RawProps.TryGetValue("target_map", out var targetMapValue))
                {
                    var targetArea = node.RawProps.TryGetValue("target_area", out var targetAreaValue) ? UnquoteStringName(targetAreaValue) : "";
                    if (targetArea.Length == 0)
                        targetArea = node.RawProps.TryGetValue("target_portal", out var targetPortalValue) ? Unquote(targetPortalValue) : "";
                    map.Portals.Add(new Portal
                    {
                        Id = nodePath,
                        Name = node.Name,
                        NodePath = nodePath,
                        X = pos.x,
                        Y = pos.y,
                        TargetMapId = Unquote(targetMapValue),
                        TargetPortalId = targetArea
                    });
                    continue;
                }

                if (node.InstanceExtResourceId != null)
                {
                    var prefab = tscn.FindExtResourcePathById(node.InstanceExtResourceId) ?? "";
                    if (prefab.Length > 0)
                    {
                        var isPortalPrefab = prefab.Contains("/Portal.tscn", StringComparison.OrdinalIgnoreCase);
                        var isCollectible = prefab.Contains("/Collectible.tscn", StringComparison.OrdinalIgnoreCase);
                        var isElevator = prefab.Contains("/Elevator.tscn", StringComparison.OrdinalIgnoreCase);

                        if (isPortalPrefab)
                        {
                            var target = node.RawProps.TryGetValue("target_map", out var tm) ? Unquote(tm) : "";
                            var targetArea = node.RawProps.TryGetValue("target_area", out var ta) ? UnquoteStringName(ta) : "";
                            if (targetArea.Length == 0)
                                targetArea = node.RawProps.TryGetValue("target_portal", out var tp) ? Unquote(tp) : "";
                            map.Portals.Add(new Portal
                            {
                                Id = nodePath,
                                Name = node.Name,
                                NodePath = nodePath,
                                X = pos.x,
                                Y = pos.y,
                                TargetMapId = target,
                                TargetPortalId = targetArea
                            });
                        }
                        else if (isCollectible || isElevator)
                        {
                            map.Entities.Add(new PlacedEntity
                            {
                                Type = isCollectible ? "Collectible" : "Elevator",
                                Prefab = prefab,
                                NodePath = nodePath,
                                X = pos.x,
                                Y = pos.y,
                                Pushable = false
                            });
                        }
                        else
                        {
                            map.Entities.Add(new PlacedEntity
                            {
                                Type = "Entity",
                                Prefab = prefab,
                                NodePath = nodePath,
                                X = pos.x,
                                Y = pos.y,
                                Pushable = true
                            });
                        }
                    }
                }
            }
        }

        if (map.TileLayers.Count > 0 && map.Skin.TileSetId == "default")
            map.Skin.TileSetId = map.TileLayers[0].TileSetPath.Length > 0 ? map.TileLayers[0].TileSetPath : map.Skin.TileSetId;

        var maxTileX = -1;
        var maxTileY = -1;
        foreach (var layer in map.TileLayers)
        {
            foreach (var c in layer.Cells)
            {
                if (c.X > maxTileX) maxTileX = c.X;
                if (c.Y > maxTileY) maxTileY = c.Y;
            }
        }

        if (maxTileX >= 0 || maxTileY >= 0)
        {
            if (maxTileX >= 0)
                map.RoomWidth = Math.Max(map.RoomWidth, maxTileX + 1);
            if (maxTileY >= 0)
                map.RoomHeight = Math.Max(map.RoomHeight, maxTileY + 1);
        }
        else
        {
            var maxPxX = 0f;
            var maxPxY = 0f;
            foreach (var p in map.Portals)
            {
                if (p.X > maxPxX) maxPxX = p.X;
                if (p.Y > maxPxY) maxPxY = p.Y;
            }
            foreach (var ent in map.Entities)
            {
                if (ent.X > maxPxX) maxPxX = ent.X;
                if (ent.Y > maxPxY) maxPxY = ent.Y;
            }

            if (maxPxX > 0 || maxPxY > 0)
            {
                map.RoomWidth = Math.Max(map.RoomWidth, (int)MathF.Ceiling(maxPxX / 32f) + 4);
                map.RoomHeight = Math.Max(map.RoomHeight, (int)MathF.Ceiling(maxPxY / 32f) + 4);
            }
        }

        map.BackgroundTextureEnabled = map.BackgroundTexturePath.Length > 0;
        map.ForegroundTextureEnabled = map.ForegroundTexturePath.Length > 0;
        map.BackgroundTileLayerVisible = map.TileLayers.Any(l => IsBackgroundTileLayerName(l.Name) && l.Visible);

        return map;
    }

    private static bool TryParseTextureAnchor(string s, out TextureAnchor anchor)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        switch (s)
        {
            case "topleft":
            case "top_left":
            case "top-left":
            case "lt":
                anchor = TextureAnchor.TopLeft;
                return true;
            case "topright":
            case "top_right":
            case "top-right":
            case "rt":
                anchor = TextureAnchor.TopRight;
                return true;
            case "bottomleft":
            case "bottom_left":
            case "bottom-left":
            case "lb":
                anchor = TextureAnchor.BottomLeft;
                return true;
            case "bottomright":
            case "bottom_right":
            case "bottom-right":
            case "rb":
                anchor = TextureAnchor.BottomRight;
                return true;
            case "center":
            case "centre":
            case "c":
                anchor = TextureAnchor.Center;
                return true;
            default:
                anchor = TextureAnchor.TopLeft;
                return false;
        }
    }

    private static bool IsBackgroundTileLayerName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
            return false;
        if (string.Equals(name, "Foreground", StringComparison.OrdinalIgnoreCase))
            return false;
        return name.Contains("back", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidResPath(string? path)
    {
        path = (path ?? "").Trim();
        return path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("uid://", StringComparison.Ordinal);
    }

    private static List<TileCell> DecodeTileMapData(string raw)
    {
        raw = raw.Trim();
        if (!raw.StartsWith("PackedByteArray", StringComparison.Ordinal))
            return [];

        byte[] bytes;
        var q1 = raw.IndexOf('"');
        if (q1 >= 0)
        {
            var q2 = raw.IndexOf('"', q1 + 1);
            if (q2 <= q1)
                return [];
            var b64 = raw[(q1 + 1)..q2];
            bytes = Convert.FromBase64String(b64);
        }
        else
        {
            var p1 = raw.IndexOf('(');
            var p2 = raw.LastIndexOf(')');
            if (p1 < 0 || p2 <= p1)
                return [];
            var content = raw[(p1 + 1)..p2];
            var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bytes = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!byte.TryParse(parts[i], out var b))
                    return [];
                bytes[i] = b;
            }
        }

        if (bytes.Length < 2)
            return [];

        var u16Count = bytes.Length / 2;
        var u = new ushort[u16Count];
        for (var i = 0; i < u16Count; i++)
            u[i] = BitConverter.ToUInt16(bytes, i * 2);

        if (u.Length < 1 + 6)
            return [];

        var cells = new List<TileCell>();
        for (var i = 1; i + 5 < u.Length; i += 6)
        {
            var x = (int)u[i + 0];
            var y = (int)u[i + 1];
            var sourceId = (int)u[i + 2];
            var atlasX = (int)u[i + 3];
            var atlasY = (int)u[i + 4];
            var alt = (int)u[i + 5];
            cells.Add(new TileCell { X = x, Y = y, SourceId = sourceId, AtlasX = atlasX, AtlasY = atlasY, Alternative = alt });
        }
        return cells;
    }

    private static void BuildLinks(MapProject project, Dictionary<string, MapDefinition> byScenePath, Dictionary<string, string> byUid)
    {
        foreach (var map in project.Maps)
        {
            foreach (var portal in map.Portals)
            {
                var target = portal.TargetMapId;
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                var normalized = NormalizeScenePath(target, byUid);
                if (normalized == null)
                    continue;

                if (!byScenePath.ContainsKey(normalized))
                    continue;

                project.Links.Add(new MapLink
                {
                    From = new LinkEndpoint { MapId = map.Id, PortalId = portal.Id },
                    To = new LinkEndpoint { MapId = normalized, PortalId = portal.TargetPortalId ?? "" }
                });
            }
        }
    }

    private static string ComputeNodePath(TscnNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Parent) || node.Parent == ".")
            return node.Name;
        return node.Parent.Trim('/') + "/" + node.Name;
    }

    private static (float x, float y) ParseVector2(string raw)
    {
        var m = Vector2Regex().Match(raw);
        if (!m.Success)
            return (0, 0);
        return (float.Parse(m.Groups["x"].Value), float.Parse(m.Groups["y"].Value));
    }

    private static string Unquote(string raw)
    {
        raw = raw.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw[1..^1];
        return raw;
    }

    private static string UnquoteStringName(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("&\"", StringComparison.Ordinal) && raw.EndsWith('"'))
            return raw[2..^1];
        return Unquote(raw);
    }

    private static string? NormalizeScenePath(string targetMapValue, Dictionary<string, string> byUid)
    {
        var t = targetMapValue.Trim();
        if (t.StartsWith("ExtResource(", StringComparison.Ordinal))
            return null;
        if (t.StartsWith("uid://", StringComparison.Ordinal))
        {
            if (byUid.TryGetValue(t, out var p))
                return p;
            return null;
        }
        if (t.StartsWith("res://", StringComparison.Ordinal))
            return t;
        return null;
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
            foreach (var f in files)
                yield return f;

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }
            foreach (var sub in subDirs)
            {
                var name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (IsIgnoredDirName(name))
                    continue;
                stack.Push(sub);
            }
        }
    }

    private static bool IsIgnoredDirName(string name)
    {
        return name.Equals(".godot", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals("GodotTools", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMapScene(TscnScene tscn)
    {
        foreach (var n in tscn.Nodes)
        {
            if (string.Equals(n.Type, "TileMapLayer", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    [GeneratedRegex("Vector2\\((?<x>-?\\d+(?:\\.\\d+)?),\\s*(?<y>-?\\d+(?:\\.\\d+)?)\\)")]
    private static partial Regex Vector2Regex();

    [GeneratedRegex("ExtResource\\(\\\"(?<id>[^\\\"]+)\\\"\\)")]
    private static partial Regex ExtResourceValueRegex();
}
