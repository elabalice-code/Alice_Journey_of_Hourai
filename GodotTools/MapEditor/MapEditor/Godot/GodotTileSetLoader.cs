using System.Globalization;
using System.Text.RegularExpressions;

namespace MapEditor.Godot;

public sealed class GodotTileSet
{
    public required Dictionary<int, GodotTileAtlasSource> Sources { get; init; }
}

public readonly record struct GodotVector2(float X, float Y);

public sealed class GodotTileAtlasSource
{
    public required string SubResourceId { get; init; }
    public required string TextureResPath { get; init; }
    public required int RegionWidth { get; init; }
    public required int RegionHeight { get; init; }
    public required Dictionary<(int atlasX, int atlasY, int alternative), GodotTilePhysicsPolygon> PhysicsPolygons { get; init; }
}

public sealed class GodotTilePhysicsPolygon
{
    public required IReadOnlyList<GodotVector2> Points { get; init; }
    public bool OneWay { get; init; }
}

public static class GodotTileSetLoader
{
    public static GodotTileSet Load(string tilesetAbsPath)
    {
        if (!File.Exists(tilesetAbsPath))
            throw new FileNotFoundException("TileSet .tres not found.", tilesetAbsPath);

        var lines = File.ReadAllLines(tilesetAbsPath);

        var extById = new Dictionary<string, string>(StringComparer.Ordinal);
        var atlasTextureExtBySubId = new Dictionary<string, string>(StringComparer.Ordinal);
        var atlasRegionBySubId = new Dictionary<string, (int w, int h)>(StringComparer.Ordinal);
        var atlasPhysicsBySubId = new Dictionary<string, Dictionary<(int atlasX, int atlasY, int alt), (List<GodotVector2> pts, bool oneWay)>>(StringComparer.Ordinal);
        var sourceToSub = new Dictionary<int, string>();

        string? currentAtlasSubId = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var mExt = ExtResourceRegex().Match(line);
            if (mExt.Success)
            {
                var id = mExt.Groups["id"].Value;
                var path = mExt.Groups["path"].Value;
                if (id.Length > 0 && path.Length > 0)
                    extById[id] = path;
                continue;
            }

            var mAtlas = AtlasHeaderRegex().Match(line);
            if (mAtlas.Success)
            {
                currentAtlasSubId = mAtlas.Groups["id"].Value;
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal))
                currentAtlasSubId = null;

            if (currentAtlasSubId != null)
            {
                var mTex = AtlasTextureRegex().Match(line);
                if (mTex.Success)
                {
                    atlasTextureExtBySubId[currentAtlasSubId] = mTex.Groups["id"].Value;
                    continue;
                }

                var mRegion = AtlasRegionRegex().Match(line);
                if (mRegion.Success)
                {
                    var w = int.Parse(mRegion.Groups["w"].Value);
                    var h = int.Parse(mRegion.Groups["h"].Value);
                    atlasRegionBySubId[currentAtlasSubId] = (w, h);
                    continue;
                }

                var mPts = AtlasPhysicsPointsRegex().Match(line);
                if (mPts.Success)
                {
                    var ax = int.Parse(mPts.Groups["x"].Value, CultureInfo.InvariantCulture);
                    var ay = int.Parse(mPts.Groups["y"].Value, CultureInfo.InvariantCulture);
                    var alt = int.Parse(mPts.Groups["alt"].Value, CultureInfo.InvariantCulture);
                    var pts = ParsePackedVector2Array(mPts.Groups["arr"].Value);
                    if (!atlasPhysicsBySubId.TryGetValue(currentAtlasSubId, out var dict))
                    {
                        dict = new Dictionary<(int atlasX, int atlasY, int alt), (List<GodotVector2> pts, bool oneWay)>();
                        atlasPhysicsBySubId[currentAtlasSubId] = dict;
                    }
                    if (!dict.TryGetValue((ax, ay, alt), out var existing))
                        existing = ([], false);
                    existing.pts = pts;
                    dict[(ax, ay, alt)] = existing;
                    continue;
                }

                var mOneWay = AtlasPhysicsOneWayRegex().Match(line);
                if (mOneWay.Success)
                {
                    var ax = int.Parse(mOneWay.Groups["x"].Value, CultureInfo.InvariantCulture);
                    var ay = int.Parse(mOneWay.Groups["y"].Value, CultureInfo.InvariantCulture);
                    var alt = int.Parse(mOneWay.Groups["alt"].Value, CultureInfo.InvariantCulture);
                    var oneWay = string.Equals(mOneWay.Groups["b"].Value, "true", StringComparison.OrdinalIgnoreCase);
                    if (!atlasPhysicsBySubId.TryGetValue(currentAtlasSubId, out var dict))
                    {
                        dict = new Dictionary<(int atlasX, int atlasY, int alt), (List<GodotVector2> pts, bool oneWay)>();
                        atlasPhysicsBySubId[currentAtlasSubId] = dict;
                    }
                    if (!dict.TryGetValue((ax, ay, alt), out var existing))
                        existing = ([], false);
                    existing.oneWay = oneWay;
                    dict[(ax, ay, alt)] = existing;
                    continue;
                }
            }

