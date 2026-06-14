using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResourceConfig;

internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Any(IsAgentSelfTest))
            {
                return RunAgentSelfTest();
            }

            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            var command = args[0].Trim().ToLowerInvariant();
            var opts = ParseOptions(args.Skip(1).ToArray());
            var root = ResolveProjectRoot(opts.GetValueOrDefault("godotRoot", ""));

            return command switch
            {
                "summary" => RunSummary(root, CreateOptions(opts)),
                "validate" => RunValidate(root, CreateOptions(opts)),
                "export-manifest" => RunExportManifest(root, opts.GetValueOrDefault("out", ""), CreateOptions(opts)),
                "export-index" => RunExportIndex(root, opts.GetValueOrDefault("out", ""), CreateOptions(opts)),
                "audit" => RunAudit(root, opts, CreateOptions(opts)),
                "plan" => RunPlan(root, opts, CreateOptions(opts)),
                "decide" => RunDecide(root, opts, CreateOptions(opts)),
                "apply" => RunApply(root, opts, CreateOptions(opts)),
                "pending" => RunPending(root, opts, CreateOptions(opts)),
                "status" => RunStatus(root, opts),
                "verify-outputs" => RunVerifyOutputs(root, opts, CreateOptions(opts)),
                "show" => RunShow(root, opts, CreateOptions(opts)),
                "find" => RunFind(root, opts, CreateOptions(opts)),
                "help" or "-h" or "--help" => RunHelp(),
                _ => RunHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ResourceConfig failed: " + ex.Message);
            return 1;
        }
    }

    private static int RunSummary(string root, ResourceScanOptions options)
    {
        var manifest = BuildManifest(root, options);
        Console.WriteLine($"projectRoot={manifest.ProjectRoot}");
        Console.WriteLine($"resourceFiles={manifest.ResourceFiles.Count}");
        Console.WriteLine($"references={manifest.References.Count}");
        Console.WriteLine($"missingReferences={manifest.MissingReferences.Count}");
        foreach (var group in manifest.ResourceFiles.GroupBy(x => x.Kind).OrderBy(x => x.Key))
        {
            Console.WriteLine($"{group.Key}={group.Count()}");
        }
        return 0;
    }

    private static int RunStatus(string root, Dictionary<string, string> opts)
    {
        var report = BuildWorkflowStatus(root, opts.GetValueOrDefault("dir", "BuildLogs"));
        Console.WriteLine(opts.ContainsKey("summary")
            ? FormatWorkflowStatusSummary(report)
            : JsonSerializer.Serialize(report, JsonOptions));
        return 0;
    }

    private static string FormatWorkflowStatusSummary(ResourceWorkflowStatus report)
    {
        var lines = new List<string>
        {
            "ResourceConfig status",
            $"Project: {report.ProjectRoot}",
            $"BuildLogs: {report.BuildLogsDir}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            FormatStatusLine("Index", report.Index.Exists, $"resources={report.Index.ResourceCount}", report.Index),
            FormatStatusLine(
                "Audit",
                report.Audit.Exists,
                $"resources={report.Audit.ResourceCount} missing={report.Audit.MissingReferenceCount} external={report.Audit.ExternalReferenceCount} unreferenced={report.Audit.UnreferencedResourceCount} duplicateNames={report.Audit.DuplicateFileNameGroupCount}",
                report.Audit),
            FormatStatusLine(
                "Plan",
                report.Plan.Exists,
                $"actions={report.Plan.ActionCount} errors={report.Plan.ErrorCount} warnings={report.Plan.WarningCount} info={report.Plan.InfoCount}",
                report.Plan),
            FormatStatusLine(
                "Decisions",
                report.Decisions.Exists,
                $"total={report.Decisions.DecisionCount} accept={report.Decisions.AcceptCount} defer={report.Decisions.DeferCount} reject={report.Decisions.RejectCount}",
                report.Decisions),
            FormatStatusLine(
                "Apply",
                report.Apply.Exists,
                $"mode={report.Apply.Mode} wouldApply={report.Apply.WouldApplyCount} skipped={report.Apply.SkippedCount}",
                report.Apply),
            FormatStatusLine(
                "ApprovedDependencies",
                report.ApprovedDependencies.Exists,
                $"dependencies={report.ApprovedDependencies.DependencyCount}",
                report.ApprovedDependencies),
            FormatStatusLine(
                "CleanupCandidates",
                report.CleanupCandidates.Exists,
                $"candidates={report.CleanupCandidates.CandidateCount}",
                report.CleanupCandidates)
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatStatusLine(string label, bool exists, string details, ResourceStatusFile file)
    {
        var state = exists ? "ok" : "missing";
        var line = $"{label}: {state} {details} path={file.Path}";
        if (!string.IsNullOrWhiteSpace(file.Error))
        {
            line += $" error={file.Error}";
        }
        return line;
    }

    private static int RunVerifyOutputs(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var planPath = ResolveProjectFile(root, opts.GetValueOrDefault("plan", Path.Combine("BuildLogs", "resource_plan.json")));
        var decisionsPath = ResolveProjectFile(root, opts.GetValueOrDefault("decisions", Path.Combine("BuildLogs", "resource_decisions.json")));
        var applyPath = ResolveProjectFile(root, opts.GetValueOrDefault("apply", Path.Combine("BuildLogs", "resource_apply_preview.json")));
        var approvedDependenciesPath = ResolveProjectFile(root, opts.GetValueOrDefault("approved", Path.Combine("BuildLogs", "resource_approved_dependencies.json")));
        var cleanupCandidatesPath = ResolveProjectFile(root, opts.GetValueOrDefault("cleanup", Path.Combine("BuildLogs", "resource_cleanup_candidates.json")));

        var plan = LoadOrBuildPlan(root, planPath, options);
        var ledger = LoadDecisionLedger(decisionsPath);
        var apply = LoadJsonFile<ResourceApplyPreview>(applyPath, "apply preview");
        var approved = LoadJsonFile<ResourceApprovedDependencies>(approvedDependenciesPath, "approved dependencies");
        var cleanup = LoadJsonFile<ResourceCleanupCandidates>(cleanupCandidatesPath, "cleanup candidates");
        var report = BuildVerifyOutputsReport(
            root,
            planPath,
            decisionsPath,
            applyPath,
            approvedDependenciesPath,
            cleanupCandidatesPath,
            plan,
            ledger,
            apply,
            approved,
            cleanup);

        Console.WriteLine(opts.ContainsKey("summary")
            ? FormatVerifyOutputsSummary(report)
            : JsonSerializer.Serialize(report, JsonOptions));
        return report.Ok ? 0 : 1;
    }

    private static string FormatVerifyOutputsSummary(ResourceOutputVerificationReport report)
    {
        var lines = new List<string>
        {
            "ResourceConfig verify outputs",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} issues={report.IssueCount}",
            $"Counts: acceptedExecutable={report.AcceptedExecutableCount} deps={report.ActualDependencyCount}/{report.ExpectedDependencyCount} cleanup={report.ActualCleanupCandidateCount}/{report.ExpectedCleanupCandidateCount}",
            "Files:",
            $"  plan={report.PlanPath}",
            $"  decisions={report.DecisionsPath}",
            $"  apply={report.ApplyPath}",
            $"  approved={report.ApprovedDependenciesPath}",
            $"  cleanup={report.CleanupCandidatesPath}",
            "Issues:"
        };

        if (report.Issues.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var issue in report.Issues)
        {
            lines.Add($"  [{issue.Code}] {issue.Subject}");
            lines.Add($"    expected: {issue.Expected}");
            lines.Add($"    actual: {issue.Actual}");
            lines.Add($"    message: {issue.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int RunPending(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var planPath = ResolveProjectFile(root, opts.GetValueOrDefault("plan", Path.Combine("BuildLogs", "resource_plan.json")));
        var decisionsPath = ResolveProjectFile(root, opts.GetValueOrDefault("decisions", Path.Combine("BuildLogs", "resource_decisions.json")));
        var limit = ParseLimit(opts.GetValueOrDefault("limit", ""), 20);
        var severityFilter = opts.GetValueOrDefault("severity", "");
        var typeFilter = opts.GetValueOrDefault("type", "");
        var query = opts.GetValueOrDefault("query", "");
        var report = BuildPendingReviewReport(root, planPath, decisionsPath, LoadOrBuildPlan(root, planPath, options), LoadDecisionLedger(decisionsPath), limit, severityFilter, typeFilter, query);
        Console.WriteLine(opts.ContainsKey("commands")
            ? FormatPendingReviewCommands(report)
            : opts.ContainsKey("summary")
                ? FormatPendingReviewSummary(report)
                : JsonSerializer.Serialize(report, JsonOptions));
        return 0;
    }

    private static int RunApply(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var planPath = ResolveProjectFile(root, opts.GetValueOrDefault("plan", Path.Combine("BuildLogs", "resource_plan.json")));
        var decisionsPath = ResolveProjectFile(root, opts.GetValueOrDefault("decisions", Path.Combine("BuildLogs", "resource_decisions.json")));
        var output = opts.GetValueOrDefault("out", Path.Combine("BuildLogs", "resource_apply_preview.json"));
        output = ResolveProjectFile(root, output);
        var execute = opts.ContainsKey("execute");
        var approvedDependenciesPath = ResolveProjectFile(root, opts.GetValueOrDefault("approved-out", Path.Combine("BuildLogs", "resource_approved_dependencies.json")));
        var cleanupCandidatesPath = ResolveProjectFile(root, opts.GetValueOrDefault("cleanup-out", Path.Combine("BuildLogs", "resource_cleanup_candidates.json")));

        var plan = LoadOrBuildPlan(root, planPath, options);
        var ledger = LoadDecisionLedger(decisionsPath);
        var report = BuildApplyPreview(root, planPath, decisionsPath, plan, ledger, execute);
        var json = JsonSerializer.Serialize(report, JsonOptions);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, json);
        if (execute)
        {
            var approved = BuildApprovedDependencies(root, report.WouldApply);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(approvedDependenciesPath)) ?? ".");
            File.WriteAllText(approvedDependenciesPath, JsonSerializer.Serialize(approved, JsonOptions));
            report.ApprovedDependenciesPath = ToDisplayPath(root, approvedDependenciesPath);
            var cleanup = BuildCleanupCandidates(root, report.WouldApply);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cleanupCandidatesPath)) ?? ".");
            File.WriteAllText(cleanupCandidatesPath, JsonSerializer.Serialize(cleanup, JsonOptions));
            report.CleanupCandidatesPath = ToDisplayPath(root, cleanupCandidatesPath);
            File.WriteAllText(output, JsonSerializer.Serialize(report, JsonOptions));
        }
        if (!opts.ContainsKey("quiet"))
        {
            Console.WriteLine(output);
        }
        return 0;
    }

    private static int RunDecide(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var id = opts.GetValueOrDefault("id", "");
        var decision = opts.GetValueOrDefault("decision", "");
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("decide requires --id <resource-plan-0000>.");
        }
        if (!IsSupportedDecision(decision))
        {
            throw new ArgumentException("decide requires --decision accept|defer|reject.");
        }

        var planPath = ResolveProjectFile(root, opts.GetValueOrDefault("plan", Path.Combine("BuildLogs", "resource_plan.json")));
        var decisionsPath = ResolveProjectFile(root, opts.GetValueOrDefault("out", Path.Combine("BuildLogs", "resource_decisions.json")));
        var plan = LoadOrBuildPlan(root, planPath, options);
        var action = plan.Actions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (action == null)
        {
            Console.Error.WriteLine("Resource plan action not found: " + id);
            return 1;
        }

        var ledger = LoadDecisionLedger(decisionsPath);
        ledger.ProjectRoot = root;
        ledger.PlanPath = ToDisplayPath(root, planPath);
        ledger.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        ledger.Decisions.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        ledger.Decisions.Add(new ResourcePlanDecision
        {
            Id = action.Id,
            Decision = decision.ToLowerInvariant(),
            ActionType = action.Type,
            Severity = action.Severity,
            Source = action.Source,
            Target = action.Target,
            Note = opts.GetValueOrDefault("note", ""),
            DecidedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
        ledger.Decisions = ledger.Decisions.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(decisionsPath)) ?? ".");
        File.WriteAllText(decisionsPath, JsonSerializer.Serialize(ledger, JsonOptions));
        if (!opts.ContainsKey("quiet"))
        {
            Console.WriteLine(decisionsPath);
        }
        return 0;
    }

    private static int RunPlan(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var limit = 50;
        if (int.TryParse(opts.GetValueOrDefault("limit", ""), out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 500);
        }

        var manifest = BuildManifest(root, options);
        var index = BuildIndex(manifest);
        var report = BuildPlanReport(manifest, index, options, limit);
        var json = JsonSerializer.Serialize(report, JsonOptions);

        var output = opts.GetValueOrDefault("out", "");
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(json);
            return 0;
        }

        if (!Path.IsPathRooted(output))
        {
            output = Path.Combine(root, output);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, json);
        Console.WriteLine(output);
        return 0;
    }

    private static int RunValidate(string root, ResourceScanOptions options)
    {
        var manifest = BuildManifest(root, options);
        if (manifest.MissingReferences.Count == 0)
        {
            Console.WriteLine("ResourceConfig validation OK.");
            return 0;
        }

        Console.Error.WriteLine($"ResourceConfig validation found {manifest.MissingReferences.Count} missing references.");
        foreach (var missing in manifest.MissingReferences.Take(20))
        {
            Console.Error.WriteLine($"{missing.Source} -> {missing.Target}");
        }
        if (manifest.MissingReferences.Count > 20)
        {
            Console.Error.WriteLine("...");
        }
        return 1;
    }

    private static int RunExportManifest(string root, string output, ResourceScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(root, "BuildLogs", "resource_manifest.json");
        }

        var manifest = BuildManifest(root, options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine(output);
        return manifest.MissingReferences.Count == 0 ? 0 : 1;
    }

    private static int RunAudit(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var limit = 50;
        if (int.TryParse(opts.GetValueOrDefault("limit", ""), out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 500);
        }

        var includeImports = opts.ContainsKey("include-imports") || opts.ContainsKey("includeImports");
        var manifest = BuildManifest(root, options);
        var index = BuildIndex(manifest);
        var report = BuildAuditReport(manifest, index, options, limit, includeImports);
        var json = JsonSerializer.Serialize(report, JsonOptions);

        var output = opts.GetValueOrDefault("out", "");
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(json);
            return 0;
        }

        if (!Path.IsPathRooted(output))
        {
            output = Path.Combine(root, output);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, json);
        Console.WriteLine(output);
        return 0;
    }

    private static int RunShow(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var path = opts.GetValueOrDefault("path", "");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("show requires --path <res://...>.");
        }
        if (!path.StartsWith("res://", StringComparison.Ordinal))
        {
            throw new ArgumentException("--path must be a res:// path.");
        }

        var indexPath = opts.GetValueOrDefault("index", "");
        if (string.IsNullOrWhiteSpace(indexPath))
        {
            indexPath = Path.Combine(root, "BuildLogs", "resource_index.json");
        }
        if (!Path.IsPathRooted(indexPath))
        {
            indexPath = Path.Combine(root, indexPath);
        }

        var refresh = opts.ContainsKey("refresh");
        var index = LoadOrBuildIndex(root, indexPath, options, refresh);
        var entry = index.Resources.FirstOrDefault(x => x.Path.Equals(path, StringComparison.Ordinal));
        if (entry == null)
        {
            Console.Error.WriteLine("Resource not found: " + path);
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
        return 0;
    }

    private static int RunFind(string root, Dictionary<string, string> opts, ResourceScanOptions options)
    {
        var indexPath = ResolveIndexPath(root, opts.GetValueOrDefault("index", ""));
        var refresh = opts.ContainsKey("refresh");
        var index = LoadOrBuildIndex(root, indexPath, options, refresh);

        var query = opts.GetValueOrDefault("query", "");
        var kind = opts.GetValueOrDefault("kind", "");
        var extension = NormalizeExtension(opts.GetValueOrDefault("extension", ""));
        var limit = 50;
        if (int.TryParse(opts.GetValueOrDefault("limit", ""), out var parsedLimit))
        {
            limit = Math.Clamp(parsedLimit, 1, 500);
        }

        var matches = index.Resources.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = matches.Where(x => x.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            matches = matches.Where(x => x.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(extension))
        {
            matches = matches.Where(x => x.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        var result = matches
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ResourceSearchResult
            {
                Path = x.Path,
                Kind = x.Kind,
                Extension = x.Extension,
                IncomingCount = x.Incoming.Count,
                OutgoingCount = x.Outgoing.Count,
                OutgoingMissingCount = x.OutgoingMissingCount
            })
            .ToList();

        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    private static int RunExportIndex(string root, string output, ResourceScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(root, "BuildLogs", "resource_index.json");
        }

        var manifest = BuildManifest(root, options);
        var index = BuildIndex(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, JsonSerializer.Serialize(index, JsonOptions));
        Console.WriteLine(output);
        return manifest.MissingReferences.Count == 0 ? 0 : 1;
    }

    private static int RunAgentSelfTest()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ResourceConfig.AgentSelfTest." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, "CoreEngine", "Resources"));
        Directory.CreateDirectory(Path.Combine(temp, "CoreEngine", "Maps"));
        File.WriteAllText(Path.Combine(temp, "project.godot"), "[application]\nrun/main_scene=\"res://CoreEngine/Maps/Test.tscn\"\n");
        File.WriteAllText(Path.Combine(temp, "CoreEngine", "Maps", "Test.tscn"), "[gd_scene format=3]\n");
        File.WriteAllText(Path.Combine(temp, "CoreEngine", "Resources", "Settings.tres"), "map_data_file = \"res://CoreEngine/Maps/Test.tscn\"\n");

        var manifest = BuildManifest(temp, ResourceScanOptions.Default);
        if (manifest.ResourceFiles.Count < 2 || manifest.MissingReferences.Count != 0)
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed manifest check.");
            return 1;
        }

        var index = BuildIndex(manifest);
        var settings = index.Resources.FirstOrDefault(x => x.Path == "res://CoreEngine/Resources/Settings.tres");
        if (index.Resources.Count < 2 || settings == null || settings.Outgoing.Count != 1 || settings.OutgoingMissingCount != 0)
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource index check.");
            return 1;
        }
        var tempIndex = Path.Combine(temp, "BuildLogs", "resource_index.json");
        var loadedIndex = LoadOrBuildIndex(temp, tempIndex, ResourceScanOptions.Default, refresh: true);
        var loadedSettings = loadedIndex.Resources.FirstOrDefault(x => x.Path == "res://CoreEngine/Resources/Settings.tres");
        if (loadedSettings == null || loadedSettings.Outgoing.Count != 1)
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource show/index load check.");
            return 1;
        }
        var findResult = loadedIndex.Resources
            .Where(x => x.Path.Contains("Settings", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Path)
            .ToList();
        if (findResult.Count != 1 || findResult[0] != "res://CoreEngine/Resources/Settings.tres")
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource find fixture check.");
            return 1;
        }

        Directory.CreateDirectory(Path.Combine(temp, "CoreEngine", "Textures"));
        File.WriteAllText(Path.Combine(temp, "CoreEngine", "Textures", "Settings.tres"), "[gd_resource type=\"Resource\"]\n");
        File.WriteAllText(Path.Combine(temp, "CoreEngine", "Resources", "Loose.png"), "fake png");
        File.WriteAllText(Path.Combine(temp, "CoreEngine", "Resources", "External.tres"), "icon = \"res://addons/Demo/Icon.png\"\n");
        var auditManifest = BuildManifest(temp, ResourceScanOptions.Default);
        var auditReport = BuildAuditReport(auditManifest, BuildIndex(auditManifest), ResourceScanOptions.Default, limit: 10, includeImports: false);
        if (auditReport.DuplicateFileNames.Count != 1 ||
            auditReport.ExternalReferences.Count != 1 ||
            auditReport.UnreferencedResources.All(x => x.Path != "res://CoreEngine/Resources/Loose.png"))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource audit check.");
            return 1;
        }
        var planReport = BuildPlanReport(auditManifest, BuildIndex(auditManifest), ResourceScanOptions.Default, limit: 10);
        if (planReport.Actions.Count < 3 ||
            planReport.Actions.All(x => x.Type != "review-unreferenced-resource") ||
            planReport.Actions.All(x => x.Type != "review-external-reference") ||
            planReport.Actions.All(x => x.Type != "review-duplicate-file-name"))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource plan check.");
            return 1;
        }
        var tempPlanPath = Path.Combine(temp, "BuildLogs", "resource_plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPlanPath)!);
        File.WriteAllText(tempPlanPath, JsonSerializer.Serialize(planReport, JsonOptions));
        var decideCode = RunDecide(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = planReport.Actions[0].Id,
            ["decision"] = "defer",
            ["note"] = "self-test decision",
            ["plan"] = tempPlanPath,
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var decisionsPath = Path.Combine(temp, "BuildLogs", "resource_decisions.json");
        var decisionLedger = LoadDecisionLedger(decisionsPath);
        if (decideCode != 0 ||
            decisionLedger.Decisions.Count != 1 ||
            decisionLedger.Decisions[0].Id != planReport.Actions[0].Id ||
            decisionLedger.Decisions[0].Decision != "defer")
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource decision check.");
            return 1;
        }
        var pendingReport = BuildPendingReviewReport(temp, tempPlanPath, decisionsPath, planReport, decisionLedger, 2, "", "", "");
        if (pendingReport.ActionCount != planReport.Actions.Count ||
            pendingReport.DecisionCount != 1 ||
            pendingReport.PendingCount != planReport.Actions.Count - 1 ||
            pendingReport.FilteredPendingCount != planReport.Actions.Count - 1 ||
            pendingReport.Pending.Count is 0 or > 2 ||
            pendingReport.Pending.Any(x => string.IsNullOrWhiteSpace(x.DecideCommand)))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed pending review report check.");
            return 1;
        }
        var warningPendingReport = BuildPendingReviewReport(temp, tempPlanPath, decisionsPath, planReport, decisionLedger, 5, "warning", "review-external-reference", "external");
        if (warningPendingReport.Pending.Any(x =>
                !x.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
                !x.Type.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase) ||
                !x.Source.Contains("External", StringComparison.OrdinalIgnoreCase)) ||
            !warningPendingReport.SeverityFilter.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
            !warningPendingReport.TypeFilter.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase) ||
            !warningPendingReport.Query.Equals("external", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed pending review filter check.");
            return 1;
        }
        var pendingSummary = FormatPendingReviewSummary(pendingReport);
        if (!pendingSummary.Contains("ResourceConfig pending reviews", StringComparison.Ordinal) ||
            !pendingSummary.Contains("pending=", StringComparison.Ordinal) ||
            !pendingSummary.Contains("Review buckets:", StringComparison.Ordinal) ||
            !pendingSummary.Contains("Suggested review queues:", StringComparison.Ordinal) ||
            !pendingSummary.Contains("decide:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed pending review summary check.");
            return 1;
        }
        var pendingCommands = FormatPendingReviewCommands(pendingReport);
        if (!pendingCommands.Contains("resource decide", StringComparison.Ordinal) ||
            pendingCommands.Contains("ResourceConfig pending reviews", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed pending review commands check.");
            return 1;
        }
        var applyCode = RunApply(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = tempPlanPath,
            ["decisions"] = decisionsPath,
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var applyPreviewPath = Path.Combine(temp, "BuildLogs", "resource_apply_preview.json");
        var applyPreview = JsonSerializer.Deserialize<ResourceApplyPreview>(File.ReadAllText(applyPreviewPath), JsonOptions);
        if (applyCode != 0 ||
            applyPreview == null ||
            applyPreview.WouldApplyCount != 0 ||
            applyPreview.SkippedCount == 0 ||
            applyPreview.SkippedActions.All(x => x.Id != planReport.Actions[0].Id))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource apply preview check.");
            return 1;
        }
        var acceptCode = RunDecide(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = planReport.Actions.First(x => x.Type == "review-external-reference").Id,
            ["decision"] = "accept",
            ["note"] = "self-test approved dependency",
            ["plan"] = tempPlanPath,
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var executeCode = RunApply(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = tempPlanPath,
            ["decisions"] = decisionsPath,
            ["execute"] = "true",
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var approvedPath = Path.Combine(temp, "BuildLogs", "resource_approved_dependencies.json");
        var approved = JsonSerializer.Deserialize<ResourceApprovedDependencies>(File.ReadAllText(approvedPath), JsonOptions);
        if (acceptCode != 0 ||
            executeCode != 0 ||
            approved == null ||
            approved.Dependencies.Count != 1 ||
            approved.Dependencies[0].Target != "res://addons/Demo/Icon.png")
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource approved dependency apply check.");
            return 1;
        }
        var looseResourceAction = planReport.Actions.First(x =>
            x.Type == "review-unreferenced-resource" &&
            x.Source == "res://CoreEngine/Resources/Loose.png");
        var cleanupAcceptCode = RunDecide(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = looseResourceAction.Id,
            ["decision"] = "accept",
            ["note"] = "self-test cleanup candidate",
            ["plan"] = tempPlanPath,
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var cleanupExecuteCode = RunApply(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = tempPlanPath,
            ["decisions"] = decisionsPath,
            ["execute"] = "true",
            ["quiet"] = "true"
        }, ResourceScanOptions.Default);
        var cleanupPath = Path.Combine(temp, "BuildLogs", "resource_cleanup_candidates.json");
        var cleanup = JsonSerializer.Deserialize<ResourceCleanupCandidates>(File.ReadAllText(cleanupPath), JsonOptions);
        if (cleanupAcceptCode != 0 ||
            cleanupExecuteCode != 0 ||
            cleanup == null ||
            cleanup.Candidates.All(x => x.Path != "res://CoreEngine/Resources/Loose.png"))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource cleanup candidate apply check.");
            return 1;
        }
        var status = BuildWorkflowStatus(temp, "BuildLogs");
        if (status.Index.ResourceCount < 2 ||
            status.Plan.ActionCount == 0 ||
            status.Decisions.DecisionCount < 2 ||
            status.Apply.WouldApplyCount == 0 ||
            status.ApprovedDependencies.DependencyCount != 1 ||
            status.CleanupCandidates.CandidateCount == 0)
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource workflow status check.");
            return 1;
        }
        var verifyCode = RunVerifyOutputs(temp, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = tempPlanPath,
            ["decisions"] = decisionsPath,
            ["apply"] = applyPreviewPath,
            ["approved"] = approvedPath,
            ["cleanup"] = cleanupPath
        }, ResourceScanOptions.Default);
        if (verifyCode != 0)
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource output verification check.");
            return 1;
        }
        var verifyReport = BuildVerifyOutputsReport(
            temp,
            tempPlanPath,
            decisionsPath,
            applyPreviewPath,
            approvedPath,
            cleanupPath,
            planReport,
            LoadDecisionLedger(decisionsPath),
            LoadJsonFile<ResourceApplyPreview>(applyPreviewPath, "apply preview"),
            LoadJsonFile<ResourceApprovedDependencies>(approvedPath, "approved dependencies"),
            LoadJsonFile<ResourceCleanupCandidates>(cleanupPath, "cleanup candidates"));
        var verifySummary = FormatVerifyOutputsSummary(verifyReport);
        if (!verifySummary.Contains("ResourceConfig verify outputs", StringComparison.Ordinal) ||
            !verifySummary.Contains("Overall: OK", StringComparison.Ordinal) ||
            !verifySummary.Contains("Issues:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ResourceConfig agent self-test failed resource output verification summary check.");
            return 1;
        }

        Console.WriteLine("ResourceConfig agent self-test OK.");
        return 0;
    }

    private static ResourceManifest BuildManifest(string root, ResourceScanOptions options)
    {
        if (!File.Exists(Path.Combine(root, "project.godot")))
        {
            throw new FileNotFoundException("project.godot not found.", Path.Combine(root, "project.godot"));
        }

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(root, path))
            .Where(path => IsInScope(root, path, options.Scope))
            .Select(path => BuildResourceFile(root, path))
            .Where(file => file != null)
            .Cast<ResourceFile>()
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();

        var references = new List<ResourceReference>();
        foreach (var file in files.Where(x => IsTextResource(x.Path)))
        {
            var abs = ToAbsolutePath(root, file.Path);
            foreach (var reference in ExtractReferences(root, file.Path, abs))
            {
                references.Add(reference);
            }
        }

        var missing = references
            .Where(r => !r.Exists)
            .Where(r => !IsAllowedMissing(r.Target, options.AllowedMissingPrefixes))
            .OrderBy(r => r.Source, StringComparer.Ordinal)
            .ThenBy(r => r.Target, StringComparer.Ordinal)
            .ToList();

        return new ResourceManifest
        {
            ProjectRoot = root,
            ResourceFiles = files,
            References = references.OrderBy(r => r.Source, StringComparer.Ordinal).ThenBy(r => r.Target, StringComparer.Ordinal).ToList(),
            MissingReferences = missing
        };
    }

    private static ResourceIndex BuildIndex(ResourceManifest manifest)
    {
        var referencesBySource = manifest.References
            .GroupBy(x => x.Source, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.Target, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var referencesByTarget = manifest.References
            .Where(x => x.Exists)
            .GroupBy(x => x.Target, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.Source, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var missingBySource = manifest.MissingReferences
            .GroupBy(x => x.Source, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

        return new ResourceIndex
        {
            ProjectRoot = manifest.ProjectRoot,
            Resources = manifest.ResourceFiles.Select(file =>
            {
                referencesBySource.TryGetValue(file.Path, out var outgoing);
                referencesByTarget.TryGetValue(file.Path, out var incoming);
                missingBySource.TryGetValue(file.Path, out var missingOutgoingCount);

                return new ResourceIndexEntry
                {
                    Path = file.Path,
                    Kind = file.Kind,
                    Extension = file.Extension,
                    Size = file.Size,
                    Outgoing = (outgoing ?? []).Select(r => new ResourceLink { Path = r.Target, Exists = r.Exists }).ToList(),
                    Incoming = (incoming ?? []).Select(r => r.Source).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                    OutgoingMissingCount = missingOutgoingCount
                };
            }).OrderBy(x => x.Path, StringComparer.Ordinal).ToList()
        };
    }

    private static ResourceAuditReport BuildAuditReport(
        ResourceManifest manifest,
        ResourceIndex index,
        ResourceScanOptions options,
        int limit,
        bool includeImports)
    {
        var filteredResources = index.Resources
            .Where(x => includeImports || !x.Extension.Equals(".import", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var unreferenced = filteredResources
            .Where(x => x.Incoming.Count == 0)
            .Where(x => !IsEntryResource(x.Path))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ResourceAuditResource
            {
                Path = x.Path,
                Kind = x.Kind,
                Extension = x.Extension,
                Size = x.Size
            })
            .ToList();
        var externalReferences = manifest.References
            .Where(x => IsExternalReference(x.Target))
            .OrderBy(x => x.Source, StringComparer.Ordinal)
            .ThenBy(x => x.Target, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ResourceAuditReference
            {
                Source = x.Source,
                Target = x.Target,
                Exists = x.Exists
            })
            .ToList();
        var duplicateNames = filteredResources
            .GroupBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new ResourceDuplicateNameGroup
            {
                FileName = x.Key,
                Count = x.Count(),
                Resources = x.OrderBy(r => r.Path, StringComparer.Ordinal)
                    .Select(r => new ResourceAuditResource
                    {
                        Path = r.Path,
                        Kind = r.Kind,
                        Extension = r.Extension,
                        Size = r.Size
                    })
                    .ToList()
            })
            .ToList();
        var largestResources = filteredResources
            .OrderByDescending(x => x.Size)
            .ThenBy(x => x.Path, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ResourceAuditResource
            {
                Path = x.Path,
                Kind = x.Kind,
                Extension = x.Extension,
                Size = x.Size
            })
            .ToList();

        return new ResourceAuditReport
        {
            ProjectRoot = manifest.ProjectRoot,
            Scope = options.Scope,
            ResourceCount = manifest.ResourceFiles.Count,
            ReferenceCount = manifest.References.Count,
            MissingReferenceCount = manifest.MissingReferences.Count,
            ExternalReferenceCount = manifest.References.Count(x => IsExternalReference(x.Target)),
            UnreferencedResourceCount = filteredResources.Count(x => x.Incoming.Count == 0 && !IsEntryResource(x.Path)),
            DuplicateFileNameGroupCount = filteredResources
                .GroupBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
                .Count(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1),
            LargestResources = largestResources,
            ExternalReferences = externalReferences,
            UnreferencedResources = unreferenced,
            DuplicateFileNames = duplicateNames
        };
    }

    private static bool IsEntryResource(string path)
    {
        return path.Equals("res://project.godot", StringComparison.Ordinal) ||
               path.Equals("res://CoreEngine/Game.tscn", StringComparison.Ordinal) ||
               path.EndsWith("/project.godot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExternalReference(string target)
    {
        return target.StartsWith("res://", StringComparison.Ordinal) &&
               !target.StartsWith("res://CoreEngine/", StringComparison.OrdinalIgnoreCase) &&
               !target.Equals("res://project.godot", StringComparison.OrdinalIgnoreCase);
    }

    private static ResourcePlanReport BuildPlanReport(
        ResourceManifest manifest,
        ResourceIndex index,
        ResourceScanOptions options,
        int limit)
    {
        var actions = new List<ResourcePlanAction>();
        var sequence = 1;
        foreach (var missing in manifest.MissingReferences.OrderBy(x => x.Source, StringComparer.Ordinal).ThenBy(x => x.Target, StringComparer.Ordinal))
        {
            actions.Add(new ResourcePlanAction
            {
                Id = NextActionId(sequence++),
                Type = "resolve-missing-reference",
                Severity = "error",
                Source = missing.Source,
                Target = missing.Target,
                Reason = "A resource reference does not resolve in the current project tree.",
                Recommendation = RecommendMissingReference(missing.Target),
                RequiresApproval = true
            });
        }

        foreach (var external in manifest.References
            .Where(x => IsExternalReference(x.Target))
            .OrderBy(x => x.Source, StringComparer.Ordinal)
            .ThenBy(x => x.Target, StringComparer.Ordinal)
            .Take(limit))
        {
            actions.Add(new ResourcePlanAction
            {
                Id = NextActionId(sequence++),
                Type = "review-external-reference",
                Severity = external.Exists ? "info" : "warning",
                Source = external.Source,
                Target = external.Target,
                Reason = "CoreEngine points outside its own content boundary.",
                Recommendation = external.Exists
                    ? "Classify this dependency as addon, extension, tool sample, or candidate CoreEngine asset before editing references."
                    : RecommendMissingReference(external.Target),
                RequiresApproval = true
            });
        }

        foreach (var resource in index.Resources
            .Where(x => x.Incoming.Count == 0)
            .Where(x => !IsEntryResource(x.Path))
            .Where(x => !IsIgnoredPlanResource(x.Path))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .Take(limit))
        {
            actions.Add(new ResourcePlanAction
            {
                Id = NextActionId(sequence++),
                Type = "review-unreferenced-resource",
                Severity = IsLikelyGeneratedSidecar(resource.Path) ? "info" : "warning",
                Source = resource.Path,
                Target = "",
                Reason = "No incoming res:// reference was found for this resource in the scanned scope.",
                Recommendation = RecommendUnreferencedResource(resource),
                RequiresApproval = true
            });
        }

        foreach (var group in index.Resources
            .Where(x => !x.Extension.Equals(".import", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit))
        {
            var resources = group.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => x.Path).ToList();
            actions.Add(new ResourcePlanAction
            {
                Id = NextActionId(sequence++),
                Type = "review-duplicate-file-name",
                Severity = "info",
                Source = group.Key,
                Target = "",
                Reason = "Multiple resources share the same file name.",
                Recommendation = "Confirm whether these are intentional per-map assets, generated sidecars, or candidates for shared resource consolidation.",
                Resources = resources,
                RequiresApproval = true
            });
        }

        return new ResourcePlanReport
        {
            ProjectRoot = manifest.ProjectRoot,
            Scope = options.Scope,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ActionCount = actions.Count,
            ErrorCount = actions.Count(x => x.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)),
            WarningCount = actions.Count(x => x.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)),
            InfoCount = actions.Count(x => x.Severity.Equals("info", StringComparison.OrdinalIgnoreCase)),
            Actions = actions
        };
    }

    private static string NextActionId(int sequence) => "resource-plan-" + sequence.ToString("0000");

    private static string RecommendMissingReference(string target)
    {
        if (target.StartsWith("res://000_UserInput/", StringComparison.OrdinalIgnoreCase))
        {
            return "Decide whether to restore this legacy user-input folder, remap the reference to UserAssets, or remove the legacy hook.";
        }
        if (target.StartsWith("res://addons/CustomRunner/", StringComparison.OrdinalIgnoreCase))
        {
            return "Decide whether CustomRunner should be restored as an addon, replaced by CoreEngine/CustomRunnerIntegration, or removed from runtime code.";
        }
        return "Restore the target, update the source reference, or document this as an intentionally optional dependency.";
    }

    private static string RecommendUnreferencedResource(ResourceIndexEntry resource)
    {
        if (IsLikelyGeneratedSidecar(resource.Path))
        {
            return "Treat this as generated or editor sidecar data unless an active workflow depends on direct references to it.";
        }
        if (resource.Kind.Equals("scene", StringComparison.OrdinalIgnoreCase))
        {
            return "Check whether this is loaded dynamically by map systems before moving, deleting, or archiving it.";
        }
        if (resource.Kind.Equals("image", StringComparison.OrdinalIgnoreCase) || resource.Kind.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            return "Inspect whether this is a loose source asset, generated map media, or a resource that should be referenced by a scene/config.";
        }
        return "Review ownership and decide whether to reference, archive, ignore, or remove this resource.";
    }

    private static bool IsIgnoredPlanResource(string path)
    {
        return path.EndsWith(".import", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyGeneratedSidecar(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".uid", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".import", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gdignore", StringComparison.OrdinalIgnoreCase);
    }

    private static ResourcePlanReport LoadOrBuildPlan(string root, string planPath, ResourceScanOptions options)
    {
        if (File.Exists(planPath))
        {
            var plan = JsonSerializer.Deserialize<ResourcePlanReport>(File.ReadAllText(planPath), JsonOptions);
            if (plan != null)
            {
                plan.Actions ??= [];
                return plan;
            }
        }

        var manifest = BuildManifest(root, options);
        return BuildPlanReport(manifest, BuildIndex(manifest), options, limit: 500);
    }

    private static ResourceDecisionLedger LoadDecisionLedger(string path)
    {
        if (File.Exists(path))
        {
            var ledger = JsonSerializer.Deserialize<ResourceDecisionLedger>(File.ReadAllText(path), JsonOptions);
            if (ledger != null)
            {
                ledger.Decisions ??= [];
                return ledger;
            }
        }

        return new ResourceDecisionLedger();
    }

    private static ResourcePendingReviewReport BuildPendingReviewReport(
        string root,
        string planPath,
        string decisionsPath,
        ResourcePlanReport plan,
        ResourceDecisionLedger ledger,
        int limit,
        string severityFilter,
        string typeFilter,
        string query)
    {
        var decisions = ledger.Decisions
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        var allPending = plan.Actions
            .Where(x => !decisions.ContainsKey(x.Id))
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var pending = allPending
            .Where(x => MatchesOptionalFilter(x.Severity, severityFilter))
            .Where(x => MatchesOptionalFilter(x.Type, typeFilter))
            .Where(x => MatchesPendingQuery(x, query))
            .ToList();
        var reviewBuckets = BuildPendingReviewBuckets(allPending);

        return new ResourcePendingReviewReport
        {
            ProjectRoot = root,
            PlanPath = ToDisplayPath(root, planPath),
            DecisionsPath = ToDisplayPath(root, decisionsPath),
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ActionCount = plan.Actions.Count,
            DecisionCount = decisions.Count,
            PendingCount = allPending.Count,
            FilteredPendingCount = pending.Count,
            AcceptedCount = decisions.Values.Count(x => x.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase)),
            DeferredCount = decisions.Values.Count(x => x.Decision.Equals("defer", StringComparison.OrdinalIgnoreCase)),
            RejectedCount = decisions.Values.Count(x => x.Decision.Equals("reject", StringComparison.OrdinalIgnoreCase)),
            ErrorCount = allPending.Count(x => x.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)),
            WarningCount = allPending.Count(x => x.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)),
            InfoCount = allPending.Count(x => x.Severity.Equals("info", StringComparison.OrdinalIgnoreCase)),
            SeverityFilter = severityFilter,
            TypeFilter = typeFilter,
            Query = query,
            Limit = limit,
            ReviewBucketCount = reviewBuckets.Count,
            ReviewBuckets = reviewBuckets,
            Pending = pending.Take(limit).Select(ToPendingReviewAction).ToList()
        };
    }

    private static List<ResourcePendingReviewBucket> BuildPendingReviewBuckets(List<ResourcePlanAction> allPending)
    {
        return allPending
            .GroupBy(x => new
            {
                Severity = string.IsNullOrWhiteSpace(x.Severity) ? "unknown" : x.Severity,
                Type = string.IsNullOrWhiteSpace(x.Type) ? "unknown" : x.Type
            })
            .OrderBy(x => SeverityRank(x.Key.Severity))
            .ThenByDescending(x => x.Count())
            .ThenBy(x => x.Key.Type, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ResourcePendingReviewBucket
            {
                Severity = x.Key.Severity,
                Type = x.Key.Type,
                Count = x.Count(),
                SampleIds = x.OrderBy(action => action.Id, StringComparer.OrdinalIgnoreCase).Take(5).Select(action => action.Id).ToList(),
                ReviewCommand = $"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource pending --summary --severity {x.Key.Severity} --type {x.Key.Type} --limit 20 -NoBuild"
            })
            .ToList();
    }

    private static bool MatchesOptionalFilter(string value, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return filter
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => value.Equals(part, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPendingQuery(ResourcePlanAction action, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var parts = query.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.All(part =>
            ContainsIgnoreCase(action.Id, part) ||
            ContainsIgnoreCase(action.Type, part) ||
            ContainsIgnoreCase(action.Severity, part) ||
            ContainsIgnoreCase(action.Source, part) ||
            ContainsIgnoreCase(action.Target, part) ||
            ContainsIgnoreCase(action.Reason, part) ||
            ContainsIgnoreCase(action.Recommendation, part));
    }

    private static bool ContainsIgnoreCase(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static ResourcePendingReviewAction ToPendingReviewAction(ResourcePlanAction action)
    {
        return new ResourcePendingReviewAction
        {
            Id = action.Id,
            Type = action.Type,
            Severity = action.Severity,
            Source = action.Source,
            Target = action.Target,
            Reason = action.Reason,
            Recommendation = action.Recommendation,
            RequiresApproval = action.RequiresApproval,
            SuggestedDecision = "defer",
            DecideCommand = $"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource decide --id {action.Id} --decision defer --note \"owner review\" -NoBuild"
        };
    }

    private static string FormatPendingReviewSummary(ResourcePendingReviewReport report)
    {
        var lines = new List<string>
        {
            "ResourceConfig pending reviews",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Plan: {report.PlanPath}",
            $"Decisions: {report.DecisionsPath}",
            $"Counts: actions={report.ActionCount} decisions={report.DecisionCount} pending={report.PendingCount} accepted={report.AcceptedCount} deferred={report.DeferredCount} rejected={report.RejectedCount}",
            $"Pending severity: errors={report.ErrorCount} warnings={report.WarningCount} info={report.InfoCount}",
            $"Review buckets: {FormatPendingReviewBuckets(report.ReviewBuckets)}",
            $"Filters: severity={DisplayFilter(report.SeverityFilter)} type={DisplayFilter(report.TypeFilter)} query={DisplayFilter(report.Query)} filteredPending={report.FilteredPendingCount}",
            $"Showing: {report.Pending.Count}/{report.FilteredPendingCount} (limit {report.Limit})",
            "Suggested review queues:"
        };

        if (report.ReviewBuckets.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var bucket in report.ReviewBuckets.Take(8))
        {
            var samples = bucket.SampleIds.Count == 0 ? "" : " samples=" + string.Join(",", bucket.SampleIds);
            lines.Add($"  [{bucket.Severity}] {bucket.Type}: count={bucket.Count}{samples}");
            lines.Add($"    command: {bucket.ReviewCommand}");
        }

        lines.AddRange([
            "Pending:"
        ]);

        if (report.Pending.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var action in report.Pending)
        {
            var target = string.IsNullOrWhiteSpace(action.Target) ? "" : " -> " + action.Target;
            lines.Add($"  [{action.Severity}] {action.Id} {action.Type}: {action.Source}{target}");
            lines.Add($"    reason: {action.Reason}");
            lines.Add($"    recommendation: {action.Recommendation}");
            lines.Add($"    decide: {action.DecideCommand}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPendingReviewBuckets(IReadOnlyCollection<ResourcePendingReviewBucket> buckets)
    {
        if (buckets.Count == 0)
        {
            return "none";
        }

        return string.Join(" ", buckets
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Severity}/{x.Type}={x.Count}"));
    }

    private static string FormatPendingReviewCommands(ResourcePendingReviewReport report)
    {
        return string.Join(Environment.NewLine, report.Pending.Select(x => x.DecideCommand));
    }

    private static string DisplayFilter(string value) =>
        string.IsNullOrWhiteSpace(value) ? "all" : value;

    private static int SeverityRank(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => 0,
            "warning" => 1,
            _ => 2
        };
    }

    private static ResourceApplyPreview BuildApplyPreview(
        string root,
        string planPath,
        string decisionsPath,
        ResourcePlanReport plan,
        ResourceDecisionLedger ledger,
        bool execute)
    {
        var decisions = ledger.Decisions
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        var wouldApply = new List<ResourceApplyAction>();
        var skipped = new List<ResourceApplySkippedAction>();

        foreach (var action in plan.Actions.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!decisions.TryGetValue(action.Id, out var decision))
            {
                skipped.Add(ToSkippedAction(action, "no-decision", "No review decision has been recorded for this action."));
                continue;
            }
            if (!decision.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(ToSkippedAction(action, "decision-" + decision.Decision.ToLowerInvariant(), "Only accepted decisions are eligible for apply."));
                continue;
            }

            if (execute && !CanExecuteAction(action))
            {
                skipped.Add(ToSkippedAction(action, "unsupported-execute-action", "This accepted action type has no execute semantics yet."));
                continue;
            }

            wouldApply.Add(new ResourceApplyAction
            {
                Id = action.Id,
                Type = action.Type,
                Severity = action.Severity,
                Source = action.Source,
                Target = action.Target,
                Mode = execute ? "execute" : "dry-run",
                Decision = decision.Decision,
                Note = decision.Note,
                Description = DescribeApplyAction(action)
            });
        }

        return new ResourceApplyPreview
        {
            ProjectRoot = root,
            PlanPath = ToDisplayPath(root, planPath),
            DecisionsPath = ToDisplayPath(root, decisionsPath),
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Mode = execute ? "execute" : "dry-run",
            WouldApplyCount = wouldApply.Count,
            SkippedCount = skipped.Count,
            WouldApply = wouldApply,
            SkippedActions = skipped
        };
    }

    private static bool CanExecuteAction(ResourcePlanAction action)
    {
        return (action.Type.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(action.Target)) ||
               (action.Type.Equals("review-unreferenced-resource", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(action.Source));
    }

    private static ResourceApprovedDependencies BuildApprovedDependencies(string root, IReadOnlyCollection<ResourceApplyAction> actions)
    {
        return new ResourceApprovedDependencies
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Dependencies = actions
                .Where(x => x.Type.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Source, StringComparer.Ordinal)
                .ThenBy(x => x.Target, StringComparer.Ordinal)
                .Select(x => new ResourceApprovedDependency
                {
                    Source = x.Source,
                    Target = x.Target,
                    DecisionId = x.Id,
                    Note = x.Note
                })
                .ToList()
        };
    }

    private static ResourceCleanupCandidates BuildCleanupCandidates(string root, IReadOnlyCollection<ResourceApplyAction> actions)
    {
        return new ResourceCleanupCandidates
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Candidates = actions
                .Where(x => x.Type.Equals("review-unreferenced-resource", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Source, StringComparer.Ordinal)
                .Select(x => new ResourceCleanupCandidate
                {
                    Path = x.Source,
                    DecisionId = x.Id,
                    Severity = x.Severity,
                    Note = x.Note
                })
                .ToList()
        };
    }

    private static ResourceApplySkippedAction ToSkippedAction(ResourcePlanAction action, string reasonCode, string reason)
    {
        return new ResourceApplySkippedAction
        {
            Id = action.Id,
            Type = action.Type,
            Severity = action.Severity,
            Source = action.Source,
            Target = action.Target,
            ReasonCode = reasonCode,
            Reason = reason
        };
    }

    private static string DescribeApplyAction(ResourcePlanAction action)
    {
        return action.Type switch
        {
            "resolve-missing-reference" => "Would prepare a missing-reference resolution task. No resource files are modified by this dry run.",
            "review-external-reference" => "Marks this external dependency as approved in the resource approved-dependencies configuration.",
            "review-unreferenced-resource" => "Marks this unreferenced resource as an approved cleanup candidate. No file is moved or deleted.",
            "review-duplicate-file-name" => "Would mark this duplicate-name group for the next consolidation/review workflow.",
            _ => "Would mark this resource plan action for the next approved workflow."
        };
    }

    private static bool IsSupportedDecision(string decision)
    {
        return decision.Equals("accept", StringComparison.OrdinalIgnoreCase) ||
               decision.Equals("defer", StringComparison.OrdinalIgnoreCase) ||
               decision.Equals("reject", StringComparison.OrdinalIgnoreCase);
    }

    private static T LoadJsonFile<T>(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(label + " file not found.", path);
        }

        var data = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        if (data == null)
        {
            throw new InvalidDataException(label + " file is empty or invalid JSON.");
        }

        return data;
    }

    private static ResourceOutputVerificationReport BuildVerifyOutputsReport(
        string root,
        string planPath,
        string decisionsPath,
        string applyPath,
        string approvedDependenciesPath,
        string cleanupCandidatesPath,
        ResourcePlanReport plan,
        ResourceDecisionLedger ledger,
        ResourceApplyPreview apply,
        ResourceApprovedDependencies approved,
        ResourceCleanupCandidates cleanup)
    {
        var issues = new List<ResourceOutputVerificationIssue>();
        var planById = plan.Actions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var decisionsById = ledger.Decisions
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        var acceptedExecutable = plan.Actions
            .Where(action =>
                decisionsById.TryGetValue(action.Id, out var decision) &&
                decision.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase) &&
                CanExecuteAction(action))
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var expectedDependencyActions = acceptedExecutable
            .Where(x => x.Type.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var expectedCleanupActions = acceptedExecutable
            .Where(x => x.Type.Equals("review-unreferenced-resource", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var wouldApplyById = apply.WouldApply.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var dependencyById = approved.Dependencies.ToDictionary(x => x.DecisionId, StringComparer.OrdinalIgnoreCase);
        var cleanupById = cleanup.Candidates.ToDictionary(x => x.DecisionId, StringComparer.OrdinalIgnoreCase);

        AddCountIssue(issues, "apply.wouldApplyCount", acceptedExecutable.Count, apply.WouldApplyCount, "Apply preview count does not match accepted executable decisions.");
        AddCountIssue(issues, "apply.wouldApply", acceptedExecutable.Count, apply.WouldApply.Count, "Apply preview item count does not match accepted executable decisions.");
        AddCountIssue(issues, "apply.skippedCount", apply.SkippedActions.Count, apply.SkippedCount, "Apply skipped count does not match skipped action list.");
        AddCountIssue(issues, "approved.dependencies", expectedDependencyActions.Count, approved.Dependencies.Count, "Approved dependency count does not match accepted external-reference decisions.");
        AddCountIssue(issues, "cleanup.candidates", expectedCleanupActions.Count, cleanup.Candidates.Count, "Cleanup candidate count does not match accepted unreferenced-resource decisions.");

        foreach (var action in acceptedExecutable)
        {
            if (!wouldApplyById.TryGetValue(action.Id, out var applyAction))
            {
                AddIssue(issues, action.Id, "apply.missing", "Expected accepted action is missing from apply preview.");
                continue;
            }

            if (!ValuesMatch(action.Type, applyAction.Type) ||
                !ValuesMatch(action.Source, applyAction.Source) ||
                !ValuesMatch(action.Target, applyAction.Target) ||
                !applyAction.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase) ||
                !applyAction.Mode.Equals("execute", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, action.Id, "apply.mismatch", "Apply preview action does not match plan action and accepted decision.");
            }
        }

        foreach (var applyAction in apply.WouldApply)
        {
            if (!planById.ContainsKey(applyAction.Id))
            {
                AddIssue(issues, applyAction.Id, "apply.unknown-action", "Apply preview references an action not present in the plan.");
            }
            if (!decisionsById.TryGetValue(applyAction.Id, out var decision) ||
                !decision.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, applyAction.Id, "apply.not-accepted", "Apply preview references an action without an accepted decision.");
            }
        }

        foreach (var action in expectedDependencyActions)
        {
            if (!dependencyById.TryGetValue(action.Id, out var dependency))
            {
                AddIssue(issues, action.Id, "approved.missing", "Accepted external-reference action is missing from approved dependencies.");
                continue;
            }
            if (!ValuesMatch(action.Source, dependency.Source) || !ValuesMatch(action.Target, dependency.Target))
            {
                AddIssue(issues, action.Id, "approved.mismatch", "Approved dependency source/target does not match the plan action.");
            }
        }

        foreach (var dependency in approved.Dependencies)
        {
            if (!planById.TryGetValue(dependency.DecisionId, out var action))
            {
                AddIssue(issues, dependency.DecisionId, "approved.unknown-action", "Approved dependency references an action not present in the plan.");
                continue;
            }
            if (!action.Type.Equals("review-external-reference", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, dependency.DecisionId, "approved.wrong-action-type", "Approved dependency references a non-external-reference action.");
            }
            if (!decisionsById.TryGetValue(dependency.DecisionId, out var decision) ||
                !decision.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, dependency.DecisionId, "approved.not-accepted", "Approved dependency references an action without an accepted decision.");
            }
        }

        foreach (var action in expectedCleanupActions)
        {
            if (!cleanupById.TryGetValue(action.Id, out var candidate))
            {
                AddIssue(issues, action.Id, "cleanup.missing", "Accepted unreferenced-resource action is missing from cleanup candidates.");
                continue;
            }
            if (!ValuesMatch(action.Source, candidate.Path) || !ValuesMatch(action.Severity, candidate.Severity))
            {
                AddIssue(issues, action.Id, "cleanup.mismatch", "Cleanup candidate path/severity does not match the plan action.");
            }
        }

        foreach (var candidate in cleanup.Candidates)
        {
            if (!planById.TryGetValue(candidate.DecisionId, out var action))
            {
                AddIssue(issues, candidate.DecisionId, "cleanup.unknown-action", "Cleanup candidate references an action not present in the plan.");
                continue;
            }
            if (!action.Type.Equals("review-unreferenced-resource", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, candidate.DecisionId, "cleanup.wrong-action-type", "Cleanup candidate references a non-unreferenced-resource action.");
            }
            if (!decisionsById.TryGetValue(candidate.DecisionId, out var decision) ||
                !decision.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, candidate.DecisionId, "cleanup.not-accepted", "Cleanup candidate references an action without an accepted decision.");
            }
        }

        return new ResourceOutputVerificationReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            PlanPath = ToDisplayPath(root, planPath),
            DecisionsPath = ToDisplayPath(root, decisionsPath),
            ApplyPath = ToDisplayPath(root, applyPath),
            ApprovedDependenciesPath = ToDisplayPath(root, approvedDependenciesPath),
            CleanupCandidatesPath = ToDisplayPath(root, cleanupCandidatesPath),
            AcceptedExecutableCount = acceptedExecutable.Count,
            ExpectedDependencyCount = expectedDependencyActions.Count,
            ActualDependencyCount = approved.Dependencies.Count,
            ExpectedCleanupCandidateCount = expectedCleanupActions.Count,
            ActualCleanupCandidateCount = cleanup.Candidates.Count,
            IssueCount = issues.Count,
            Ok = issues.Count == 0,
            Issues = issues
        };
    }

    private static void AddCountIssue(List<ResourceOutputVerificationIssue> issues, string subject, int expected, int actual, string message)
    {
        if (expected != actual)
        {
            issues.Add(new ResourceOutputVerificationIssue
            {
                Subject = subject,
                Code = "count-mismatch",
                Expected = expected.ToString(),
                Actual = actual.ToString(),
                Message = message
            });
        }
    }

    private static void AddIssue(List<ResourceOutputVerificationIssue> issues, string subject, string code, string message)
    {
        issues.Add(new ResourceOutputVerificationIssue
        {
            Subject = subject,
            Code = code,
            Message = message
        });
    }

    private static bool ValuesMatch(string expected, string actual)
    {
        return expected.Equals(actual, StringComparison.Ordinal);
    }

    private static string ResolveProjectFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.");
        }
        return Path.IsPathRooted(path) ? path : Path.Combine(root, path);
    }

    private static string ToDisplayPath(string root, string path)
    {
        var rel = Path.GetRelativePath(root, path);
        if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
        {
            return rel.Replace('\\', '/');
        }
        return path;
    }

    private static ResourceWorkflowStatus BuildWorkflowStatus(string root, string buildLogsDir)
    {
        var dir = ResolveProjectFile(root, buildLogsDir);
        return new ResourceWorkflowStatus
        {
            ProjectRoot = root,
            BuildLogsDir = ToDisplayPath(root, dir),
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Index = ReadStatusFile<ResourceIndex, ResourceIndexStatus>(root, dir, "resource_index.json", index => new ResourceIndexStatus
            {
                ResourceCount = index.Resources.Count
            }),
            Audit = ReadStatusFile<ResourceAuditReport, ResourceAuditStatus>(root, dir, "resource_audit.json", audit => new ResourceAuditStatus
            {
                ResourceCount = audit.ResourceCount,
                MissingReferenceCount = audit.MissingReferenceCount,
                ExternalReferenceCount = audit.ExternalReferenceCount,
                UnreferencedResourceCount = audit.UnreferencedResourceCount,
                DuplicateFileNameGroupCount = audit.DuplicateFileNameGroupCount
            }),
            Plan = ReadStatusFile<ResourcePlanReport, ResourcePlanStatus>(root, dir, "resource_plan.json", plan => new ResourcePlanStatus
            {
                ActionCount = plan.ActionCount,
                ErrorCount = plan.ErrorCount,
                WarningCount = plan.WarningCount,
                InfoCount = plan.InfoCount
            }),
            Decisions = ReadStatusFile<ResourceDecisionLedger, ResourceDecisionStatus>(root, dir, "resource_decisions.json", ledger => new ResourceDecisionStatus
            {
                DecisionCount = ledger.Decisions.Count,
                AcceptCount = ledger.Decisions.Count(x => x.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase)),
                DeferCount = ledger.Decisions.Count(x => x.Decision.Equals("defer", StringComparison.OrdinalIgnoreCase)),
                RejectCount = ledger.Decisions.Count(x => x.Decision.Equals("reject", StringComparison.OrdinalIgnoreCase))
            }),
            Apply = ReadStatusFile<ResourceApplyPreview, ResourceApplyStatus>(root, dir, "resource_apply_preview.json", apply => new ResourceApplyStatus
            {
                Mode = apply.Mode,
                WouldApplyCount = apply.WouldApplyCount,
                SkippedCount = apply.SkippedCount
            }),
            ApprovedDependencies = ReadStatusFile<ResourceApprovedDependencies, ResourceApprovedDependencyStatus>(root, dir, "resource_approved_dependencies.json", approved => new ResourceApprovedDependencyStatus
            {
                DependencyCount = approved.Dependencies.Count
            }),
            CleanupCandidates = ReadStatusFile<ResourceCleanupCandidates, ResourceCleanupCandidateStatus>(root, dir, "resource_cleanup_candidates.json", cleanup => new ResourceCleanupCandidateStatus
            {
                CandidateCount = cleanup.Candidates.Count
            })
        };
    }

    private static TStatus ReadStatusFile<TData, TStatus>(
        string root,
        string dir,
        string fileName,
        Func<TData, TStatus> map)
        where TStatus : ResourceStatusFile, new()
    {
        var path = Path.Combine(dir, fileName);
        var status = new TStatus
        {
            Path = ToDisplayPath(root, path),
            Exists = File.Exists(path)
        };
        if (!status.Exists)
        {
            return status;
        }

        var info = new FileInfo(path);
        try
        {
            var data = JsonSerializer.Deserialize<TData>(File.ReadAllText(path), JsonOptions);
            if (data == null)
            {
                status.Error = "File is empty or invalid JSON.";
                return status;
            }

            var mapped = map(data);
            mapped.Path = status.Path;
            mapped.Exists = true;
            mapped.Size = info.Length;
            mapped.LastWriteTimeUtc = info.LastWriteTimeUtc.ToString("O");
            return mapped;
        }
        catch (Exception ex)
        {
            status.Size = info.Length;
            status.LastWriteTimeUtc = info.LastWriteTimeUtc.ToString("O");
            status.Error = ex.Message;
            return status;
        }
    }

    private static ResourceIndex LoadOrBuildIndex(string root, string indexPath, ResourceScanOptions options, bool refresh)
    {
        if (!refresh && File.Exists(indexPath))
        {
            var loaded = JsonSerializer.Deserialize<ResourceIndex>(File.ReadAllText(indexPath), JsonOptions);
            if (loaded != null)
            {
                loaded.Resources ??= [];
                return loaded;
            }
        }

        var manifest = BuildManifest(root, options);
        var index = BuildIndex(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(indexPath)) ?? ".");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(index, JsonOptions));
        return index;
    }

    private static string ResolveIndexPath(string root, string indexPath)
    {
        if (string.IsNullOrWhiteSpace(indexPath))
        {
            return Path.Combine(root, "BuildLogs", "resource_index.json");
        }
        return Path.IsPathRooted(indexPath) ? indexPath : Path.Combine(root, indexPath);
    }

    private static string NormalizeExtension(string extension)
    {
        extension = extension.Trim();
        if (extension.Length == 0 || extension.StartsWith(".", StringComparison.Ordinal))
        {
            return extension;
        }
        return "." + extension;
    }

    private static ResourceFile? BuildResourceFile(string root, string absPath)
    {
        var resPath = ToResPath(root, absPath);
        if (resPath.Length == 0)
        {
            return null;
        }

        var ext = Path.GetExtension(absPath).ToLowerInvariant();
        var kind = ext switch
        {
            ".tscn" => "scene",
            ".tres" or ".res" => "resource",
            ".json" => "json",
            ".import" => "import",
            ".po" => "translation",
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".svg" => "image",
            ".mp4" or ".webm" or ".ogv" => "video",
            ".gd" => "script",
            ".txt" => "text",
            _ => "other"
        };

        var info = new FileInfo(absPath);
        return new ResourceFile
        {
            Path = resPath,
            Kind = kind,
            Extension = ext,
            Size = info.Length
        };
    }

    private static IEnumerable<ResourceReference> ExtractReferences(string root, string sourceResPath, string absPath)
    {
        string text;
        try
        {
            text = File.ReadAllText(absPath);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in ResPathRegex().Matches(text))
        {
            var target = match.Groups["path"].Value.Trim();
            target = target.Replace("\\/", "/").TrimEnd('\\');
            if (!LooksLikeResourcePath(target))
            {
                continue;
            }
            if (target.Contains("::", StringComparison.Ordinal))
            {
                target = target[..target.IndexOf("::", StringComparison.Ordinal)];
            }
            if (target.StartsWith("res://.godot/", StringComparison.Ordinal))
            {
                continue;
            }

            var exists = File.Exists(ToAbsolutePath(root, target)) || Directory.Exists(ToAbsolutePath(root, target));
            yield return new ResourceReference
            {
                Source = sourceResPath,
                Target = target,
                Exists = exists
            };
        }
    }

    private static bool IsIgnoredPath(string root, string absPath)
    {
        var rel = Path.GetRelativePath(root, absPath).Replace('\\', '/');
        return rel.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
               rel.StartsWith(".godot/", StringComparison.OrdinalIgnoreCase) ||
               rel.StartsWith("BuildLogs/", StringComparison.OrdinalIgnoreCase) ||
               rel.StartsWith("GodotTools-Build/", StringComparison.OrdinalIgnoreCase) ||
               rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInScope(string root, string absPath, string scope)
    {
        if (scope.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rel = Path.GetRelativePath(root, absPath).Replace('\\', '/');
        return rel.Equals("project.godot", StringComparison.OrdinalIgnoreCase) ||
               rel.StartsWith("CoreEngine/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedMissing(string target, IReadOnlyCollection<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTextResource(string resPath)
    {
        var ext = Path.GetExtension(resPath).ToLowerInvariant();
        return ext is ".gd" or ".tscn" or ".tres" or ".import" or ".godot" or ".json" or ".cfg" or ".txt";
    }

    private static string ResolveProjectRoot(string requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return Path.GetFullPath(requested);
        }

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrWhiteSpace(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "project.godot")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool LooksLikeResourcePath(string target)
    {
        if (!target.StartsWith("res://", StringComparison.Ordinal))
        {
            return false;
        }
        if (target.Length <= "res://".Length)
        {
            return false;
        }

        var first = target["res://".Length];
        return char.IsLetterOrDigit(first) || first is '_' or '.' or '/';
    }

    private static string ToResPath(string root, string absPath)
    {
        var rel = Path.GetRelativePath(root, absPath);
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
        {
            return "";
        }
        return "res://" + rel.Replace('\\', '/');
    }

    private static string ToAbsolutePath(string root, string resPath)
    {
        var rel = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath["res://".Length..] : resPath;
        rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(root, rel);
    }

    private static bool IsAgentSelfTest(string arg) =>
        arg.Equals("--agent-self-test", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("agent-self-test", StringComparison.OrdinalIgnoreCase);

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "/?" or "help";

    private static int RunHelp()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ResourceConfig usage:");
        Console.WriteLine("  summary --godotRoot <dir> [--scope core|all]");
        Console.WriteLine("  validate --godotRoot <dir> [--scope core|all] [--allow-missing-prefix <res://...>]");
        Console.WriteLine("  export-manifest --godotRoot <dir> [--out <file>] [--scope core|all]");
        Console.WriteLine("  export-index --godotRoot <dir> [--out <file>] [--scope core|all]");
        Console.WriteLine("  audit --godotRoot <dir> [--out <file>] [--scope core|all] [--limit <n>] [--include-imports]");
        Console.WriteLine("  plan --godotRoot <dir> [--out <file>] [--scope core|all] [--limit <n>]");
        Console.WriteLine("  decide --godotRoot <dir> --id <resource-plan-id> --decision accept|defer|reject [--note <text>] [--plan <file>] [--out <file>]");
        Console.WriteLine("  apply --godotRoot <dir> [--plan <file>] [--decisions <file>] [--out <file>] [--execute] [--approved-out <file>] [--cleanup-out <file>]");
        Console.WriteLine("  pending --godotRoot <dir> [--plan <file>] [--decisions <file>] [--limit <n>] [--severity error|warning|info] [--type <action-type>] [--query <text>] [--summary] [--commands]");
        Console.WriteLine("  status --godotRoot <dir> [--dir <BuildLogs>] [--summary]");
        Console.WriteLine("  verify-outputs --godotRoot <dir> [--plan <file>] [--decisions <file>] [--apply <file>] [--approved <file>] [--cleanup <file>] [--summary]");
        Console.WriteLine("  show --godotRoot <dir> --path <res://...> [--index <file>] [--refresh] [--scope core|all]");
        Console.WriteLine("  find --godotRoot <dir> [--query <text>] [--kind <kind>] [--extension <ext>] [--limit <n>] [--index <file>] [--refresh]");
        Console.WriteLine("  --agent-self-test");
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }
            var key = arg[2..];
            var value = "";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            if (d.TryGetValue(key, out var existing) && existing.Length > 0)
            {
                d[key] = existing + ";" + value;
            }
            else
            {
                d[key] = value;
            }
        }
        return d;
    }

    private static int ParseLimit(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 1, 500) : fallback;
    }

    private static ResourceScanOptions CreateOptions(Dictionary<string, string> opts)
    {
        var scope = opts.GetValueOrDefault("scope", "core");
        if (!scope.Equals("core", StringComparison.OrdinalIgnoreCase) &&
            !scope.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--scope must be core or all.");
        }

        var allowed = new List<string>();
        if (opts.TryGetValue("allowMissingPrefix", out var single) && !string.IsNullOrWhiteSpace(single))
        {
            allowed.AddRange(SplitOptionList(single));
        }
        if (opts.TryGetValue("allow-missing-prefix", out var kebab) && !string.IsNullOrWhiteSpace(kebab))
        {
            allowed.AddRange(SplitOptionList(kebab));
        }

        return new ResourceScanOptions
        {
            Scope = scope,
            AllowedMissingPrefixes = allowed
        };
    }

    private static IEnumerable<string> SplitOptionList(string value)
    {
        return value.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [GeneratedRegex("\"[^\"\\r\\n]*(?<path>res://[^\"\\r\\n]+)\"|'[^'\\r\\n]*(?<path>res://[^'\\r\\n]+)'|(?<path>res://[^\\s\\\"'\\)\\]\\},]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ResPathRegex();
}

public sealed class ResourceManifest
{
    public string ProjectRoot { get; set; } = "";
    public List<ResourceFile> ResourceFiles { get; set; } = [];
    public List<ResourceReference> References { get; set; } = [];
    public List<ResourceReference> MissingReferences { get; set; } = [];
}

public sealed class ResourceScanOptions
{
    public static ResourceScanOptions Default { get; } = new();
    public string Scope { get; set; } = "core";
    public List<string> AllowedMissingPrefixes { get; set; } = [];
}

public sealed class ResourceFile
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Extension { get; set; } = "";
    public long Size { get; set; }
}

public sealed class ResourceReference
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Exists { get; set; }
}

public sealed class ResourceIndex
{
    public string ProjectRoot { get; set; } = "";
    public List<ResourceIndexEntry> Resources { get; set; } = [];
}

public sealed class ResourceIndexEntry
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Extension { get; set; } = "";
    public long Size { get; set; }
    public List<ResourceLink> Outgoing { get; set; } = [];
    public List<string> Incoming { get; set; } = [];
    public int OutgoingMissingCount { get; set; }
}

