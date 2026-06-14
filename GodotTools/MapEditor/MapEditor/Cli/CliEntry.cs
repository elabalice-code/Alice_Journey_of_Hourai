using System.Text.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MapEditor.Godot;
using MapEditor.Godot.Tscn;

namespace MapEditor.Cli;

public static class CliEntry
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
                return 0;

            var cmd = args[0].Trim().ToLowerInvariant();
            var opts = ParseOptions(args.Skip(1).ToArray());

            return cmd switch
            {
                "status" => RunStatus(opts),
                "portal-review" => RunPortalReview(opts),
                "runtime-verify" => RunRuntimeVerify(opts),
                "ux-audit" => RunUxAudit(opts),
                "ux-walkthrough" => RunUxWalkthrough(opts),
                "ux-review" => RunUxReview(opts),
                "import" => RunImport(opts),
                "validate" => RunValidate(opts),
                "patchpos" => RunPatchPos(opts),
                "tracealpha" => RunTraceAlpha(opts),
                "portalanim" => RunPortalAnim(opts),
                "agent-self-test" or "--agent-self-test" => RunAgentSelfTest(opts),
                "help" or "--help" or "-h" => RunHelp(),
                _ => RunHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int RunHelp()
    {
        Console.WriteLine("MapEditor CLI");
        Console.WriteLine("  MapEditor.exe status --godotRoot <dir> [--summary]");
        Console.WriteLine("  MapEditor.exe portal-review --godotRoot <dir> [--summary]");
        Console.WriteLine("  MapEditor.exe runtime-verify --godotRoot <dir> [--summary]");
        Console.WriteLine("  MapEditor.exe ux-audit --godotRoot <dir> [--summary]");
        Console.WriteLine("  MapEditor.exe ux-walkthrough --godotRoot <dir> [--out <file>] [--summary]");
        Console.WriteLine("  MapEditor.exe ux-review --godotRoot <dir> [--in <file>] [--out <file>] [--reviewer <name>] [--result pass|partial|fail|pending] [--step-results <id=pass;id=fail>] [--notes <text>] [--summary]");
        Console.WriteLine("  MapEditor.exe import --godotRoot <dir> --out <file> [--summary]");
        Console.WriteLine("  MapEditor.exe validate --godotRoot <dir> --in <file> [--summary]");
        Console.WriteLine("  MapEditor.exe patchpos --godotRoot <dir> --scene <res://...> --nodePath <path> --x <num> --y <num>");
        Console.WriteLine("  MapEditor.exe tracealpha --in <image> [--worldW <num>] [--worldH <num>] [--threshold <0-254>]");
        Console.WriteLine("  MapEditor.exe portalanim --godotRoot <dir> --in <mp4> [--outDir <res://...|abs>] [--pattern <name_%03d.png>]");
        Console.WriteLine("  MapEditor.exe agent-self-test [--godotRoot <dir>]");
        return 0;
    }

    private static int RunAgentSelfTest(Dictionary<string, string> opts)
    {
        var project = Models.MapProject.CreateDefault();
        var json = JsonSerializer.Serialize(project, JsonOptions.Default);
        var loaded = JsonSerializer.Deserialize<Models.MapProject>(json, JsonOptions.Default);
        if (loaded == null || loaded.Maps.Count != 1 || loaded.Maps[0].Id != "main")
        {
            Console.Error.WriteLine("MapEditor agent self-test failed project roundtrip.");
            return 1;
        }

        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (!string.IsNullOrWhiteSpace(godotRoot))
        {
            var coreMaps = Path.Combine(godotRoot, "CoreEngine", "Maps");
            if (!Directory.Exists(coreMaps))
            {
                Console.Error.WriteLine("MapEditor agent self-test failed Godot root check: " + coreMaps);
                return 1;
            }
            var status = BuildStatus(godotRoot);
            if (!status.ProjectFileExists || status.MapCount == 0)
            {
                Console.Error.WriteLine("MapEditor agent self-test failed status scan.");
                return 1;
            }
        }

        Console.WriteLine("MapEditor agent self-test OK.");
        return 0;
    }

    private static int RunStatus(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var status = BuildStatus(godotRoot);
        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatStatusSummary(status));
        else
            Console.WriteLine(JsonSerializer.Serialize(status, JsonOptions.Default));
        return status.ProjectFileExists ? 0 : 1;
    }

    private static int RunPortalReview(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var review = BuildPortalReview(godotRoot);
        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatPortalReviewSummary(review));
        else
            Console.WriteLine(JsonSerializer.Serialize(review, JsonOptions.Default));
        return review.ProjectFileExists ? 0 : 1;
    }

    private static int RunRuntimeVerify(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var report = BuildRuntimeVerificationReport(godotRoot);
        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatRuntimeVerificationSummary(report));
        else
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Default));
        return report.Ok ? 0 : 1;
    }

    private static int RunUxAudit(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var report = BuildUxAuditReport(godotRoot);
        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatUxAuditSummary(report));
        else
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Default));
        return report.BlockingIssueCount == 0 ? 0 : 1;
    }

    private static int RunUxWalkthrough(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var report = BuildUxWalkthroughReport(godotRoot);
        var output = opts.GetValueOrDefault("out", "");
        if (!string.IsNullOrWhiteSpace(output))
        {
            report.OutputPath = output;
            report.OutputWritten = true;
            var absoluteOutput = Path.IsPathRooted(output) ? output : Path.Combine(godotRoot, output);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absoluteOutput)) ?? ".");
            File.WriteAllText(absoluteOutput, JsonSerializer.Serialize(report, JsonOptions.Default));
        }

        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatUxWalkthroughSummary(report));
        else
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Default));
        return report.ProjectFileExists && report.StaticAuditOk ? 0 : 1;
    }

    private static int RunUxReview(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var input = opts.GetValueOrDefault("in", Path.Combine("BuildLogs", "map_ux_review_result.json"));
        var output = opts.GetValueOrDefault("out", "");
        var report = BuildUxReviewResult(godotRoot, input, opts);

        if (!string.IsNullOrWhiteSpace(output))
        {
            report.OutputPath = output;
            report.OutputWritten = true;
            var absoluteOutput = Path.IsPathRooted(output) ? output : Path.Combine(godotRoot, output);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absoluteOutput)) ?? ".");
            File.WriteAllText(absoluteOutput, JsonSerializer.Serialize(report, JsonOptions.Default));
        }

        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatUxReviewSummary(report));
        else
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Default));

        return report.Ok ? 0 : 1;
    }

    private static string FormatStatusSummary(MapEditorStatus status)
    {
        var lines = new List<string>
        {
            "MapEditor status",
            $"Project: {status.ProjectRoot}",
            $"Generated UTC: {status.GeneratedAtUtc}",
            $"Project file: {(status.ProjectFileExists ? "ok" : "missing")}",
            "Counts: " +
                $"maps={status.MapCount} " +
                $"links={status.LinkCount} " +
                $"portals={status.PortalCount} " +
                $"tileLayers={status.TileLayerCount} " +
                $"entities={status.EntityCount}",
            "Issues: " +
                $"missingScenes={status.MissingSceneCount} " +
                $"missingTargets={status.LinksWithMissingTargetsCount} " +
                $"mapsWithoutPortals={status.MapsWithoutPortalsCount}"
        };

        AddSummaryList(lines, "Missing scenes", status.MissingScenes);
        AddSummaryList(lines, "Links with missing targets", status.LinksWithMissingTargets);
        AddSummaryList(lines, "Maps without portals", status.MapsWithoutPortals);

        return string.Join(Environment.NewLine, lines);
    }

    private static MapUxAuditReport BuildUxAuditReport(string godotRoot)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var mainFormPath = Path.Combine(godotRoot, "GodotTools", "MapEditor", "MapEditor", "MainForm.cs");
        var cliPath = Path.Combine(godotRoot, "GodotTools", "MapEditor", "MapEditor", "Cli", "CliEntry.cs");
        var mainText = File.Exists(mainFormPath) ? File.ReadAllText(mainFormPath) : "";
        var cliText = File.Exists(cliPath) ? File.ReadAllText(cliPath) : "";
        var checks = new List<MapUxAuditCheck>();

        AddUxCheck(checks, "discoverability", "main-window-title", mainText.Contains("Text =", StringComparison.Ordinal) && mainText.Contains("StartPosition", StringComparison.Ordinal), "Main window declares a title and centered startup position.", "MainForm.cs");
        AddUxCheck(checks, "discoverability", "menu-import", mainText.Contains("ImportFromGodot", StringComparison.Ordinal) && mainText.Contains("ToolStripMenuItem", StringComparison.Ordinal), "UI has a menu entry for reloading/importing the Godot project.", "MainForm.cs");
        AddUxCheck(checks, "discoverability", "project-file-actions", mainText.Contains("NewProject", StringComparison.Ordinal) && mainText.Contains("OpenProject", StringComparison.Ordinal) && mainText.Contains("SaveProject", StringComparison.Ordinal) && mainText.Contains("SaveProjectAs", StringComparison.Ordinal) && mainText.Contains("Keys.Control | Keys.N", StringComparison.Ordinal) && mainText.Contains("Keys.Control | Keys.O", StringComparison.Ordinal) && mainText.Contains("Keys.Control | Keys.S", StringComparison.Ordinal), "UI exposes new/open/save/save-as project actions with keyboard shortcuts.", "MainForm.cs");
        AddUxCheck(checks, "discoverability", "map-and-link-tabs", mainText.Contains("new TabPage", StringComparison.Ordinal) && mainText.Contains("_tabs.TabPages.Add", StringComparison.Ordinal), "UI separates map and connection workflows into tabs.", "MainForm.cs");
        AddUxCheck(checks, "discoverability", "context-actions", mainText.Contains("ContextMenuStrip", StringComparison.Ordinal) && mainText.Contains("新增", StringComparison.Ordinal), "UI exposes contextual add/remove or portal actions.", "MainForm.cs");
        AddUxCheck(checks, "discoverability", "tooltips", mainText.Contains("ToolTip", StringComparison.Ordinal) && mainText.Contains("ShowGridHelpToolTip", StringComparison.Ordinal), "UI provides hover/property help through tooltips.", "MainForm.cs");

        AddUxCheck(checks, "feedback", "status-strip", mainText.Contains("StatusStrip", StringComparison.Ordinal) && mainText.Contains("UpdateStatus()", StringComparison.Ordinal), "UI has a persistent status strip refreshed after major operations.", "MainForm.cs");
        AddUxCheck(checks, "feedback", "status-counts", mainText.Contains("_project.Maps.Count", StringComparison.Ordinal) && mainText.Contains("_project.Links.Count", StringComparison.Ordinal), "Status text includes map and link counts.", "MainForm.cs");
        AddUxCheck(checks, "feedback", "save-success-feedback", mainText.Contains("保存成功", StringComparison.Ordinal) || mainText.Contains("淇濆瓨", StringComparison.Ordinal), "UI reports save success/failure for collision layout writes.", "MainForm.cs");
        AddUxCheck(checks, "feedback", "error-dialogs", mainText.Contains("MessageBox.Show", StringComparison.Ordinal) && mainText.Contains("MessageBoxIcon.Error", StringComparison.Ordinal), "UI shows modal error dialogs for failed import/write operations.", "MainForm.cs");
        AddUxCheck(checks, "feedback", "warning-dialogs", mainText.Contains("MessageBoxIcon.Warning", StringComparison.Ordinal), "UI shows warnings for missing project roots, invalid scene paths, or destructive actions.", "MainForm.cs");

        AddUxCheck(checks, "recovery", "undo-redo", mainText.Contains("Undo()", StringComparison.Ordinal) && mainText.Contains("Redo()", StringComparison.Ordinal) && mainText.Contains("Keys.Control | Keys.Z", StringComparison.Ordinal), "UI supports undo/redo through menu and shortcuts.", "MainForm.cs");
        AddUxCheck(checks, "recovery", "destructive-confirmation", mainText.Contains("MessageBoxButtons.OKCancel", StringComparison.Ordinal), "UI asks for confirmation before destructive map removal.", "MainForm.cs");
        AddUxCheck(checks, "recovery", "browse-resource-paths", mainText.Contains("BrowseAndAssign", StringComparison.Ordinal) && mainText.Contains("OpenFileDialog", StringComparison.Ordinal), "Property grid supports browsing file/resource paths.", "MainForm.cs");

        AddUxCheck(checks, "agent-mirror", "cli-summary", cliText.Contains("ux-audit", StringComparison.Ordinal) && cliText.Contains("--summary", StringComparison.Ordinal), "UX audit is mirrored as a non-interactive CLI summary.", "CliEntry.cs");
        AddUxCheck(checks, "agent-mirror", "map-status-summary", cliText.Contains("status --godotRoot", StringComparison.Ordinal) && cliText.Contains("FormatStatusSummary", StringComparison.Ordinal), "Map state has a human-readable CLI status summary.", "CliEntry.cs");

        var suspiciousEncodingCount = CountSuspiciousMojibake(mainText);
        if (suspiciousEncodingCount > 0)
        {
            checks.Add(new MapUxAuditCheck
            {
                Category = "readability",
                Id = "possible-garbled-ui-text",
                Severity = "warning",
                Passed = false,
                Evidence = "MainForm.cs",
                Detail = $"Detected {suspiciousEncodingCount} suspicious garbled text marker(s) in UI strings; human labels may need encoding/localization cleanup."
            });
        }
        else
        {
            AddUxCheck(checks, "readability", "possible-garbled-ui-text", true, "No obvious garbled UI text markers were detected.", "MainForm.cs");
        }

        var blocking = checks.Count(x => x.Severity == "error" && !x.Passed);
        var warnings = checks.Count(x => x.Severity == "warning" && !x.Passed);
        return new MapUxAuditReport
        {
            ProjectRoot = godotRoot,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            AuditKind = "static-ui-ux",
            Scope = "Static UX audit: checks discoverability, feedback, recovery, and Agent mirror surfaces in MapEditor source. It does not replace a human click-through or screenshot review.",
            CheckCount = checks.Count,
            PassedCount = checks.Count(x => x.Passed),
            WarningCount = warnings,
            BlockingIssueCount = blocking,
            Ok = blocking == 0,
            Checks = checks,
            Recommendations = BuildUxRecommendations(checks)
        };
    }

    private static void AddUxCheck(List<MapUxAuditCheck> checks, string category, string id, bool passed, string detail, string evidence, string severity = "error")
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

    private static int CountSuspiciousMojibake(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        var markers = new[] { "锛", "鍦", "缂", "瑙", "淇", "瀵", "纰", "鐩", "褰", "鏈" };
        return markers.Sum(marker => Regex.Matches(text, Regex.Escape(marker)).Count);
    }

    private static List<string> BuildUxRecommendations(List<MapUxAuditCheck> checks)
    {
        var recommendations = new List<string>();
        if (checks.Any(x => x.Id == "possible-garbled-ui-text" && !x.Passed))
            recommendations.Add("Review MapEditor UI labels in a live window and repair source encoding/localization for garbled Chinese text.");
        if (checks.Any(x => x.Category == "feedback" && !x.Passed))
            recommendations.Add("Add visible status or dialog feedback for each save/apply/validation operation.");
        if (checks.Any(x => x.Category == "recovery" && !x.Passed))
            recommendations.Add("Add undo, confirmation, or recovery paths before approving more mutating UI workflows.");
        recommendations.Add("Run a human click-through review for import, edit, save/apply, validation, and error-recovery flows; this static audit is only the Agent mirror.");
        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FormatUxAuditSummary(MapUxAuditReport report)
    {
        var lines = new List<string>
        {
            "MapEditor UX audit",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Kind: {report.AuditKind}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} blocking={report.BlockingIssueCount} warnings={report.WarningCount}",
            $"Counts: checks={report.CheckCount} passed={report.PassedCount}",
            "Scope: " + report.Scope,
            "Checks:"
        };

        foreach (var check in report.Checks)
            lines.Add($"  {(check.Passed ? "OK" : check.Severity.ToUpperInvariant())} [{check.Category}] {check.Id} - {check.Detail}");

        lines.Add("Recommendations:");
        foreach (var recommendation in report.Recommendations)
            lines.Add("  " + recommendation);

        return string.Join(Environment.NewLine, lines);
    }

    private static MapUxWalkthroughReport BuildUxWalkthroughReport(string godotRoot)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var status = BuildStatus(godotRoot);
        var uxAudit = BuildUxAuditReport(godotRoot);
        var validationInput = Path.Combine("BuildLogs", "map_project.json");
        var sampleScene = status.SampleScenes.FirstOrDefault() ?? "res://CoreEngine/Maps/<reviewed-map>.tscn";
        var steps = new List<MapUxWalkthroughStep>
        {
            BuildWalkthroughStep(1, "launch", "Launch MapEditor from ToolHub or the built executable.", "MapEditor window opens without errors and shows map/link editing surfaces.", ".\\tools.ps1 run map-editor launch -NoBuild"),
            BuildWalkthroughStep(2, "import", "Use the import/reload action for the current Godot project.", "The UI reports the current project root and map count; no project resources are modified.", ".\\tools.ps1 map import --summary -NoBuild"),
            BuildWalkthroughStep(3, "inspect", "Select a representative map and inspect its scene path, portals, links, and editable properties.", $"The selected map is understandable to a non-technical user; suggested sample: {sampleScene}.", ".\\tools.ps1 map status --summary -NoBuild"),
            BuildWalkthroughStep(4, "edit-preview", "Make a harmless in-memory edit or select an existing editable field without applying it to Godot resources.", "The UI makes dirty/selection state visible and the user can tell what would change before saving.", ".\\tools.ps1 map portal-review --summary -NoBuild"),
            BuildWalkthroughStep(5, "save-review", "Save the MapEditor project JSON or use Save As to a review location.", "The UI gives success/failure feedback and the saved file can be validated against the current Godot scan.", $".\\tools.ps1 map validate --summary --in {validationInput} -NoBuild"),
            BuildWalkthroughStep(6, "error-recovery", "Trigger or simulate an invalid path/action, then recover without changing game resources.", "The UI shows a clear warning/error, and Cancel/Undo/Open can return the user to a known-good state.", ".\\tools.ps1 map ux-audit --summary -NoBuild"),
            BuildWalkthroughStep(7, "agent-mirror", "Run the Agent mirror commands after the UI pass.", "CLI output matches the human observations and records the same map counts, validation state, and UX notes.", ".\\tools.ps1 handoff --summary -NoBuild")
        };

        return new MapUxWalkthroughReport
        {
            ProjectRoot = godotRoot,
            ProjectFileExists = status.ProjectFileExists,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            WalkthroughKind = "human-live-ux",
            Purpose = "Human click-through checklist for MapEditor import, inspect, edit preview, save/review, validation, and recovery flows. This report is a review script, not proof that the click-through has been completed.",
            StaticAuditOk = uxAudit.Ok,
            StaticAuditBlockingIssueCount = uxAudit.BlockingIssueCount,
            StaticAuditWarningCount = uxAudit.WarningCount,
            MapCount = status.MapCount,
            PortalCount = status.PortalCount,
            MapsWithoutPortalsCount = status.MapsWithoutPortalsCount,
            SampleScenes = status.SampleScenes.Take(8).ToList(),
            StepCount = steps.Count,
            Steps = steps,
            AcceptanceCriteria =
            [
                "A human can discover import/open/save/save-as without reading source code.",
                "The UI shows enough map, portal, link, and property context to understand the selected data.",
                "Save/apply actions give visible success or failure feedback.",
                "Validation can be run after the UI pass and reports no missing or extra scenes for the saved project JSON.",
                "Bad edits or invalid paths have a clear recovery path through cancel, undo, open, or restore-from-dump.",
                "Agent mirror commands can reproduce the key state without opening the UI."
            ],
            RecommendedRecordPath = "BuildLogs/map_ux_walkthrough.json",
            FollowUpCommands =
            [
                ".\\tools.ps1 map ux-walkthrough --summary --out BuildLogs\\map_ux_walkthrough.json -NoBuild",
                ".\\tools.ps1 map ux-audit --summary -NoBuild",
                ".\\tools.ps1 map import --summary -NoBuild",
                ".\\tools.ps1 map validate --summary -NoBuild",
                ".\\tools.ps1 handoff --summary -NoBuild"
            ]
        };
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

    private static string FormatUxWalkthroughSummary(MapUxWalkthroughReport report)
    {
        var lines = new List<string>
        {
            "MapEditor UX walkthrough",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Kind: {report.WalkthroughKind}",
            $"Project file: {(report.ProjectFileExists ? "ok" : "missing")}",
            $"Static audit: {(report.StaticAuditOk ? "OK" : "FAILED")} blocking={report.StaticAuditBlockingIssueCount} warnings={report.StaticAuditWarningCount}",
            $"Counts: maps={report.MapCount} portals={report.PortalCount} mapsWithoutPortals={report.MapsWithoutPortalsCount} steps={report.StepCount}",
            $"Record path: {report.RecommendedRecordPath}",
            "Purpose: " + report.Purpose
        };

        AddSummaryList(lines, "Sample scenes", report.SampleScenes);
        lines.Add("Steps:");
        foreach (var step in report.Steps.OrderBy(x => x.Order))
        {
            lines.Add($"  {step.Order}. {step.Id}: {step.Action}");
            lines.Add($"     expect: {step.ExpectedResult}");
            lines.Add($"     mirror: {step.AgentMirrorCommand}");
        }

        lines.Add("Acceptance criteria:");
        foreach (var criterion in report.AcceptanceCriteria)
            lines.Add("  " + criterion);

        lines.Add("Follow-up commands:");
        foreach (var command in report.FollowUpCommands)
            lines.Add("  " + command);

        return string.Join(Environment.NewLine, lines);
    }

    private static MapUxReviewResult BuildUxReviewResult(string godotRoot, string input, Dictionary<string, string> opts)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var absoluteInput = Path.IsPathRooted(input) ? input : Path.Combine(godotRoot, input);
        var hasReviewInput = File.Exists(absoluteInput) && LooksLikeUxReviewResult(absoluteInput);
        if (hasReviewInput && !HasNewUxReviewInput(opts))
        {
            var existing = JsonSerializer.Deserialize<MapUxReviewResult>(File.ReadAllText(absoluteInput), JsonOptions.Default)
                ?? throw new InvalidDataException("Failed to parse UX review result JSON.");
            RecomputeUxReviewResult(existing);
            existing.ProjectRoot = godotRoot;
            existing.InputPath = input;
            return existing;
        }

        var walkthrough = File.Exists(absoluteInput) && !hasReviewInput
            ? JsonSerializer.Deserialize<MapUxWalkthroughReport>(File.ReadAllText(absoluteInput), JsonOptions.Default)
            : BuildUxWalkthroughReport(godotRoot);
        if (walkthrough == null)
            throw new InvalidDataException("Failed to parse UX walkthrough JSON.");

        var stepResults = ParseStepResults(opts.GetValueOrDefault("step-results", ""));
        var defaultResult = NormalizeUxResult(opts.GetValueOrDefault("result", "pending"));
        var reviewer = opts.GetValueOrDefault("reviewer", "");
        var notes = opts.GetValueOrDefault("notes", "");
        var reviewedAt = opts.GetValueOrDefault("reviewed-at", DateTimeOffset.UtcNow.ToString("O"));

        var steps = walkthrough.Steps
            .OrderBy(x => x.Order)
            .Select(step =>
            {
                var result = stepResults.TryGetValue(step.Id, out var explicitResult) ? explicitResult : defaultResult;
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

        var passed = steps.Count(x => x.Result.Equals("pass", StringComparison.OrdinalIgnoreCase));
        var failed = steps.Count(x => x.Result.Equals("fail", StringComparison.OrdinalIgnoreCase));
        var partial = steps.Count(x => x.Result.Equals("partial", StringComparison.OrdinalIgnoreCase));
        var pending = steps.Count(x => x.Result.Equals("pending", StringComparison.OrdinalIgnoreCase));
        var reviewerProvided = !string.IsNullOrWhiteSpace(reviewer);
        var overall = NormalizeUxResult(defaultResult);
        var complete = steps.Count > 0 && pending == 0 && reviewerProvided && !overall.Equals("pending", StringComparison.OrdinalIgnoreCase);
        var ok = complete && failed == 0 && partial == 0 && steps.All(x => x.Passed) && overall.Equals("pass", StringComparison.OrdinalIgnoreCase);

        var issues = new List<string>();
        if (!reviewerProvided)
            issues.Add("Missing --reviewer.");
        if (overall.Equals("pending", StringComparison.OrdinalIgnoreCase))
            issues.Add("Overall --result is pending.");
        if (pending > 0)
            issues.Add($"{pending} UX walkthrough step(s) are still pending.");
        if (partial > 0)
            issues.Add($"{partial} UX walkthrough step(s) are marked partial.");
        if (failed > 0)
            issues.Add($"{failed} UX walkthrough step(s) are marked fail.");
        if (!walkthrough.ProjectFileExists)
            issues.Add("Project file was missing when the walkthrough was generated.");
        if (!walkthrough.StaticAuditOk)
            issues.Add("Static UX audit is not OK.");

        return new MapUxReviewResult
        {
            ProjectRoot = godotRoot,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ReviewKind = "human-live-ux-result",
            InputPath = input,
            Reviewer = reviewer,
            ReviewedAtUtc = reviewedAt,
            OverallResult = overall,
            Notes = notes,
            ProjectFileExists = walkthrough.ProjectFileExists,
            StaticAuditOk = walkthrough.StaticAuditOk,
            StepCount = steps.Count,
            PassedStepCount = passed,
            PartialStepCount = partial,
            FailedStepCount = failed,
            PendingStepCount = pending,
            Complete = complete,
            Ok = ok,
            IssueCount = issues.Count,
            Issues = issues,
            Steps = steps,
            VerificationCommand = ".\\tools.ps1 map ux-review --summary --in BuildLogs\\map_ux_walkthrough.json --out BuildLogs\\map_ux_review_result.json --reviewer <name> --result pass --step-results \"launch=pass;import=pass;inspect=pass;edit-preview=pass;save-review=pass;error-recovery=pass;agent-mirror=pass\" -NoBuild"
        };
    }

    private static bool HasNewUxReviewInput(Dictionary<string, string> opts) =>
        opts.ContainsKey("reviewer") ||
        opts.ContainsKey("result") ||
        opts.ContainsKey("step-results") ||
        opts.ContainsKey("notes") ||
        opts.ContainsKey("reviewed-at");

    private static bool LooksLikeUxReviewResult(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("reviewKind", out _);
        }
        catch
        {
            return false;
        }
    }

    private static void RecomputeUxReviewResult(MapUxReviewResult report)
    {
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
            issues.Add($"{report.PendingStepCount} UX walkthrough step(s) are still pending.");
        if (report.PartialStepCount > 0)
            issues.Add($"{report.PartialStepCount} UX walkthrough step(s) are marked partial.");
        if (report.FailedStepCount > 0)
            issues.Add($"{report.FailedStepCount} UX walkthrough step(s) are marked fail.");
        report.Issues = issues;
        report.IssueCount = issues.Count;
    }

    private static Dictionary<string, string> ParseStepResults(string raw)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return results;

        foreach (var part in raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2 || string.IsNullOrWhiteSpace(pieces[0]))
                continue;
            results[pieces[0]] = NormalizeUxResult(pieces[1]);
        }
        return results;
    }

    private static string NormalizeUxResult(string value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized is "pass" or "partial" or "fail" or "pending" ? normalized : "pending";
    }

    private static string FormatUxReviewSummary(MapUxReviewResult report)
    {
        var lines = new List<string>
        {
            "MapEditor UX review result",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Kind: {report.ReviewKind}",
            $"Input: {report.InputPath}",
            $"Output: {report.OutputPath}",
            $"Reviewer: {(string.IsNullOrWhiteSpace(report.Reviewer) ? "missing" : report.Reviewer)}",
            $"Overall: {(report.Ok ? "OK" : "NOT ACCEPTED")} result={report.OverallResult} complete={report.Complete.ToString().ToLowerInvariant()} issues={report.IssueCount}",
            $"Counts: steps={report.StepCount} pass={report.PassedStepCount} partial={report.PartialStepCount} fail={report.FailedStepCount} pending={report.PendingStepCount}",
            $"Static audit: {(report.StaticAuditOk ? "OK" : "FAILED")} projectFile={(report.ProjectFileExists ? "ok" : "missing")}"
        };

        AddSummaryList(lines, "Issues", report.Issues);
        lines.Add("Steps:");
        foreach (var step in report.Steps.OrderBy(x => x.Order))
        {
            lines.Add($"  {step.Order}. {step.Id}: {step.Result}");
            lines.Add($"     expect: {step.ExpectedResult}");
            lines.Add($"     mirror: {step.AgentMirrorCommand}");
        }

        lines.Add("Verification command:");
        lines.Add("  " + report.VerificationCommand);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddSummaryList(List<string> lines, string title, IReadOnlyCollection<string> values)
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

    private static MapPortalReview BuildPortalReview(string godotRoot)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var project = GodotMapImporter.ImportFromGodot(godotRoot);
        var mapScenes = project.Maps
            .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath) && x.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var linkedPortalIds = project.Links
            .Select(link => $"{link.From.MapId}\n{link.From.PortalId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            .SelectMany(map => map.Portals.Select(portal => new { Map = map, Portal = portal }))
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Portal.TargetMapId) &&
                !linkedPortalIds.Contains($"{x.Map.Id}\n{x.Portal.Id}"))
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

    private static (string Category, string Confidence, string Reason, string Recommendation) ClassifyMapWithoutPortals(
        string scenePath,
        string displayName,
        int incomingLinkCount,
        int outgoingLinkCount,
        string sceneText)
    {
        var name = Path.GetFileNameWithoutExtension(scenePath);
        var key = (string.IsNullOrWhiteSpace(displayName) ? name : displayName).ToLowerInvariant();
        var text = sceneText ?? "";

        if (incomingLinkCount > 0 || outgoingLinkCount > 0)
        {
            return (
                "map-graph-gap",
                "high",
                "Scene participates in the imported link graph but has no imported Portal nodes.",
                "Add or repair the missing Portal nodes before accepting this map graph.");
        }

        if (key.Contains("ending", StringComparison.Ordinal) || key.EndsWith("end", StringComparison.Ordinal) || text.Contains("END", StringComparison.Ordinal))
        {
            return (
                "terminal-candidate",
                "medium",
                "Name or scene content indicates an ending/terminal room while the link graph is isolated.",
                "Human review should confirm this room is intentionally terminal.");
        }

        if (key.Contains("loop", StringComparison.Ordinal) || text.Contains("LoopScript.gd", StringComparison.Ordinal))
        {
            return (
                "dynamic-scripted-candidate",
                "medium",
                "Scene appears to use scripted loop transitions that may not be represented as static Portal nodes.",
                "Verify the scripted transition behavior before adding static portals.");
        }

        if (key.Contains("test", StringComparison.Ordinal) || key.Contains("demo", StringComparison.Ordinal) || key.Contains("template", StringComparison.Ordinal) || key.Contains("sample", StringComparison.Ordinal))
        {
            return (
                "test-helper-candidate",
                "medium",
                "Name suggests a test, demo, template, or helper scene.",
                "Confirm this scene is not part of the shipping traversal graph.");
        }

        if (text.Contains("SavePoint.gd", StringComparison.Ordinal) || key.Contains("save", StringComparison.Ordinal))
        {
            return (
                "utility-room-candidate",
                "low",
                "Scene contains utility/save-point logic but no imported Portal nodes.",
                "Check whether the room is reached dynamically or should receive portal links.");
        }

        if (key.Contains("corridor", StringComparison.Ordinal) ||
            key.Contains("junction", StringComparison.Ordinal) ||
            key.Contains("staircase", StringComparison.Ordinal) ||
            key.Contains("relic", StringComparison.Ordinal) ||
            text.Contains("TileMapLayer", StringComparison.Ordinal))
        {
            return (
                "playable-isolated-candidate",
                "low",
                "Scene looks like a playable room but has no static portals and no imported links.",
                "Human review should decide whether this is intentional, dynamically connected, or a map-graph gap.");
        }

        return (
            "unclassified-isolated-map",
            "low",
            "Scene has no imported Portal nodes or links, and no stronger heuristic matched.",
            "Human review should classify this scene before mutating the map graph.");
    }

    private static MapRuntimeVerificationReport BuildRuntimeVerificationReport(string godotRoot)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var project = GodotMapImporter.ImportFromGodot(godotRoot);
        var mapScenes = project.Maps
            .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath) && x.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var mapSceneSet = mapScenes
            .Select(x => x.ScenePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uidIndex = BuildSceneUidIndex(godotRoot);

        var checks = new List<MapRuntimeCheck>();
        AddTextCheck(checks, godotRoot, "portal-script-exists", "CoreEngine/Scripts/World/Portal.gd", text => true, "Portal script exists.");
        AddTextCheck(checks, godotRoot, "portal-exports-target-map", "CoreEngine/Scripts/World/Portal.gd",
            text => text.Contains("@export_file(\"room_link\") var target_map", StringComparison.Ordinal),
            "Portal exports target_map with room_link picker.");
        AddTextCheck(checks, godotRoot, "portal-sends-load-room-request", "CoreEngine/Scripts/World/Portal.gd",
            text => text.Contains("TYPE_LOAD_ROOM_REQUEST", StringComparison.Ordinal) && text.Contains("\"target_map\": target_map", StringComparison.Ordinal),
            "Portal sends TYPE_LOAD_ROOM_REQUEST with target_map.");
        AddTextCheck(checks, godotRoot, "room-flow-handles-load-room-request", "CoreEngine/Scripts/Actor/RoomFlowActor.gd",
            text => text.Contains("TYPE_LOAD_ROOM_REQUEST", StringComparison.Ordinal) && text.Contains("msg.get(\"target_map\"", StringComparison.Ordinal),
            "RoomFlowActor handles TYPE_LOAD_ROOM_REQUEST and reads target_map.");
        AddTextCheck(checks, godotRoot, "room-flow-calls-game-load-room", "CoreEngine/Scripts/Actor/RoomFlowActor.gd",
            text => text.Contains("game.load_room(target)", StringComparison.Ordinal),
            "RoomFlowActor calls game.load_room(target).");
        AddTextCheck(checks, godotRoot, "metsys-load-room-exists", "addons/MetroidvaniaSystem/Template/Scripts/MetSysGame.gd",
            text => text.Contains("func load_room(path", StringComparison.Ordinal),
            "MetSysGame exposes load_room(path).");
        AddTextCheck(checks, godotRoot, "game-declares-initial-room", "CoreEngine/Scripts/Systems/Game.gd",
            text => text.Contains("INITIAL_ROOM_PATH", StringComparison.Ordinal),
            "Game.gd declares INITIAL_ROOM_PATH.");
        AddTextCheck(checks, godotRoot, "game-exports-starting-map", "CoreEngine/Scripts/Systems/Game.gd",
            text => text.Contains("@export_file(\"room_link\") var starting_map", StringComparison.Ordinal),
            "Game.gd exports starting_map with room_link picker.");
        AddTextCheck(checks, godotRoot, "area-catalog-starting-rooms", "CoreEngine/Scripts/World/AreaCatalog.gd",
            text => ExtractResPaths(text).Any(x => x.StartsWith("res://CoreEngine/Maps/", StringComparison.Ordinal)),
            "AreaCatalog.gd references area starting rooms.");

        var entryRooms = BuildRuntimeEntryRooms(godotRoot, uidIndex, mapSceneSet);
        foreach (var entry in entryRooms)
        {
            if (!entry.Exists || !entry.InImportedMapGraph)
            {
                checks.Add(new MapRuntimeCheck
                {
                    Id = "entry-room-" + SanitizeCheckId(entry.Source),
                    Passed = false,
                    Path = entry.ResolvedPath,
                    Detail = $"{entry.Source} entry room does not resolve to an imported map scene."
                });
            }
        }

        var portalTargets = BuildRuntimePortalTargets(project, uidIndex, mapSceneSet);
        foreach (var target in portalTargets.Where(x => !x.ResolvesToImportedMap))
        {
            checks.Add(new MapRuntimeCheck
            {
                Id = "portal-target-" + SanitizeCheckId(target.FromMapPath + "-" + target.PortalId),
                Passed = false,
                Path = target.FromMapPath,
                Detail = $"Portal target does not resolve to an imported map: {target.RawTargetMap}"
            });
        }

        var issues = checks.Where(x => !x.Passed).ToList();
        return new MapRuntimeVerificationReport
        {
            ProjectRoot = godotRoot,
            ProjectFileExists = File.Exists(Path.Combine(godotRoot, "project.godot")),
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            VerificationKind = "static-game-effect",
            ProofScope = "Static verifier: checks that imported map/portal data lines up with CoreEngine script surfaces that consume target_map and load rooms. It does not execute a live player transition.",
            MapCount = mapScenes.Count,
            PortalCount = project.Maps.Sum(x => x.Portals.Count),
            LinkCount = project.Links.Count,
            PortalTargetCount = portalTargets.Count,
            ResolvedPortalTargetCount = portalTargets.Count(x => x.ResolvesToImportedMap),
            EntryRoomCount = entryRooms.Count,
            ResolvedEntryRoomCount = entryRooms.Count(x => x.Exists && x.InImportedMapGraph),
            CheckCount = checks.Count,
            IssueCount = issues.Count,
            Ok = File.Exists(Path.Combine(godotRoot, "project.godot")) && issues.Count == 0,
            Checks = checks,
            EntryRooms = entryRooms,
            PortalTargets = portalTargets.Take(100).ToList(),
            Issues = issues.Select(x => x.Detail).Take(50).ToList()
        };
    }

    private static void AddTextCheck(
        List<MapRuntimeCheck> checks,
        string godotRoot,
        string id,
        string relativePath,
        Func<string, bool> predicate,
        string detail)
    {
        var path = Path.Combine(godotRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            checks.Add(new MapRuntimeCheck
            {
                Id = id,
                Passed = false,
                Path = relativePath,
                Detail = detail + " File missing."
            });
            return;
        }

        var text = File.ReadAllText(path);
        checks.Add(new MapRuntimeCheck
        {
            Id = id,
            Passed = predicate(text),
            Path = relativePath,
            Detail = detail
        });
    }

    private static List<MapRuntimeEntryRoom> BuildRuntimeEntryRooms(string godotRoot, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
    {
        var entries = new List<MapRuntimeEntryRoom>();
        var gamePath = Path.Combine(godotRoot, "CoreEngine", "Game.tscn");
        if (File.Exists(gamePath))
        {
            var scene = TscnParser.ParseFile(gamePath);
            var gameNode = scene.Nodes.FirstOrDefault(x => string.Equals(x.Name, "Game", StringComparison.Ordinal));
            if (gameNode?.RawProps.TryGetValue("starting_map", out var startingMap) == true)
            {
                entries.Add(BuildRuntimeEntryRoom("Game.tscn starting_map", UnquoteGodotValue(startingMap), godotRoot, uidIndex, mapSceneSet));
            }
        }

        var gameScriptPath = Path.Combine(godotRoot, "CoreEngine", "Scripts", "Systems", "Game.gd");
        if (File.Exists(gameScriptPath))
        {
            var initialRoom = ExtractConstString(File.ReadAllText(gameScriptPath), "INITIAL_ROOM_PATH");
            if (!string.IsNullOrWhiteSpace(initialRoom))
                entries.Add(BuildRuntimeEntryRoom("Game.gd INITIAL_ROOM_PATH", initialRoom, godotRoot, uidIndex, mapSceneSet));
        }

        var areaCatalogPath = Path.Combine(godotRoot, "CoreEngine", "Scripts", "World", "AreaCatalog.gd");
        if (File.Exists(areaCatalogPath))
        {
            foreach (var path in ExtractResPaths(File.ReadAllText(areaCatalogPath))
                .Where(x => x.StartsWith("res://CoreEngine/Maps/", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(BuildRuntimeEntryRoom("AreaCatalog.gd starting_room", path, godotRoot, uidIndex, mapSceneSet));
            }
        }

        return entries;
    }

    private static MapRuntimeEntryRoom BuildRuntimeEntryRoom(string source, string rawValue, string godotRoot, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
    {
        var resolved = ResolveRuntimeResPath(rawValue, uidIndex);
        return new MapRuntimeEntryRoom
        {
            Source = source,
            RawValue = rawValue,
            ResolvedPath = resolved,
            Exists = !string.IsNullOrWhiteSpace(resolved) && File.Exists(ToAbsoluteGodotPath(godotRoot, resolved)),
            InImportedMapGraph = !string.IsNullOrWhiteSpace(resolved) && mapSceneSet.Contains(resolved)
        };
    }

    private static List<MapRuntimePortalTarget> BuildRuntimePortalTargets(Models.MapProject project, Dictionary<string, string> uidIndex, HashSet<string> mapSceneSet)
    {
        return project.Maps
            .SelectMany(map => map.Portals.Select(portal =>
            {
                var raw = portal.TargetMapId?.Trim() ?? "";
                var resolved = ResolveRuntimeResPath(raw, uidIndex);
                return new MapRuntimePortalTarget
                {
                    FromMapPath = map.ScenePath,
                    PortalId = portal.Id,
                    PortalName = portal.Name,
                    RawTargetMap = raw,
                    ResolvedTargetMap = resolved,
                    ResolvesToImportedMap = !string.IsNullOrWhiteSpace(resolved) && mapSceneSet.Contains(resolved)
                };
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x.RawTargetMap))
            .OrderBy(x => x.FromMapPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PortalName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> BuildSceneUidIndex(string godotRoot)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(godotRoot, "*.tscn", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var scene = TscnParser.ParseFile(file);
                if (string.IsNullOrWhiteSpace(scene.SceneUid))
                    continue;
                var rel = Path.GetRelativePath(godotRoot, file).Replace('\\', '/');
                index[scene.SceneUid] = "res://" + rel;
            }
            catch
            {
                // Ignore malformed or transient files in generated folders; verifier reports runtime wiring, not parser health.
            }
        }
        return index;
    }

    private static string ResolveRuntimeResPath(string rawValue, Dictionary<string, string> uidIndex)
    {
        var value = rawValue.Trim();
        if (value.StartsWith("uid://", StringComparison.OrdinalIgnoreCase))
            return uidIndex.GetValueOrDefault(value, "");
        if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return value;
        return "";
    }

    private static string ExtractConstString(string text, string constName)
    {
        var match = Regex.Match(text, @"const\s+" + Regex.Escape(constName) + @"\s*:\s*\w+\s*=\s*""(?<value>[^""]+)""");
        return match.Success ? match.Groups["value"].Value : "";
    }

    private static List<string> ExtractResPaths(string text)
    {
        return Regex.Matches(text, @"""(?<path>res://[^""]+)""")
            .Select(x => x.Groups["path"].Value)
            .ToList();
    }

    private static string UnquoteGodotValue(string raw)
    {
        raw = raw.Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw[1..^1];
        return raw;
    }

    private static string SanitizeCheckId(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();
        return string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries)).Trim('-');
    }

    private static string FormatRuntimeVerificationSummary(MapRuntimeVerificationReport report)
    {
        var lines = new List<string>
        {
            "MapEditor runtime verify",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Kind: {report.VerificationKind}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} issues={report.IssueCount}",
            $"Counts: maps={report.MapCount} links={report.LinkCount} portals={report.PortalCount} portalTargets={report.ResolvedPortalTargetCount}/{report.PortalTargetCount} entryRooms={report.ResolvedEntryRoomCount}/{report.EntryRoomCount} checks={report.CheckCount}",
            "Scope: " + report.ProofScope,
            "Runtime checks:"
        };

        foreach (var check in report.Checks)
            lines.Add($"  {(check.Passed ? "OK" : "FAIL")} {check.Id} - {check.Detail}");

        lines.Add("Entry rooms:");
        if (report.EntryRooms.Count == 0)
            lines.Add("  none");
        foreach (var entry in report.EntryRooms)
            lines.Add($"  {(entry.Exists && entry.InImportedMapGraph ? "OK" : "FAIL")} {entry.Source}: {entry.RawValue} -> {entry.ResolvedPath}");

        lines.Add("Issues:");
        if (report.Issues.Count == 0)
            lines.Add("  none");
        foreach (var issue in report.Issues)
            lines.Add("  " + issue);

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPortalReviewSummary(MapPortalReview review)
    {
        var lines = new List<string>
        {
            "MapEditor portal review",
            $"Project: {review.ProjectRoot}",
            $"Generated UTC: {review.GeneratedAtUtc}",
            $"Project file: {(review.ProjectFileExists ? "ok" : "missing")}",
            $"Counts: maps={review.MapCount} portals={review.PortalCount} links={review.LinkCount}",
            $"Review: mapsWithoutPortals={review.MapsWithoutPortalsCount} portalsWithMissingTargets={review.PortalsWithMissingTargetsCount}",
            "Classification: " + FormatCounts(review.PortalCoverageClassifications),
            "Maps without portals:"
        };

        if (review.MapsWithoutPortals.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var item in review.MapsWithoutPortals)
        {
            lines.Add($"  {item.ScenePath} incoming={item.IncomingLinkCount} outgoing={item.OutgoingLinkCount}");
            lines.Add($"    class: {item.CoverageClassification} confidence={item.ClassificationConfidence}");
            lines.Add($"    reason: {item.ClassificationReason}");
            lines.Add($"    recommendation: {item.Recommendation}");
        }

        lines.Add("Portals with missing targets:");
        if (review.PortalsWithMissingTargets.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var item in review.PortalsWithMissingTargets)
        {
            lines.Add($"  {item.ScenePath} -> {item.TargetMapId}");
            lines.Add($"    recommendation: {item.Recommendation}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCounts(IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
            return "none";

        return string.Join(" ", counts
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key}={x.Value}"));
    }

    private static MapEditorStatus BuildStatus(string godotRoot)
    {
        godotRoot = Path.GetFullPath(godotRoot);
        var project = GodotMapImporter.ImportFromGodot(godotRoot);
        var mapScenes = project.Maps
            .Where(x => !string.IsNullOrWhiteSpace(x.ScenePath) && x.ScenePath.StartsWith("res://", StringComparison.Ordinal))
            .OrderBy(x => x.ScenePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
            .Select(link => $"{link.From.MapId}->{link.To.MapId}")
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

    private static int RunImport(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var output = opts.GetValueOrDefault("out", "");
        if (string.IsNullOrWhiteSpace(output))
            output = Path.Combine(Environment.CurrentDirectory, "map_project.json");

        var project = GodotMapImporter.ImportFromGodot(godotRoot);
        var json = JsonSerializer.Serialize(project, JsonOptions.Default);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, json);

        if (opts.ContainsKey("summary"))
            Console.WriteLine(FormatImportSummary(godotRoot, output, project));
        else
        {
            Console.WriteLine($"Imported {project.Maps.Count} maps, {project.Links.Count} links.");
            Console.WriteLine(output);
        }
        return 0;
    }

    private static string FormatImportSummary(string godotRoot, string output, Models.MapProject project)
    {
        var lines = new List<string>
        {
            "MapEditor import",
            $"Project: {Path.GetFullPath(godotRoot)}",
            $"Generated UTC: {DateTimeOffset.UtcNow:O}",
            $"Output: {output}",
            $"Counts: maps={project.Maps.Count} links={project.Links.Count} portals={project.Maps.Sum(x => x.Portals.Count)} tileLayers={project.Maps.Sum(x => x.TileLayers.Count)} entities={project.Maps.Sum(x => x.Entities.Count)}",
            "Sample scenes:"
        };

        var sampleScenes = project.Maps
            .Select(x => x.ScenePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(10)
            .ToList();
        if (sampleScenes.Count == 0)
            lines.Add("  none");
        foreach (var scene in sampleScenes)
            lines.Add("  " + scene);

        return string.Join(Environment.NewLine, lines);
    }

    private static int RunValidate(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var input = opts.GetValueOrDefault("in", "");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            throw new FileNotFoundException("Input project JSON not found.", input);

        var json = File.ReadAllText(input);
        var loaded = JsonSerializer.Deserialize<Models.MapProject>(json, JsonOptions.Default);
        if (loaded == null)
            throw new InvalidDataException("Failed to parse input JSON.");

        var scanned = GodotMapImporter.ImportFromGodot(godotRoot);

        var report = BuildValidationReport(godotRoot, input, loaded, scanned);

        if (opts.ContainsKey("summary"))
        {
            Console.WriteLine(FormatValidationSummary(report));
        }
        else
        {
            Console.WriteLine($"Loaded maps: {report.LoadedMapCount}, scanned maps: {report.ScannedMapCount}");
            if (report.MissingInGodot.Count > 0)
            {
                Console.WriteLine("Missing in Godot:");
                foreach (var m in report.MissingInGodot)
                    Console.WriteLine($"  {m}");
            }
            if (report.ExtraInGodot.Count > 0)
            {
                Console.WriteLine("Extra in Godot:");
                foreach (var e in report.ExtraInGodot)
                    Console.WriteLine($"  {e}");
            }
        }

        return report.Ok ? 0 : 1;
    }

    private static MapValidationReport BuildValidationReport(
        string godotRoot,
        string input,
        Models.MapProject loaded,
        Models.MapProject scanned)
    {
        var missing = loaded.Maps
            .Select(m => m.ScenePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .Except(scanned.Maps.Select(m => m.ScenePath))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var extra = scanned.Maps
            .Select(m => m.ScenePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .Except(loaded.Maps.Select(m => m.ScenePath))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MapValidationReport
        {
            ProjectRoot = Path.GetFullPath(godotRoot),
            InputPath = input,
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

    private static string FormatValidationSummary(MapValidationReport report)
    {
        var lines = new List<string>
        {
            "MapEditor validate",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Input: {report.InputPath}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} missing={report.MissingInGodotCount} extra={report.ExtraInGodotCount}",
            $"Counts: loadedMaps={report.LoadedMapCount} scannedMaps={report.ScannedMapCount}"
        };

        AddSummaryList(lines, "Missing in Godot", report.MissingInGodot);
        AddSummaryList(lines, "Extra in Godot", report.ExtraInGodot);
        return string.Join(Environment.NewLine, lines);
    }

    private static int RunPatchPos(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var scenePath = opts.GetValueOrDefault("scene", "");
        if (string.IsNullOrWhiteSpace(scenePath))
            throw new ArgumentException("Missing --scene <res://...>.");

        var nodePath = opts.GetValueOrDefault("nodePath", "");
        if (string.IsNullOrWhiteSpace(nodePath))
            throw new ArgumentException("Missing --nodePath <path>.");

        if (!float.TryParse(opts.GetValueOrDefault("x", ""), out var x))
            throw new ArgumentException("Invalid --x.");
        if (!float.TryParse(opts.GetValueOrDefault("y", ""), out var y))
            throw new ArgumentException("Invalid --y.");

        var abs = ToAbsoluteGodotPath(godotRoot, scenePath);
        var scene = TscnParser.ParseFile(abs);

        var node = scene.Nodes.FirstOrDefault(n => ComputeNodePath(n.Parent, n.Name) == nodePath);
        if (node == null)
            throw new InvalidOperationException($"Node not found: {nodePath}");

        node.RawProps["position"] = FormatVector2(x, y);
        var dirty = TscnWriter.PatchFile(abs, scene, ["position"]);
        Console.WriteLine(dirty ? "patched" : "no changes");
        Console.WriteLine(abs);
        return 0;
    }

    private static int RunTraceAlpha(Dictionary<string, string> opts)
    {
        var input = opts.GetValueOrDefault("in", "");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            throw new FileNotFoundException("Input image not found.", input);

        var threshold = 254;
        if (int.TryParse(opts.GetValueOrDefault("threshold", ""), out var t))
            threshold = Math.Clamp(t, 0, 254);

        using var bmp = new Bitmap(input);
        using var bmp32 = Ensure32bppArgb(bmp);

        var worldW = bmp32.Width;
        var worldH = bmp32.Height;
        if (int.TryParse(opts.GetValueOrDefault("worldW", ""), out var ww))
            worldW = Math.Max(1, ww);
        if (int.TryParse(opts.GetValueOrDefault("worldH", ""), out var wh))
            worldH = Math.Max(1, wh);

        var mi = typeof(MainForm).GetMethod("BuildCollisionPolygonsFromAlpha", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (mi == null)
            throw new MissingMethodException("MainForm.BuildCollisionPolygonsFromAlpha not found.");

        var polys = (List<List<GodotVector2>>?)mi.Invoke(null, [bmp32, worldW, worldH, threshold]);
        polys ??= [];

        Console.WriteLine($"polygons={polys.Count}");
        if (polys.Count > 0)
        {
            Console.WriteLine($"poly0_points={polys[0].Count}");
            var n = Math.Min(5, polys[0].Count);
            for (var i = 0; i < n; i++)
                Console.WriteLine($"p{i}={polys[0][i].X:0.###},{polys[0][i].Y:0.###}");
        }

        return 0;
    }

    private static int RunPortalAnim(Dictionary<string, string> opts)
    {
        var godotRoot = opts.GetValueOrDefault("godotRoot", "");
        if (string.IsNullOrWhiteSpace(godotRoot))
            godotRoot = GodotProjectLocator.FindGodotRoot(Environment.CurrentDirectory);

        var input = opts.GetValueOrDefault("in", "");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            throw new FileNotFoundException("Input mp4 not found.", input);

        var outDirOpt = opts.GetValueOrDefault("outDir", "");
        var pattern = opts.GetValueOrDefault("pattern", "frame_%03d.png");

        var absOutDir = ResolveOutDir(godotRoot, input, outDirOpt);
        Directory.CreateDirectory(absOutDir);

        var ffmpeg = FindBundledTool("ffmpeg.exe");
        var args = $"-y -hide_banner -loglevel error -i \"{input}\" -vsync 0 -start_number 0 \"{Path.Combine(absOutDir, pattern)}\"";
        RunProcessChecked(ffmpeg, args);

        var resOutDir = TryMakeResPath(godotRoot, absOutDir);
        Console.WriteLine(resOutDir);
        return 0;
    }

    private static string ResolveOutDir(string godotRoot, string inputMp4, string outDirOpt)
    {
        if (!string.IsNullOrWhiteSpace(outDirOpt))
        {
            outDirOpt = outDirOpt.Trim();
            if (outDirOpt.StartsWith("res://", StringComparison.Ordinal))
                return ToAbsoluteGodotPath(godotRoot, outDirOpt);
            if (Path.IsPathRooted(outDirOpt))
                return outDirOpt;
            return Path.GetFullPath(outDirOpt);
        }

        var baseName = Path.GetFileNameWithoutExtension(inputMp4);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "PortalAnim";
        baseName = string.Concat(baseName.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "PortalAnim";

        return Path.Combine(godotRoot, "CoreEngine", "Resources", "PortalAnimations", baseName);
    }

    private static string FindBundledTool(string fileName)
    {
        var starts = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };

        foreach (var s in starts)
        {
            var dir = s;
            for (var i = 0; i < 12 && !string.IsNullOrWhiteSpace(dir); i++)
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
        }

        throw new FileNotFoundException($"Bundled tool not found: {fileName}");
    }

    private static void RunProcessChecked(string exePath, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Process failed: {Path.GetFileName(exePath)} exit={p.ExitCode}\n{stdout}\n{stderr}");
    }

    private static Bitmap Ensure32bppArgb(Bitmap src)
    {
        if (src.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb || src.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
            return (Bitmap)src.Clone();

        var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
        return dst;
    }

    private static string ToAbsoluteGodotPath(string godotRoot, string resPath)
    {
        var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath["res://".Length..] : resPath;
        rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(godotRoot, rel);
    }

    private static string TryReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch
        {
            return "";
        }
    }

    private static string TryMakeResPath(string godotRoot, string absPath)
    {
        if (!Path.IsPathRooted(absPath))
            return absPath;
        var rel = Path.GetRelativePath(godotRoot, absPath);
        if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            return "res://" + rel.Replace('\\', '/');
        return absPath;
    }

    private static string ComputeNodePath(string? parent, string name)
    {
        parent = parent?.Trim();
        if (string.IsNullOrWhiteSpace(parent) || parent == ".")
            return name;
        return parent.Trim('/') + "/" + name;
    }

    private static string FormatVector2(float x, float y)
    {
        return $"Vector2({x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, {y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
                continue;
            var key = a[2..];
            var val = "";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                val = args[i + 1];
                i++;
            }
            d[key] = val;
        }
        return d;
    }
}

public sealed class MapEditorStatus
{
    public string ProjectRoot { get; set; } = "";
    public bool ProjectFileExists { get; set; }
    public string GeneratedAtUtc { get; set; } = "";
    public int MapCount { get; set; }
    public int LinkCount { get; set; }
    public int PortalCount { get; set; }
    public int TileLayerCount { get; set; }
    public int EntityCount { get; set; }
    public int MissingSceneCount { get; set; }
    public int MapsWithoutPortalsCount { get; set; }
    public int LinksWithMissingTargetsCount { get; set; }
    public List<string> SampleScenes { get; set; } = [];
    public List<string> MissingScenes { get; set; } = [];
    public List<string> MapsWithoutPortals { get; set; } = [];
    public List<string> LinksWithMissingTargets { get; set; } = [];
}

public sealed class MapPortalReview
{
    public string ProjectRoot { get; set; } = "";
    public bool ProjectFileExists { get; set; }
    public string GeneratedAtUtc { get; set; } = "";
    public int MapCount { get; set; }
    public int PortalCount { get; set; }
    public int LinkCount { get; set; }
    public int MapsWithoutPortalsCount { get; set; }
    public int PortalsWithMissingTargetsCount { get; set; }
    public Dictionary<string, int> PortalCoverageClassifications { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MapPortalReviewItem> MapsWithoutPortals { get; set; } = [];
    public List<MapPortalReviewItem> PortalsWithMissingTargets { get; set; } = [];
}

public sealed class MapPortalReviewItem
{
    public string Id { get; set; } = "";
    public string ScenePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int PortalCount { get; set; }
    public int IncomingLinkCount { get; set; }
    public int OutgoingLinkCount { get; set; }
    public string TargetMapId { get; set; } = "";
    public string TargetPortalId { get; set; } = "";
    public string CoverageClassification { get; set; } = "";
    public string ClassificationConfidence { get; set; } = "";
    public string ClassificationReason { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class MapValidationReport
{
    public string ProjectRoot { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public int LoadedMapCount { get; set; }
    public int ScannedMapCount { get; set; }
    public int MissingInGodotCount { get; set; }
    public int ExtraInGodotCount { get; set; }
    public bool Ok { get; set; }
    public List<string> MissingInGodot { get; set; } = [];
    public List<string> ExtraInGodot { get; set; } = [];
}

public sealed class MapRuntimeVerificationReport
{
    public string ProjectRoot { get; set; } = "";
    public bool ProjectFileExists { get; set; }
    public string GeneratedAtUtc { get; set; } = "";
    public string VerificationKind { get; set; } = "";
    public string ProofScope { get; set; } = "";
    public int MapCount { get; set; }
    public int PortalCount { get; set; }
    public int LinkCount { get; set; }
    public int PortalTargetCount { get; set; }
    public int ResolvedPortalTargetCount { get; set; }
    public int EntryRoomCount { get; set; }
    public int ResolvedEntryRoomCount { get; set; }
    public int CheckCount { get; set; }
    public int IssueCount { get; set; }
    public bool Ok { get; set; }
    public List<MapRuntimeCheck> Checks { get; set; } = [];
    public List<MapRuntimeEntryRoom> EntryRooms { get; set; } = [];
    public List<MapRuntimePortalTarget> PortalTargets { get; set; } = [];
    public List<string> Issues { get; set; } = [];
}

public sealed class MapRuntimeCheck
{
    public string Id { get; set; } = "";
    public bool Passed { get; set; }
    public string Path { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class MapRuntimeEntryRoom
{
    public string Source { get; set; } = "";
    public string RawValue { get; set; } = "";
    public string ResolvedPath { get; set; } = "";
    public bool Exists { get; set; }
    public bool InImportedMapGraph { get; set; }
}

public sealed class MapRuntimePortalTarget
{
    public string FromMapPath { get; set; } = "";
    public string PortalId { get; set; } = "";
    public string PortalName { get; set; } = "";
    public string RawTargetMap { get; set; } = "";
    public string ResolvedTargetMap { get; set; } = "";
    public bool ResolvesToImportedMap { get; set; }
}

public sealed class MapUxAuditReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string AuditKind { get; set; } = "";
    public string Scope { get; set; } = "";
    public int CheckCount { get; set; }
    public int PassedCount { get; set; }
    public int WarningCount { get; set; }
    public int BlockingIssueCount { get; set; }
    public bool Ok { get; set; }
    public List<MapUxAuditCheck> Checks { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}

public sealed class MapUxAuditCheck
{
    public string Category { get; set; } = "";
    public string Id { get; set; } = "";
    public string Severity { get; set; } = "";
    public bool Passed { get; set; }
    public string Evidence { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class MapUxWalkthroughReport
{
    public string ProjectRoot { get; set; } = "";
    public bool ProjectFileExists { get; set; }
    public string GeneratedAtUtc { get; set; } = "";
    public string WalkthroughKind { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool StaticAuditOk { get; set; }
    public int StaticAuditBlockingIssueCount { get; set; }
    public int StaticAuditWarningCount { get; set; }
    public int MapCount { get; set; }
    public int PortalCount { get; set; }
    public int MapsWithoutPortalsCount { get; set; }
    public int StepCount { get; set; }
    public string OutputPath { get; set; } = "";
    public bool OutputWritten { get; set; }
    public string RecommendedRecordPath { get; set; } = "";
    public List<string> SampleScenes { get; set; } = [];
    public List<MapUxWalkthroughStep> Steps { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> FollowUpCommands { get; set; } = [];
}

public sealed class MapUxWalkthroughStep
{
    public int Order { get; set; }
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string AgentMirrorCommand { get; set; } = "";
    public string HumanResult { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class MapUxReviewResult
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string ReviewKind { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public bool OutputWritten { get; set; }
    public string Reviewer { get; set; } = "";
    public string ReviewedAtUtc { get; set; } = "";
    public string OverallResult { get; set; } = "";
    public string Notes { get; set; } = "";
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
    public List<string> Issues { get; set; } = [];
    public List<MapUxReviewStepResult> Steps { get; set; } = [];
    public string VerificationCommand { get; set; } = "";
}

public sealed class MapUxReviewStepResult
{
    public int Order { get; set; }
    public string Id { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string Result { get; set; } = "";
    public bool Passed { get; set; }
    public string Notes { get; set; } = "";
    public string AgentMirrorCommand { get; set; } = "";
}
