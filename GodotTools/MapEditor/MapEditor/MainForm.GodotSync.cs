using System.Globalization;
using MapEditor.Godot;
using MapEditor.Godot.Tscn;
using MapEditor.Models;

namespace MapEditor;

public sealed partial class MainForm
{
    private static void PatchNodePosition(string sceneAbsPath, string nodePath, float x, float y)
    {
        var scene = TscnParser.ParseFile(sceneAbsPath);
        var node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
        if (node == null)
        {
            if (TryRepairMissingPortalNode(sceneAbsPath, nodePath))
            {
                scene = TscnParser.ParseFile(sceneAbsPath);
                node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
            }
            if (node == null)
                throw new InvalidOperationException($"Node not found: {nodePath}");
        }
        node.RawProps["position"] = FormatVector2(x, y);
        TscnWriter.PatchFile(sceneAbsPath, scene, ["position"]);
    }

    private static void PatchPortalTarget(string sceneAbsPath, string nodePath, string targetMapId, string targetPortalId)
    {
        var scene = TscnParser.ParseFile(sceneAbsPath);
        var node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
        if (node == null)
        {
            if (TryRepairMissingPortalNode(sceneAbsPath, nodePath))
            {
                scene = TscnParser.ParseFile(sceneAbsPath);
                node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
            }
            if (node == null)
                throw new InvalidOperationException($"Node not found: {nodePath}");
        }
        node.RawProps["target_map"] = QuoteGodotString(targetMapId ?? "");
        node.RawProps["target_area"] = QuoteGodotStringName(targetPortalId ?? "");
        TscnWriter.PatchFile(sceneAbsPath, scene, ["target_map", "target_area"]);
    }

    private static void PatchPortalAnimation(string sceneAbsPath, string nodePath, string animDirResPath, float fps, float upscale)
    {
        var scene = TscnParser.ParseFile(sceneAbsPath);
        var node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
        if (node == null)
        {
            if (TryRepairMissingPortalNode(sceneAbsPath, nodePath))
            {
                scene = TscnParser.ParseFile(sceneAbsPath);
                node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
            }
            if (node == null)
                throw new InvalidOperationException($"Node not found: {nodePath}");
        }

        node.RawProps["portal_anim_dir"] = QuoteGodotString(animDirResPath ?? "");
        node.RawProps["portal_anim_fps"] = fps.ToString("0.###", CultureInfo.InvariantCulture);
        node.RawProps["portal_upscale"] = Math.Max(0.001f, upscale).ToString("0.###", CultureInfo.InvariantCulture);
        TscnWriter.PatchFile(sceneAbsPath, scene, ["portal_anim_dir", "portal_anim_fps", "portal_upscale"]);
    }

    private static void PatchPortalAnimSettings(string sceneAbsPath, string nodePath, float fps, float upscale)
    {
        var scene = TscnParser.ParseFile(sceneAbsPath);
        var node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
        if (node == null)
        {
            if (TryRepairMissingPortalNode(sceneAbsPath, nodePath))
            {
                scene = TscnParser.ParseFile(sceneAbsPath);
                node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
            }
            if (node == null)
                throw new InvalidOperationException($"Node not found: {nodePath}");
        }

        node.RawProps["portal_anim_fps"] = fps.ToString("0.###", CultureInfo.InvariantCulture);
        node.RawProps["portal_upscale"] = Math.Max(0.001f, upscale).ToString("0.###", CultureInfo.InvariantCulture);
        TscnWriter.PatchFile(sceneAbsPath, scene, ["portal_anim_fps", "portal_upscale"]);
    }

    private static string QuoteGodotString(string value)
    {
        value ??= "";
        value = value.Replace("\"", "\\\"");
        return $"\"{value}\"";
    }

    private static string QuoteGodotStringName(string value)
    {
        value ??= "";
        value = value.Replace("\"", "\\\"");
        return $"&\"{value}\"";
    }

    private static string ComputeNodePath(string? parent, string name)
    {
        parent = parent?.Trim();
        if (string.IsNullOrWhiteSpace(parent) || parent == ".")
            return name;
        return parent.Trim('/') + "/" + name;
    }

    private void ApplyMapToGodot(MapDefinition map)
    {
        if (map.ScenePath.Length == 0)
            return;
        var root = _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);
        var abs = ToAbsoluteGodotPath(root, map.ScenePath);
        var scene = TscnParser.ParseFile(abs);

