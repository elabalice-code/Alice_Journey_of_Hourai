using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
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

        public MapUxAuditReport BuildUxAudit(string godotRoot)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            var formPath = Path.Combine(godotRoot, "GodotTools", "MapEditorTool", "MapEditorTool", "UI", "Form1.cs");
            var cliPath = Path.Combine(godotRoot, "GodotTools", "MapEditorTool", "MapEditorTool", "Cli", "CliEntry.cs");
            var runtimeVerifyPath = Path.Combine(godotRoot, "GodotTools", "MapEditorTool", "MapEditorTool", "Executor", "RuntimeVerify", "RuntimeVerificationExecutor.cs");
            var formText = TryReadText(formPath);
            var cliText = TryReadText(cliPath);
            var runtimeVerifyText = TryReadText(runtimeVerifyPath);

            var checks = new List<MapUxAuditCheck>();
            AddUxCheck(checks, "discoverability", "main-window-shell",
                HasText(formText, "Text = \"MapEditorTool") &&
                    HasText(formText, "StartPosition = FormStartPosition.CenterScreen"),
                "Main window declares an explicit MapEditorTool title and centered startup position.", "UI/Form1.cs");
            AddUxCheck(checks, "discoverability", "project-file-actions",
                HasText(formText, "NewProject()") &&
                    HasText(formText, "OpenProject()") &&
                    HasText(formText, "SaveProject()") &&
                    HasText(formText, "SaveProjectAs()") &&
                    HasText(formText, "Keys.Control | Keys.N") &&
                    HasText(formText, "Keys.Control | Keys.O") &&
                    HasText(formText, "Keys.Control | Keys.S"),
                "UI exposes new/open/save/save-as project actions with shortcuts.", "UI/Form1.cs");
            AddUxCheck(checks, "discoverability", "map-godot-actions",
                HasText(formText, "ImportFromGodot") &&
                    HasText(formText, "ApplySelectedMapToGodot") &&
                    HasText(formText, "Runtime Verification Report"),
                "UI exposes import, apply, and runtime verification actions.", "UI/Form1.cs");
            AddUxCheck(checks, "discoverability", "context-actions",
                HasText(formText, "mapListContextMenu") &&
                    HasText(formText, "linkListContextMenu") &&
                    HasText(formText, "Add Portal Here"),
                "UI exposes map/link context menus and portal context actions.", "UI/Form1.cs");
            AddUxCheck(checks, "discoverability", "resource-browse",
                HasText(formText, "HookResourceBrowse") &&
                    HasText(formText, "BrowseAndAssignResourcePath") &&
                    HasText(formText, "ResourcePathExecutor"),
                "Property grid resource browsing is discoverable and delegated to ResourcePathExecutor.", "UI/Form1.cs");
            AddUxCheck(checks, "feedback", "status-text",
                HasText(formText, "statusText.Text") &&
                    HasText(formText, "_viewModel.SetStatusText"),
                "UI writes operation feedback through status text and ViewModel status state.", "UI/Form1.cs");
            AddUxCheck(checks, "feedback", "error-dialogs",
                HasText(formText, "MessageBoxIcon.Error") &&
                    HasText(formText, "MessageBox.Show"),
                "UI shows modal error dialogs for failed import/write/report operations.", "UI/Form1.cs");
            AddUxCheck(checks, "feedback", "warning-dialogs",
                HasText(formText, "MessageBoxIcon.Warning"),
                "UI shows warnings for invalid or destructive operations.", "UI/Form1.cs");
            AddUxCheck(checks, "feedback", "hover-tooltips",
                HasText(formText, "ToolTip") &&
                    HasText(formText, "HoverHintRequested") &&
                    HasText(formText, "ShowPropertyGridToolTip"),
                "UI provides hover and PropertyGrid tooltips.", "UI/Form1.cs");
            AddUxCheck(checks, "recovery", "undo-redo",
                HasText(formText, "UndoLastAction") &&
                    HasText(formText, "RedoLastAction") &&
                    HasText(formText, "Keys.Control | Keys.Z") &&
                    HasText(formText, "Keys.Control | Keys.Y"),
                "UI supports undo/redo through menu and global shortcuts.", "UI/Form1.cs");
            AddUxCheck(checks, "recovery", "destructive-confirmation",
                HasText(formText, "DeleteSelectedMap") &&
                    HasText(formText, "MessageBoxButtons.OKCancel"),
                "UI asks for confirmation before destructive map deletion.", "UI/Form1.cs");
            AddUxCheck(checks, "developer-feedback", "developer-comment-mode",
                HasText(formText, "DeveloperCommentBox") &&
                    HasText(formText, "developerCommentModeCheckBox") &&
                    HasText(formText, "DeveloperCommentExecutor"),
                "Developer comment mode is available as a controlled feedback channel.", "UI/Form1.cs");
            AddUxCheck(checks, "agent-mirror", "cli-summary-commands",
                HasText(cliText, "status --godotRoot") &&
                    HasText(cliText, "runtime-verify") &&
                    HasText(cliText, "ux-walkthrough") &&
                    HasText(cliText, "ux-review") &&
                    HasText(cliText, "import --godotRoot") &&
                    HasText(cliText, "validate --godotRoot"),
                "CLI mirrors key map status, verification, UX review, import, and validation workflows.", "Cli/CliEntry.cs");
            AddUxCheck(checks, "agent-mirror", "cli-utility-commands",
                HasText(cliText, "tracealpha") &&
                    HasText(cliText, "portalanim"),
                "CLI mirrors legacy diagnostic utilities for alpha tracing and portal animation extraction.", "Cli/CliEntry.cs");
            AddUxCheck(checks, "agent-mirror", "runtime-verifier-covers-ux-audit",
                HasText(runtimeVerifyText, "mapeditortool-cli-ux-audit"),
                "Runtime verifier tracks the ux-audit CLI surface.", "Executor/RuntimeVerify/RuntimeVerificationExecutor.cs", "warning");

            AddUxCheck(checks, "readability", "no-garbled-text-markers",
                CountSuspiciousMojibake(formText + cliText) == 0,
                "No obvious garbled text markers were detected in active MapEditorTool UI/CLI source.", "UI/Form1.cs; Cli/CliEntry.cs", "warning");

            var blocking = checks.Count(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase) && !x.Passed);
            var warnings = checks.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase) && !x.Passed);
            return new MapUxAuditReport
            {
                ProjectRoot = godotRoot,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                AuditKind = "static-mapeditor-tool-ux",
                Scope = "Static UX audit for MapEditorTool UI, CLI mirror commands, feedback, recovery, and developer-comment surfaces. It does not replace a human click-through.",
                CheckCount = checks.Count,
                PassedCount = checks.Count(x => x.Passed),
                WarningCount = warnings,
                BlockingIssueCount = blocking,
                Ok = blocking == 0,
                Checks = checks,
                Recommendations = BuildUxRecommendations(checks)
            };
        }

        public MapUxWalkthroughReport BuildUxWalkthrough(string godotRoot)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            var status = BuildStatus(godotRoot);
            var uxAudit = BuildUxAudit(godotRoot);
            var validationInput = Path.Combine("BuildLogs", "map_project.json");
            var sampleScene = status.SampleScenes.FirstOrDefault() ?? "res://CoreEngine/Maps/<reviewed-map>.tscn";
            var steps = new List<MapUxWalkthroughStep>
            {
                BuildWalkthroughStep(1, "launch", "Launch MapEditorTool from ToolHub or the built executable.", "MapEditorTool window opens without errors and shows map/link editing surfaces.", ".\\tools.ps1 run map-editor launch -NoBuild"),
                BuildWalkthroughStep(2, "import", "Use the import/reload action for the current Godot project.", "The UI reports the current project root and map count; no project resources are modified.", ".\\tools.ps1 map import --summary -NoBuild"),
                BuildWalkthroughStep(3, "inspect", "Select a representative map and inspect its scene path, portals, links, and editable properties.", "The selected map is understandable to a non-technical user; suggested sample: " + sampleScene + ".", ".\\tools.ps1 map status --summary -NoBuild"),
                BuildWalkthroughStep(4, "edit-preview", "Make a harmless in-memory edit or select an existing editable field without applying it to Godot resources.", "The UI makes dirty/selection state visible and the user can tell what would change before saving.", ".\\tools.ps1 map portal-review --summary -NoBuild"),
                BuildWalkthroughStep(5, "save-review", "Save the MapEditorTool project JSON or use Save As to a review location.", "The UI gives success/failure feedback and the saved file can be validated against the current Godot scan.", ".\\tools.ps1 map validate --summary --in " + validationInput + " -NoBuild"),
                BuildWalkthroughStep(6, "error-recovery", "Trigger or simulate an invalid path/action, then recover without changing game resources.", "The UI shows a clear warning/error, and Cancel/Undo/Open can return the user to a known-good state.", ".\\tools.ps1 map ux-audit --summary -NoBuild"),
                BuildWalkthroughStep(7, "agent-mirror", "Run the agent mirror commands after the UI pass.", "CLI output matches the human observations and records the same map counts, validation state, and UX notes.", ".\\tools.ps1 handoff --summary -NoBuild")
            };

            return new MapUxWalkthroughReport
            {
                ProjectRoot = godotRoot,
                ProjectFileExists = status.ProjectFileExists,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                WalkthroughKind = "human-live-ux",
                Purpose = "Human click-through checklist for MapEditorTool import, inspect, edit preview, save/review, validation, and recovery flows. This report is a review script, not proof that the click-through has been completed.",
                StaticAuditOk = uxAudit.Ok,
                StaticAuditBlockingIssueCount = uxAudit.BlockingIssueCount,
                StaticAuditWarningCount = uxAudit.WarningCount,
                MapCount = status.MapCount,
                PortalCount = status.PortalCount,
                MapsWithoutPortalsCount = status.MapsWithoutPortalsCount,
                SampleScenes = status.SampleScenes.Take(8).ToList(),
                StepCount = steps.Count,
                Steps = steps,
                AcceptanceCriteria = new List<string>
                {
                    "A human can discover import/open/save/save-as without reading source code.",
                    "The UI shows enough map, portal, link, and property context to understand the selected data.",
                    "Save/apply actions give visible success or failure feedback.",
                    "Validation can be run after the UI pass and reports no missing or extra scenes for the saved project JSON.",
                    "Bad edits or invalid paths have a clear recovery path through cancel, undo, open, or restore-from-dump.",
                    "Agent mirror commands can reproduce the key state without opening the UI."
                },
                RecommendedRecordPath = "BuildLogs/map_ux_walkthrough.json",
                FollowUpCommands = new List<string>
                {
                    ".\\tools.ps1 map ux-walkthrough --summary --out BuildLogs\\map_ux_walkthrough.json -NoBuild",
                    ".\\tools.ps1 map ux-audit --summary -NoBuild",
                    ".\\tools.ps1 map import --summary -NoBuild",
                    ".\\tools.ps1 map validate --summary -NoBuild",
                    ".\\tools.ps1 handoff --summary -NoBuild"
                }
            };
        }

        public MapUxReviewResult BuildUxReview(string godotRoot, string input, IReadOnlyDictionary<string, string> options)
        {
            godotRoot = Path.GetFullPath(godotRoot);
            input = string.IsNullOrWhiteSpace(input) ? Path.Combine("BuildLogs", "map_ux_review_result.json") : input;
            options = options ?? new Dictionary<string, string>();

            var absoluteInput = Path.IsPathRooted(input) ? input : Path.Combine(godotRoot, input);
            var hasReviewInput = File.Exists(absoluteInput) && LooksLikeUxReviewResult(absoluteInput);
            if (hasReviewInput && !HasNewUxReviewInput(options))
            {
                var existing = ReadJsonFile<MapUxReviewResult>(absoluteInput);
                RecomputeUxReviewResult(existing);
                existing.ProjectRoot = godotRoot;
                existing.InputPath = input;
                return existing;
            }

            var walkthrough = File.Exists(absoluteInput) && !hasReviewInput
                ? ReadJsonFile<MapUxWalkthroughReport>(absoluteInput)
                : BuildUxWalkthrough(godotRoot);

            var stepResults = ParseStepResults(GetOption(options, "step-results"));
            var defaultResult = NormalizeUxResult(GetOption(options, "result", "pending"));
            var reviewer = GetOption(options, "reviewer");
            var notes = GetOption(options, "notes");
            var reviewedAt = GetOption(options, "reviewed-at", DateTimeOffset.UtcNow.ToString("O"));

            var steps = walkthrough.Steps
                .OrderBy(x => x.Order)
                .Select(step =>
                {
                    string explicitResult;
                    var result = stepResults.TryGetValue(step.Id, out explicitResult) ? explicitResult : defaultResult;
                    return new MapUxReviewStepResult
                    {
                        Order = step.Order,
                        Id = step.Id,
                        ExpectedResult = step.ExpectedResult,
                        Result = result,
                        Passed = result.Equals("pass", StringComparison.OrdinalIgnoreCase),
                        Notes = step.Notes,
                        AgentMirrorCommand = step.AgentMirrorCommand
                    };
                })
                .ToList();

            var report = new MapUxReviewResult
            {
                ProjectRoot = godotRoot,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ReviewKind = "human-live-ux-result",
                InputPath = input,
                Reviewer = reviewer,
                ReviewedAtUtc = reviewedAt,
                OverallResult = defaultResult,
                Notes = notes,
                ProjectFileExists = walkthrough.ProjectFileExists,
                StaticAuditOk = walkthrough.StaticAuditOk,
                Steps = steps,
                VerificationCommand = ".\\tools.ps1 map ux-review --summary --in BuildLogs\\map_ux_walkthrough.json --out BuildLogs\\map_ux_review_result.json --reviewer <name> --result pass --step-results \"launch=pass;import=pass;inspect=pass;edit-preview=pass;save-review=pass;error-recovery=pass;agent-mirror=pass\" -NoBuild"
            };
            RecomputeUxReviewResult(report);
            if (!walkthrough.ProjectFileExists)
                report.Issues.Add("Project file was missing when the walkthrough was generated.");
            if (!walkthrough.StaticAuditOk)
                report.Issues.Add("Static UX audit is not OK.");
            report.IssueCount = report.Issues.Count;
            report.Ok = report.Ok && report.IssueCount == 0;
            return report;
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

        public string FormatUxAuditSummary(MapUxAuditReport report)
        {
            var lines = new List<string>
            {
                "MapEditorTool UX audit",
                "Project: " + report.ProjectRoot,
                "Generated UTC: " + report.GeneratedAtUtc,
                "Kind: " + report.AuditKind,
                "Overall: " + (report.Ok ? "OK" : "FAILED") +
                    " blocking=" + report.BlockingIssueCount +
                    " warnings=" + report.WarningCount,
                "Counts: checks=" + report.CheckCount + " passed=" + report.PassedCount,
                "Scope: " + report.Scope,
                "Checks:"
            };

            foreach (var check in report.Checks)
            {
                lines.Add("  " + (check.Passed ? "OK" : check.Severity.ToUpperInvariant()) +
                    " [" + check.Category + "] " + check.Id + " - " + check.Detail);
            }

            lines.Add("Recommendations:");
            if (report.Recommendations.Count == 0)
                lines.Add("  none");
            foreach (var recommendation in report.Recommendations)
                lines.Add("  " + recommendation);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public string FormatUxWalkthroughSummary(MapUxWalkthroughReport report)
        {
            var lines = new List<string>
            {
                "MapEditorTool UX walkthrough",
                "Project: " + report.ProjectRoot,
                "Generated UTC: " + report.GeneratedAtUtc,
                "Kind: " + report.WalkthroughKind,
                "Project file: " + (report.ProjectFileExists ? "ok" : "missing"),
                "Static audit: " + (report.StaticAuditOk ? "OK" : "FAILED") +
                    " blocking=" + report.StaticAuditBlockingIssueCount +
                    " warnings=" + report.StaticAuditWarningCount,
                "Counts: maps=" + report.MapCount +
                    " portals=" + report.PortalCount +
                    " mapsWithoutPortals=" + report.MapsWithoutPortalsCount +
                    " steps=" + report.StepCount,
                "Record path: " + report.RecommendedRecordPath,
                "Purpose: " + report.Purpose
            };

            AddSummaryList(lines, "Sample scenes", report.SampleScenes);
            lines.Add("Steps:");
            foreach (var step in report.Steps.OrderBy(x => x.Order))
            {
                lines.Add("  " + step.Order + ". " + step.Id + ": " + step.Action);
                lines.Add("     expect: " + step.ExpectedResult);
                lines.Add("     mirror: " + step.AgentMirrorCommand);
            }

            lines.Add("Acceptance criteria:");
            foreach (var criterion in report.AcceptanceCriteria)
                lines.Add("  " + criterion);

            lines.Add("Follow-up commands:");
            foreach (var command in report.FollowUpCommands)
                lines.Add("  " + command);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public string FormatUxReviewSummary(MapUxReviewResult report)
        {
            var lines = new List<string>
            {
                "MapEditorTool UX review result",
                "Project: " + report.ProjectRoot,
                "Generated UTC: " + report.GeneratedAtUtc,
                "Kind: " + report.ReviewKind,
                "Input: " + report.InputPath,
                "Output: " + report.OutputPath,
                "Reviewer: " + (string.IsNullOrWhiteSpace(report.Reviewer) ? "missing" : report.Reviewer),
                "Overall: " + (report.Ok ? "OK" : "NOT ACCEPTED") +
                    " result=" + report.OverallResult +
                    " complete=" + report.Complete.ToString().ToLowerInvariant() +
                    " issues=" + report.IssueCount,
                "Counts: steps=" + report.StepCount +
                    " pass=" + report.PassedStepCount +
                    " partial=" + report.PartialStepCount +
                    " fail=" + report.FailedStepCount +
                    " pending=" + report.PendingStepCount,
                "Static audit: " + (report.StaticAuditOk ? "OK" : "FAILED") +
                    " projectFile=" + (report.ProjectFileExists ? "ok" : "missing")
            };

            AddSummaryList(lines, "Issues", report.Issues);
            lines.Add("Steps:");
            foreach (var step in report.Steps.OrderBy(x => x.Order))
            {
                lines.Add("  " + step.Order + ". " + step.Id + ": " + step.Result);
                lines.Add("     expect: " + step.ExpectedResult);
                lines.Add("     mirror: " + step.AgentMirrorCommand);
            }

            lines.Add("Verification command:");
            lines.Add("  " + report.VerificationCommand);
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

        private static void AddUxCheck(
            List<MapUxAuditCheck> checks,
            string category,
            string id,
            bool passed,
            string detail,
            string evidence,
            string severity = "error")
        {
            checks.Add(new MapUxAuditCheck
            {
                Category = category,
                Id = id,
                Severity = severity,
                Passed = passed,
                Evidence = evidence,
                Detail = detail
            });
        }

        private static MapUxWalkthroughStep BuildWalkthroughStep(int order, string id, string action, string expected, string agentMirrorCommand)
        {
            return new MapUxWalkthroughStep
            {
                Order = order,
                Id = id,
                Action = action,
                ExpectedResult = expected,
                AgentMirrorCommand = agentMirrorCommand,
                HumanResult = "pending"
            };
        }

        private static bool HasNewUxReviewInput(IReadOnlyDictionary<string, string> options)
        {
            return options.ContainsKey("reviewer") ||
                options.ContainsKey("result") ||
                options.ContainsKey("step-results") ||
                options.ContainsKey("notes") ||
                options.ContainsKey("reviewed-at");
        }

        private static bool LooksLikeUxReviewResult(string path)
        {
            var text = TryReadText(path);
            return HasText(text, "\"ReviewKind\"") || HasText(text, "\"reviewKind\"");
        }

        private static void RecomputeUxReviewResult(MapUxReviewResult report)
        {
            if (report.Steps == null)
                report.Steps = new List<MapUxReviewStepResult>();

            foreach (var step in report.Steps)
            {
                step.Result = NormalizeUxResult(step.Result);
                step.Passed = step.Result.Equals("pass", StringComparison.OrdinalIgnoreCase);
            }

            report.StepCount = report.Steps.Count;
            report.PassedStepCount = report.Steps.Count(x => x.Result.Equals("pass", StringComparison.OrdinalIgnoreCase));
            report.PartialStepCount = report.Steps.Count(x => x.Result.Equals("partial", StringComparison.OrdinalIgnoreCase));
            report.FailedStepCount = report.Steps.Count(x => x.Result.Equals("fail", StringComparison.OrdinalIgnoreCase));
            report.PendingStepCount = report.Steps.Count(x => x.Result.Equals("pending", StringComparison.OrdinalIgnoreCase));
            report.OverallResult = NormalizeUxResult(report.OverallResult);
            report.Complete = report.StepCount > 0 &&
                report.PendingStepCount == 0 &&
                !string.IsNullOrWhiteSpace(report.Reviewer) &&
                !report.OverallResult.Equals("pending", StringComparison.OrdinalIgnoreCase);
            report.Ok = report.Complete &&
                report.FailedStepCount == 0 &&
                report.PartialStepCount == 0 &&
                report.Steps.All(x => x.Passed) &&
                report.OverallResult.Equals("pass", StringComparison.OrdinalIgnoreCase);

            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(report.Reviewer))
                issues.Add("Missing reviewer.");
            if (report.OverallResult.Equals("pending", StringComparison.OrdinalIgnoreCase))
                issues.Add("Overall result is pending.");
            if (report.PendingStepCount > 0)
                issues.Add(report.PendingStepCount + " UX walkthrough step(s) are still pending.");
            if (report.PartialStepCount > 0)
                issues.Add(report.PartialStepCount + " UX walkthrough step(s) are marked partial.");
            if (report.FailedStepCount > 0)
                issues.Add(report.FailedStepCount + " UX walkthrough step(s) are marked fail.");
            report.Issues = issues;
            report.IssueCount = issues.Count;
        }

        private static Dictionary<string, string> ParseStepResults(string raw)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
                return results;

            foreach (var part in raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pieces = part.Split(new[] { '=' }, 2);
                if (pieces.Length != 2 || string.IsNullOrWhiteSpace(pieces[0]))
                    continue;
                results[pieces[0].Trim()] = NormalizeUxResult(pieces[1]);
            }

            return results;
        }

        private static string NormalizeUxResult(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "pass" || normalized == "partial" || normalized == "fail" || normalized == "pending")
                return normalized;
            return "pending";
        }

        private static string GetOption(IReadOnlyDictionary<string, string> options, string key)
        {
            return GetOption(options, key, string.Empty);
        }

        private static string GetOption(IReadOnlyDictionary<string, string> options, string key, string defaultValue)
        {
            string value;
            return options != null && options.TryGetValue(key, out value) ? value : defaultValue;
        }

        private static T ReadJsonFile<T>(string path)
        {
            var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path))))
            {
                var value = serializer.ReadObject(stream);
                if (value == null)
                    throw new InvalidDataException("Failed to parse JSON file: " + path);
                return (T)value;
            }
        }

        private static int CountSuspiciousMojibake(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Count(ch => ch == '\uFFFD');
        }

        private static bool HasText(string text, string value)
        {
            return (text ?? string.Empty).IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        private static List<string> BuildUxRecommendations(List<MapUxAuditCheck> checks)
        {
            var recommendations = new List<string>();
            if (checks.Any(x => string.Equals(x.Category, "feedback", StringComparison.OrdinalIgnoreCase) && !x.Passed))
                recommendations.Add("Add visible status text or dialogs for any save/apply/validation operation that lacks feedback.");
            if (checks.Any(x => string.Equals(x.Category, "recovery", StringComparison.OrdinalIgnoreCase) && !x.Passed))
                recommendations.Add("Add undo, confirmation, or recovery affordances before approving more mutating workflows.");
            if (checks.Any(x => string.Equals(x.Id, "no-garbled-text-markers", StringComparison.OrdinalIgnoreCase) && !x.Passed))
                recommendations.Add("Repair garbled UI or CLI text and prefer English comments/diagnostic strings in active MapEditorTool code.");
            recommendations.Add("Run a human click-through review for import, edit, save/apply, validation, and recovery flows; this static audit is only the agent mirror.");
            return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
