using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEditorTool.Executor.MapImport;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.MapReport
{
    public sealed class MapReportExecutor
    {
        private readonly MapImportExecutor _importExecutor;

        public MapReportExecutor()
            : this(new MapImportExecutor())
        {
        }

        public MapReportExecutor(MapImportExecutor importExecutor)
        {
            _importExecutor = importExecutor;
        }

        public MapEditorStatus BuildStatus(string godotRoot)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            var project = _importExecutor.ImportFromGodotRoot(godotRoot);
            var mapScenes = GetMapScenes(project);
            var missingScenePaths = mapScenes
                .Select(x => x.ScenePath)
                .Where(x => !File.Exists(ToAbsoluteGodotPath(godotRoot, x)))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var mapsWithoutPortals = mapScenes
                .Where(x => x.Portals.Count == 0)
                .Select(x => x.ScenePath)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var linksWithMissingTargets = project.Links
                .Where(link =>
                    project.Maps.All(map => map.Id != link.From.MapId) ||
                    project.Maps.All(map => map.Id != link.To.MapId))
                .Select(link => link.From.MapId + "->" + link.To.MapId)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new MapEditorStatus
            {
                ProjectRoot = godotRoot,
                ProjectFileExists = File.Exists(Path.Combine(godotRoot, "project.godot")),
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                MapCount = mapScenes.Count,
                LinkCount = project.Links.Count,
                PortalCount = project.Maps.Sum(x => x.Portals.Count),
                TileLayerCount = project.Maps.Sum(x => x.TileLayers.Count),
                EntityCount = project.Maps.Sum(x => x.Entities.Count),
                MissingSceneCount = missingScenePaths.Count,
                MapsWithoutPortalsCount = mapsWithoutPortals.Count,
                LinksWithMissingTargetsCount = linksWithMissingTargets.Count,
                SampleScenes = mapScenes.Take(10).Select(x => x.ScenePath).ToList(),
                MissingScenes = missingScenePaths.Take(20).ToList(),
                MapsWithoutPortals = mapsWithoutPortals.Take(20).ToList(),
                LinksWithMissingTargets = linksWithMissingTargets.Take(20).ToList()
            };
        }

        public MapPortalReview BuildPortalReview(string godotRoot)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            var project = _importExecutor.ImportFromGodotRoot(godotRoot);
            var mapScenes = GetMapScenes(project);
            var linkedPortalIds = new HashSet<string>(
                project.Links.Select(link => link.From.MapId + "\n" + link.From.PortalId),
                StringComparer.OrdinalIgnoreCase);

            var mapsWithoutPortals = mapScenes
                .Where(x => x.Portals.Count == 0)
                .Select(x =>
                {
                    var incoming = project.Links.Count(link => string.Equals(link.To.MapId, x.Id, StringComparison.OrdinalIgnoreCase));
                    var outgoing = project.Links.Count(link => string.Equals(link.From.MapId, x.Id, StringComparison.OrdinalIgnoreCase));
                    var sceneText = TryReadText(ToAbsoluteGodotPath(godotRoot, x.ScenePath));
                    var classification = ClassifyMapWithoutPortals(x.ScenePath, x.DisplayName, incoming, outgoing, sceneText);
                    return new MapPortalReviewItem
                    {
                        Id = x.Id,
                        ScenePath = x.ScenePath,
                        DisplayName = x.DisplayName,
                        PortalCount = x.Portals.Count,
                        IncomingLinkCount = incoming,
                        OutgoingLinkCount = outgoing,
                        CoverageClassification = classification.Category,
                        ClassificationConfidence = classification.Confidence,
                        ClassificationReason = classification.Reason,
                        Recommendation = classification.Recommendation
                    };
                })
                .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var portalsWithMissingTargets = project.Maps
                .SelectMany(map => map.Portals.Select(portal => new MapPortalPair(map, portal)))
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Portal.TargetMapId) &&
                    !linkedPortalIds.Contains(x.Map.Id + "\n" + x.Portal.Id))
                .Select(x => new MapPortalReviewItem
                {
                    Id = x.Map.Id,
                    ScenePath = x.Map.ScenePath,
                    DisplayName = string.IsNullOrWhiteSpace(x.Portal.Name) ? x.Portal.Id : x.Portal.Name,
                    PortalCount = x.Map.Portals.Count,
                    IncomingLinkCount = project.Links.Count(link => string.Equals(link.To.MapId, x.Map.Id, StringComparison.OrdinalIgnoreCase)),
                    OutgoingLinkCount = project.Links.Count(link => string.Equals(link.From.MapId, x.Map.Id, StringComparison.OrdinalIgnoreCase)),
                    TargetMapId = x.Portal.TargetMapId,
                    TargetPortalId = x.Portal.TargetPortalId,
                    Recommendation = "Review this portal target; the target map was not found in the current imported map graph."
                })
                .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new MapPortalReview
            {
                ProjectRoot = godotRoot,
                ProjectFileExists = File.Exists(Path.Combine(godotRoot, "project.godot")),
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                MapCount = mapScenes.Count,
                PortalCount = project.Maps.Sum(x => x.Portals.Count),
                LinkCount = project.Links.Count,
                MapsWithoutPortalsCount = mapsWithoutPortals.Count,
                PortalsWithMissingTargetsCount = portalsWithMissingTargets.Count,
                PortalCoverageClassifications = mapsWithoutPortals
                    .GroupBy(x => x.CoverageClassification)
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase),
                MapsWithoutPortals = mapsWithoutPortals.Take(50).ToList(),
                PortalsWithMissingTargets = portalsWithMissingTargets.Take(50).ToList()
            };
        }

        public MapValidationReport ValidateProjectAgainstGodot(string godotRoot, string inputPath, MapProject loadedProject)
        {
            if (loadedProject == null)
                throw new ArgumentNullException("loadedProject");

            godotRoot = Path.GetFullPath(godotRoot);
            var scanned = _importExecutor.ImportFromGodotRoot(godotRoot);
            return BuildValidationReport(godotRoot, inputPath ?? string.Empty, loadedProject, scanned);
        }

        public string FormatStatusSummary(MapEditorStatus status)
        {
            var lines = new List<string>
            {
                "MapEditorTool status",
                "Project: " + status.ProjectRoot,
                "Generated UTC: " + status.GeneratedAtUtc,
                "Project file: " + (status.ProjectFileExists ? "ok" : "missing"),
                "Counts: " +
                    "maps=" + status.MapCount + " " +
                    "links=" + status.LinkCount + " " +
                    "portals=" + status.PortalCount + " " +
                    "tileLayers=" + status.TileLayerCount + " " +
                    "entities=" + status.EntityCount,
                "Issues: " +
                    "missingScenes=" + status.MissingSceneCount + " " +
                    "missingTargets=" + status.LinksWithMissingTargetsCount + " " +
                    "mapsWithoutPortals=" + status.MapsWithoutPortalsCount
            };

            AddSummaryList(lines, "Missing scenes", status.MissingScenes);
            AddSummaryList(lines, "Links with missing targets", status.LinksWithMissingTargets);
            AddSummaryList(lines, "Maps without portals", status.MapsWithoutPortals);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public string FormatPortalReviewSummary(MapPortalReview review)
        {
            var lines = new List<string>
            {
                "MapEditorTool portal review",
                "Project: " + review.ProjectRoot,
                "Generated UTC: " + review.GeneratedAtUtc,
                "Project file: " + (review.ProjectFileExists ? "ok" : "missing"),
                "Counts: maps=" + review.MapCount + " portals=" + review.PortalCount + " links=" + review.LinkCount,
                "Review: mapsWithoutPortals=" + review.MapsWithoutPortalsCount + " portalsWithMissingTargets=" + review.PortalsWithMissingTargetsCount,
                "Classification: " + FormatCounts(review.PortalCoverageClassifications),
                "Maps without portals:"
            };

            if (review.MapsWithoutPortals.Count == 0)
                lines.Add("  none");
            foreach (var item in review.MapsWithoutPortals)
            {
                lines.Add("  " + item.ScenePath + " incoming=" + item.IncomingLinkCount + " outgoing=" + item.OutgoingLinkCount);
                lines.Add("    class: " + item.CoverageClassification + " confidence=" + item.ClassificationConfidence);
                lines.Add("    reason: " + item.ClassificationReason);
                lines.Add("    recommendation: " + item.Recommendation);
            }

            lines.Add("Portals with missing targets:");
            if (review.PortalsWithMissingTargets.Count == 0)
                lines.Add("  none");
            foreach (var item in review.PortalsWithMissingTargets)
            {
                lines.Add("  " + item.ScenePath + " -> " + item.TargetMapId);
                lines.Add("    recommendation: " + item.Recommendation);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public string FormatValidationSummary(MapValidationReport report)
        {
            var lines = new List<string>
            {
                "MapEditorTool validate",
                "Project: " + report.ProjectRoot,
                "Generated UTC: " + report.GeneratedAtUtc,
                "Input: " + report.InputPath,
                "Overall: " + (report.Ok ? "OK" : "FAILED") + " missing=" + report.MissingInGodotCount + " extra=" + report.ExtraInGodotCount,
                "Counts: loadedMaps=" + report.LoadedMapCount + " scannedMaps=" + report.ScannedMapCount
            };

            AddSummaryList(lines, "Missing in Godot", report.MissingInGodot);
            AddSummaryList(lines, "Extra in Godot", report.ExtraInGodot);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static MapValidationReport BuildValidationReport(
            string godotRoot,
            string inputPath,
            MapProject loaded,
            MapProject scanned)
        {
            var missing = loaded.Maps
                .Select(m => m.ScenePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Except(scanned.Maps.Select(m => m.ScenePath), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var extra = scanned.Maps
                .Select(m => m.ScenePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Except(loaded.Maps.Select(m => m.ScenePath), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new MapValidationReport
            {
                ProjectRoot = Path.GetFullPath(godotRoot),
                InputPath = inputPath,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                LoadedMapCount = loaded.Maps.Count,
                ScannedMapCount = scanned.Maps.Count,
                MissingInGodotCount = missing.Count,
                ExtraInGodotCount = extra.Count,
                MissingInGodot = missing.Take(50).ToList(),
                ExtraInGodot = extra.Take(50).ToList(),
                Ok = missing.Count == 0 && extra.Count == 0
            };
        }

        private static List<MapDefinition> GetMapScenes(MapProject project)
        {
            return project.Maps
                .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath) && x.ScenePath.StartsWith("res://", StringComparison.Ordinal))
                .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static PortalClassification ClassifyMapWithoutPortals(
            string scenePath,
            string displayName,
            int incomingLinkCount,
            int outgoingLinkCount,
            string sceneText)
        {
            var name = Path.GetFileNameWithoutExtension(scenePath);
            var key = (string.IsNullOrWhiteSpace(displayName) ? name : displayName).ToLowerInvariant();
            var text = sceneText ?? string.Empty;

            if (incomingLinkCount > 0 || outgoingLinkCount > 0)
            {
                return new PortalClassification(
                    "map-graph-gap",
                    "high",
                    "Scene participates in the imported link graph but has no imported Portal nodes.",
                    "Add or repair the missing Portal nodes before accepting this map graph.");
            }

            if (key.Contains("ending") || key.EndsWith("end") || text.Contains("END"))
            {
                return new PortalClassification(
                    "terminal-candidate",
                    "medium",
                    "Name or scene content indicates an ending/terminal room while the link graph is isolated.",
                    "Human review should confirm this room is intentionally terminal.");
            }

            if (key.Contains("loop") || text.Contains("LoopScript.gd"))
            {
                return new PortalClassification(
                    "dynamic-scripted-candidate",
                    "medium",
                    "Scene appears to use scripted loop transitions that may not be represented as static Portal nodes.",
                    "Verify the scripted transition behavior before adding static portals.");
            }

            if (key.Contains("test") || key.Contains("demo") || key.Contains("template") || key.Contains("sample"))
            {
                return new PortalClassification(
                    "test-helper-candidate",
                    "medium",
                    "Name suggests a test, demo, template, or helper scene.",
                    "Confirm this scene is not part of the shipping traversal graph.");
            }

            if (text.Contains("SavePoint.gd") || key.Contains("save"))
            {
                return new PortalClassification(
                    "utility-room-candidate",
                    "low",
                    "Scene contains utility/save-point logic but no imported Portal nodes.",
                    "Check whether the room is reached dynamically or should receive portal links.");
            }

            if (key.Contains("corridor") ||
                key.Contains("junction") ||
                key.Contains("staircase") ||
                key.Contains("relic") ||
                text.Contains("TileMapLayer"))
            {
                return new PortalClassification(
                    "playable-isolated-candidate",
                    "low",
                    "Scene looks like a playable room but has no static portals and no imported links.",
                    "Human review should decide whether this is intentional, dynamically connected, or a map-graph gap.");
            }

            return new PortalClassification(
                "unclassified-isolated-map",
                "low",
                "Scene has no imported Portal nodes or links, and no stronger heuristic matched.",
                "Human review should classify this scene before mutating the map graph.");
        }

        private static void AddSummaryList(List<string> lines, string title, IList<string> values)
        {
            lines.Add(title + ":");
            if (values.Count == 0)
            {
                lines.Add("  none");
                return;
            }

            foreach (var value in values)
                lines.Add("  " + value);
        }

        private static string FormatCounts(IReadOnlyDictionary<string, int> counts)
        {
            if (counts.Count == 0)
                return "none";

            return string.Join(" ", counts
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Key + "=" + x.Value)
                .ToArray());
        }

        private static string TryReadText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
        {
            var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath.Substring("res://".Length) : resPath;
            rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(godotRoot, rel);
        }

        private sealed class PortalClassification
        {
            public PortalClassification(string category, string confidence, string reason, string recommendation)
            {
                Category = category;
                Confidence = confidence;
                Reason = reason;
                Recommendation = recommendation;
            }

            public string Category { get; private set; }
            public string Confidence { get; private set; }
            public string Reason { get; private set; }
            public string Recommendation { get; private set; }
        }

        private sealed class MapPortalPair
        {
            public MapPortalPair(MapDefinition map, Portal portal)
            {
                Map = map;
                Portal = portal;
            }

            public MapDefinition Map { get; private set; }
            public Portal Portal { get; private set; }
        }
    }
}
