using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using MapEditorTool.Executor.MapCreation;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.CollisionLayout
{
    public sealed class CollisionLayoutExecutor
    {
        public CollisionLayoutFileResult LoadLayout(string godotRoot, MapDefinition map, CollisionLayoutTarget target, bool ensureDefaultPath)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var pathResult = GetCollisionDataResPath(map, target, ensureDefaultPath);
            var resPath = pathResult.ResPath;
            if (string.IsNullOrWhiteSpace(resPath))
            {
                return new CollisionLayoutFileResult
                {
                    Layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight),
                    Summary = "collisionPath=empty; loadedDefaultLayout=true"
                };
            }

            var filePath = ToAbsoluteGodotPath(godotRoot, resPath);
            CollisionLayoutData layout = null;
            var fileExists = File.Exists(filePath);
            if (fileExists)
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(CollisionLayoutData));
                        layout = serializer.ReadObject(stream) as CollisionLayoutData;
                    }
                }
                catch
                {
                    layout = null;
                }
            }

            if (layout == null)
                layout = CollisionLayoutData.Create(map.RoomWidth, map.RoomHeight);

            var normalizeResult = NormalizeLayoutInternal(layout, map.RoomWidth, map.RoomHeight);
            return new CollisionLayoutFileResult
            {
                CollisionResPath = resPath,
                CollisionFilePath = Path.GetFullPath(filePath),
                Layout = normalizeResult.Layout,
                FileExists = fileExists,
                CreatedDefaultPath = pathResult.CreatedDefaultPath,
                ResizedLayout = normalizeResult.ResizedLayout,
                FixedSolidBuffer = normalizeResult.FixedSolidBuffer,
                Summary = "collisionPath=" + resPath +
                    "; fileExists=" + fileExists +
                    "; resizedLayout=" + normalizeResult.ResizedLayout +
                    "; fixedSolidBuffer=" + normalizeResult.FixedSolidBuffer
            };
        }

        public CollisionLayoutFileResult SaveLayout(string godotRoot, MapDefinition map, CollisionLayoutTarget target, CollisionLayoutData layout)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (layout == null)
                throw new ArgumentNullException("layout");

            var pathResult = GetCollisionDataResPath(map, target, true);
            if (string.IsNullOrWhiteSpace(pathResult.ResPath))
                throw new InvalidOperationException("Collision layout path could not be resolved.");

            var normalizeResult = NormalizeLayoutInternal(layout, map.RoomWidth, map.RoomHeight);
            var filePath = ToAbsoluteGodotPath(godotRoot, pathResult.ResPath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var serializer = new DataContractJsonSerializer(typeof(CollisionLayoutData));
                serializer.WriteObject(stream, normalizeResult.Layout);
            }

            return new CollisionLayoutFileResult
            {
                CollisionResPath = pathResult.ResPath,
                CollisionFilePath = Path.GetFullPath(filePath),
                Layout = normalizeResult.Layout,
                FileExists = true,
                CreatedDefaultPath = pathResult.CreatedDefaultPath,
                ResizedLayout = normalizeResult.ResizedLayout,
                FixedSolidBuffer = normalizeResult.FixedSolidBuffer,
                WroteFile = true,
                Summary = "collisionPath=" + pathResult.ResPath +
                    "; wroteFile=true; resizedLayout=" + normalizeResult.ResizedLayout +
                    "; fixedSolidBuffer=" + normalizeResult.FixedSolidBuffer
            };
        }

        public string ResolveCollisionDataResPath(MapDefinition map, CollisionLayoutTarget target, bool ensureDefaultPath)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            return GetCollisionDataResPath(map, target, ensureDefaultPath).ResPath;
        }

        public CollisionLayoutData NormalizeLayout(CollisionLayoutData layout, int roomWidth, int roomHeight)
        {
            return NormalizeLayoutInternal(layout, roomWidth, roomHeight).Layout;
        }

        private static PathResult GetCollisionDataResPath(MapDefinition map, CollisionLayoutTarget target, bool ensureDefaultPath)
        {
            var resPath = target == CollisionLayoutTarget.ForegroundTexture
                ? map.ForegroundTextureCollisionDataPath
                : map.TileCollisionDataPath;
            resPath = (resPath ?? string.Empty).Trim();
            if (resPath.Length > 0 && !resPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                resPath = string.Empty;
            if (resPath.Length > 0)
                return new PathResult(resPath, false);
            if (!ensureDefaultPath)
                return new PathResult(string.Empty, false);

            var defaultPath = BuildDefaultCollisionDataResPath(map, target);
            if (defaultPath.Length == 0)
                return new PathResult(string.Empty, false);

            if (target == CollisionLayoutTarget.ForegroundTexture)
                map.ForegroundTextureCollisionDataPath = defaultPath;
            else
                map.TileCollisionDataPath = defaultPath;

            return new PathResult(defaultPath, true);
        }

        private static string BuildDefaultCollisionDataResPath(MapDefinition map, CollisionLayoutTarget target)
        {
            var scene = (map.ScenePath ?? string.Empty).Trim();
            if (!scene.StartsWith("res://", StringComparison.Ordinal))
                return string.Empty;

            var relative = scene.Substring("res://".Length).TrimStart('/').Replace('\\', '/');
            var sceneDirectory = Path.GetDirectoryName(relative);
            sceneDirectory = (sceneDirectory ?? string.Empty).Replace('\\', '/');
            var baseName = Path.GetFileNameWithoutExtension(relative);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = string.IsNullOrWhiteSpace(map.DisplayName) ? "Map" : map.DisplayName;

            var safeName = SanitizeFolderName(baseName);
            if (safeName.Length == 0)
                safeName = "Map";

            var fileName = target == CollisionLayoutTarget.ForegroundTexture ? "collision_fgtex.json" : "collision_tile.json";
            var resRelative = (sceneDirectory.Length > 0 ? sceneDirectory.TrimEnd('/') + "/" : string.Empty) +
                "Resources/" + safeName + "/" + fileName;
            return "res://" + resRelative;
        }

        private static NormalizeResult NormalizeLayoutInternal(CollisionLayoutData layout, int roomWidth, int roomHeight)
        {
            if (layout == null)
                layout = CollisionLayoutData.Create(roomWidth, roomHeight);

            var expectedWidth = Math.Max(1, roomWidth);
            var expectedHeight = Math.Max(1, roomHeight);
            var resized = false;
            var fixedSolid = false;

            if (layout.RoomWidth != expectedWidth || layout.RoomHeight != expectedHeight)
            {
                var target = CollisionLayoutData.Create(expectedWidth, expectedHeight);
                var copyWidth = Math.Min(Math.Max(1, layout.RoomWidth), target.RoomWidth);
                var copyHeight = Math.Min(Math.Max(1, layout.RoomHeight), target.RoomHeight);
                var sourceSolid = layout.Solid ?? new bool[0];
                for (var y = 0; y < copyHeight; y++)
                {
                    for (var x = 0; x < copyWidth; x++)
                    {
                        var fromIndex = y * Math.Max(1, layout.RoomWidth) + x;
                        var toIndex = y * target.RoomWidth + x;
                        if (fromIndex >= 0 && fromIndex < sourceSolid.Length && toIndex >= 0 && toIndex < target.Solid.Length)
                            target.Solid[toIndex] = sourceSolid[fromIndex];
                    }
                }

                target.Polygons = layout.Polygons ?? target.Polygons;
                layout = target;
                resized = true;
            }

            if (layout.Polygons == null)
                layout.Polygons = new System.Collections.Generic.List<System.Collections.Generic.List<GodotVector2Data>>();

            var expectedSolidLength = Math.Max(1, layout.RoomWidth) * Math.Max(1, layout.RoomHeight);
            if (layout.Solid == null || layout.Solid.Length != expectedSolidLength)
            {
                var fixedLayout = CollisionLayoutData.Create(layout.RoomWidth, layout.RoomHeight);
                var copy = Math.Min(layout.Solid == null ? 0 : layout.Solid.Length, fixedLayout.Solid.Length);
                if (copy > 0)
                    Array.Copy(layout.Solid, fixedLayout.Solid, copy);
                fixedLayout.Polygons = layout.Polygons;
                layout = fixedLayout;
                fixedSolid = true;
            }

            return new NormalizeResult(layout, resized, fixedSolid);
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");
            if (string.IsNullOrWhiteSpace(resPath))
                throw new FileNotFoundException("Godot resource path is empty.");

            var relative = resPath.StartsWith("res://", StringComparison.Ordinal)
                ? resPath.Substring("res://".Length)
                : resPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, relative);
        }

        private static string SanitizeFolderName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch).ToArray();
            return new string(chars).Trim(' ', '.');
        }

        private struct PathResult
        {
            public PathResult(string resPath, bool createdDefaultPath)
            {
                ResPath = resPath ?? string.Empty;
                CreatedDefaultPath = createdDefaultPath;
            }

            public string ResPath { get; private set; }
            public bool CreatedDefaultPath { get; private set; }
        }

        private struct NormalizeResult
        {
            public NormalizeResult(CollisionLayoutData layout, bool resizedLayout, bool fixedSolidBuffer)
            {
                Layout = layout;
                ResizedLayout = resizedLayout;
                FixedSolidBuffer = fixedSolidBuffer;
            }

            public CollisionLayoutData Layout { get; private set; }
            public bool ResizedLayout { get; private set; }
            public bool FixedSolidBuffer { get; private set; }
        }
    }
}