        var anyCreated = false;
        foreach (var portal in map.Portals)
        {
            if (portal.NodePath.Length == 0)
                continue;
            if (scene.Nodes.Any(n => string.Equals(ComputeNodePath(n.Parent, n.Name), portal.NodePath, StringComparison.Ordinal)))
                continue;
            if (EnsurePortalNodeExists(abs, portal.NodePath, portal.X, portal.Y))
                anyCreated = true;
        }
        if (anyCreated)
            scene = TscnParser.ParseFile(abs);

        var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
        foreach (var n in scene.Nodes)
        {
            var p = string.IsNullOrWhiteSpace(n.Parent) || n.Parent == "."
                ? n.Name
                : n.Parent.Trim('/') + "/" + n.Name;
            byPath[p] = n;
        }

        foreach (var portal in map.Portals)
        {
            if (portal.NodePath.Length == 0)
                continue;
            if (byPath.TryGetValue(portal.NodePath, out var node))
            {
                node.RawProps["position"] = FormatVector2(portal.X, portal.Y);
                node.RawProps["target_map"] = QuoteGodotString(portal.TargetMapId ?? "");
                node.RawProps["target_area"] = QuoteGodotStringName(portal.TargetPortalId ?? "");
            }
        }
        foreach (var e in map.Entities)
        {
            if (e.NodePath.Length == 0)
                continue;
            if (byPath.TryGetValue(e.NodePath, out var node))
                node.RawProps["position"] = FormatVector2(e.X, e.Y);
        }