public sealed class ResourceLink
{
    public string Path { get; set; } = "";
    public bool Exists { get; set; }
}

public sealed class ResourceSearchResult
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Extension { get; set; } = "";
    public int IncomingCount { get; set; }
    public int OutgoingCount { get; set; }
    public int OutgoingMissingCount { get; set; }
}

public sealed class ResourceAuditReport
{
    public string ProjectRoot { get; set; } = "";
    public string Scope { get; set; } = "";
    public int ResourceCount { get; set; }
    public int ReferenceCount { get; set; }
    public int MissingReferenceCount { get; set; }
    public int ExternalReferenceCount { get; set; }
    public int UnreferencedResourceCount { get; set; }
    public int DuplicateFileNameGroupCount { get; set; }
    public List<ResourceAuditResource> LargestResources { get; set; } = [];
    public List<ResourceAuditReference> ExternalReferences { get; set; } = [];
    public List<ResourceAuditResource> UnreferencedResources { get; set; } = [];
    public List<ResourceDuplicateNameGroup> DuplicateFileNames { get; set; } = [];
}

public sealed class ResourceAuditResource
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Extension { get; set; } = "";
    public long Size { get; set; }
}

public sealed class ResourceAuditReference
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Exists { get; set; }
}

public sealed class ResourceDuplicateNameGroup
{
    public string FileName { get; set; } = "";
    public int Count { get; set; }
    public List<ResourceAuditResource> Resources { get; set; } = [];
}

