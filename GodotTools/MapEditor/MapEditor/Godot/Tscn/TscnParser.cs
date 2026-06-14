using System.Text.RegularExpressions;

namespace MapEditor.Godot.Tscn;

public static partial class TscnParser
{
    public static TscnScene ParseFile(string filePath)
    {
        var scene = new TscnScene();
        var lines = File.ReadAllLines(filePath);

        TscnNode? currentNode = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentNode = null;

                if (line.StartsWith("[gd_scene", StringComparison.Ordinal))
                {
                    var attrs = ParseAttributes(line);
                    scene.SceneUid = attrs.GetValueOrDefault("uid", "");
                    continue;
                }

                if (line.StartsWith("[ext_resource", StringComparison.Ordinal))
                {
                    var attrs = ParseAttributes(line);
                    var r = new TscnExtResource
                    {
                        Type = attrs.GetValueOrDefault("type", ""),
                        Path = attrs.GetValueOrDefault("path", ""),
                        Id = attrs.GetValueOrDefault("id", "")
                    };
                    scene.ExtResources.Add(r);
                    continue;
                }

                if (line.StartsWith("[node", StringComparison.Ordinal))
                {
                    var attrs = ParseAttributes(line);
                    currentNode = new TscnNode
                    {
                        Name = attrs.GetValueOrDefault("name", ""),
                        Type = attrs.GetValueOrDefault("type", ""),
                        Parent = attrs.GetValueOrDefault("parent", "")
                    };

                    if (attrs.TryGetValue("instance", out var inst))
                    {
                        var m = ExtResourceValueRegex().Match(inst);
                        if (m.Success)
                            currentNode.InstanceExtResourceId = m.Groups["id"].Value;
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
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
                continue;
            currentNode.RawProps[key] = value;
        }

        return scene;
    }

    private static Dictionary<string, string> ParseAttributes(string sectionHeaderLine)
    {
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in AttributeRegex().Matches(sectionHeaderLine))
        {
            var key = m.Groups["k"].Value;
            var val = Unquote(m.Groups["v"].Value);
            attrs[key] = val;
        }
        return attrs;
    }

    private static string Unquote(string raw)
    {
        raw = raw.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw[1..^1];
        return raw;
    }

    [GeneratedRegex("(?<k>[A-Za-z0-9_]+)=(?<v>\\\"[^\\\"]*\\\"|[^\\s\\]]+)")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex("ExtResource\\(\\\"(?<id>[^\\\"]+)\\\"\\)")]
    private static partial Regex ExtResourceValueRegex();
}
