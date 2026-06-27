using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapDeletion
{
    public sealed class MapDeletionExecutor
    {
        public MapDeletionResult DeleteMapResources(string godotRoot, MapDefinition map, bool deleteResourceDirectory)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            var result = new MapDeletionResult();
            var rootFullPath = EnsureDirectoryRoot(godotRoot);

            var scenePath = (map.ScenePath ?? string.Empty).Trim();
            if (scenePath.StartsWith("res://", StringComparison.Ordinal))
            {
                var sceneAbsPath = ToAbsoluteGodotPath(rootFullPath, scenePath);
                if (TryDeleteFileWithinRoot(rootFullPath, sceneAbsPath, result))
                    result.DeletedSceneFile = true;
            }

            var resourceFilePaths = new[]
            {
                map.TileCollisionDataPath,
                map.ForegroundTextureCollisionDataPath
            }
                .Where(path => !string.IsNullOrWhiteSpace(path) && path.Trim().StartsWith("res://", StringComparison.Ordinal))
                .Select(path => ToAbsoluteGodotPath(rootFullPath, path.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var filePath in resourceFilePaths)
                TryDeleteFileWithinRoot(rootFullPath, filePath, result);

            if (deleteResourceDirectory)
            {
                foreach (var directory in resourceFilePaths
                    .Select(Path.GetDirectoryName)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    TryDeleteDirectoryWithinRoot(rootFullPath, directory, result);
                }
            }

            result.DeletedFileCount = result.DeletedFiles.Count;
            result.DeletedDirectoryCount = result.DeletedDirectories.Count;
            result.Summary = "deletedFiles=" + result.DeletedFileCount +
                "; deletedDirectories=" + result.DeletedDirectoryCount +
                "; skippedPaths=" + result.SkippedPaths.Count;
            return result;
        }

        private static bool TryDeleteFileWithinRoot(string rootFullPath, string filePath, MapDeletionResult result)
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!IsPathInsideRoot(rootFullPath, fullPath))
            {
                result.SkippedPaths.Add(fullPath);
                return false;
            }

            if (!File.Exists(fullPath))
                return false;

            File.Delete(fullPath);
            result.DeletedFiles.Add(fullPath);
            return true;
        }

        private static bool TryDeleteDirectoryWithinRoot(string rootFullPath, string directoryPath, MapDeletionResult result)
        {
            var fullPath = Path.GetFullPath(directoryPath);
            if (!IsPathInsideRoot(rootFullPath, fullPath))
            {
                result.SkippedPaths.Add(fullPath);
                return false;
            }

            if (string.Equals(NormalizeRoot(rootFullPath), NormalizeRoot(fullPath), StringComparison.OrdinalIgnoreCase))
            {
                result.SkippedPaths.Add(fullPath);
                return false;
            }

            if (!Directory.Exists(fullPath))
                return false;

            Directory.Delete(fullPath, true);
            result.DeletedDirectories.Add(fullPath);
            return true;
        }

        private static string ToAbsoluteGodotPath(string rootFullPath, string resPath)
        {
            var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath.Substring("res://".Length) : resPath;
            rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(rootFullPath, rel);
        }

        private static string EnsureDirectoryRoot(string godotRoot)
        {
            var fullPath = Path.GetFullPath(godotRoot);
            Directory.CreateDirectory(fullPath);
            return NormalizeRoot(fullPath);
        }

        private static bool IsPathInsideRoot(string rootFullPath, string pathFullPath)
        {
            var normalizedRoot = NormalizeRoot(rootFullPath);
            var normalizedPath = Path.GetFullPath(pathFullPath);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRoot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullPath += Path.DirectorySeparatorChar;
            return fullPath;
        }
    }
}