        TscnWriter.PatchFile(abs, scene, ["position", "target_map", "target_area"]);
    }

    private void ApplyTileCollisionsToGodot(MapDefinition map, IReadOnlyList<MapCanvas.TileCollisionCommit> edits)
    {
        if (edits.Count == 0)
            return;

        var root = _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);

        var sceneAbs = ToAbsoluteGodotPath(root, map.ScenePath);
        var scene = TscnParser.ParseFile(sceneAbs);

        var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
        foreach (var n in scene.Nodes)
        {
            var p = string.IsNullOrWhiteSpace(n.Parent) || n.Parent == "."
                ? n.Name
                : n.Parent.Trim('/') + "/" + n.Name;
            byPath[p] = n;
        }

        var tileSetCache = new Dictionary<string, GodotTileSet>(StringComparer.OrdinalIgnoreCase);
        var tileSetAbsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edit in edits)
        {
            var tilesetAbs = tileSetAbsCache.TryGetValue(edit.TileSetResPath, out var cachedAbs)
                ? cachedAbs
                : (tileSetAbsCache[edit.TileSetResPath] = ToAbsoluteGodotPath(root, edit.TileSetResPath));

            if (!tileSetCache.TryGetValue(tilesetAbs, out var ts))
            {
                ts = GodotTileSetLoader.Load(tilesetAbs);
                tileSetCache[tilesetAbs] = ts;
            }

            if (!ts.Sources.TryGetValue(edit.SourceId, out var src))
                throw new InvalidOperationException($"TileSet source_id not found: {edit.SourceId}");

            var newAlt = GodotTileSetLoader.CreateAtlasPhysicsPolygonAlternative(tilesetAbs, src.SubResourceId, edit.AtlasX, edit.AtlasY, edit.OneWay, edit.ToPoints);

            if (!byPath.TryGetValue(edit.LayerNodePath, out var layerNode))
                throw new InvalidOperationException($"TileMapLayer node not found: {edit.LayerNodePath}");
            if (!layerNode.RawProps.TryGetValue("tile_map_data", out var tmd))
                throw new InvalidOperationException($"tile_map_data not found on node: {edit.LayerNodePath}");

            var patched = PatchTileMapDataAlternative(tmd, edit.CellX, edit.CellY, newAlt);
            layerNode.RawProps["tile_map_data"] = patched;

            var layerModel = map.TileLayers.FirstOrDefault(l => string.Equals(l.NodePath, edit.LayerNodePath, StringComparison.Ordinal));
            if (layerModel != null)
            {
                var cellModel = layerModel.Cells.FirstOrDefault(c => c.X == edit.CellX && c.Y == edit.CellY);
                if (cellModel != null)
                    cellModel.Alternative = newAlt;
            }
        }

        TscnWriter.PatchFile(sceneAbs, scene, ["tile_map_data"]);
    }

    private void ApplyTileCollisionAlternativesToGodot(MapDefinition map, IReadOnlyList<MapCanvas.TileCollisionAltCommit> edits)
    {
        if (edits.Count == 0)
            return;

        var root = _godotRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = GodotProjectLocator.FindGodotRoot(AppContext.BaseDirectory);

        var sceneAbs = ToAbsoluteGodotPath(root, map.ScenePath);
        var scene = TscnParser.ParseFile(sceneAbs);

        var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
        foreach (var n in scene.Nodes)
        {
            var p = string.IsNullOrWhiteSpace(n.Parent) || n.Parent == "."
                ? n.Name
                : n.Parent.Trim('/') + "/" + n.Name;
            byPath[p] = n;
        }

        foreach (var edit in edits)
        {
            if (!byPath.TryGetValue(edit.LayerNodePath, out var layerNode))
                throw new InvalidOperationException($"TileMapLayer node not found: {edit.LayerNodePath}");
            if (!layerNode.RawProps.TryGetValue("tile_map_data", out var tmd))
                throw new InvalidOperationException($"tile_map_data not found on node: {edit.LayerNodePath}");

            var patched = PatchTileMapDataAlternative(tmd, edit.CellX, edit.CellY, edit.ToAlternative);
            layerNode.RawProps["tile_map_data"] = patched;

            var layerModel = map.TileLayers.FirstOrDefault(l => string.Equals(l.NodePath, edit.LayerNodePath, StringComparison.Ordinal));
            if (layerModel != null)
            {
                var cellModel = layerModel.Cells.FirstOrDefault(c => c.X == edit.CellX && c.Y == edit.CellY);
                if (cellModel != null)
                    cellModel.Alternative = edit.ToAlternative;
            }
        }

        TscnWriter.PatchFile(sceneAbs, scene, ["tile_map_data"]);
    }

    private static string PatchTileMapDataAlternative(string raw, int cellX, int cellY, int newAlt)
    {
        raw = raw.Trim();
        if (!raw.StartsWith("PackedByteArray", StringComparison.Ordinal))
            throw new InvalidOperationException("tile_map_data is not a PackedByteArray.");

        byte[] bytes;
        var q1 = raw.IndexOf('"');
        if (q1 >= 0)
        {
            var q2 = raw.IndexOf('"', q1 + 1);
            if (q2 <= q1)
                throw new InvalidOperationException("Invalid PackedByteArray base64 string.");
            var b64 = raw[(q1 + 1)..q2];
            bytes = Convert.FromBase64String(b64);
        }
        else
        {
            var p1 = raw.IndexOf('(');
            var p2 = raw.LastIndexOf(')');
            if (p1 < 0 || p2 <= p1)
                throw new InvalidOperationException("Invalid PackedByteArray.");
            var content = raw[(p1 + 1)..p2];
            var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bytes = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!byte.TryParse(parts[i], out var b))
                    throw new InvalidOperationException("Invalid PackedByteArray.");
                bytes[i] = b;
            }
        }

        if (bytes.Length < 2)
            throw new InvalidOperationException("Invalid tile_map_data buffer.");

        var u16Count = bytes.Length / 2;
        var u = new ushort[u16Count];
        for (var i = 0; i < u16Count; i++)
            u[i] = BitConverter.ToUInt16(bytes, i * 2);

        var found = false;
        for (var i = 1; i + 5 < u.Length; i += 6)
        {
            var x = (int)u[i + 0];
            var y = (int)u[i + 1];
            if (x == cellX && y == cellY)
            {
                u[i + 5] = (ushort)newAlt;
                found = true;
                break;
            }
        }

        if (!found)
            throw new InvalidOperationException($"Cell not found in tile_map_data: ({cellX}, {cellY})");

        var outBytes = new byte[u.Length * 2];
        for (var i = 0; i < u.Length; i++)
        {
            var b = BitConverter.GetBytes(u[i]);
            outBytes[i * 2 + 0] = b[0];
            outBytes[i * 2 + 1] = b[1];
        }

        var outB64 = Convert.ToBase64String(outBytes);
        return $"PackedByteArray(\"{outB64}\")";
    }

    private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
    {
        var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath["res://".Length..] : resPath;
        rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(godotRoot, rel);
    }

    private static string FormatVector2(float x, float y)
    {
        return $"Vector2({x.ToString("0.###", CultureInfo.InvariantCulture)}, {y.ToString("0.###", CultureInfo.InvariantCulture)})";
    }
}
