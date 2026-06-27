using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MapEditorTool.Executor.MapImport.Tscn
{
    public static class TscnParser
    {
        private static readonly Regex AttributeRegex = new Regex("(?<k>[A-Za-z0-9_]+)=(?<v>\\\"[^\\\"]*\\\"|[^\\s\\]]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExtResourceValueRegex = new Regex("ExtResource\\(\\\"(?<id>[^\\\"]+)\\\"\\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static TscnScene ParseFile(string filePath)
        {
            var scene = new TscnScene();
            var lines = File.ReadAllLines(filePath);

            TscnNode currentNode = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    currentNode = null;

                    if (line.StartsWith("[gd_scene", StringComparison.Ordinal))
                    {
                        var attrs = ParseAttributes(line);
                        scene.SceneUid = GetValueOrDefault(attrs, "uid");
                        continue;
                    }

                    if (line.StartsWith("[ext_resource", StringComparison.Ordinal))
                    {
                        var attrs = ParseAttributes(line);
                        scene.ExtResources.Add(new TscnExtResource
                        {
                            Type = GetValueOrDefault(attrs, "type"),
                            Path = GetValueOrDefault(attrs, "path"),
                            Id = GetValueOrDefault(attrs, "id")
                        });
                        continue;
                    }

                    if (line.StartsWith("[node", StringComparison.Ordinal))
                    {
                        var attrs = ParseAttributes(line);
                        currentNode = new TscnNode
                        {
                            Name = GetValueOrDefault(attrs, "name"),
                            Type = GetValueOrDefault(attrs, "type"),
                            Parent = GetValueOrDefault(attrs, "parent")
                        };

                        string instance;
                        if (attrs.TryGetValue("instance", out instance))
                        {
                            var match = ExtResourceValueRegex.Match(instance);
                            if (match.Success)
                                currentNode.InstanceExtResourceId = match.Groups["id"].Value;
                        }

                        scene.Nodes.Add(currentNode);
                    }

                    continue;
                }

                if (currentNode == null)
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                if (key.Length > 0)
                    currentNode.RawProps[key] = value;
            }

            return scene;
        }

        private static Dictionary<string, string> ParseAttributes(string sectionHeaderLine)
        {
            var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in AttributeRegex.Matches(sectionHeaderLine))
            {
                attrs[match.Groups["k"].Value] = Unquote(match.Groups["v"].Value);
            }

            return attrs;
        }

        private static string GetValueOrDefault(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static string Unquote(string raw)
        {
            raw = raw.Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return raw.Substring(1, raw.Length - 2);

            return raw;
        }
    }
}
