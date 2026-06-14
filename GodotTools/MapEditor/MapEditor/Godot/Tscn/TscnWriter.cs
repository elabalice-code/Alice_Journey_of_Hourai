using System.Text.RegularExpressions;

namespace MapEditor.Godot.Tscn;

public static partial class TscnWriter
{
    public static bool PatchFile(string filePath, TscnScene scene, IReadOnlyCollection<string> keysToPatch)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("TSCN file not found.", filePath);
        if (keysToPatch.Count == 0)
            return false;

        var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
        foreach (var n in scene.Nodes)
        {
            var p = ComputeNodePath(n.Parent, n.Name);
            if (!string.IsNullOrWhiteSpace(p))
                byPath[p] = n;
        }

        var lines = File.ReadAllLines(filePath).ToList();
        var dirty = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var header = lines[i].TrimStart();
            if (!header.StartsWith("[node", StringComparison.Ordinal))
                continue;

            var attrs = ParseAttributes(lines[i]);
            if (!attrs.TryGetValue("name", out var nodeName) || string.IsNullOrWhiteSpace(nodeName))
                continue;
            attrs.TryGetValue("parent", out var parent);
            var nodePath = ComputeNodePath(parent, nodeName);
            if (!byPath.TryGetValue(nodePath, out var node))
                continue;

            var start = i + 1;
            var end = start;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                end++;

            foreach (var key in keysToPatch)
            {
                if (!node.RawProps.TryGetValue(key, out var newRaw))
                    continue;

                var found = false;
                var propRegex = BuildPropRegex(key);
                for (var k = start; k < end; k++)
                {
                    var line = lines[k];
                    if (!propRegex.IsMatch(line))
                        continue;
                    var eq = line.IndexOf('=', StringComparison.Ordinal);
                    if (eq < 0)
                        continue;
                    var prefix = line[..eq].TrimEnd();
                    var replaced = $"{prefix} = {newRaw}";
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
                    lines.Insert(end, $"{key} = {newRaw}");
                    end++;
                    dirty = true;
                }
            }
        }

        if (dirty)
            File.WriteAllLines(filePath, lines);
        return dirty;
    }

    public static bool PatchFileWithExtResources(string filePath, TscnScene scene, IReadOnlyCollection<string> keysToPatch)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("TSCN file not found.", filePath);

        var lines = File.ReadAllLines(filePath).ToList();
        var dirty = false;

        if (scene.ExtResources.Count > 0)
        {
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                var t = lines[i].TrimStart();
                if (!t.StartsWith("[ext_resource", StringComparison.Ordinal))
                    continue;
                var attrs = ParseAttributes(lines[i]);
                if (attrs.TryGetValue("id", out var id) && id.Length > 0)
                    existingIds.Add(id);
            }

            var toInsert = new List<string>();
            foreach (var r in scene.ExtResources)
            {
                if (string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Path) || string.IsNullOrWhiteSpace(r.Type))
                    continue;
                if (existingIds.Contains(r.Id))
                    continue;
                toInsert.Add(BuildExtResourceLine(r));
                existingIds.Add(r.Id);
            }

            if (toInsert.Count > 0)
            {
                var insertAt = FindExtResourceInsertIndex(lines);
                lines.InsertRange(insertAt, toInsert);
                dirty = true;
            }
        }

        if (keysToPatch.Count == 0)
        {
            if (dirty)
                File.WriteAllLines(filePath, lines);
            return dirty;
        }

        var byPath = new Dictionary<string, TscnNode>(StringComparer.Ordinal);
        foreach (var n in scene.Nodes)
        {
            var p = ComputeNodePath(n.Parent, n.Name);
            if (!string.IsNullOrWhiteSpace(p))
                byPath[p] = n;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var header = lines[i].TrimStart();
            if (!header.StartsWith("[node", StringComparison.Ordinal))
                continue;

            var attrs = ParseAttributes(lines[i]);
            if (!attrs.TryGetValue("name", out var nodeName) || string.IsNullOrWhiteSpace(nodeName))
                continue;
            attrs.TryGetValue("parent", out var parent);
            var nodePath = ComputeNodePath(parent, nodeName);
            if (!byPath.TryGetValue(nodePath, out var node))
                continue;

            var start = i + 1;
            var end = start;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                end++;

            foreach (var key in keysToPatch)
            {
                if (!node.RawProps.TryGetValue(key, out var newRaw))
                    continue;

                var found = false;
                var propRegex = BuildPropRegex(key);
                for (var k = start; k < end; k++)
                {
                    var line = lines[k];
                    if (!propRegex.IsMatch(line))
                        continue;
                    var eq = line.IndexOf('=', StringComparison.Ordinal);
                    if (eq < 0)
                        continue;
                    var prefix = line[..eq].TrimEnd();
                    var replaced = $"{prefix} = {newRaw}";
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
                    lines.Insert(end, $"{key} = {newRaw}");
                    end++;
                    dirty = true;
                }
            }
        }

        if (dirty)
            File.WriteAllLines(filePath, lines);
        return dirty;
    }

    private static string ComputeNodePath(string? parent, string name)
    {
        parent = parent?.Trim();
        if (string.IsNullOrWhiteSpace(parent) || parent == ".")
            return name;
        return parent.Trim('/') + "/" + name;
    }

    private static Dictionary<string, string> ParseAttributes(string line)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in AttrRegex().Matches(line))
        {
            if (!m.Success)
                continue;
            var k = m.Groups["k"].Value;
            var v = m.Groups["v"].Value;
            if (k.Length > 0)
                dict[k] = v;
        }
        return dict;
    }

    [GeneratedRegex("\\b(?<k>\\w+)=\"(?<v>[^\"]*)\"")]
    private static partial Regex AttrRegex();

    private static Regex BuildPropRegex(string key)
    {
        return new Regex("^\\s*" + Regex.Escape(key) + "\\s*=", RegexOptions.CultureInvariant);
    }

    private static int FindExtResourceInsertIndex(List<string> lines)
    {
        var lastExt = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[ext_resource", StringComparison.Ordinal))
                lastExt = i;
        }
        if (lastExt >= 0)
            return lastExt + 1;

        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("[node", StringComparison.Ordinal) || t.StartsWith("[sub_resource", StringComparison.Ordinal))
                return i;
        }
        return lines.Count;
    }

    private static string BuildExtResourceLine(TscnExtResource r)
    {
        var type = r.Type.Trim();
        var path = r.Path.Trim().Replace("\\", "/");
        var id = r.Id.Trim();
        return $"[ext_resource type=\"{type}\" path=\"{path}\" id=\"{id}\"]";
    }
}
