using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MapEditorTool.Executor.MapImport.Tscn;

namespace MapEditorTool.Executor.ScenePatch
{
    public sealed class ScenePatchExecutor
    {
        public ScenePatchResult PatchNodePosition(string sceneFilePath, string nodePath, float x, float y)
        {
            var scene = TscnParser.ParseFile(ValidateScenePatchInput(sceneFilePath, nodePath));
            var node = FindNode(scene, nodePath);

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
            var node = FindNode(scene, nodePath);

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
            var node = FindNode(scene, nodePath);

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
            var node = FindNode(scene, nodePath);

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

        private static string ComputeNodePath(string parent, string name)
        {
            parent = (parent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return name;

            return parent.Trim('/') + "/" + name;
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
