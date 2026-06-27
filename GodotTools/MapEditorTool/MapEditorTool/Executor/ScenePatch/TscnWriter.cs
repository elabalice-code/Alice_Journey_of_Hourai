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

            var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
            foreach (var node in scene.Nodes)
            {
                var path = ComputeNodePath(node.Parent, node.Name);
                if (!string.IsNullOrWhiteSpace(path))
                    byPath[path] = node;
            }

            var lines = File.ReadAllLines(filePath).ToList();
            var dirty = false;

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
    }
}
