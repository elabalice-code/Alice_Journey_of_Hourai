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

    public sealed class MapUxAuditReport
    {
        public MapUxAuditReport()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            AuditKind = string.Empty;
            Scope = string.Empty;
            Checks = new List<MapUxAuditCheck>();
            Recommendations = new List<string>();
        }

        public string ProjectRoot { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string AuditKind { get; set; }
        public string Scope { get; set; }
        public int CheckCount { get; set; }
        public int PassedCount { get; set; }
        public int WarningCount { get; set; }
        public int BlockingIssueCount { get; set; }
        public bool Ok { get; set; }
        public List<MapUxAuditCheck> Checks { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public sealed class MapUxAuditCheck
    {
        public MapUxAuditCheck()
        {
            Category = string.Empty;
            Id = string.Empty;
            Severity = string.Empty;
            Evidence = string.Empty;
            Detail = string.Empty;
        }

        public string Category { get; set; }
        public string Id { get; set; }
        public string Severity { get; set; }
        public bool Passed { get; set; }
        public string Evidence { get; set; }
        public string Detail { get; set; }
    }

    public sealed class MapUxWalkthroughReport
    {
        public MapUxWalkthroughReport()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            WalkthroughKind = string.Empty;
            Purpose = string.Empty;
            OutputPath = string.Empty;
            RecommendedRecordPath = string.Empty;
            SampleScenes = new List<string>();
            Steps = new List<MapUxWalkthroughStep>();
            AcceptanceCriteria = new List<string>();
            FollowUpCommands = new List<string>();
        }

        public string ProjectRoot { get; set; }
        public bool ProjectFileExists { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string WalkthroughKind { get; set; }
        public string Purpose { get; set; }
        public bool StaticAuditOk { get; set; }
        public int StaticAuditBlockingIssueCount { get; set; }
        public int StaticAuditWarningCount { get; set; }
        public int MapCount { get; set; }
        public int PortalCount { get; set; }
        public int MapsWithoutPortalsCount { get; set; }
        public int StepCount { get; set; }
        public string OutputPath { get; set; }
        public bool OutputWritten { get; set; }
        public string RecommendedRecordPath { get; set; }
        public List<string> SampleScenes { get; set; }
        public List<MapUxWalkthroughStep> Steps { get; set; }
        public List<string> AcceptanceCriteria { get; set; }
        public List<string> FollowUpCommands { get; set; }
    }

    public sealed class MapUxWalkthroughStep
    {
        public MapUxWalkthroughStep()
        {
            Id = string.Empty;
            Action = string.Empty;
            ExpectedResult = string.Empty;
            AgentMirrorCommand = string.Empty;
            HumanResult = string.Empty;
            Notes = string.Empty;
        }

        public int Order { get; set; }
        public string Id { get; set; }
        public string Action { get; set; }
        public string ExpectedResult { get; set; }
        public string AgentMirrorCommand { get; set; }
        public string HumanResult { get; set; }
        public string Notes { get; set; }
    }

    public sealed class MapUxReviewResult
    {
        public MapUxReviewResult()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            ReviewKind = string.Empty;
            InputPath = string.Empty;
            OutputPath = string.Empty;
            Reviewer = string.Empty;
            ReviewedAtUtc = string.Empty;
            OverallResult = string.Empty;
            Notes = string.Empty;
            Issues = new List<string>();
            Steps = new List<MapUxReviewStepResult>();
            VerificationCommand = string.Empty;
        }

        public string ProjectRoot { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string ReviewKind { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public bool OutputWritten { get; set; }
        public string Reviewer { get; set; }
        public string ReviewedAtUtc { get; set; }
        public string OverallResult { get; set; }
        public string Notes { get; set; }
        public bool ProjectFileExists { get; set; }
        public bool StaticAuditOk { get; set; }
        public int StepCount { get; set; }
        public int PassedStepCount { get; set; }
        public int PartialStepCount { get; set; }
        public int FailedStepCount { get; set; }
        public int PendingStepCount { get; set; }
        public bool Complete { get; set; }
        public bool Ok { get; set; }
        public int IssueCount { get; set; }
        public List<string> Issues { get; set; }
        public List<MapUxReviewStepResult> Steps { get; set; }
        public string VerificationCommand { get; set; }
    }

    public sealed class MapUxReviewStepResult
    {
        public MapUxReviewStepResult()
        {
            Id = string.Empty;
            ExpectedResult = string.Empty;
            Result = string.Empty;
            Notes = string.Empty;
            AgentMirrorCommand = string.Empty;
        }

        public int Order { get; set; }
        public string Id { get; set; }
        public string ExpectedResult { get; set; }
        public string Result { get; set; }
        public bool Passed { get; set; }
        public string Notes { get; set; }
        public string AgentMirrorCommand { get; set; }
    }
}
