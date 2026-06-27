using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Executor.PortalAnimation;
using MapEditorTool.Executor.ScenePatch;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.PortalEditing
{
    public sealed class PortalEditingExecutor
    {
        private readonly MapCreationExecutor _mapCreationExecutor;
        private readonly ScenePatchExecutor _scenePatchExecutor;
        private readonly PortalAnimationExecutor _portalAnimationExecutor;

        public PortalEditingExecutor()
            : this(new MapCreationExecutor(), new ScenePatchExecutor(), new PortalAnimationExecutor())
        {
        }

        public PortalEditingExecutor(
            MapCreationExecutor mapCreationExecutor,
            ScenePatchExecutor scenePatchExecutor,
            PortalAnimationExecutor portalAnimationExecutor)
        {
            _mapCreationExecutor = mapCreationExecutor ?? throw new ArgumentNullException("mapCreationExecutor");
            _scenePatchExecutor = scenePatchExecutor ?? throw new ArgumentNullException("scenePatchExecutor");
            _portalAnimationExecutor = portalAnimationExecutor ?? throw new ArgumentNullException("portalAnimationExecutor");
        }

        public PortalEditingResult CreatePortal(string godotRoot, MapProject project, MapDefinition map)
        {
            return CreatePortal(godotRoot, project, map, 0f, 0f);
        }

        public PortalEditingResult CreatePortal(string godotRoot, MapProject project, MapDefinition map, float x, float y)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            if (map == null)
                throw new ArgumentNullException("map");
            if (project == null)
                project = new MapProject();

            var sceneFilePath = EnsureMapScene(godotRoot, map);
            var uniqueName = MakeUniquePortalName(sceneFilePath, map);
            var portal = new Portal
            {
                Id = uniqueName,
                Name = uniqueName,
                NodePath = uniqueName,
                X = x,
                Y = y,
                TargetMapId = string.Empty,
                TargetPortalId = string.Empty
            };

            _scenePatchExecutor.EnsurePortalNodeExists(sceneFilePath, portal.NodePath, portal.X, portal.Y, null);

            return new PortalEditingResult
            {
                SceneFilePath = sceneFilePath,
                Portal = portal,
                Summary = "createdPortal=" + portal.NodePath + "; scene=" + sceneFilePath
            };
        }

        public PortalEditingResult ApplyPortalPropertyChange(string godotRoot, MapDefinition map, Portal portal, string propertyName)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            if (map == null)
                throw new ArgumentNullException("map");
            if (portal == null)
                throw new ArgumentNullException("portal");

            propertyName = propertyName ?? string.Empty;
            var sceneFilePath = EnsureMapScene(godotRoot, map);
            if (string.IsNullOrWhiteSpace(portal.NodePath))
                return new PortalEditingResult
                {
                    SceneFilePath = sceneFilePath,
                    Portal = portal,
                    Summary = "skippedPortalPatch=noNodePath"
                };

            if (string.Equals(propertyName, "X", StringComparison.Ordinal) ||
                string.Equals(propertyName, "Y", StringComparison.Ordinal) ||
                string.Equals(propertyName, "NodePath", StringComparison.Ordinal))
            {
                _scenePatchExecutor.PatchNodePosition(sceneFilePath, portal.NodePath, portal.X, portal.Y);
                return BuildResult(sceneFilePath, portal, "patchedPortalPosition=" + portal.NodePath);
            }

            if (string.Equals(propertyName, "TargetMapId", StringComparison.Ordinal) ||
                string.Equals(propertyName, "TargetPortalId", StringComparison.Ordinal))
            {
                _scenePatchExecutor.PatchPortalTarget(sceneFilePath, portal.NodePath, portal.TargetMapId, portal.TargetPortalId);
                return BuildResult(sceneFilePath, portal, "patchedPortalTarget=" + portal.NodePath);
            }

            if (string.Equals(propertyName, "AnimationVideoPath", StringComparison.Ordinal) ||
                string.Equals(propertyName, "AnimationFrameCount", StringComparison.Ordinal))
            {
                var import = _portalAnimationExecutor.ImportPortalVideoAndPatchScene(
                    godotRoot,
                    sceneFilePath,
                    map,
                    portal,
                    portal.AnimationVideoPath);
                return BuildResult(sceneFilePath, portal, "importedPortalAnimation=" + import.Summary);
            }

            if (string.Equals(propertyName, "AnimationFramesDir", StringComparison.Ordinal))
            {
                _scenePatchExecutor.PatchPortalAnimation(
                    sceneFilePath,
                    portal.NodePath,
                    portal.AnimationFramesDir,
                    PortalAnimationExecutor.ComputePortalAnimFps(portal),
                    Math.Max(0.001f, portal.Upscale));
                return BuildResult(sceneFilePath, portal, "patchedPortalAnimationDirectory=" + portal.NodePath);
            }

            if (string.Equals(propertyName, "AnimationFps", StringComparison.Ordinal) ||
                string.Equals(propertyName, "AnimationDurationSec", StringComparison.Ordinal) ||
                string.Equals(propertyName, "Upscale", StringComparison.Ordinal))
            {
                _portalAnimationExecutor.PatchPortalAnimationSettings(sceneFilePath, portal);
                return BuildResult(sceneFilePath, portal, "patchedPortalAnimationSettings=" + portal.NodePath);
            }

            if (string.Equals(propertyName, "KeyoutTolerance", StringComparison.Ordinal))
            {
                ReapplyKeyout(godotRoot, portal);
                return BuildResult(sceneFilePath, portal, "reappliedPortalKeyout=" + portal.NodePath);
            }

            return BuildResult(sceneFilePath, portal, "portalPropertyChanged=" + propertyName);
        }

        public List<PortalChoice> BuildTargetMapChoices(MapProject project)
        {
            var result = new List<PortalChoice>
            {
                new PortalChoice("(clear)", string.Empty)
            };

            if (project == null || project.Maps == null)
                return result;

            foreach (var map in project.Maps.OrderBy(m => (m.DisplayName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var value = NormalizeMapTargetValue(map);
                if (value.Length == 0)
                    continue;

                var label = (map.DisplayName ?? string.Empty).Trim();
                if (label.Length == 0)
                    label = Path.GetFileNameWithoutExtension(value);
                if (label.Length == 0)
                    label = value;

                result.Add(new PortalChoice(label, value));
            }

            return result;
        }

        public List<PortalChoice> BuildTargetAreaChoices(string godotRoot)
        {
            var result = new List<PortalChoice>
            {
                new PortalChoice("(clear)", string.Empty)
            };

            foreach (var id in ReadAreaIds(godotRoot).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                if (id.Length > 0)
                    result.Add(new PortalChoice(id, id));
            }

            return result;
        }

        public string FormatMapTargetLabel(MapProject project, string mapTarget)
        {
            mapTarget = (mapTarget ?? string.Empty).Trim();
            if (mapTarget.Length == 0)
                return string.Empty;

            var map = ResolveMapByAnyId(project, mapTarget);
            if (map == null)
                return mapTarget;

            var displayName = (map.DisplayName ?? string.Empty).Trim();
            if (displayName.Length > 0)
                return displayName;

            var value = NormalizeMapTargetValue(map);
            return Path.GetFileNameWithoutExtension(value);
        }

        private string EnsureMapScene(string godotRoot, MapDefinition map)
        {
            var result = _mapCreationExecutor.EnsureMapSceneExists(godotRoot, map);
            if (string.IsNullOrWhiteSpace(result.SceneFilePath) || !File.Exists(result.SceneFilePath))
                throw new FileNotFoundException("Map scene file was not created.", result.SceneFilePath);

            return result.SceneFilePath;
        }

        private void ReapplyKeyout(string godotRoot, Portal portal)
        {
            var framesDirectory = (portal.AnimationFramesDir ?? string.Empty).Trim();
            if (framesDirectory.Length == 0 || !framesDirectory.StartsWith("res://", StringComparison.Ordinal))
                return;

            var absoluteDirectory = ToAbsoluteGodotPath(godotRoot, framesDirectory);
            if (!Directory.Exists(absoluteDirectory))
                return;

            PortalAnimationExecutor.KeyOutBlackBackgroundInDir(absoluteDirectory, ClampByte(portal.KeyoutTolerance));
        }

        private string MakeUniquePortalName(string sceneFilePath, MapDefinition map)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(sceneFilePath))
            {
                var scene = TscnParser.ParseFile(sceneFilePath);
                foreach (var node in scene.Nodes)
                {
                    var name = (node.Name ?? string.Empty).Trim();
                    if (name.Length > 0)
                        names.Add(name);
                }
            }

            foreach (var portal in map.Portals ?? Enumerable.Empty<Portal>())
            {
                var nodePath = (portal.NodePath ?? string.Empty).Trim().Trim('/');
                if (nodePath.Length == 0)
                    continue;

                var parts = nodePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    names.Add(parts[parts.Length - 1]);
            }

            var index = 1;
            var candidate = "Portal";
            while (names.Contains(candidate))
            {
                index++;
                candidate = "Portal" + index.ToString(CultureInfo.InvariantCulture);
            }

            return candidate;
        }

        private static IEnumerable<string> ReadAreaIds(string godotRoot)
        {
            if (string.IsNullOrWhiteSpace(godotRoot) || !Directory.Exists(godotRoot))
                return Enumerable.Empty<string>();

            string filePath = null;
            try
            {
                filePath = Directory.EnumerateFiles(godotRoot, "AreaCatalog.gd", SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.EndsWith(Path.Combine("Scripts", "World", "AreaCatalog.gd"), StringComparison.OrdinalIgnoreCase))
                    ?? Directory.EnumerateFiles(godotRoot, "AreaCatalog.gd", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                filePath = null;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return Enumerable.Empty<string>();

            try
            {
                var text = File.ReadAllText(filePath);
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match match in Regex.Matches(text, "&\"(?<id>[^\"]+)\"\\s*:", RegexOptions.CultureInvariant))
                {
                    if (!match.Success)
                        continue;

                    var id = (match.Groups["id"].Value ?? string.Empty).Trim();
                    if (id.Length > 0)
                        set.Add(id);
                }

                return set;
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static MapDefinition ResolveMapByAnyId(MapProject project, string mapTarget)
        {
            mapTarget = (mapTarget ?? string.Empty).Trim();
            if (project == null || project.Maps == null || mapTarget.Length == 0)
                return null;

            return project.Maps.FirstOrDefault(map =>
                string.Equals((map.Id ?? string.Empty).Trim(), mapTarget, StringComparison.Ordinal) ||
                string.Equals((map.ScenePath ?? string.Empty).Trim(), mapTarget, StringComparison.Ordinal));
        }

        private static string NormalizeMapTargetValue(MapDefinition map)
        {
            if (map == null)
                return string.Empty;

            var scenePath = (map.ScenePath ?? string.Empty).Trim();
            if (scenePath.Length > 0)
                return scenePath;

            return (map.Id ?? string.Empty).Trim();
        }

        private static PortalEditingResult BuildResult(string sceneFilePath, Portal portal, string summary)
        {
            return new PortalEditingResult
            {
                SceneFilePath = sceneFilePath,
                Portal = portal,
                Summary = summary
            };
        }

        private static string ValidateGodotRoot(string godotRoot)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            return Path.GetFullPath(godotRoot);
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            var relative = resPath.StartsWith("res://", StringComparison.Ordinal)
                ? resPath.Substring("res://".Length)
                : resPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, relative);
        }

        private static byte ClampByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return (byte)value;
        }
    }

    public sealed class PortalEditingResult
    {
        public PortalEditingResult()
        {
            SceneFilePath = string.Empty;
            Summary = string.Empty;
        }

        public string SceneFilePath { get; set; }
        public Portal Portal { get; set; }
        public string Summary { get; set; }
    }

    public sealed class PortalChoice
    {
        public PortalChoice(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; private set; }
        public string Value { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
