using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapTexture
{
    public sealed class MapTextureExecutor
    {
        public MapTexturePatchResult PatchMapTextures(string sceneFilePath, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (string.IsNullOrWhiteSpace(sceneFilePath))
                throw new FileNotFoundException("Scene file path is empty.", sceneFilePath);
            if (!File.Exists(sceneFilePath))
                throw new FileNotFoundException("Scene file not found.", sceneFilePath);

            var scene = TscnParser.ParseFile(sceneFilePath);
            var mapNode = FindNode(scene, "Map");
            if (mapNode == null)
                throw new InvalidOperationException("Map node was not found.");

            var originalExtResourceCount = scene.ExtResources.Count;
            var keys = new List<string>();
            var isTemplate = IsTemplateRoomMap(scene, mapNode);

            if (isTemplate)
            {
                keys.AddRange(ApplyMapNodeTexturePatch(scene, mapNode, "background_texture", map.BackgroundTextureEnabled ? map.BackgroundTexturePath : string.Empty, "bg_"));
                keys.AddRange(ApplyMapNodeTexturePatch(scene, mapNode, "foreground_texture", map.ForegroundTextureEnabled ? map.ForegroundTexturePath : string.Empty, "fg_"));
                keys.AddRange(ApplyMapNodeTexturePatch(scene, mapNode, "template", map.TemplateTexturePath, "tpl_"));
            }
            else
            {
                var structureChanged = false;
                if (map.BackgroundTextureEnabled &&
                    (string.IsNullOrWhiteSpace(map.BackgroundNodePath) || FindNode(scene, map.BackgroundNodePath) == null))
                {
                    structureChanged = EnsureBackgroundLayerNodes(sceneFilePath) || structureChanged;
                    map.BackgroundNodePath = "BackgroundLayer/BackgroundTexture";
                }

                if (map.ForegroundTextureEnabled)
                {
                    structureChanged = EnsureForegroundTextureWorldNodes(sceneFilePath) || structureChanged;
                    if (string.IsNullOrWhiteSpace(map.ForegroundTextureNodePath))
                        map.ForegroundTextureNodePath = "ForegroundTextureLayer/ForegroundTexture";
                }

                if (structureChanged)
                {
                    scene = TscnParser.ParseFile(sceneFilePath);
                    originalExtResourceCount = scene.ExtResources.Count;
                    if (map.ForegroundTextureEnabled && FindNode(scene, map.ForegroundTextureNodePath) == null)
                        map.ForegroundTextureNodePath = "ForegroundTextureLayer/ForegroundTexture";
                }

                if (!string.IsNullOrWhiteSpace(map.BackgroundNodePath))
                {
                    var backgroundNode = FindNode(scene, map.BackgroundNodePath);
                    if (backgroundNode != null)
                        keys.AddRange(ApplyTextureNodePatch(scene, backgroundNode, map.BackgroundTextureEnabled ? map.BackgroundTexturePath : string.Empty, "bg_"));
                }

                if (!string.IsNullOrWhiteSpace(map.ForegroundTextureNodePath))
                {
                    var foregroundNode = FindNode(scene, map.ForegroundTextureNodePath);
                    if (foregroundNode != null)
                        keys.AddRange(ApplyTextureNodePatch(scene, foregroundNode, map.ForegroundTextureEnabled ? map.ForegroundTexturePath : string.Empty, "fg_"));
                }
            }

            if (keys.Count == 0)
                return new MapTexturePatchResult
                {
                    SceneFilePath = Path.GetFullPath(sceneFilePath),
                    Patched = false,
                    IsTemplateMap = isTemplate,
                    AddedExtResourceCount = 0,
                    Summary = "No texture keys were patched."
                };

            var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToList();
            var patched = TscnWriter.PatchFileWithExtResources(sceneFilePath, scene, distinctKeys);
            var addedExtResources = Math.Max(0, scene.ExtResources.Count - originalExtResourceCount);

            return new MapTexturePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                Patched = patched,
                IsTemplateMap = isTemplate,
                AddedExtResourceCount = addedExtResources,
                PatchedKeys = distinctKeys,
                Summary = "isTemplateMap=" + isTemplate +
                    "; patchedKeys=" + distinctKeys.Count +
                    "; addedExtResources=" + addedExtResources
            };
        }

        public MapTexturePatchResult PatchTextureMetadata(string sceneFilePath, MapDefinition map)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (string.IsNullOrWhiteSpace(sceneFilePath))
                throw new FileNotFoundException("Scene file path is empty.", sceneFilePath);
            if (!File.Exists(sceneFilePath))
                throw new FileNotFoundException("Scene file not found.", sceneFilePath);

            var scene = TscnParser.ParseFile(sceneFilePath);
            var mapNode = FindNode(scene, "Map");
            if (mapNode == null)
                throw new InvalidOperationException("Map node was not found.");

            mapNode.RawProps["metadata/foreground_texture_anchor"] = QuoteGodotString(map.ForegroundTextureAnchor.ToString());
            mapNode.RawProps["metadata/foreground_texture_upscale"] = QuoteGodotString(FormatFloat(Math.Max(0.0001f, map.ForegroundTextureUpscale)));
            mapNode.RawProps["metadata/background_texture_anchor"] = QuoteGodotString(map.BackgroundTextureAnchor.ToString());
            mapNode.RawProps["metadata/background_texture_upscale"] = QuoteGodotString(FormatFloat(Math.Max(0.0001f, map.BackgroundTextureUpscale)));

            var keys = new List<string>
            {
                "metadata/foreground_texture_anchor",
                "metadata/foreground_texture_upscale",
                "metadata/background_texture_anchor",
                "metadata/background_texture_upscale"
            };
            var patched = TscnWriter.PatchFile(sceneFilePath, scene, keys);

            return new MapTexturePatchResult
            {
                SceneFilePath = Path.GetFullPath(sceneFilePath),
                Patched = patched,
                IsTemplateMap = IsTemplateRoomMap(scene, mapNode),
                PatchedKeys = keys,
                Summary = "patchedTextureMetadata=" + patched
            };
        }

        private static IEnumerable<string> ApplyMapNodeTexturePatch(TscnScene scene, TscnNode mapNode, string key, string resPath, string idPrefix)
        {
            resPath = (resPath ?? string.Empty).Trim();
            if (resPath.Length == 0)
            {
                mapNode.RawProps[key] = "null";
                return new[] { key };
            }

            var id = EnsureExtResource(scene, "Texture2D", resPath, idPrefix);
            mapNode.RawProps[key] = "ExtResource(\"" + id + "\")";
            return new[] { key };
        }

        private static IEnumerable<string> ApplyTextureNodePatch(TscnScene scene, TscnNode node, string resPath, string idPrefix)
        {
            resPath = (resPath ?? string.Empty).Trim();
            if (resPath.Length == 0)
            {
                node.RawProps["texture"] = "null";
                return new[] { "texture" };
            }

            var id = EnsureExtResource(scene, "Texture2D", resPath, idPrefix);
            node.RawProps["texture"] = "ExtResource(\"" + id + "\")";
            return new[] { "texture" };
        }

        private static bool EnsureBackgroundLayerNodes(string sceneFilePath)
        {
            var lines = File.ReadAllLines(sceneFilePath).ToList();
            var hasBackground = lines.Any(line =>
                line.TrimStart().StartsWith("[node name=\"BackgroundLayer\" type=\"CanvasLayer\"", StringComparison.Ordinal));
            var hasForeground = lines.Any(line =>
                line.TrimStart().StartsWith("[node name=\"ForegroundTextureLayer\"", StringComparison.Ordinal));
            if (hasBackground && hasForeground)
                return false;

            var insertAt = FindInsertIndexAfterMapNode(lines);
            if (insertAt < 0)
                return false;

            var block = BuildTextureLayerBlock();
            if (hasBackground && !hasForeground)
            {
                var fgOnly = block.TakeWhile(line => !line.StartsWith("[node name=\"BackgroundLayer\"", StringComparison.Ordinal)).ToList();
                lines.InsertRange(insertAt, fgOnly);
            }
            else if (!hasBackground && hasForeground)
            {
                var bgStart = block.FindIndex(line => line.StartsWith("[node name=\"BackgroundLayer\"", StringComparison.Ordinal));
                var bgOnly = bgStart >= 0 ? block.Skip(bgStart).ToList() : new List<string>();
                lines.InsertRange(insertAt, bgOnly);
            }
            else
            {
                lines.InsertRange(insertAt, block);
            }

            File.WriteAllLines(sceneFilePath, lines);
            return true;
        }

        private static bool EnsureForegroundTextureWorldNodes(string sceneFilePath)
        {
            if (!File.Exists(sceneFilePath))
                return false;

            var changed = false;
            var lines = File.ReadAllLines(sceneFilePath).ToList();
            var hasForegroundLayer = lines.Any(line =>
                line.TrimStart().StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal));
            if (!hasForegroundLayer)
            {
                changed = EnsureBackgroundLayerNodes(sceneFilePath) || changed;
                lines = File.ReadAllLines(sceneFilePath).ToList();
            }

            changed = ConvertForegroundLayerToNode2D(lines) || changed;
            changed = ConvertForegroundTextureToSprite2D(lines) || changed;

            if (!changed)
                return false;

            File.WriteAllLines(sceneFilePath, lines);
            return true;
        }

        private static bool ConvertForegroundLayerToNode2D(List<string> lines)
        {
            var changed = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (!text.StartsWith("[node name=\"ForegroundTextureLayer\" ", StringComparison.Ordinal))
                    continue;

                if (ContainsOrdinal(text, "type=\"CanvasLayer\""))
                {
                    lines[i] = lines[i].Replace("type=\"CanvasLayer\"", "type=\"Node2D\"");
                    changed = true;
                }

                var index = i + 1;
                var hasZIndex = false;
                while (index < lines.Count && !lines[index].TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    var prop = lines[index].Trim();
                    if (prop.StartsWith("layer =", StringComparison.Ordinal))
                    {
                        lines.RemoveAt(index);
                        changed = true;
                        continue;
                    }
                    if (prop.StartsWith("z_index =", StringComparison.Ordinal))
                        hasZIndex = true;
                    index++;
                }

                if (!hasZIndex)
                {
                    lines.Insert(i + 1, "z_index = -1");
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ConvertForegroundTextureToSprite2D(List<string> lines)
        {
            var changed = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (!text.StartsWith("[node name=\"ForegroundTexture\" ", StringComparison.Ordinal) ||
                    !ContainsOrdinal(text, "parent=\"ForegroundTextureLayer\""))
                    continue;

                if (ContainsOrdinal(text, "type=\"TextureRect\""))
                {
                    lines[i] = lines[i].Replace("type=\"TextureRect\"", "type=\"Sprite2D\"");
                    changed = true;
                }

                var index = i + 1;
                var hasCentered = false;
                var hasPosition = false;
                while (index < lines.Count && !lines[index].TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    var prop = lines[index].Trim();
                    if (IsTextureRectLayoutProperty(prop))
                    {
                        lines.RemoveAt(index);
                        changed = true;
                        continue;
                    }

                    if (prop.StartsWith("centered", StringComparison.Ordinal))
                        hasCentered = true;
                    if (prop.StartsWith("position", StringComparison.Ordinal))
                        hasPosition = true;
                    index++;
                }

                var insertAt = i + 1;
                if (!hasCentered)
                {
                    lines.Insert(insertAt, "centered = false");
                    insertAt++;
                    changed = true;
                }
                if (!hasPosition)
                {
                    lines.Insert(insertAt, "position = Vector2(0, 0)");
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsTextureRectLayoutProperty(string prop)
        {
            return prop.StartsWith("anchors_preset", StringComparison.Ordinal) ||
                prop.StartsWith("anchor_left", StringComparison.Ordinal) ||
                prop.StartsWith("anchor_top", StringComparison.Ordinal) ||
                prop.StartsWith("anchor_right", StringComparison.Ordinal) ||
                prop.StartsWith("anchor_bottom", StringComparison.Ordinal) ||
                prop.StartsWith("offset_left", StringComparison.Ordinal) ||
                prop.StartsWith("offset_top", StringComparison.Ordinal) ||
                prop.StartsWith("offset_right", StringComparison.Ordinal) ||
                prop.StartsWith("offset_bottom", StringComparison.Ordinal) ||
                prop.StartsWith("grow_horizontal", StringComparison.Ordinal) ||
                prop.StartsWith("grow_vertical", StringComparison.Ordinal) ||
                prop.StartsWith("size", StringComparison.Ordinal) ||
                prop.StartsWith("expand_mode", StringComparison.Ordinal) ||
                prop.StartsWith("stretch_mode", StringComparison.Ordinal) ||
                prop.StartsWith("mouse_filter", StringComparison.Ordinal);
        }

        private static bool ContainsOrdinal(string value, string pattern)
        {
            return (value ?? string.Empty).IndexOf(pattern, StringComparison.Ordinal) >= 0;
        }

        private static int FindInsertIndexAfterMapNode(List<string> lines)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!string.Equals(lines[i].Trim(), "[node name=\"Map\" type=\"Node2D\"]", StringComparison.Ordinal))
                    continue;

                var insertAt = i + 1;
                while (insertAt < lines.Count && !lines[insertAt].TrimStart().StartsWith("[", StringComparison.Ordinal))
                    insertAt++;
                return insertAt;
            }

            return -1;
        }

        private static List<string> BuildTextureLayerBlock()
        {
            return new List<string>
            {
                string.Empty,
                "[node name=\"ForegroundTextureLayer\" type=\"Node2D\" parent=\".\"]",
                "z_index = -1",
                string.Empty,
                "[node name=\"ForegroundTexture\" type=\"Sprite2D\" parent=\"ForegroundTextureLayer\"]",
                "centered = false",
                "position = Vector2(0, 0)",
                string.Empty,
                "[node name=\"BackgroundLayer\" type=\"CanvasLayer\" parent=\".\"]",
                "layer = -100",
                string.Empty,
                "[node name=\"BackgroundTexture\" type=\"TextureRect\" parent=\"BackgroundLayer\"]",
                "anchors_preset = 15",
                "anchor_right = 1.0",
                "anchor_bottom = 1.0",
                "mouse_filter = 2",
                "expand_mode = 1",
                "stretch_mode = 6",
                string.Empty
            };
        }

        private static string EnsureExtResource(TscnScene scene, string type, string resPath, string idPrefix)
        {
            var existing = scene.ExtResources.FirstOrDefault(resource =>
                string.Equals((resource.Path ?? string.Empty).Trim(), resPath, StringComparison.Ordinal));
            if (existing != null && !string.IsNullOrWhiteSpace(existing.Id))
                return existing.Id;

            var baseId = BuildExtResourceId(resPath, idPrefix);
            var id = baseId;
            var index = 2;
            while (scene.ExtResources.Any(resource => string.Equals(resource.Id, id, StringComparison.Ordinal)))
            {
                id = baseId + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }

            scene.ExtResources.Add(new TscnExtResource
            {
                Type = type,
                Path = resPath,
                Id = id
            });
            return id;
        }

        private static string BuildExtResourceId(string resPath, string idPrefix)
        {
            var name = Path.GetFileNameWithoutExtension(resPath) ?? string.Empty;
            var builder = new StringBuilder();
            var previousUnderscore = false;
            foreach (var raw in name)
            {
                var ch = char.ToLowerInvariant(raw);
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            var core = builder.ToString().Trim('_');
            if (core.Length == 0)
                core = "res";
            return (idPrefix ?? string.Empty) + core;
        }

        private static bool IsTemplateRoomMap(TscnScene scene, TscnNode mapNode)
        {
            string scriptRaw;
            if (!mapNode.RawProps.TryGetValue("script", out scriptRaw))
                return false;

            var id = TryExtractExtResourceId(scriptRaw);
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var scriptPath = scene.FindExtResourcePathById(id) ?? string.Empty;
            return scriptPath.EndsWith("/TemplateRoomMap.gd", StringComparison.OrdinalIgnoreCase) ||
                scriptPath.EndsWith("\\TemplateRoomMap.gd", StringComparison.OrdinalIgnoreCase);
        }

        private static string TryExtractExtResourceId(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            const string token = "ExtResource(\"";
            var start = raw.IndexOf(token, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += token.Length;
            var end = raw.IndexOf("\")", start, StringComparison.Ordinal);
            return end < 0 ? string.Empty : raw.Substring(start, end - start);
        }

        private static TscnNode FindNode(TscnScene scene, string nodePath)
        {
            return scene.Nodes.FirstOrDefault(node =>
                string.Equals(ComputeNodePath(node.Parent, node.Name), nodePath, StringComparison.Ordinal));
        }

        private static string ComputeNodePath(string parent, string name)
        {
            parent = (parent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return name;
            return parent.Trim('/') + "/" + name;
        }

        private static string QuoteGodotString(string value)
        {
            value = value ?? string.Empty;
            value = value.Replace("\"", "\\\"");
            return "\"" + value + "\"";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
