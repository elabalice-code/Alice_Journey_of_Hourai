using System;
using System.IO;
using System.Linq;
using System.Text;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.ResourcePath
{
    public sealed class ResourcePathExecutor
    {
        public ResourcePathImportResult ConvertToProjectResourcePath(
            string godotRoot,
            string chosenPath,
            string fallbackDirectory,
            MapDefinition selectedMap)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            chosenPath = (chosenPath ?? string.Empty).Trim();
            if (chosenPath.Length == 0)
                return new ResourcePathImportResult();

            if (!Path.IsPathRooted(chosenPath))
            {
                return new ResourcePathImportResult
                {
                    SourcePath = chosenPath,
                    ResultPath = chosenPath,
                    Imported = false
                };
            }

            var fullChosenPath = Path.GetFullPath(chosenPath);
            if (IsUnderRoot(godotRoot, fullChosenPath))
            {
                return new ResourcePathImportResult
                {
                    SourcePath = fullChosenPath,
                    ResultPath = TryMakeResPath(godotRoot, fullChosenPath),
                    Imported = false
                };
            }

            var destinationBaseDirectory = EnsurePreferredProjectResourceDirectory(godotRoot, fallbackDirectory, selectedMap);
            Directory.CreateDirectory(destinationBaseDirectory);

            string importedPath;
            if (File.Exists(fullChosenPath))
                importedPath = ImportFileToDirectory(fullChosenPath, destinationBaseDirectory);
            else if (Directory.Exists(fullChosenPath))
                importedPath = ImportDirectoryToDirectory(fullChosenPath, destinationBaseDirectory);
            else
                importedPath = fullChosenPath;

            return new ResourcePathImportResult
            {
                SourcePath = fullChosenPath,
                ImportedPath = importedPath,
                ResultPath = TryMakeResPath(godotRoot, importedPath),
                Imported = !string.Equals(fullChosenPath, importedPath, StringComparison.OrdinalIgnoreCase)
            };
        }

        public string TryResolveToExistingPath(string godotRoot, string value)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
                return string.Empty;

            if (value.StartsWith("res://", StringComparison.Ordinal))
            {
                var absolute = ToAbsoluteGodotPath(godotRoot, value);
                if (File.Exists(absolute) || Directory.Exists(absolute))
                    return absolute;
                return string.Empty;
            }

            if (Path.IsPathRooted(value) && (File.Exists(value) || Directory.Exists(value)))
                return Path.GetFullPath(value);

            return string.Empty;
        }

        public string ResolveInitialDirectory(string godotRoot, string currentAbsolutePath)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            currentAbsolutePath = (currentAbsolutePath ?? string.Empty).Trim();
            if (currentAbsolutePath.Length > 0)
            {
                if (Directory.Exists(currentAbsolutePath))
                    return currentAbsolutePath;

                var directory = Path.GetDirectoryName(currentAbsolutePath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }

            return Directory.Exists(godotRoot) ? godotRoot : Environment.CurrentDirectory;
        }

        public string EnsurePreferredProjectResourceDirectory(string godotRoot, string fallbackDirectory, MapDefinition selectedMap)
        {
            godotRoot = ValidateGodotRoot(godotRoot);
            fallbackDirectory = string.IsNullOrWhiteSpace(fallbackDirectory) ? godotRoot : fallbackDirectory;

            if (selectedMap == null || string.IsNullOrWhiteSpace(selectedMap.ScenePath))
                return fallbackDirectory;

            if (!selectedMap.ScenePath.StartsWith("res://", StringComparison.Ordinal))
                return fallbackDirectory;

            var relativeScenePath = selectedMap.ScenePath.Substring("res://".Length).TrimStart('/').Replace('\\', '/');
            var sceneBaseName = Path.GetFileNameWithoutExtension(relativeScenePath);
            if (sceneBaseName.Length == 0)
                sceneBaseName = string.IsNullOrWhiteSpace(selectedMap.DisplayName) ? "Map" : selectedMap.DisplayName;

            var safeName = SanitizeFolderName(sceneBaseName);
            if (safeName.Length == 0)
                safeName = "Map";

            string preferredAbsolutePath;
            var coreEngineMapsIndex = relativeScenePath.IndexOf("/CoreEngine/Maps/", StringComparison.OrdinalIgnoreCase);
            if (coreEngineMapsIndex >= 0)
            {
                var prefix = relativeScenePath.Substring(0, coreEngineMapsIndex + 1);
                var resourceRelativePath = prefix + "CoreEngine/Resources/Maps/" + safeName;
                preferredAbsolutePath = Path.Combine(godotRoot, resourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            else
            {
                var absoluteScenePath = Path.Combine(godotRoot, relativeScenePath.Replace('/', Path.DirectorySeparatorChar));
                var sceneDirectory = Path.GetDirectoryName(absoluteScenePath);
                preferredAbsolutePath = string.IsNullOrWhiteSpace(sceneDirectory)
                    ? fallbackDirectory
                    : Path.Combine(sceneDirectory, "Resources", safeName);
            }

            preferredAbsolutePath = Path.GetFullPath(preferredAbsolutePath);
            if (!IsUnderRoot(godotRoot, preferredAbsolutePath))
                return fallbackDirectory;

            Directory.CreateDirectory(preferredAbsolutePath);
            return preferredAbsolutePath;
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

        private static string TryMakeResPath(string godotRoot, string absolutePath)
        {
            if (!Path.IsPathRooted(absolutePath))
                return absolutePath;

            var rootFull = EnsureTrailingSeparator(Path.GetFullPath(godotRoot));
            var pathFull = Path.GetFullPath(absolutePath);
            if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return pathFull;

            var relative = pathFull.Substring(rootFull.Length)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return "res://" + relative.TrimStart('/');
        }

        private static bool IsUnderRoot(string root, string path)
        {
            var rootFull = EnsureTrailingSeparator(Path.GetFullPath(root));
            var pathFull = Path.GetFullPath(path);
            if (File.Exists(pathFull))
                pathFull = Path.GetDirectoryName(pathFull) ?? pathFull;
            pathFull = EnsureTrailingSeparator(pathFull);
            return pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;
            return path + Path.DirectorySeparatorChar;
        }

        private static string ImportFileToDirectory(string sourcePath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            var fileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "ImportedFile";

            var destinationPath = GetUniquePath(Path.Combine(destinationDirectory, fileName));
            File.Copy(sourcePath, destinationPath, false);
            return destinationPath;
        }

        private static string ImportDirectoryToDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            var directoryName = new DirectoryInfo(sourceDirectory).Name;
            if (string.IsNullOrWhiteSpace(directoryName))
                directoryName = "ImportedFolder";

            var destinationPath = GetUniquePath(Path.Combine(destinationDirectory, directoryName));
            CopyDirectory(sourceDirectory, destinationPath);
            return destinationPath;
        }

        private static string GetUniquePath(string desiredPath)
        {
            if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
                return desiredPath;

            var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(desiredPath);
            var extension = Path.GetExtension(desiredPath);
            if (name.Length == 0)
                name = "Imported";

            for (var index = 2; index < 10000; index++)
            {
                var candidate = Path.Combine(directory, name + "_" + index + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    return candidate;
            }

            return Path.Combine(directory, name + "_" + Guid.NewGuid().ToString("N") + extension);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destinationDirectory, name), true);
            }

            foreach (var childDirectory in Directory.GetDirectories(sourceDirectory))
            {
                var name = new DirectoryInfo(childDirectory).Name;
                CopyDirectory(childDirectory, Path.Combine(destinationDirectory, name));
            }
        }

        private static string SanitizeFolderName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (invalid.Contains(ch) || ch == '/' || ch == '\\' || char.IsControl(ch))
                    builder.Append('_');
                else
                    builder.Append(ch);
            }

            return builder.ToString().Trim(' ', '.');
        }
    }

    public sealed class ResourcePathImportResult
    {
        public ResourcePathImportResult()
        {
            SourcePath = string.Empty;
            ImportedPath = string.Empty;
            ResultPath = string.Empty;
        }

        public string SourcePath { get; set; }
        public string ImportedPath { get; set; }
        public string ResultPath { get; set; }
        public bool Imported { get; set; }
    }
}
