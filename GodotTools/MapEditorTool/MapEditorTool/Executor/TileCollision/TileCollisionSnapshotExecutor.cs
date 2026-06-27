using System;
using System.Collections.Generic;
using System.IO;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.TileCollision
{
    public sealed class TileCollisionSnapshotExecutor
    {
        public TileCollisionFileSnapshot CaptureFiles(string godotRoot, MapDefinition map, IEnumerable<string> tileSetResPaths)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            var snapshot = new TileCollisionFileSnapshot();
            snapshot.Files[ResolveGodotResourcePath(godotRoot, map.ScenePath)] = ReadTextFile(ResolveGodotResourcePath(godotRoot, map.ScenePath));

            if (tileSetResPaths != null)
            {
                foreach (var tileSetResPath in tileSetResPaths)
                {
                    if (string.IsNullOrWhiteSpace(tileSetResPath))
                        continue;

                    var path = ResolveGodotResourcePath(godotRoot, tileSetResPath);
                    if (!snapshot.Files.ContainsKey(path))
                        snapshot.Files[path] = ReadTextFile(path);
                }
            }

            return snapshot;
        }

        public void RestoreFiles(TileCollisionFileSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Files == null)
                return;

            foreach (var item in snapshot.Files)
            {
                var directory = Path.GetDirectoryName(item.Key);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(item.Key, item.Value ?? string.Empty);
            }
        }

        private static string ReadTextFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new FileNotFoundException("File path is empty.");
            if (!File.Exists(path))
                throw new FileNotFoundException("File not found.", path);

            return File.ReadAllText(path);
        }

        private static string ResolveGodotResourcePath(string godotRoot, string resourcePath)
        {
            resourcePath = (resourcePath ?? string.Empty).Trim();
            if (resourcePath.Length == 0)
                throw new ArgumentException("Resource path is empty.", "resourcePath");

            if (Path.IsPathRooted(resourcePath))
                return Path.GetFullPath(resourcePath);

            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new ArgumentException("Godot root is empty.", "godotRoot");

            var relative = resourcePath.StartsWith("res://", StringComparison.Ordinal)
                ? resourcePath.Substring("res://".Length)
                : resourcePath;
            relative = relative.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(godotRoot, relative));
        }
    }
}