            var mSource = SourceMapRegex().Match(line);
            if (mSource.Success)
            {
                var sourceId = int.Parse(mSource.Groups["source"].Value);
                var subId = mSource.Groups["sub"].Value;
                if (subId.Length > 0)
                    sourceToSub[sourceId] = subId;
            }
        }

        var sources = new Dictionary<int, GodotTileAtlasSource>();
        foreach (var kv in sourceToSub)
        {
            var sourceId = kv.Key;
            var subId = kv.Value;
            if (!atlasTextureExtBySubId.TryGetValue(subId, out var extId))
                continue;
            if (!extById.TryGetValue(extId, out var resPath))
                continue;
            if (!atlasRegionBySubId.TryGetValue(subId, out var region))
                region = (32, 32);
            var physics = new Dictionary<(int atlasX, int atlasY, int alternative), GodotTilePhysicsPolygon>();
            if (atlasPhysicsBySubId.TryGetValue(subId, out var p))
            {
                foreach (var pp in p)
                {
                    if (pp.Value.pts.Count == 0)
                        continue;
                    physics[(pp.Key.atlasX, pp.Key.atlasY, pp.Key.alt)] = new GodotTilePhysicsPolygon { Points = pp.Value.pts, OneWay = pp.Value.oneWay };
                }
            }
            sources[sourceId] = new GodotTileAtlasSource
            {
                SubResourceId = subId,
                TextureResPath = resPath,
                RegionWidth = region.w,
                RegionHeight = region.h,
                PhysicsPolygons = physics
            };
        }

        return new GodotTileSet { Sources = sources };
    }

    public static bool PatchAtlasPhysicsPolygonPoints(string tilesetAbsPath, string atlasSubResourceId, int atlasX, int atlasY, IReadOnlyList<GodotVector2> points)
    {
        if (!File.Exists(tilesetAbsPath))
            throw new FileNotFoundException("TileSet .tres not found.", tilesetAbsPath);

        var lines = File.ReadAllLines(tilesetAbsPath).ToList();
        var header = $"[sub_resource type=\"TileSetAtlasSource\" id=\"{atlasSubResourceId}\"]";

        var sectionStart = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.Ordinal))
            {
                sectionStart = i;
                break;
            }
        }
        if (sectionStart < 0)
            throw new InvalidOperationException($"TileSetAtlasSource section not found: {atlasSubResourceId}");

        var sectionEnd = sectionStart + 1;
        while (sectionEnd < lines.Count && !lines[sectionEnd].TrimStart().StartsWith("[", StringComparison.Ordinal))
            sectionEnd++;

        var key = $"{atlasX}:{atlasY}/0/physics_layer_0/polygon_0/points";
        var propRegex = new Regex("^\\s*" + Regex.Escape(key) + "\\s*=", RegexOptions.CultureInvariant);
        var newValue = $"{key} = {FormatPackedVector2Array(points)}";

        var dirty = false;
        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            if (!propRegex.IsMatch(lines[i]))
                continue;
            if (!string.Equals(lines[i], newValue, StringComparison.Ordinal))
            {
                lines[i] = newValue;
                dirty = true;
            }
            if (dirty)
                File.WriteAllLines(tilesetAbsPath, lines);
            return dirty;
        }

        var anchorKey = $"{atlasX}:{atlasY}/0";
        var anchorRegex = new Regex("^\\s*" + Regex.Escape(anchorKey) + "\\s*=", RegexOptions.CultureInvariant);
        var insertAt = sectionEnd;
        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            if (anchorRegex.IsMatch(lines[i]))
            {
                insertAt = i + 1;
                break;
            }
        }

        lines.Insert(insertAt, newValue);
        File.WriteAllLines(tilesetAbsPath, lines);
        return true;
    }

    public static int CreateAtlasPhysicsPolygonAlternative(string tilesetAbsPath, string atlasSubResourceId, int atlasX, int atlasY, bool oneWay, IReadOnlyList<GodotVector2> points)
    {
        if (!File.Exists(tilesetAbsPath))
            throw new FileNotFoundException("TileSet .tres not found.", tilesetAbsPath);

        var lines = File.ReadAllLines(tilesetAbsPath).ToList();
        var (sectionStart, sectionEnd) = FindAtlasSection(lines, atlasSubResourceId);

        var prefix = $"{atlasX}:{atlasY}/";
        var hasBase = false;
        var maxAlt = -1;
        var lastForTile = -1;

        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            var t = lines[i].TrimStart();
            if (!t.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            lastForTile = i;

            var m = AtlasAnyAltRegex().Match(t);
            if (!m.Success)
                continue;
            if (!int.TryParse(m.Groups["alt"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var alt))
                continue;
            maxAlt = Math.Max(maxAlt, alt);
            if (alt == 0)
                hasBase = true;
        }

        var insertAt = lastForTile >= 0 ? lastForTile + 1 : sectionEnd;
        if (!hasBase)
        {
            var baseLine = $"{atlasX}:{atlasY}/0 = 0";
            lines.Insert(insertAt, baseLine);
            insertAt++;
            maxAlt = Math.Max(maxAlt, 0);
        }

        var newAlt = Math.Max(1, maxAlt + 1);

        var altLine = $"{atlasX}:{atlasY}/{newAlt} = 0";
        var pointsKey = $"{atlasX}:{atlasY}/{newAlt}/physics_layer_0/polygon_0/points";
        var pointsLine = $"{pointsKey} = {FormatPackedVector2Array(points)}";

        lines.Insert(insertAt, altLine);
        lines.Insert(insertAt + 1, pointsLine);
        if (oneWay)
        {
            var oneWayKey = $"{atlasX}:{atlasY}/{newAlt}/physics_layer_0/polygon_0/one_way";
            lines.Insert(insertAt + 2, $"{oneWayKey} = true");
        }

        File.WriteAllLines(tilesetAbsPath, lines);
        return newAlt;
    }

    private static (int start, int end) FindAtlasSection(List<string> lines, string atlasSubResourceId)
    {
        var header = $"[sub_resource type=\"TileSetAtlasSource\" id=\"{atlasSubResourceId}\"]";
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), header, StringComparison.Ordinal))
            {
                start = i;
                break;
            }
        }
        if (start < 0)
            throw new InvalidOperationException($"TileSetAtlasSource section not found: {atlasSubResourceId}");

        var end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
            end++;
        return (start, end);
    }

    private static Regex ExtResourceRegex()
    {
        return new Regex("^\\[ext_resource\\s+[^\\]]*path=\"(?<path>[^\"]+)\"\\s+id=\"(?<id>[^\"]+)\"\\]$", RegexOptions.CultureInvariant);
    }

    private static Regex AtlasHeaderRegex()
    {
        return new Regex("^\\[sub_resource\\s+type=\"TileSetAtlasSource\"\\s+id=\"(?<id>[^\"]+)\"\\]$", RegexOptions.CultureInvariant);
    }

    private static Regex AtlasTextureRegex()
    {
        return new Regex("^texture\\s*=\\s*ExtResource\\(\"(?<id>[^\"]+)\"\\)\\s*$", RegexOptions.CultureInvariant);
    }

    private static Regex AtlasRegionRegex()
    {
        return new Regex("^texture_region_size\\s*=\\s*Vector2i\\((?<w>-?\\d+)\\s*,\\s*(?<h>-?\\d+)\\)\\s*$", RegexOptions.CultureInvariant);
    }

    private static Regex SourceMapRegex()
    {
        return new Regex("^sources\\/(?<source>\\d+)\\s*=\\s*SubResource\\(\"(?<sub>[^\"]+)\"\\)\\s*$", RegexOptions.CultureInvariant);
    }

    private static Regex AtlasPhysicsPointsRegex()
    {
        return new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\/physics_layer_0\\/polygon_0\\/points\\s*=\\s*(?<arr>PackedVector2Array\\([^\\)]*\\))\\s*$", RegexOptions.CultureInvariant);
    }

    private static Regex AtlasPhysicsOneWayRegex()
    {
        return new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\/physics_layer_0\\/polygon_0\\/one_way\\s*=\\s*(?<b>true|false)\\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static Regex AtlasAnyAltRegex()
    {
        return new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\s*=", RegexOptions.CultureInvariant);
    }

    private static List<GodotVector2> ParsePackedVector2Array(string raw)
    {
        raw = raw.Trim();
        if (!raw.StartsWith("PackedVector2Array", StringComparison.Ordinal))
            return [];
        var p1 = raw.IndexOf('(');
        var p2 = raw.LastIndexOf(')');
        if (p1 < 0 || p2 <= p1)
            return [];
        var content = raw[(p1 + 1)..p2];
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return [];
        var pts = new List<GodotVector2>(parts.Length / 2);
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                return [];
            if (!float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return [];
            pts.Add(new GodotVector2(x, y));
        }
        return pts;
    }

    private static string FormatPackedVector2Array(IReadOnlyList<GodotVector2> pts)
    {
        var parts = new string[pts.Count * 2];
        for (var i = 0; i < pts.Count; i++)
        {
            parts[i * 2 + 0] = pts[i].X.ToString("0.###", CultureInfo.InvariantCulture);
            parts[i * 2 + 1] = pts[i].Y.ToString("0.###", CultureInfo.InvariantCulture);
        }
        return $"PackedVector2Array({string.Join(", ", parts)})";
    }
}
