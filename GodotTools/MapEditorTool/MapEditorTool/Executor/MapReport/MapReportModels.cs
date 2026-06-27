using System.Collections.Generic;

namespace MapEditorTool.Executor.MapReport
{
    public sealed class MapEditorStatus
    {
        public MapEditorStatus()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            SampleScenes = new List<string>();
            MissingScenes = new List<string>();
            MapsWithoutPortals = new List<string>();
            LinksWithMissingTargets = new List<string>();
        }

        public string ProjectRoot { get; set; }
        public bool ProjectFileExists { get; set; }
        public string GeneratedAtUtc { get; set; }
        public int MapCount { get; set; }
        public int LinkCount { get; set; }
        public int PortalCount { get; set; }
        public int TileLayerCount { get; set; }
        public int EntityCount { get; set; }
        public int MissingSceneCount { get; set; }
        public int MapsWithoutPortalsCount { get; set; }
        public int LinksWithMissingTargetsCount { get; set; }
        public List<string> SampleScenes { get; set; }
        public List<string> MissingScenes { get; set; }
        public List<string> MapsWithoutPortals { get; set; }
        public List<string> LinksWithMissingTargets { get; set; }
    }

    public sealed class MapPortalReview
    {
        public MapPortalReview()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            PortalCoverageClassifications = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            MapsWithoutPortals = new List<MapPortalReviewItem>();
            PortalsWithMissingTargets = new List<MapPortalReviewItem>();
        }

        public string ProjectRoot { get; set; }
        public bool ProjectFileExists { get; set; }
        public string GeneratedAtUtc { get; set; }
        public int MapCount { get; set; }
        public int PortalCount { get; set; }
        public int LinkCount { get; set; }
        public int MapsWithoutPortalsCount { get; set; }
        public int PortalsWithMissingTargetsCount { get; set; }
        public Dictionary<string, int> PortalCoverageClassifications { get; set; }
        public List<MapPortalReviewItem> MapsWithoutPortals { get; set; }
        public List<MapPortalReviewItem> PortalsWithMissingTargets { get; set; }
    }

    public sealed class MapPortalReviewItem
    {
        public MapPortalReviewItem()
        {
            Id = string.Empty;
            ScenePath = string.Empty;
            DisplayName = string.Empty;
            TargetMapId = string.Empty;
            TargetPortalId = string.Empty;
            CoverageClassification = string.Empty;
            ClassificationConfidence = string.Empty;
            ClassificationReason = string.Empty;
            Recommendation = string.Empty;
        }

        public string Id { get; set; }
        public string ScenePath { get; set; }
        public string DisplayName { get; set; }
        public int PortalCount { get; set; }
        public int IncomingLinkCount { get; set; }
        public int OutgoingLinkCount { get; set; }
        public string TargetMapId { get; set; }
        public string TargetPortalId { get; set; }
        public string CoverageClassification { get; set; }
        public string ClassificationConfidence { get; set; }
        public string ClassificationReason { get; set; }
        public string Recommendation { get; set; }
    }

    public sealed class MapValidationReport
    {
        public MapValidationReport()
        {
            ProjectRoot = string.Empty;
            InputPath = string.Empty;
            GeneratedAtUtc = string.Empty;
            MissingInGodot = new List<string>();
            ExtraInGodot = new List<string>();
        }

        public string ProjectRoot { get; set; }
        public string InputPath { get; set; }
        public string GeneratedAtUtc { get; set; }
        public int LoadedMapCount { get; set; }
        public int ScannedMapCount { get; set; }
        public int MissingInGodotCount { get; set; }
        public int ExtraInGodotCount { get; set; }
        public bool Ok { get; set; }
        public List<string> MissingInGodot { get; set; }
        public List<string> ExtraInGodot { get; set; }
    }
}
