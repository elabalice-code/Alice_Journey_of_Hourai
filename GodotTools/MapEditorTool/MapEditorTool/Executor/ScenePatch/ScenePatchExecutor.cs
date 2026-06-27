using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.ScenePatch
{
    public sealed class ScenePatchExecutor
    {
        public ScenePatchResult PatchNodePosition(string sceneFilePath, string nodePath, float x, float y)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var node = FindNodeOrCreatePortal(sceneFilePath, ref scene, nodePath, x, y);

            node.RawProps["position"] = FormatVector2(x, y);
            var dirty = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "position" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = nodePath,
                Patched = dirty,
                PatchedKey = "position",
                NewRawValue = node.RawProps["position"]
            };
        }

        public ScenePatchResult PatchPortalTarget(string sceneFilePath, string nodePath, string targetMapId, string targetPortalId)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var node = FindNodeOrCreatePortal(sceneFilePath, ref scene, nodePath, 0, 0);

            node.RawProps["target_map"] = QuoteGodotString(targetMapId);
            node.RawProps["target_area"] = QuoteGodotStringName(targetPortalId);
            var dirty = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "target_map", "target_area" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = nodePath,
                Patched = dirty,
                PatchedKey = "target_map,target_area",
                NewRawValue = "target_map = " + node.RawProps["target_map"] + "; target_area = " + node.RawProps["target_area"]
            };
        }

        public ScenePatchResult PatchPortalAnimation(string sceneFilePath, string nodePath, string animationDirectoryResPath, float fps, float upscale)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var node = FindNodeOrCreatePortal(sceneFilePath, ref scene, nodePath, 0, 0);

            node.RawProps["portal_anim_dir"] = QuoteGodotString(animationDirectoryResPath);
            node.RawProps["portal_anim_fps"] = FormatFloat(fps);
            node.RawProps["portal_upscale"] = FormatFloat(Math.Max(0.001f, upscale));
            var dirty = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "portal_anim_dir", "portal_anim_fps", "portal_upscale" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = nodePath,
                Patched = dirty,
                PatchedKey = "portal_anim_dir,portal_anim_fps,portal_upscale",
                NewRawValue = "portal_anim_dir = " + node.RawProps["portal_anim_dir"] +
                    "; portal_anim_fps = " + node.RawProps["portal_anim_fps"] +
                    "; portal_upscale = " + node.RawProps["portal_upscale"]
            };
        }

        public ScenePatchResult PatchPortalAnimationSettings(string sceneFilePath, string nodePath, float fps, float upscale)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var node = FindNodeOrCreatePortal(sceneFilePath, ref scene, nodePath, 0, 0);

            node.RawProps["portal_anim_fps"] = FormatFloat(fps);
            node.RawProps["portal_upscale"] = FormatFloat(Math.Max(0.001f, upscale));
            var dirty = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "portal_anim_fps", "portal_upscale" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = nodePath,
                Patched = dirty,
                PatchedKey = "portal_anim_fps,portal_upscale",
                NewRawValue = "portal_anim_fps = " + node.RawProps["portal_anim_fps"] +
                    "; portal_upscale = " + node.RawProps["portal_upscale"]
            };
        }

        public ScenePatchResult PatchCollisionMetadata(
            string sceneFilePath,
            CollisionMode collisionMode,
            string tileCollisionDataResPath,
            string foregroundTextureCollisionDataResPath)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, "Map"));
            var node = FindNode(scene, "Map");

            var mode = collisionMode == CollisionMode.ForegroundTexture ? "foreground_texture" : "tile_foreground";
            node.RawProps["metadata/collision_mode"] = QuoteGodotString(mode);
            node.RawProps["metadata/collision_tile_path"] = QuoteGodotString(tileCollisionDataResPath);
            node.RawProps["metadata/collision_fgtex_path"] = QuoteGodotString(foregroundTextureCollisionDataResPath);

            var dirty = TscnWriter.PatchFile(
                sceneFilePath,
                scene,
                new[] { "metadata/collision_mode", "metadata/collision_tile_path", "metadata/collision_fgtex_path" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = "Map",
                Patched = dirty,
                PatchedKey = "metadata/collision_mode,metadata/collision_tile_path,metadata/collision_fgtex_path",
                NewRawValue = "metadata/collision_mode = " + node.RawProps["metadata/collision_mode"] +
                    "; metadata/collision_tile_path = " + node.RawProps["metadata/collision_tile_path"] +
                    "; metadata/collision_fgtex_path = " + node.RawProps["metadata/collision_fgtex_path"]
            };
        }

        public ScenePatchResult PatchMapRuntimeNodes(string sceneFilePath, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, "Map"));
            var byPath = BuildNodePathIndex(scene);
            var patchedPortalCount = 0;
            var patchedEntityCount = 0;
            var missingNodeCount = 0;

            foreach (var portal in map.Portals ?? Enumerable.Empty<Portal>())
            {
                if (portal == null || string.IsNullOrWhiteSpace(portal.NodePath))
                    continue;

                TscnNode node;
                if (!byPath.TryGetValue(portal.NodePath, out node))
                {
                    var createResult = EnsurePortalNodeExists(sceneFilePath, portal.NodePath, portal.X, portal.Y, null);
                    if (createResult.Patched)
                    {
                        scene = TscnParser.ParseFile(sceneFilePath);
                        byPath = BuildNodePathIndex(scene);
                        if (!byPath.TryGetValue(portal.NodePath, out node))
                        {
                            missingNodeCount++;
                            continue;
                        }
                    }
                    else
                    {
                        missingNodeCount++;
                        continue;
                    }
                }

                node.RawProps["position"] = FormatVector2(portal.X, portal.Y);
                node.RawProps["target_map"] = QuoteGodotString(portal.TargetMapId);
                node.RawProps["target_area"] = QuoteGodotStringName(portal.TargetPortalId);
                patchedPortalCount++;
            }

            foreach (var entity in map.Entities ?? Enumerable.Empty<PlacedEntity>())
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.NodePath))
                    continue;

                TscnNode node;
                if (!byPath.TryGetValue(entity.NodePath, out node))
                {
                    missingNodeCount++;
                    continue;
                }

                node.RawProps["position"] = FormatVector2(entity.X, entity.Y);
                patchedEntityCount++;
            }

            var dirty = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "position", "target_map", "target_area" });

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = "Map",
                Patched = dirty,
                PatchedKey = "position,target_map,target_area",
                NewRawValue = "patchedPortals=" + patchedPortalCount +
                    "; patchedEntities=" + patchedEntityCount +
                    "; missingNodes=" + missingNodeCount
            };
        }

        public ScenePatchResult PatchBackgroundTileLayerVisibility(string sceneFilePath, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, "Map"));
            var patchedLayerCount = 0;
            foreach (var node in scene.Nodes)
            {
                if (!string.Equals(node.Type, "TileMapLayer", StringComparison.Ordinal))
                    continue;
                if (!IsBackgroundTileLayerName(node.Name))
                    continue;

                node.RawProps["visible"] = map.BackgroundTileLayerVisible ? "true" : "false";
                patchedLayerCount++;
            }

            var dirty = patchedLayerCount > 0 && TscnWriter.PatchFile(sceneFilePath, scene, new[] { "visible" });
            SyncBackgroundTileLayerVisibility(map);

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = "TileMapLayer(background)",
                Patched = dirty,
                PatchedKey = "visible",
                NewRawValue = "backgroundTileLayerVisible=" + map.BackgroundTileLayerVisible +
                    "; patchedLayers=" + patchedLayerCount
            };
        }

        public ScenePatchResult EnsurePortalNodeExists(string sceneFilePath, string nodePath, float x, float y, string portalPrefabResPath)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var existing = scene.Nodes.FirstOrDefault(n => string.Equals(ComputeNodePath(n.Parent, n.Name), nodePath, StringComparison.Ordinal));
            if (existing != null)
            {
                existing.RawProps["position"] = FormatVector2(x, y);
                var positionPatched = TscnWriter.PatchFile(sceneFilePath, scene, new[] { "position" });
                return new ScenePatchResult
                {
                    SceneFilePath = Path.GetFullPath(sceneFilePath),
                    NodePath = nodePath,
                    Patched = positionPatched,
                    PatchedKey = "position",
                    NewRawValue = "existingPortalNode=true; position = " + existing.RawProps["position"]
                };
            }

            nodePath = (nodePath ?? string.Empty).Trim().Trim('/');
            if (nodePath.Length == 0)
                throw new ArgumentException("Node path is empty.", "nodePath");

            var parent = ".";
            var name = nodePath;
            var slash = nodePath.LastIndexOf('/');
            if (slash >= 0)
            {
                parent = nodePath.Substring(0, slash);
                name = nodePath.Substring(slash + 1);
                if (parent.Length == 0)
                    parent = ".";
            }

            var portalResPath = string.IsNullOrWhiteSpace(portalPrefabResPath)
                ? (FindExistingPortalPrefabResPath(scene) ?? "res://CoreEngine/Objects/Portal.tscn")
                : portalPrefabResPath.Trim();

            var extId = FindOrCreatePortalExtResource(sceneFilePath, scene, portalResPath);
            AppendPortalNode(sceneFilePath, parent, name, extId, x, y);

            return new ScenePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                NodePath = nodePath,
                Patched = true,
                PatchedKey = "portal_node",
                NewRawValue = "createdPortalNode=true; portalPrefab=" + portalResPath + "; extId=" + extId
            };
        }

        private static string ValidateScenePatchInput(string sceneFilePath, string nodePath)
        {
            if (string.IsNullOrWhiteSpace(sceneFilePath))
                throw new FileNotFoundException("Scene file path is empty.", sceneFilePath);
            if (!File.Exists(sceneFilePath))
                throw new FileNotFoundException("Scene file not found.", sceneFilePath);
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("Node path is empty.", "nodePath");

            return sceneFilePath;
        }

        private static TscnNode FindNode(TscnScene scene, string nodePath)
        {
            var node = scene.Nodes.FirstOrDefault(n => string.Equals(ComputeNodePath(n.Parent, n.Name), nodePath, StringComparison.Ordinal));
            if (node == null)
                throw new InvalidOperationException("Node not found: " + nodePath);

            return node;
        }

        private static TscnNode FindNodeOrCreatePortal(string sceneFilePath, ref TscnScene scene, string nodePath, float x, float y)
        {
            var node = scene.Nodes.FirstOrDefault(n => string.Equals(ComputeNodePath(n.Parent, n.Name), nodePath, StringComparison.Ordinal));
            if (node != null)
                return node;

            var executor = new ScenePatchExecutor();
            executor.EnsurePortalNodeExists(sceneFilePath, nodePath, x, y, null);
            scene = TscnParser.ParseFile(sceneFilePath);
            return FindNode(scene, nodePath);
        }

        private static Dictionary<string, TscnNode> BuildNodePathIndex(TscnScene scene)
        {
            var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
            foreach (var node in scene.Nodes)
            {
                var path = ComputeNodePath(node.Parent, node.Name);
                if (!string.IsNullOrWhiteSpace(path))
                    byPath[path] = node;
            }

            return byPath;
        }

        private static string ComputeNodePath(string parent, string name)
        {
            parent = (parent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return name;

            return parent.Trim('/') + "/" + name;
        }

        private static void SyncBackgroundTileLayerVisibility(MapDefinition map)
        {
            foreach (var layer in map.TileLayers ?? Enumerable.Empty<TileLayer>())
            {
                if (IsBackgroundTileLayerName(layer.Name))
                    layer.Visible = map.BackgroundTileLayerVisible;
            }
        }

        private static bool IsBackgroundTileLayerName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return false;
            if (string.Equals(name, "Foreground", StringComparison.OrdinalIgnoreCase))
                return false;
            return name.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindExistingPortalPrefabResPath(TscnScene scene)
        {
            var hit = scene.ExtResources.FirstOrDefault(resource =>
                string.Equals((resource.Type ?? string.Empty).Trim(), "PackedScene", StringComparison.Ordinal) &&
                (resource.Path ?? string.Empty).Trim().EndsWith("/Portal.tscn", StringComparison.OrdinalIgnoreCase));
            var path = hit == null ? string.Empty : (hit.Path ?? string.Empty).Trim();
            return path.Length == 0 ? null : path;
        }

        private static string FindOrCreatePortalExtResource(string sceneFilePath, TscnScene scene, string portalResPath)
        {
            portalResPath = (portalResPath ?? string.Empty).Trim();
            if (portalResPath.Length == 0)
                throw new ArgumentException("Portal prefab resource path is empty.", "portalResPath");

            var portalExt = scene.ExtResources.FirstOrDefault(resource =>
                string.Equals((resource.Type ?? string.Empty).Trim(), "PackedScene", StringComparison.Ordinal) &&
                string.Equals((resource.Path ?? string.Empty).Trim(), portalResPath, StringComparison.Ordinal));
            var extId = portalExt == null ? string.Empty : (portalExt.Id ?? string.Empty).Trim();
            if (extId.Length > 0)
                return extId;

            var existingIds = new HashSet<string>(
                scene.ExtResources.Select(resource => (resource.Id ?? string.Empty).Trim()).Where(id => id.Length > 0),
                StringComparer.Ordinal);

            var number = 1;
            foreach (var id in existingIds)
            {
                var pieces = id.Split(new[] { '_' }, 2);
                int parsed;
                if (pieces.Length >= 1 && int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed >= number)
                    number = parsed + 1;
            }

            extId = number.ToString(CultureInfo.InvariantCulture) + "_portal";
            while (existingIds.Contains(extId))
            {
                number++;
                extId = number.ToString(CultureInfo.InvariantCulture) + "_portal";
            }

            var lines = File.ReadAllLines(sceneFilePath).ToList();
            var insertAt = FindExtResourceInsertIndex(lines);
            lines.Insert(insertAt, "[ext_resource type=\"PackedScene\" path=\"" + portalResPath.Replace("\\", "/") + "\" id=\"" + extId + "\"]");
            UpdateGdSceneLoadSteps(lines);
            File.WriteAllLines(sceneFilePath, lines);
            return extId;
        }

        private static void AppendPortalNode(string sceneFilePath, string parent, string name, string extId, float x, float y)
        {
            var lines = File.ReadAllLines(sceneFilePath).ToList();
            UpdateGdSceneLoadSteps(lines);
            lines.Add(string.Empty);
            lines.Add("[node name=\"" + name + "\" parent=\"" + parent + "\" instance=ExtResource(\"" + extId + "\")]");
            lines.Add("position = " + FormatVector2(x, y));
            lines.Add("target_map = \"\"");
            lines.Add("target_area = &\"\"");
            File.WriteAllLines(sceneFilePath, lines);
        }

        private static int FindExtResourceInsertIndex(List<string> lines)
        {
            var lastExt = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (text.StartsWith("[ext_resource", StringComparison.Ordinal))
                    lastExt = i;
            }

            if (lastExt >= 0)
                return lastExt + 1;

            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (text.StartsWith("[node", StringComparison.Ordinal) || text.StartsWith("[sub_resource", StringComparison.Ordinal))
                    return i;
            }

            return lines.Count;
        }

        private static void UpdateGdSceneLoadSteps(List<string> lines)
        {
            var extCount = 0;
            var subCount = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (text.StartsWith("[ext_resource", StringComparison.Ordinal))
                    extCount++;
                else if (text.StartsWith("[sub_resource", StringComparison.Ordinal))
                    subCount++;
            }

            var loadSteps = extCount + subCount + 1;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (!text.StartsWith("[gd_scene", StringComparison.Ordinal))
                    continue;

                var line = lines[i];
                if (!Regex.IsMatch(line, "\\bload_steps=\\d+\\b", RegexOptions.CultureInvariant))
                    return;

                lines[i] = Regex.Replace(line, "\\bload_steps=\\d+\\b", "load_steps=" + loadSteps.ToString(CultureInfo.InvariantCulture), RegexOptions.CultureInvariant);
                return;
            }
        }

        private static string FormatVector2(float x, float y)
        {
            return "Vector2(" +
                FormatFloat(x) +
                ", " +
                FormatFloat(y) +
                ")";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string QuoteGodotString(string value)
        {
            value = value ?? string.Empty;
            value = value.Replace("\"", "\\\"");
            return "\"" + value + "\"";
        }

        private static string QuoteGodotStringName(string value)
        {
            value = value ?? string.Empty;
            value = value.Replace("\"", "\\\"");
            return "&\"" + value + "\"";
        }
    }
}
