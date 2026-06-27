using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapEditorTool.Executor.TileCollision
{
    public sealed class GodotTileSet
    {
        public GodotTileSet()
        {
            Sources = new Dictionary<int, GodotTileAtlasSource>();
        }

        public Dictionary<int, GodotTileAtlasSource> Sources { get; set; }
    }

    public sealed class GodotTileAtlasSource
    {
        public GodotTileAtlasSource()
        {
            SubResourceId = string.Empty;
            TextureResPath = string.Empty;
            PhysicsPolygons = new Dictionary<string, GodotTilePhysicsPolygon>(StringComparer.Ordinal);
        }

        public string SubResourceId { get; set; }
        public string TextureResPath { get; set; }
        public int RegionWidth { get; set; }
        public int RegionHeight { get; set; }
        public Dictionary<string, GodotTilePhysicsPolygon> PhysicsPolygons { get; set; }
    }

    public sealed class GodotTilePhysicsPolygon
    {
        public GodotTilePhysicsPolygon()
        {
            Points = new List<GodotVector2>();
        }

        public List<GodotVector2> Points { get; set; }
        public bool OneWay { get; set; }
    }

    public static class GodotTileSetLoader
    {
        private static readonly Regex ExtResourceRegex = new Regex("^\\[ext_resource\\s+[^\\]]*path=\"(?<path>[^\"]+)\"\\s+id=\"(?<id>[^\"]+)\"\\]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AtlasHeaderRegex = new Regex("^\\[sub_resource\\s+type=\"TileSetAtlasSource\"\\s+id=\"(?<id>[^\"]+)\"\\]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AtlasTextureRegex = new Regex("^texture\\s*=\\s*ExtResource\\(\"(?<id>[^\"]+)\"\\)\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AtlasRegionRegex = new Regex("^texture_region_size\\s*=\\s*Vector2i\\((?<w>-?\\d+)\\s*,\\s*(?<h>-?\\d+)\\)\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SourceMapRegex = new Regex("^sources\\/(?<source>\\d+)\\s*=\\s*SubResource\\(\"(?<sub>[^\"]+)\"\\)\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AtlasPhysicsPointsRegex = new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\/physics_layer_0\\/polygon_0\\/points\\s*=\\s*(?<arr>PackedVector2Array\\([^\\)]*\\))\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AtlasPhysicsOneWayRegex = new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\/physics_layer_0\\/polygon_0\\/one_way\\s*=\\s*(?<b>true|false)\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex AtlasAnyAltRegex = new Regex("^(?<x>-?\\d+):(?<y>-?\\d+)\\/(?<alt>\\d+)\\s*=", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static GodotTileSet Load(string tilesetAbsPath)
        {
            if (!File.Exists(tilesetAbsPath))
                throw new FileNotFoundException("TileSet .tres not found.", tilesetAbsPath);

            var lines = File.ReadAllLines(tilesetAbsPath);
            var extById = new Dictionary<string, string>(StringComparer.Ordinal);
            var atlasTextureExtBySubId = new Dictionary<string, string>(StringComparer.Ordinal);
            var atlasRegionBySubId = new Dictionary<string, TileAtlasRegion>(StringComparer.Ordinal);
            var atlasPhysicsBySubId = new Dictionary<string, Dictionary<string, PendingPhysicsPolygon>>(StringComparer.Ordinal);
            var sourceToSub = new Dictionary<int, string>();

            string currentAtlasSubId = null;
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                var extMatch = ExtResourceRegex.Match(line);
                if (extMatch.Success)
                {
                    extById[extMatch.Groups["id"].Value] = extMatch.Groups["path"].Value;
                    continue;
                }

                var atlasMatch = AtlasHeaderRegex.Match(line);
                if (atlasMatch.Success)
                {
                    currentAtlasSubId = atlasMatch.Groups["id"].Value;
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal))
                    currentAtlasSubId = null;

                if (currentAtlasSubId != null)
                    ReadAtlasLine(currentAtlasSubId, line, atlasTextureExtBySubId, atlasRegionBySubId, atlasPhysicsBySubId);

                var sourceMatch = SourceMapRegex.Match(line);
                if (sourceMatch.Success)
                {
                    int sourceId;
                    if (int.TryParse(sourceMatch.Groups["source"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceId))
                        sourceToSub[sourceId] = sourceMatch.Groups["sub"].Value;
                }
            }

            var tileSet = new GodotTileSet();
            foreach (var source in sourceToSub)
            {
                var subId = source.Value;
                string extId;
                string resPath;
                if (!atlasTextureExtBySubId.TryGetValue(subId, out extId) || !extById.TryGetValue(extId, out resPath))
                    continue;

                TileAtlasRegion region;
                if (!atlasRegionBySubId.TryGetValue(subId, out region))
                    region = new TileAtlasRegion(32, 32);

                var atlasSource = new GodotTileAtlasSource
                {
                    SubResourceId = subId,
                    TextureResPath = resPath,
                    RegionWidth = region.Width,
                    RegionHeight = region.Height
                };

                Dictionary<string, PendingPhysicsPolygon> pending;
                if (atlasPhysicsBySubId.TryGetValue(subId, out pending))
                {
                    foreach (var polygon in pending)
                    {
                        if (polygon.Value.Points.Count == 0)
                            continue;

                        atlasSource.PhysicsPolygons[polygon.Key] = new GodotTilePhysicsPolygon
                        {
                            Points = polygon.Value.Points,
                            OneWay = polygon.Value.OneWay
                        };
                    }
                }

                tileSet.Sources[source.Key] = atlasSource;
            }

            return tileSet;
        }

        public static int CreateAtlasPhysicsPolygonAlternative(string tilesetAbsPath, string atlasSubResourceId, int atlasX, int atlasY, bool oneWay, IList<GodotVector2> points)
        {
            if (!File.Exists(tilesetAbsPath))
                throw new FileNotFoundException("TileSet .tres not found.", tilesetAbsPath);

            var lines = File.ReadAllLines(tilesetAbsPath).ToList();
            int sectionStart;
            int sectionEnd;
            FindAtlasSection(lines, atlasSubResourceId, out sectionStart, out sectionEnd);

            var prefix = atlasX.ToString(CultureInfo.InvariantCulture) + ":" + atlasY.ToString(CultureInfo.InvariantCulture) + "/";
            var hasBase = false;
            var maxAlt = -1;
            var lastForTile = -1;

            for (var i = sectionStart + 1; i < sectionEnd; i++)
            {
                var line = lines[i].TrimStart();
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                lastForTile = i;
                var match = AtlasAnyAltRegex.Match(line);
                if (!match.Success)
                    continue;

                int alt;
                if (!int.TryParse(match.Groups["alt"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out alt))
                    continue;

                maxAlt = Math.Max(maxAlt, alt);
                if (alt == 0)
                    hasBase = true;
            }

            var insertAt = lastForTile >= 0 ? lastForTile + 1 : sectionEnd;
            if (!hasBase)
            {
                lines.Insert(insertAt, prefix + "0 = 0");
                insertAt++;
                maxAlt = Math.Max(maxAlt, 0);
            }

            var newAlt = Math.Max(1, maxAlt + 1);
            var altPrefix = atlasX.ToString(CultureInfo.InvariantCulture) + ":" +
                atlasY.ToString(CultureInfo.InvariantCulture) + "/" +
                newAlt.ToString(CultureInfo.InvariantCulture);

            lines.Insert(insertAt, altPrefix + " = 0");
            lines.Insert(insertAt + 1, altPrefix + "/physics_layer_0/polygon_0/points = " + FormatPackedVector2Array(points));
            if (oneWay)
                lines.Insert(insertAt + 2, altPrefix + "/physics_layer_0/polygon_0/one_way = true");

            File.WriteAllLines(tilesetAbsPath, lines);
            return newAlt;
        }

        private static void ReadAtlasLine(
            string currentAtlasSubId,
            string line,
            Dictionary<string, string> atlasTextureExtBySubId,
            Dictionary<string, TileAtlasRegion> atlasRegionBySubId,
            Dictionary<string, Dictionary<string, PendingPhysicsPolygon>> atlasPhysicsBySubId)
        {
            var textureMatch = AtlasTextureRegex.Match(line);
            if (textureMatch.Success)
            {
                atlasTextureExtBySubId[currentAtlasSubId] = textureMatch.Groups["id"].Value;
                return;
            }

            var regionMatch = AtlasRegionRegex.Match(line);
            if (regionMatch.Success)
            {
                atlasRegionBySubId[currentAtlasSubId] = new TileAtlasRegion(
                    int.Parse(regionMatch.Groups["w"].Value, CultureInfo.InvariantCulture),
                    int.Parse(regionMatch.Groups["h"].Value, CultureInfo.InvariantCulture));
                return;
            }

            var pointsMatch = AtlasPhysicsPointsRegex.Match(line);
            if (pointsMatch.Success)
            {
                var key = BuildPhysicsKey(pointsMatch.Groups["x"].Value, pointsMatch.Groups["y"].Value, pointsMatch.Groups["alt"].Value);
                var polygon = GetPendingPhysicsPolygon(atlasPhysicsBySubId, currentAtlasSubId, key);
                polygon.Points = ParsePackedVector2Array(pointsMatch.Groups["arr"].Value);
                return;
            }

            var oneWayMatch = AtlasPhysicsOneWayRegex.Match(line);
            if (oneWayMatch.Success)
            {
                var key = BuildPhysicsKey(oneWayMatch.Groups["x"].Value, oneWayMatch.Groups["y"].Value, oneWayMatch.Groups["alt"].Value);
                var polygon = GetPendingPhysicsPolygon(atlasPhysicsBySubId, currentAtlasSubId, key);
                polygon.OneWay = string.Equals(oneWayMatch.Groups["b"].Value, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static PendingPhysicsPolygon GetPendingPhysicsPolygon(Dictionary<string, Dictionary<string, PendingPhysicsPolygon>> bySubId, string subId, string key)
        {
            Dictionary<string, PendingPhysicsPolygon> polygons;
            if (!bySubId.TryGetValue(subId, out polygons))
            {
                polygons = new Dictionary<string, PendingPhysicsPolygon>(StringComparer.Ordinal);
                bySubId[subId] = polygons;
            }

            PendingPhysicsPolygon polygon;
            if (!polygons.TryGetValue(key, out polygon))
            {
                polygon = new PendingPhysicsPolygon();
                polygons[key] = polygon;
            }

            return polygon;
        }

        private static void FindAtlasSection(List<string> lines, string atlasSubResourceId, out int start, out int end)
        {
            var header = "[sub_resource type=\"TileSetAtlasSource\" id=\"" + atlasSubResourceId + "\"]";
            start = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].Trim(), header, StringComparison.Ordinal))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
                throw new InvalidOperationException("TileSetAtlasSource section not found: " + atlasSubResourceId);

            end = start + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
                end++;
        }

        private static string BuildPhysicsKey(string atlasX, string atlasY, string alternative)
        {
            return atlasX + ":" + atlasY + ":" + alternative;
        }

        private static List<GodotVector2> ParsePackedVector2Array(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (!raw.StartsWith("PackedVector2Array", StringComparison.Ordinal))
                return new List<GodotVector2>();

            var open = raw.IndexOf('(');
            var close = raw.LastIndexOf(')');
            if (open < 0 || close <= open)
                return new List<GodotVector2>();

            var parts = raw.Substring(open + 1, close - open - 1)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            var points = new List<GodotVector2>(parts.Length / 2);
            for (var i = 0; i + 1 < parts.Length; i += 2)
            {
                float x;
                float y;
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
                    return new List<GodotVector2>();
                if (!float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                    return new List<GodotVector2>();
                points.Add(new GodotVector2(x, y));
            }

            return points;
        }

        private static string FormatPackedVector2Array(IList<GodotVector2> points)
        {
            points = points ?? new List<GodotVector2>();
            var parts = new string[points.Count * 2];
            for (var i = 0; i < points.Count; i++)
            {
                parts[i * 2] = points[i].X.ToString("0.###", CultureInfo.InvariantCulture);
                parts[i * 2 + 1] = points[i].Y.ToString("0.###", CultureInfo.InvariantCulture);
            }

            return "PackedVector2Array(" + string.Join(", ", parts) + ")";
        }

        private struct TileAtlasRegion
        {
            public TileAtlasRegion(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; private set; }
            public int Height { get; private set; }
        }

        private sealed class PendingPhysicsPolygon
        {
            public PendingPhysicsPolygon()
            {
                Points = new List<GodotVector2>();
            }

            public List<GodotVector2> Points { get; set; }
            public bool OneWay { get; set; }
        }
    }
}