public sealed class ResourcePlanReport
{
    public string ProjectRoot { get; set; } = "";
    public string Scope { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public int ActionCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public List<ResourcePlanAction> Actions { get; set; } = [];
}

public sealed class ResourcePlanAction
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public List<string> Resources { get; set; } = [];
    public bool RequiresApproval { get; set; }
}

public sealed class ResourceDecisionLedger
{
    public string ProjectRoot { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
    public List<ResourcePlanDecision> Decisions { get; set; } = [];
}

public sealed class ResourcePlanDecision
{
    public string Id { get; set; } = "";
    public string Decision { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Note { get; set; } = "";
    public string DecidedAtUtc { get; set; } = "";
}

public sealed class ResourcePendingReviewReport
{
    public string ProjectRoot { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string DecisionsPath { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public int ActionCount { get; set; }
    public int DecisionCount { get; set; }
    public int PendingCount { get; set; }
    public int FilteredPendingCount { get; set; }
    public int AcceptedCount { get; set; }
    public int DeferredCount { get; set; }
    public int RejectedCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public string SeverityFilter { get; set; } = "";
    public string TypeFilter { get; set; } = "";
    public string Query { get; set; } = "";
    public int Limit { get; set; }
    public int ReviewBucketCount { get; set; }
    public List<ResourcePendingReviewBucket> ReviewBuckets { get; set; } = [];
    public List<ResourcePendingReviewAction> Pending { get; set; } = [];
}

public sealed class ResourcePendingReviewBucket
{
    public string Severity { get; set; } = "";
    public string Type { get; set; } = "";
    public int Count { get; set; }
    public List<string> SampleIds { get; set; } = [];
    public string ReviewCommand { get; set; } = "";
}

public sealed class ResourcePendingReviewAction
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public bool RequiresApproval { get; set; }
    public string SuggestedDecision { get; set; } = "";
    public string DecideCommand { get; set; } = "";
}

public sealed class ResourceApplyPreview
{
    public string ProjectRoot { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string DecisionsPath { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ApprovedDependenciesPath { get; set; } = "";
    public string CleanupCandidatesPath { get; set; } = "";
    public int WouldApplyCount { get; set; }
    public int SkippedCount { get; set; }
    public List<ResourceApplyAction> WouldApply { get; set; } = [];
    public List<ResourceApplySkippedAction> SkippedActions { get; set; } = [];
}

public sealed class ResourceApplyAction
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Note { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class ResourceApplySkippedAction
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class ResourceApprovedDependencies
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public List<ResourceApprovedDependency> Dependencies { get; set; } = [];
}

public sealed class ResourceApprovedDependency
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string DecisionId { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class ResourceCleanupCandidates
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public List<ResourceCleanupCandidate> Candidates { get; set; } = [];
}

public sealed class ResourceCleanupCandidate
{
    public string Path { get; set; } = "";
    public string DecisionId { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class ResourceOutputVerificationReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string DecisionsPath { get; set; } = "";
    public string ApplyPath { get; set; } = "";
    public string ApprovedDependenciesPath { get; set; } = "";
    public string CleanupCandidatesPath { get; set; } = "";
    public int AcceptedExecutableCount { get; set; }
    public int ExpectedDependencyCount { get; set; }
    public int ActualDependencyCount { get; set; }
    public int ExpectedCleanupCandidateCount { get; set; }
    public int ActualCleanupCandidateCount { get; set; }
    public int IssueCount { get; set; }
    public bool Ok { get; set; }
    public List<ResourceOutputVerificationIssue> Issues { get; set; } = [];
}

public sealed class ResourceOutputVerificationIssue
{
    public string Subject { get; set; } = "";
    public string Code { get; set; } = "";
    public string Expected { get; set; } = "";
    public string Actual { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class ResourceWorkflowStatus
{
    public string ProjectRoot { get; set; } = "";
    public string BuildLogsDir { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public ResourceIndexStatus Index { get; set; } = new();
    public ResourceAuditStatus Audit { get; set; } = new();
    public ResourcePlanStatus Plan { get; set; } = new();
    public ResourceDecisionStatus Decisions { get; set; } = new();
    public ResourceApplyStatus Apply { get; set; } = new();
    public ResourceApprovedDependencyStatus ApprovedDependencies { get; set; } = new();
    public ResourceCleanupCandidateStatus CleanupCandidates { get; set; } = new();
}

public class ResourceStatusFile
{
    public string Path { get; set; } = "";
    public bool Exists { get; set; }
    public long Size { get; set; }
    public string LastWriteTimeUtc { get; set; } = "";
    public string Error { get; set; } = "";
}

public sealed class ResourceIndexStatus : ResourceStatusFile
{
    public int ResourceCount { get; set; }
}

public sealed class ResourceAuditStatus : ResourceStatusFile
{
    public int ResourceCount { get; set; }
    public int MissingReferenceCount { get; set; }
    public int ExternalReferenceCount { get; set; }
    public int UnreferencedResourceCount { get; set; }
    public int DuplicateFileNameGroupCount { get; set; }
}

public sealed class ResourcePlanStatus : ResourceStatusFile
{
    public int ActionCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}

public sealed class ResourceDecisionStatus : ResourceStatusFile
{
    public int DecisionCount { get; set; }
    public int AcceptCount { get; set; }
    public int DeferCount { get; set; }
    public int RejectCount { get; set; }
}

public sealed class ResourceApplyStatus : ResourceStatusFile
{
    public string Mode { get; set; } = "";
    public int WouldApplyCount { get; set; }
    public int SkippedCount { get; set; }
}

public sealed class ResourceApprovedDependencyStatus : ResourceStatusFile
{
    public int DependencyCount { get; set; }
}

public sealed class ResourceCleanupCandidateStatus : ResourceStatusFile
{
    public int CandidateCount { get; set; }
}
