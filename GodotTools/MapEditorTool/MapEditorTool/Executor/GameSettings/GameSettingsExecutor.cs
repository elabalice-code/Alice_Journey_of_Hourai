using System;
using System.IO;
using System.Linq;
using MapEditorTool.Executor.MapImport.Tscn;
using MapEditorTool.Executor.ScenePatch;

namespace MapEditorTool.Executor.GameSettings
{
    public sealed class GameSettingsExecutor
    {
        public GameStartingMapResult ReadStartingMap(string godotRoot)
        {
            var gamePath = GetGameScenePath(godotRoot);
            if (!File.Exists(gamePath))
                return new GameStartingMapResult
                {
                    GameSceneFilePath = gamePath,
                    Summary = "CoreEngine/Game.tscn was not found."
                };

            var scene = TscnParser.ParseFile(gamePath);
            var gameNode = scene.Nodes.FirstOrDefault(node => string.Equals(node.Name, "Game", StringComparison.Ordinal));
            string raw;
            if (gameNode == null || !gameNode.RawProps.TryGetValue("starting_map", out raw))
                return new GameStartingMapResult
                {
                    GameSceneFilePath = gamePath,
                    Summary = "Game node or starting_map was not found."
                };

            var unquoted = UnquoteGodotValue(raw);
            var normalized = unquoted.StartsWith("uid://", StringComparison.OrdinalIgnoreCase)
                ? NormalizeResPath(ResolveUidToResPath(godotRoot, unquoted))
                : NormalizeResPath(unquoted);

            return new GameStartingMapResult
            {
                GameSceneFilePath = gamePath,
                RawStartingMap = unquoted,
                NormalizedStartingMap = normalized,
                Patched = false,
                Summary = "startingMap=" + normalized
            };
        }

        public GameStartingMapResult WriteStartingMap(string godotRoot, string scenePath)
        {
            var gamePath = GetGameScenePath(godotRoot);
            if (!File.Exists(gamePath))
                throw new FileNotFoundException("CoreEngine/Game.tscn was not found.", gamePath);

            var scene = TscnParser.ParseFile(gamePath);
            var gameNode = scene.Nodes.FirstOrDefault(node => string.Equals(node.Name, "Game", StringComparison.Ordinal));
            if (gameNode == null)
                throw new InvalidOperationException("Game node was not found in CoreEngine/Game.tscn.");

            var normalized = string.IsNullOrWhiteSpace(scenePath) ? string.Empty : NormalizeResPath(scenePath);
            gameNode.RawProps["starting_map"] = string.IsNullOrWhiteSpace(normalized) ? "\"\"" : QuoteGodotString(normalized);
            var patched = TscnWriter.PatchFile(gamePath, scene, new[] { "starting_map" });

            return new GameStartingMapResult
            {
                GameSceneFilePath = gamePath,
                RawStartingMap = normalized,
                NormalizedStartingMap = normalized,
                Patched = patched,
                Summary = "startingMap=" + normalized + "; patched=" + patched
            };
        }

        public bool IsStartingMap(string godotRoot, string scenePath)
        {
            var current = ReadStartingMap(godotRoot).NormalizedStartingMap;
            var candidate = NormalizeResPath(scenePath);
            return candidate.Length > 0 && string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveUidToResPath(string godotRoot, string uid)
        {
            if (string.IsNullOrWhiteSpace(godotRoot) || string.IsNullOrWhiteSpace(uid))
                return string.Empty;

            foreach (var file in Directory.EnumerateFiles(godotRoot, "*.tscn", SearchOption.AllDirectories))
            {
                if (ShouldSkipUidScanFile(file))
                    continue;

                try
                {
                    var scene = TscnParser.ParseFile(file);
                    if (!string.Equals(scene.SceneUid, uid, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return ToResPath(godotRoot, file);
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static bool ShouldSkipUidScanFile(string file)
        {
            return ContainsPathPart(file, ".godot")
                || ContainsPathPart(file, "GodotTools")
                || ContainsPathPart(file, "bin")
                || ContainsPathPart(file, "obj");
        }

        private static bool ContainsPathPart(string path, string part)
        {
            var marker = Path.DirectorySeparatorChar + part + Path.DirectorySeparatorChar;
            return path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToResPath(string godotRoot, string filePath)
        {
            var root = Path.GetFullPath(godotRoot);
            var file = Path.GetFullPath(filePath);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var rel = file.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
            return "res://" + rel;
        }

        private static string GetGameScenePath(string godotRoot)
        {
            if (string.IsNullOrWhiteSpace(godotRoot))
                throw new DirectoryNotFoundException("Godot root is empty.");

            return Path.Combine(godotRoot, "CoreEngine", "Game.tscn");
        }

        private static string NormalizeResPath(string value)
        {
            value = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (value.Length == 0)
                return string.Empty;
            if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return "res://" + value.Substring("res://".Length).TrimStart('/');
            return value;
        }

        private static string UnquoteGodotValue(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (raw.StartsWith("&\"", StringComparison.Ordinal) && raw.EndsWith("\"", StringComparison.Ordinal) && raw.Length >= 3)
                return raw.Substring(2, raw.Length - 3);
            if (raw.StartsWith("\"", StringComparison.Ordinal) && raw.EndsWith("\"", StringComparison.Ordinal) && raw.Length >= 2)
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static string QuoteGodotString(string value)
        {
            value = value ?? string.Empty;
            value = value.Replace("\"", "\\\"");
            return "\"" + value + "\"";
        }
    }
}
