using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MapEditorTool.Executor.MapImport.Tscn;

namespace MapEditorTool.Executor.ScenePatch
{
    public static class TscnWriter
    {
        private static readonly Regex AttrRegex = new Regex("\\b(?<k>\\w+)=\"(?<v>[^\"]*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool PatchFile(string filePath, TscnScene scene, IReadOnlyCollection<string> keysToPatch)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TSCN file not found.", filePath);
            if (keysToPatch == null || keysToPatch.Count == 0)
                return false;

            return PatchFileCore(filePath, scene, keysToPatch, false);
        }

        public static bool PatchFileWithExtResources(string filePath, TscnScene scene, IReadOnlyCollection<string> keysToPatch)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TSCN file not found.", filePath);

            return PatchFileCore(filePath, scene, keysToPatch ?? new string[0], true);
        }

        private static bool PatchFileCore(string filePath, TscnScene scene, IReadOnlyCollection<string> keysToPatch, bool patchExtResources)
        {
            var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
            foreach (var node in scene.Nodes)
            {
                var path = ComputeNodePath(node.Parent, node.Name);
                if (!string.IsNullOrWhiteSpace(path))
                    byPath[path] = node;
            }

            var lines = File.ReadAllLines(filePath).ToList();
            var dirty = false;

            if (patchExtResources)
                dirty = InsertMissingExtResources(lines, scene) || dirty;

            for (var i = 0; i < lines.Count; i++)
            {
                var header = lines[i].TrimStart();
                if (!header.StartsWith("[node", StringComparison.Ordinal))
                    continue;

                var attrs = ParseAttributes(lines[i]);
                string nodeName;
                if (!attrs.TryGetValue("name", out nodeName) || string.IsNullOrWhiteSpace(nodeName))
                    continue;

                string parent;
                attrs.TryGetValue("parent", out parent);
                var nodePath = ComputeNodePath(parent, nodeName);
                TscnNode node;
                if (!byPath.TryGetValue(nodePath, out node))
                    continue;

                var start = i + 1;
                var end = start;
                while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                    end++;

                foreach (var key in keysToPatch)
                {
                    string newRaw;
                    if (!node.RawProps.TryGetValue(key, out newRaw))
                        continue;

                    var found = false;
                    var propRegex = BuildPropRegex(key);
                    for (var k = start; k < end; k++)
                    {
                        var line = lines[k];
                        if (!propRegex.IsMatch(line))
                            continue;

                        var eq = line.IndexOf('=');
                        if (eq < 0)
                            continue;

                        var prefix = line.Substring(0, eq).TrimEnd();
                        var replaced = prefix + " = " + newRaw;
                        if (!string.Equals(lines[k], replaced, StringComparison.Ordinal))
                        {
                            lines[k] = replaced;
                            dirty = true;
                        }

                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        lines.Insert(end, key + " = " + newRaw);
                        end++;
                        dirty = true;
                    }
                }
            }

            if (dirty)
                File.WriteAllLines(filePath, lines);

            return dirty;
        }

        private static bool InsertMissingExtResources(List<string> lines, TscnScene scene)
        {
            if (scene == null || scene.ExtResources.Count == 0)
                return false;

            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].TrimStart();
                if (!text.StartsWith("[ext_resource", StringComparison.Ordinal))
                    continue;

                var attrs = ParseAttributes(lines[i]);
                string id;
                if (attrs.TryGetValue("id", out id) && id.Length > 0)
                    existingIds.Add(id);
            }

            var toInsert = new List<string>();
            foreach (var resource in scene.ExtResources)
            {
                if (string.IsNullOrWhiteSpace(resource.Id) ||
                    string.IsNullOrWhiteSpace(resource.Path) ||
                    string.IsNullOrWhiteSpace(resource.Type) ||
                    existingIds.Contains(resource.Id))
                    continue;

                toInsert.Add(BuildExtResourceLine(resource));
                existingIds.Add(resource.Id);
            }

            if (toInsert.Count == 0)
                return false;

            lines.InsertRange(FindExtResourceInsertIndex(lines), toInsert);
            return true;
        }

        private static string ComputeNodePath(string parent, string name)
        {
            parent = (parent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return name;

            return parent.Trim('/') + "/" + name;
        }

        private static Dictionary<string, string> ParseAttributes(string line)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in AttrRegex.Matches(line))
            {
                if (!match.Success)
                    continue;

                var key = match.Groups["k"].Value;
                var value = match.Groups["v"].Value;
                if (key.Length > 0)
                    dict[key] = value;
            }

            return dict;
        }

        private static Regex BuildPropRegex(string key)
        {
            return new Regex("^\\s*" + Regex.Escape(key) + "\\s*=", RegexOptions.CultureInvariant);
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

        private static string BuildExtResourceLine(TscnExtResource resource)
        {
            return "[ext_resource type=\"" + resource.Type.Trim() +
                "\" path=\"" + resource.Path.Trim().Replace("\\", "/") +
                "\" id=\"" + resource.Id.Trim() + "\"]";
        }
    }
}
