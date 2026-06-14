using System.Text.Json;

namespace ToolHub;

internal static class Program
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "map",
        "resource",
        "event",
        "replay",
        "validation",
        "utility"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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
            var root = ResolveProjectRoot(GetOption(opts, "godotRoot", "godot-root"));
            var manifest = LoadManifest(root);

            return command switch
            {
                "list" => RunList(manifest),
                "status" => RunStatus(root, manifest),
                "dump-index" => RunDumpIndex(root, args.Skip(1).ToArray()),
                "closure-gates" => RunClosureGates(root, manifest, args.Skip(1).ToArray()),
                "mutation-plan" => RunMutationPlan(root, args.Skip(1).ToArray()),
                "handoff" => RunHandoff(root, manifest, args.Skip(1).ToArray()),
                "next-actions" => RunNextActions(root, manifest, args.Skip(1).ToArray()),
                "show" => RunShow(manifest, args.Skip(1).FirstOrDefault(x => !x.StartsWith("--", StringComparison.Ordinal)) ?? ""),
                "run" => RunToolCommand(root, manifest, args.Skip(1).ToArray()),
                "run-all" => RunAllToolCommands(root, manifest, args.Skip(1).ToArray()),
                "resource" => RunResourceCommand(root, manifest, args.Skip(1).ToArray()),
                "map" => RunMapCommand(root, manifest, args.Skip(1).ToArray()),
                "doctor" => RunDoctor(root, manifest),
                "validate-manifest" => RunValidateManifest(root, manifest),
                "export" => RunExport(manifest, opts.GetValueOrDefault("out", "")),
                "help" or "-h" or "--help" => RunHelp(),
                _ => RunHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ToolHub failed: " + ex.Message);
            return 1;
        }
    }

    private static int RunList(ToolManifest manifest)
    {
        foreach (var tool in manifest.Tools.OrderBy(x => x.Category).ThenBy(x => x.Id))
        {
            Console.WriteLine($"{tool.Id}\t{tool.Category}\t{tool.Name}\t{tool.Purpose}");
        }
        return 0;
    }

    private static int RunStatus(string root, ToolManifest manifest)
    {
        var report = BuildStatus(root, manifest);
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return report.ManifestIssueCount == 0 ? 0 : 1;
    }

    private static int RunDumpIndex(string root, string[] args)
    {
        var report = BuildDumpIndex(root);
        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatDumpIndexSummary(report));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        return report.MissingRequiredCount == 0 ? 0 : 1;
    }

    private static int RunHandoff(string root, ToolManifest manifest, string[] args)
    {
        var report = BuildHandoffReport(root, manifest);
        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatHandoffSummary(report));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        return report.Ok ? 0 : 1;
    }

    private static int RunClosureGates(string root, ToolManifest manifest, string[] args)
    {
        var report = BuildClosureGateReport(root, manifest);
        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatClosureGateSummary(report));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        return report.CurrentBaselineOk ? 0 : 1;
    }

    private static int RunMutationPlan(string root, string[] args)
    {
        if (args.Length > 0 && args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
        {
            var verifyArgs = args.Skip(1).ToArray();
            var verifyReport = BuildMutationPlanVerifyReport(root, verifyArgs);
            if (HasOption(verifyArgs, "summary"))
            {
                Console.WriteLine(FormatMutationPlanVerifySummary(verifyReport));
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(verifyReport, JsonOptions));
            }
            return verifyReport.Ok ? 0 : 1;
        }

        var report = BuildMutationPlanReport(root, args);
        var opts = ParseOptions(args);
        var outPath = GetOption(opts, "out");
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            WriteMutationPlanReport(root, report, outPath);
        }

        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatMutationPlanSummary(report));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        return report.ReadyForDesignReview ? 0 : 1;
    }

    private static ToolMutationPlanVerifyReport BuildMutationPlanVerifyReport(string root, string[] args)
    {
        var opts = ParseOptions(args);
        var dir = GetOption(opts, "dir");
        var pattern = GetOption(opts, "pattern");
        var scanDir = string.IsNullOrWhiteSpace(dir) ? "BuildLogs" : dir;
        var scanPattern = string.IsNullOrWhiteSpace(pattern) ? "mutation_plan*.json" : pattern;
        var absoluteDir = ResolveRootedPath(root, scanDir);
        var plans = new List<ToolMutationPlanVerifyItem>();

        if (Directory.Exists(absoluteDir))
        {
            foreach (var file in Directory.EnumerateFiles(absoluteDir, scanPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                try
                {
                    var plan = ReadMutationPlanReport(root, relativePath);
                    RecomputeMutationPlanChecks(plan);
                    plans.Add(new ToolMutationPlanVerifyItem
                    {
                        Path = relativePath,
                        ReadyForDesignReview = plan.ReadyForDesignReview,
                        MissingCount = plan.MissingCount,
                        MissingChecks = plan.MissingChecks,
                        Domain = plan.Domain,
                        Intent = plan.Intent,
                        Issue = ""
                    });
                }
                catch (Exception ex)
                {
                    plans.Add(new ToolMutationPlanVerifyItem
                    {
                        Path = relativePath,
                        ReadyForDesignReview = false,
                        MissingCount = 1,
                        MissingChecks = ["parse"],
                        Domain = "",
                        Intent = "",
                        Issue = ex.Message
                    });
                }
            }
        }

        var readyCount = plans.Count(x => x.ReadyForDesignReview);
        var failedCount = plans.Count - readyCount;
        return new ToolMutationPlanVerifyReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Directory = scanDir.Replace('\\', '/'),
            Pattern = scanPattern,
            PlanCount = plans.Count,
            ReadyCount = readyCount,
            FailedCount = failedCount,
            Ok = plans.Count > 0 && failedCount == 0,
            Plans = plans,
            SuggestedCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary --out BuildLogs\\mutation_plan.json --domain map --intent \"describe intended edit\" --writes \"res://CoreEngine/...\" --before-dump \"BuildLogs/before.json\" --after-dump \"BuildLogs/after.json\" --summary-command \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --verifier \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --ux \"human review path\" --recovery \"rollback or reject path\" -NoBuild"
        };
    }

    private static int RunNextActions(string root, ToolManifest manifest, string[] args)
    {
        var opts = ParseOptions(args);
        var limit = ParsePositiveInt(opts.GetValueOrDefault("limit", ""), 5);
        var report = BuildNextActionsReport(root, manifest, limit);
        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatNextActionsSummary(report));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        return 0;
    }

    private static int RunMapSceneResourceReview(string root, ToolManifest manifest, string[] args)
    {
        var opts = ParseOptions(args);
        var limit = ParsePositiveInt(opts.GetValueOrDefault("limit", ""), 20);
        var report = BuildMapSceneResourceReviewReport(root, manifest, limit);
        var outPath = opts.GetValueOrDefault("out", "");
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            report.OutputPath = outPath;
            report.OutputWritten = true;
            var absoluteOutPath = ResolveRootedPath(root, outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutPath) ?? root);
            var jsonForFile = JsonSerializer.Serialize(report, JsonOptions);
            File.WriteAllText(absoluteOutPath, jsonForFile);
        }
        var json = JsonSerializer.Serialize(report, JsonOptions);
        if (HasOption(args, "summary"))
        {
            Console.WriteLine(FormatMapSceneResourceReviewSummary(report));
        }
        else
        {
            Console.WriteLine(json);
        }
        return report.PortalReviewExitCode == 0 ? 0 : 1;
    }

    private static ToolHandoffReport BuildHandoffReport(string root, ToolManifest manifest)
    {
        var sections = new List<ToolHandoffSection>
        {
            BuildInlineHandoffSection("toolhub-status", "ToolHub status", BuildStatus(root, manifest)),
            BuildInlineHandoffSection("dump-index", "ToolHub dump index", BuildDumpIndex(root))
        };

        sections.Add(RunCapturedHandoffSection(
            "map-status",
            "MapEditor map status",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "map-editor", ["status"])));
        sections.Add(RunCapturedHandoffSection(
            "map-portal-review",
            "MapEditor portal review",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "map-editor", ["portal-review"])));
        sections.Add(RunCapturedHandoffSection(
            "map-runtime-verify",
            "MapEditor runtime verification",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "map-editor", ["runtime-verify"])));
        sections.Add(RunCapturedHandoffSection(
            "map-ux-audit",
            "MapEditor UX audit",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "map-editor", ["ux-audit"])));
        sections.Add(RunCapturedHandoffSection(
            "resource-status",
            "ResourceConfig workflow status",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "resource-config", ["status"])));
        sections.Add(RunCapturedHandoffSection(
            "resource-verify-outputs",
            "ResourceConfig output verification",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "resource-config", WithDefaultResourceVerifyOutputsArgs(["verify-outputs"]))));

        return new ToolHandoffReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            SectionCount = sections.Count,
            FailedSectionCount = sections.Count(x => x.ExitCode != 0),
            Ok = sections.All(x => x.ExitCode == 0),
            Sections = sections
        };
    }

    private static string FormatHandoffSummary(ToolHandoffReport report)
    {
        var lines = new List<string>
        {
            "ToolHub handoff summary",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} (sections {report.SectionCount - report.FailedSectionCount}/{report.SectionCount}, failed {report.FailedSectionCount})"
        };

        if (TryGetSectionPayload(report, "toolhub-status", out var toolhub))
        {
            lines.Add(
                "ToolHub: " +
                $"tools={GetJsonInt(toolhub, "toolCount")} " +
                $"manifestIssues={GetJsonInt(toolhub, "manifestIssueCount")} " +
                $"outputs={GetJsonInt(toolhub, "outputRootFileCount")} files");
        }
        else
        {
            lines.Add("ToolHub: unavailable");
        }

        if (TryGetSectionPayload(report, "map-status", out var map))
        {
            lines.Add(
                "Map: " +
                $"maps={GetJsonInt(map, "mapCount")} " +
                $"links={GetJsonInt(map, "linkCount")} " +
                $"portals={GetJsonInt(map, "portalCount")} " +
                $"missingScenes={GetJsonInt(map, "missingSceneCount")} " +
                $"missingTargets={GetJsonInt(map, "linksWithMissingTargetsCount")} " +
                $"mapsWithoutPortals={GetJsonInt(map, "mapsWithoutPortalsCount")}");
        }
        else
        {
            lines.Add("Map: unavailable");
        }

        if (TryGetSectionPayload(report, "dump-index", out var dumpIndex))
        {
            lines.Add(
                "DumpIndex: " +
                $"artifacts={GetJsonInt(dumpIndex, "artifactCount")} " +
                $"existing={GetJsonInt(dumpIndex, "existingCount")} " +
                $"requiredMissing={GetJsonInt(dumpIndex, "missingRequiredCount")} " +
                $"canonical={GetJsonInt(dumpIndex, "canonicalCount")} " +
                $"review={GetJsonInt(dumpIndex, "reviewCount")}");
        }
        else
        {
            lines.Add("DumpIndex: unavailable");
        }

        if (TryGetSectionPayload(report, "map-portal-review", out var portalReview))
        {
            lines.Add(
                "PortalReview: " +
                $"mapsWithoutPortals={GetJsonInt(portalReview, "mapsWithoutPortalsCount")} " +
                $"portalsWithMissingTargets={GetJsonInt(portalReview, "portalsWithMissingTargetsCount")}");
        }
        else
        {
            lines.Add("PortalReview: unavailable");
        }

        if (TryGetSectionPayload(report, "map-runtime-verify", out var runtimeVerify))
        {
            lines.Add(
                "RuntimeVerify: " +
                $"ok={GetJsonBoolText(runtimeVerify, "ok")} " +
                $"issues={GetJsonInt(runtimeVerify, "issueCount")} " +
                $"portalTargets={GetJsonInt(runtimeVerify, "resolvedPortalTargetCount")}/{GetJsonInt(runtimeVerify, "portalTargetCount")} " +
                $"entryRooms={GetJsonInt(runtimeVerify, "resolvedEntryRoomCount")}/{GetJsonInt(runtimeVerify, "entryRoomCount")} " +
                $"checks={GetJsonInt(runtimeVerify, "checkCount")}");
        }
        else
        {
            lines.Add("RuntimeVerify: unavailable");
        }

        if (TryGetSectionPayload(report, "map-ux-audit", out var uxAudit))
        {
            lines.Add(
                "UxAudit: " +
                $"ok={GetJsonBoolText(uxAudit, "ok")} " +
                $"blocking={GetJsonInt(uxAudit, "blockingIssueCount")} " +
                $"warnings={GetJsonInt(uxAudit, "warningCount")} " +
                $"checks={GetJsonInt(uxAudit, "checkCount")}");
        }
        else
        {
            lines.Add("UxAudit: unavailable");
        }

        if (TryGetSectionPayload(report, "resource-status", out var resource))
        {
            lines.Add(
                "Resource: " +
                $"resources={GetJsonInt(resource, "index", "resourceCount")} " +
                $"planActions={GetJsonInt(resource, "plan", "actionCount")} " +
                $"warnings={GetJsonInt(resource, "plan", "warningCount")} " +
                $"decisions={GetJsonInt(resource, "decisions", "decisionCount")} " +
                $"accepted={GetJsonInt(resource, "decisions", "acceptCount")} " +
                $"apply={GetJsonString(resource, "apply", "mode")} " +
                $"wouldApply={GetJsonInt(resource, "apply", "wouldApplyCount")} " +
                $"skipped={GetJsonInt(resource, "apply", "skippedCount")} " +
                $"approvedDeps={GetJsonInt(resource, "approvedDependencies", "dependencyCount")} " +
                $"cleanupCandidates={GetJsonInt(resource, "cleanupCandidates", "candidateCount")}");
        }
        else
        {
            lines.Add("Resource: unavailable");
        }

        if (TryGetSectionPayload(report, "resource-verify-outputs", out var verify))
        {
            lines.Add(
                "Verify: " +
                $"ok={GetJsonBoolText(verify, "ok")} " +
                $"issues={GetJsonInt(verify, "issueCount")} " +
                $"acceptedExecutable={GetJsonInt(verify, "acceptedExecutableCount")} " +
                $"deps={GetJsonInt(verify, "actualDependencyCount")}/{GetJsonInt(verify, "expectedDependencyCount")} " +
                $"cleanup={GetJsonInt(verify, "actualCleanupCandidateCount")}/{GetJsonInt(verify, "expectedCleanupCandidateCount")}");
        }
        else
        {
            lines.Add("Verify: unavailable");
        }

        lines.Add("Sections:");
        foreach (var section in report.Sections)
        {
            lines.Add($"  {(section.ExitCode == 0 ? "OK" : "FAIL")} {section.Id} exit={section.ExitCode} - {section.Title}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryGetSectionPayload(ToolHandoffReport report, string id, out JsonElement payload)
    {
        var section = report.Sections.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (section?.Payload is JsonElement jsonElement)
        {
            payload = jsonElement;
            return true;
        }
        if (section?.Payload != null)
        {
            payload = JsonSerializer.SerializeToElement(section.Payload, JsonOptions);
            return true;
        }

        payload = default;
        return false;
    }

    private static ToolClosureGateReport BuildClosureGateReport(string root, ToolManifest manifest)
    {
        var handoff = BuildHandoffReport(root, manifest);
        var gates = new List<ToolClosureGate>();

        if (TryGetSectionPayload(handoff, "dump-index", out var dumpIndex))
        {
            var missingRequired = GetJsonInt(dumpIndex, "missingRequiredCount");
            var artifactCount = GetJsonInt(dumpIndex, "artifactCount");
            var existingCount = GetJsonInt(dumpIndex, "existingCount");
            gates.Add(new ToolClosureGate
            {
                Id = "human-friendly-dumps",
                Title = "Human-friendly data dumps",
                Status = missingRequired == 0 ? "ok" : "fail",
                CurrentBaselineSatisfied = missingRequired == 0,
                Evidence = $"dump-index artifacts={artifactCount} existing={existingCount} requiredMissing={missingRequired} canonical={GetJsonInt(dumpIndex, "canonicalCount")} review={GetJsonInt(dumpIndex, "reviewCount")}",
                Gap = missingRequired == 0
                    ? "Current read-only/generated baseline is covered. Future editable data surfaces must be added to dump-index with refresh and verify commands."
                    : "One or more required dump artifacts are missing.",
                VerifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 dump-index --summary -NoBuild",
                RequiredForMutation = true,
                FutureMutationRequirement = "Every mutating workflow needs a before/after machine-readable artifact plus a concise human summary or review report."
            });
        }
        else
        {
            gates.Add(BuildUnavailableClosureGate(
                "human-friendly-dumps",
                "Human-friendly data dumps",
                "ToolHub handoff did not provide a dump-index section.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 dump-index --summary -NoBuild"));
        }

        if (TryGetSectionPayload(handoff, "map-runtime-verify", out var runtimeVerify))
        {
            var ok = GetJsonBool(runtimeVerify, "ok");
            var issueCount = GetJsonInt(runtimeVerify, "issueCount");
            gates.Add(new ToolClosureGate
            {
                Id = "game-effect-verification",
                Title = "Game-effect verification",
                Status = ok && issueCount == 0 ? "partial" : "fail",
                CurrentBaselineSatisfied = ok && issueCount == 0,
                Evidence = $"map runtime-verify ok={ok.ToString().ToLowerInvariant()} issues={issueCount} portalTargets={GetJsonInt(runtimeVerify, "resolvedPortalTargetCount")}/{GetJsonInt(runtimeVerify, "portalTargetCount")} entryRooms={GetJsonInt(runtimeVerify, "resolvedEntryRoomCount")}/{GetJsonInt(runtimeVerify, "entryRoomCount")}",
                Gap = ok && issueCount == 0
                    ? "Static map runtime wiring is proven for the current read-only baseline. Real tool edits still need a verifier proving CoreEngine consumes the exact written result."
                    : "Current map runtime verification is failing or incomplete.",
                VerifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild",
                RequiredForMutation = true,
                FutureMutationRequirement = "Every mutating map/resource command needs a paired Testor command that proves the game-visible effect of that write."
            });
        }
        else
        {
            gates.Add(BuildUnavailableClosureGate(
                "game-effect-verification",
                "Game-effect verification",
                "ToolHub handoff did not provide a map-runtime-verify section.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild"));
        }

        if (TryGetSectionPayload(handoff, "map-ux-audit", out var uxAudit))
        {
            var ok = GetJsonBool(uxAudit, "ok");
            var blocking = GetJsonInt(uxAudit, "blockingIssueCount");
            gates.Add(new ToolClosureGate
            {
                Id = "human-ux-review",
                Title = "Human UX and recovery",
                Status = ok && blocking == 0 ? "partial" : "fail",
                CurrentBaselineSatisfied = ok && blocking == 0,
                Evidence = $"map ux-audit ok={ok.ToString().ToLowerInvariant()} blocking={blocking} warnings={GetJsonInt(uxAudit, "warningCount")} checks={GetJsonInt(uxAudit, "checkCount")}",
                Gap = ok && blocking == 0
                    ? "Static MapEditor UX audit is green. A live human click-through is still required before treating interactive UX as fully accepted."
                    : "Current MapEditor UX audit has blocking issues.",
                VerifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-review --summary -NoBuild",
                RequiredForMutation = true,
                FutureMutationRequirement = "Interactive mutating flows need visible labels, status, save/apply feedback, validation feedback, and a recovery path. CLI-only flows need equivalent review/recovery output."
            });
        }
        else
        {
            gates.Add(BuildUnavailableClosureGate(
                "human-ux-review",
                "Human UX and recovery",
                "ToolHub handoff did not provide a map-ux-audit section.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-audit --summary -NoBuild"));
        }

        return new ToolClosureGateReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            HandoffOk = handoff.Ok,
            HandoffFailedSectionCount = handoff.FailedSectionCount,
            CurrentBaselineOk = handoff.Ok && gates.All(x => x.CurrentBaselineSatisfied),
            MutatingWorkflowReady = false,
            GateCount = gates.Count,
            OkGateCount = gates.Count(x => x.Status.Equals("ok", StringComparison.OrdinalIgnoreCase)),
            PartialGateCount = gates.Count(x => x.Status.Equals("partial", StringComparison.OrdinalIgnoreCase)),
            FailedGateCount = gates.Count(x => x.Status.Equals("fail", StringComparison.OrdinalIgnoreCase)),
            MutationAcceptanceRule = "Do not accept a future map/resource mutating workflow unless it has a dump-indexed artifact, a Testor game-effect verifier for the exact write, and human UX/recovery evidence.",
            Gates = gates,
            RecommendedCommands =
            [
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 closure-gates --summary -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 dump-index --summary -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-review --summary -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 next-actions --summary -NoBuild"
            ]
        };
    }

    private static ToolClosureGate BuildUnavailableClosureGate(string id, string title, string gap, string verifyCommand)
    {
        return new ToolClosureGate
        {
            Id = id,
            Title = title,
            Status = "fail",
            CurrentBaselineSatisfied = false,
            Evidence = "unavailable",
            Gap = gap,
            VerifyCommand = verifyCommand,
            RequiredForMutation = true,
            FutureMutationRequirement = "The gate must be restored before any mutating workflow can be accepted."
        };
    }

    private static string FormatClosureGateSummary(ToolClosureGateReport report)
    {
        var lines = new List<string>
        {
            "ToolHub closure gates",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Current baseline: {(report.CurrentBaselineOk ? "OK" : "FAILED")} (handoffOk={report.HandoffOk.ToString().ToLowerInvariant()} failedSections={report.HandoffFailedSectionCount})",
            $"Future mutating workflows: {(report.MutatingWorkflowReady ? "READY" : "NOT READY")} (per-command evidence required)",
            $"Gate counts: ok={report.OkGateCount} partial={report.PartialGateCount} failed={report.FailedGateCount}",
            "Gates:"
        };

        foreach (var gate in report.Gates)
        {
            lines.Add($"  [{gate.Status}] {gate.Id}: {gate.Title}");
            lines.Add($"    current baseline: {(gate.CurrentBaselineSatisfied ? "satisfied" : "not satisfied")}");
            lines.Add($"    evidence: {gate.Evidence}");
            lines.Add($"    gap: {gate.Gap}");
            lines.Add($"    verify: {gate.VerifyCommand}");
            lines.Add($"    mutation rule: {gate.FutureMutationRequirement}");
        }

        lines.Add("Acceptance rule:");
        lines.Add($"  {report.MutationAcceptanceRule}");
        lines.Add("Recommended commands:");
        foreach (var command in report.RecommendedCommands)
        {
            lines.Add($"  {command}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ToolMutationPlanReport BuildMutationPlanReport(string root, string[] args)
    {
        var opts = ParseOptions(args);
        var inputPath = GetOption(opts, "in", "input");
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            var loaded = ReadMutationPlanReport(root, inputPath);
            RecomputeMutationPlanChecks(loaded);
            loaded.ProjectRoot = root;
            loaded.InputPath = inputPath.Replace('\\', '/');
            loaded.OutputPath = GetOption(opts, "out").Replace('\\', '/');
            loaded.OutputWritten = false;
            return loaded;
        }

        var domain = GetOption(opts, "domain");
        var intent = GetOption(opts, "intent");
        var writes = SplitPlanValues(GetOption(opts, "writes"));
        var beforeDump = GetOption(opts, "before-dump", "beforeDump");
        var afterDump = GetOption(opts, "after-dump", "afterDump");
        var summaryCommand = GetOption(opts, "summary-command", "summaryCommand");
        var verifier = GetOption(opts, "verifier");
        var uxPath = GetOption(opts, "ux", "ux-path", "uxPath");
        var recovery = GetOption(opts, "recovery");
        var owner = GetOption(opts, "owner");
        var notes = GetOption(opts, "notes");
        var checks = new List<ToolMutationPlanCheck>
        {
            BuildMutationPlanCheck("domain", "Mutation domain", !string.IsNullOrWhiteSpace(domain), "Provide --domain map|resource|resource-map|event|other."),
            BuildMutationPlanCheck("intent", "Human intent", !string.IsNullOrWhiteSpace(intent), "Provide --intent with the exact change being designed."),
            BuildMutationPlanCheck("writes", "Declared write targets", writes.Count > 0, "Provide --writes with semicolon-separated files, resource paths, or output artifacts."),
            BuildMutationPlanCheck("before-dump", "Before/after dump", !string.IsNullOrWhiteSpace(beforeDump) && !string.IsNullOrWhiteSpace(afterDump), "Provide --before-dump and --after-dump so the edit can be inspected as data."),
            BuildMutationPlanCheck("summary-command", "Human-readable summary", !string.IsNullOrWhiteSpace(summaryCommand), "Provide --summary-command for a concise human review path."),
            BuildMutationPlanCheck("verifier", "Game-effect verifier", !string.IsNullOrWhiteSpace(verifier), "Provide --verifier with the Testor command that proves CoreEngine sees the written result."),
            BuildMutationPlanCheck("ux-recovery", "UX/recovery path", !string.IsNullOrWhiteSpace(uxPath) && !string.IsNullOrWhiteSpace(recovery), "Provide --ux and --recovery for human validation and backing away from a bad edit.")
        };

        var missing = checks.Where(x => !x.Satisfied).Select(x => x.Id).ToList();
        return new ToolMutationPlanReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Domain = domain,
            Intent = intent,
            Owner = owner,
            Notes = notes,
            WriteTargets = writes,
            BeforeDump = beforeDump,
            AfterDump = afterDump,
            SummaryCommand = summaryCommand,
            VerifierCommand = verifier,
            UxPath = uxPath,
            RecoveryPath = recovery,
            InputPath = "",
            OutputPath = GetOption(opts, "out").Replace('\\', '/'),
            OutputWritten = false,
            ReadyForDesignReview = missing.Count == 0,
            MissingCount = missing.Count,
            MissingChecks = missing,
            CheckCount = checks.Count,
            Checks = checks,
            AcceptanceRule = "This is a design/review plan only. Do not implement or accept the mutating command until all checks are satisfied and the verifier proves the exact written effect.",
            SuggestedNextCommand = missing.Count == 0
                ? "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 closure-gates --summary -NoBuild"
                : BuildMutationPlanTemplateCommand(domain)
        };
    }

    private static ToolMutationPlanReport ReadMutationPlanReport(string root, string inputPath)
    {
        var absoluteInputPath = ResolveRootedPath(root, inputPath);
        if (!File.Exists(absoluteInputPath))
        {
            throw new FileNotFoundException($"mutation plan input not found: {inputPath}", absoluteInputPath);
        }

        return JsonSerializer.Deserialize<ToolMutationPlanReport>(File.ReadAllText(absoluteInputPath), JsonOptions)
            ?? throw new InvalidDataException($"mutation plan input is invalid: {inputPath}");
    }

    private static void WriteMutationPlanReport(string root, ToolMutationPlanReport report, string outputPath)
    {
        report.OutputPath = outputPath.Replace('\\', '/');
        report.OutputWritten = true;
        var absoluteOutputPath = ResolveRootedPath(root, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath) ?? root);
        File.WriteAllText(absoluteOutputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static void RecomputeMutationPlanChecks(ToolMutationPlanReport report)
    {
        var checks = new List<ToolMutationPlanCheck>
        {
            BuildMutationPlanCheck("domain", "Mutation domain", !string.IsNullOrWhiteSpace(report.Domain), "Provide --domain map|resource|resource-map|event|other."),
            BuildMutationPlanCheck("intent", "Human intent", !string.IsNullOrWhiteSpace(report.Intent), "Provide --intent with the exact change being designed."),
            BuildMutationPlanCheck("writes", "Declared write targets", report.WriteTargets.Count > 0, "Provide --writes with semicolon-separated files, resource paths, or output artifacts."),
            BuildMutationPlanCheck("before-dump", "Before/after dump", !string.IsNullOrWhiteSpace(report.BeforeDump) && !string.IsNullOrWhiteSpace(report.AfterDump), "Provide --before-dump and --after-dump so the edit can be inspected as data."),
            BuildMutationPlanCheck("summary-command", "Human-readable summary", !string.IsNullOrWhiteSpace(report.SummaryCommand), "Provide --summary-command for a concise human review path."),
            BuildMutationPlanCheck("verifier", "Game-effect verifier", !string.IsNullOrWhiteSpace(report.VerifierCommand), "Provide --verifier with the Testor command that proves CoreEngine sees the written result."),
            BuildMutationPlanCheck("ux-recovery", "UX/recovery path", !string.IsNullOrWhiteSpace(report.UxPath) && !string.IsNullOrWhiteSpace(report.RecoveryPath), "Provide --ux and --recovery for human validation and backing away from a bad edit.")
        };

        var missing = checks.Where(x => !x.Satisfied).Select(x => x.Id).ToList();
        report.ReadyForDesignReview = missing.Count == 0;
        report.MissingCount = missing.Count;
        report.MissingChecks = missing;
        report.CheckCount = checks.Count;
        report.Checks = checks;
        report.AcceptanceRule = string.IsNullOrWhiteSpace(report.AcceptanceRule)
            ? "This is a design/review plan only. Do not implement or accept the mutating command until all checks are satisfied and the verifier proves the exact written effect."
            : report.AcceptanceRule;
        report.SuggestedNextCommand = missing.Count == 0
            ? "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 closure-gates --summary -NoBuild"
            : BuildMutationPlanTemplateCommand(report.Domain);
    }

    private static ToolMutationPlanCheck BuildMutationPlanCheck(string id, string title, bool satisfied, string requirement)
    {
        return new ToolMutationPlanCheck
        {
            Id = id,
            Title = title,
            Satisfied = satisfied,
            Requirement = requirement
        };
    }

    private static List<string> SplitPlanValues(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
    }

    private static string BuildMutationPlanTemplateCommand(string domain)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "map" : domain;
        return "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary " +
            $"--domain {normalizedDomain} " +
            "--intent \"describe intended edit\" " +
            "--writes \"res://CoreEngine/...\" " +
            "--before-dump \"BuildLogs/before.json\" " +
            "--after-dump \"BuildLogs/after.json\" " +
            "--summary-command \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" " +
            "--verifier \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" " +
            "--ux \"human review path\" " +
            "--recovery \"rollback or reject path\" -NoBuild";
    }

    private static string FormatMutationPlanSummary(ToolMutationPlanReport report)
    {
        var lines = new List<string>
        {
            "ToolHub mutation plan",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Ready for design review: {(report.ReadyForDesignReview ? "YES" : "NO")} missing={report.MissingCount}/{report.CheckCount}",
            $"Domain: {BlankAsUnknown(report.Domain)}",
            $"Intent: {BlankAsUnknown(report.Intent)}",
            $"Owner: {BlankAsUnknown(report.Owner)}",
            "Write targets:"
        };

        if (report.WriteTargets.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var target in report.WriteTargets)
        {
            lines.Add($"  {target}");
        }

        lines.Add($"Before dump: {BlankAsUnknown(report.BeforeDump)}");
        lines.Add($"After dump: {BlankAsUnknown(report.AfterDump)}");
        lines.Add($"Summary command: {BlankAsUnknown(report.SummaryCommand)}");
        lines.Add($"Verifier command: {BlankAsUnknown(report.VerifierCommand)}");
        lines.Add($"UX path: {BlankAsUnknown(report.UxPath)}");
        lines.Add($"Recovery path: {BlankAsUnknown(report.RecoveryPath)}");
        lines.Add($"Input artifact: {BlankAsUnknown(report.InputPath)}");
        lines.Add($"Output artifact: {BlankAsUnknown(report.OutputPath)} written={report.OutputWritten.ToString().ToLowerInvariant()}");
        lines.Add("Checks:");
        foreach (var check in report.Checks)
        {
            lines.Add($"  [{(check.Satisfied ? "ok" : "missing")}] {check.Id}: {check.Title}");
            lines.Add($"    requirement: {check.Requirement}");
        }

        lines.Add("Acceptance rule:");
        lines.Add($"  {report.AcceptanceRule}");
        if (!string.IsNullOrWhiteSpace(report.Notes))
        {
            lines.Add($"Notes: {report.Notes}");
        }
        lines.Add($"Suggested next command: {report.SuggestedNextCommand}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatMutationPlanVerifySummary(ToolMutationPlanVerifyReport report)
    {
        var lines = new List<string>
        {
            "ToolHub mutation plan verification",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Directory: {report.Directory}",
            $"Pattern: {report.Pattern}",
            $"Overall: {(report.Ok ? "OK" : "FAILED")} plans={report.PlanCount} ready={report.ReadyCount} failed={report.FailedCount}",
            "Plans:"
        };

        if (report.Plans.Count == 0)
        {
            lines.Add("  none");
            lines.Add($"Suggested command: {report.SuggestedCommand}");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var plan in report.Plans)
        {
            var state = plan.ReadyForDesignReview ? "OK" : "MISSING";
            lines.Add($"  {state} {plan.Path} domain={BlankAsUnknown(plan.Domain)} missing={plan.MissingCount} intent={BlankAsUnknown(plan.Intent)}");
            if (plan.MissingChecks.Count > 0)
            {
                lines.Add($"    missing: {string.Join(",", plan.MissingChecks)}");
            }
            if (!string.IsNullOrWhiteSpace(plan.Issue))
            {
                lines.Add($"    issue: {plan.Issue}");
            }
        }

        lines.Add($"Suggested command: {report.SuggestedCommand}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BlankAsUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(missing)" : value;
    }

    private static int GetJsonInt(JsonElement root, params string[] path)
    {
        return TryGetJsonProperty(root, path, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var result)
                ? result
                : 0;
    }

    private static string GetJsonString(JsonElement root, params string[] path)
    {
        return TryGetJsonProperty(root, path, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static string GetJsonBoolText(JsonElement root, params string[] path)
    {
        return TryGetJsonProperty(root, path, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean().ToString().ToLowerInvariant()
            : "unknown";
    }

    private static Dictionary<string, int> ReadJsonIntObject(JsonElement root, params string[] path)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetJsonProperty(root, path, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var count))
            {
                result[property.Name] = count;
            }
        }

        return result;
    }

    private static string FormatIntCounts(IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
        {
            return "none";
        }

        return string.Join(" ", counts
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key}={x.Value}"));
    }

    private static bool TryGetJsonProperty(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var part in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value))
            {
                value = default;
                return false;
            }
        }
        return true;
    }

    private static ToolNextActionsReport BuildNextActionsReport(string root, ToolManifest manifest, int sampleLimit)
    {
        var handoff = BuildHandoffReport(root, manifest);
        var actions = new List<ToolNextAction>();
        var portalCoverageClassifications = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var portalCoverageByScene = new Dictionary<string, PortalCoverageHint>(StringComparer.OrdinalIgnoreCase);

        if (!handoff.Ok)
        {
            foreach (var section in handoff.Sections.Where(x => x.ExitCode != 0))
            {
                actions.Add(new ToolNextAction
                {
                    Id = "fix-" + section.Id,
                    Area = "toolhub",
                    Severity = "error",
                    Title = $"Fix failing handoff section: {section.Title}",
                    Reason = $"Section `{section.Id}` exited with {section.ExitCode}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 handoff --summary -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "toolhub-status", out var toolhub))
        {
            var manifestIssues = GetJsonInt(toolhub, "manifestIssueCount");
            if (manifestIssues > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "fix-manifest",
                    Area = "toolhub",
                    Severity = "error",
                    Title = "Fix tool manifest issues",
                    Reason = $"ToolHub reports {manifestIssues} manifest issue(s).",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 validate-manifest -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "dump-index", out var dumpIndex))
        {
            var missingRequired = GetJsonInt(dumpIndex, "missingRequiredCount");
            if (missingRequired > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "refresh-required-dumps",
                    Area = "toolhub",
                    Severity = "error",
                    Title = "Refresh missing required tool dumps",
                    Reason = $"Dump index reports {missingRequired} missing required artifact(s).",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 dump-index --summary -NoBuild"
                });
            }
            else
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-tool-closure-gates",
                    Area = "toolhub",
                    Severity = "info",
                    Title = "Review external-tool closure gates",
                    Reason = "Current read-only/generated baseline is green, but future mutating workflows still require dump, game-effect, and UX/recovery evidence.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 closure-gates --summary -NoBuild"
                });
                actions.Add(new ToolNextAction
                {
                    Id = "draft-mutation-plan-before-editing",
                    Area = "toolhub",
                    Severity = "info",
                    Title = "Draft a mutation plan before editing",
                    Reason = "Use a persisted pre-write plan to name write targets, before/after dumps, summary command, verifier, UX path, and recovery path before any map/resource mutation is implemented. Recheck it with mutation-plan --in before Coder starts writing.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary --out BuildLogs\\mutation_plan.json --domain map --intent \"describe intended edit\" --writes \"res://CoreEngine/...\" --before-dump \"BuildLogs/before.json\" --after-dump \"BuildLogs/after.json\" --summary-command \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --verifier \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --ux \"human review path\" --recovery \"rollback or reject path\" -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "map-status", out var map))
        {
            var missingScenes = GetJsonInt(map, "missingSceneCount");
            var missingTargets = GetJsonInt(map, "linksWithMissingTargetsCount");
            var mapsWithoutPortals = GetJsonInt(map, "mapsWithoutPortalsCount");
            if (missingScenes > 0 || missingTargets > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-map-graph-errors",
                    Area = "map",
                    Severity = "error",
                    Title = "Review broken map graph references",
                    Reason = $"Map status reports missingScenes={missingScenes}, missingTargets={missingTargets}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map status -NoBuild"
                });
            }
            if (mapsWithoutPortals > 0)
            {
                var classificationText = "";
                if (TryGetSectionPayload(handoff, "map-portal-review", out var portalReview))
                {
                    portalCoverageClassifications = ReadJsonIntObject(portalReview, "portalCoverageClassifications");
                    portalCoverageByScene = ReadPortalCoverageHints(portalReview);
                    classificationText = portalCoverageClassifications.Count == 0
                        ? ""
                        : " Current classifications: " + FormatIntCounts(portalCoverageClassifications) + ".";
                }
                actions.Add(new ToolNextAction
                {
                    Id = "review-map-portal-coverage",
                    Area = "map",
                    Severity = "info",
                    Title = "Review maps without portals",
                    Reason = $"Map status reports {mapsWithoutPortals} map(s) without portals. Confirm whether each is intentional.{classificationText}",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map portal-review --summary -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "map-runtime-verify", out var runtimeVerify))
        {
            var issueCount = GetJsonInt(runtimeVerify, "issueCount");
            if (issueCount > 0 || !GetJsonBool(runtimeVerify, "ok"))
            {
                actions.Add(new ToolNextAction
                {
                    Id = "fix-map-runtime-verification",
                    Area = "map",
                    Severity = "error",
                    Title = "Fix map runtime verification",
                    Reason = $"Map runtime verifier reports ok={GetJsonBoolText(runtimeVerify, "ok")} and issueCount={issueCount}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "map-ux-audit", out var uxAudit))
        {
            var blocking = GetJsonInt(uxAudit, "blockingIssueCount");
            if (blocking > 0 || !GetJsonBool(uxAudit, "ok"))
            {
                actions.Add(new ToolNextAction
                {
                    Id = "fix-map-ux-audit",
                    Area = "map",
                    Severity = "error",
                    Title = "Fix blocking MapEditor UX audit issues",
                    Reason = $"MapEditor UX audit reports ok={GetJsonBoolText(uxAudit, "ok")} and blockingIssueCount={blocking}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-audit --summary -NoBuild"
                });
            }
            else if (GetJsonInt(uxAudit, "warningCount") > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-map-ux-warnings",
                    Area = "map",
                    Severity = "warning",
                    Title = "Review MapEditor UX warnings",
                    Reason = $"MapEditor UX audit reports {GetJsonInt(uxAudit, "warningCount")} warning(s).",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-audit --summary -NoBuild"
                });
            }
            else
            {
                actions.Add(new ToolNextAction
                {
                    Id = "run-map-ux-walkthrough",
                    Area = "map",
                    Severity = "info",
                    Title = "Run MapEditor live UX walkthrough",
                    Reason = "Static MapEditor UX audit is green; the remaining UX gate needs a human click-through checklist and result record for import, inspect, edit preview, save/review, validation, and recovery flows.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-walkthrough --summary --out BuildLogs\\map_ux_walkthrough.json -NoBuild"
                });
                actions.Add(new ToolNextAction
                {
                    Id = "record-map-ux-review-result",
                    Area = "map",
                    Severity = "info",
                    Title = "Record MapEditor UX review result",
                    Reason = "After the human walkthrough, record reviewer, overall result, and per-step results so Testor can verify whether the interactive UX gate is accepted.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-review --summary --in BuildLogs\\map_ux_walkthrough.json --out BuildLogs\\map_ux_review_result.json --reviewer <name> --result pass --step-results \"launch=pass;import=pass;inspect=pass;edit-preview=pass;save-review=pass;error-recovery=pass;agent-mirror=pass\" -NoBuild"
                });
            }
        }

        var allResourceReviewCandidates = ReadAllResourceReviewCandidates(root);
        var resourceReviewBuckets = BuildResourceReviewBuckets(allResourceReviewCandidates);
        var mapSceneResourceReview = BuildMapSceneResourceReviewSummary(allResourceReviewCandidates, portalCoverageByScene);
        if (TryGetSectionPayload(handoff, "resource-status", out var resource))
        {
            var planActions = GetJsonInt(resource, "plan", "actionCount");
            var warnings = GetJsonInt(resource, "plan", "warningCount");
            var decisions = GetJsonInt(resource, "decisions", "decisionCount");
            var wouldApply = GetJsonInt(resource, "apply", "wouldApplyCount");
            var approvedDeps = GetJsonInt(resource, "approvedDependencies", "dependencyCount");
            var cleanupCandidates = GetJsonInt(resource, "cleanupCandidates", "candidateCount");
            var undecided = Math.Max(0, planActions - decisions);

            if (undecided > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-resource-plan-decisions",
                    Area = "resource",
                    Severity = warnings > 0 ? "warning" : "info",
                    Title = "Review undecided resource plan actions",
                    Reason = $"Resource workflow has {planActions} plan action(s), {decisions} decision(s), and {undecided} undecided action(s). Current review buckets: {FormatResourceReviewBuckets(resourceReviewBuckets)}.",
                    Command = resourceReviewBuckets.FirstOrDefault()?.ReviewCommand ?? "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource pending --summary --limit 20 -NoBuild"
                });
            }

            if (mapSceneResourceReview.ActionCount > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-map-scene-resource-overlap",
                    Area = "resource-map",
                    Severity = mapSceneResourceReview.WarningCount > 0 ? "warning" : "info",
                    Title = "Review map scenes in the resource plan",
                    Reason = $"Resource review includes {mapSceneResourceReview.ActionCount} undecided CoreEngine map-scene action(s) across {mapSceneResourceReview.UniqueSceneCount} scene(s), {mapSceneResourceReview.WarningCount} warning(s), and {mapSceneResourceReview.WithPortalHintCount} with portal-coverage hints. Classifications: {FormatIntCounts(mapSceneResourceReview.PortalClassificationCounts)}.",
                    Command = mapSceneResourceReview.ReviewCommand
                });
            }

            if (wouldApply > 0 || approvedDeps > 0 || cleanupCandidates > 0)
            {
                actions.Add(new ToolNextAction
                {
                    Id = "review-generated-resource-outputs",
                    Area = "resource",
                    Severity = "info",
                    Title = "Review generated resource approval outputs",
                    Reason = $"Current apply preview has wouldApply={wouldApply}, approvedDeps={approvedDeps}, cleanupCandidates={cleanupCandidates}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource verify-outputs -NoBuild"
                });
            }
        }

        if (TryGetSectionPayload(handoff, "resource-verify-outputs", out var verify))
        {
            var issueCount = GetJsonInt(verify, "issueCount");
            if (issueCount > 0 || !GetJsonBool(verify, "ok"))
            {
                actions.Add(new ToolNextAction
                {
                    Id = "fix-resource-output-verification",
                    Area = "resource",
                    Severity = "error",
                    Title = "Fix resource output verification issues",
                    Reason = $"Resource output verification reports ok={GetJsonBoolText(verify, "ok")} and issueCount={issueCount}.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource verify-outputs -NoBuild"
                });
            }
        }

        var samples = allResourceReviewCandidates.Take(Math.Max(0, sampleLimit)).ToList();
        AddPortalCoverageHints(samples, portalCoverageByScene);
        if (actions.Count == 0)
        {
            actions.Add(new ToolNextAction
            {
                Id = "maintain-tool-baseline",
                Area = "toolhub",
                Severity = "info",
                Title = "Tool baseline is green",
                Reason = "No blocking tool, map, or resource workflow issue was detected by the handoff checks.",
                Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 doctor -NoBuild"
            });
        }

        return new ToolNextActionsReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            HandoffOk = handoff.Ok,
            HandoffFailedSectionCount = handoff.FailedSectionCount,
            RecommendationCount = actions.Count,
            PortalCoverageClassificationCount = portalCoverageClassifications.Count,
            PortalCoverageClassifications = portalCoverageClassifications,
            ResourceReviewBucketCount = resourceReviewBuckets.Count,
            MapSceneResourceReviewCount = mapSceneResourceReview.ActionCount,
            MapSceneResourceReview = mapSceneResourceReview,
            ResourceReviewSampleCount = samples.Count,
            Recommendations = actions,
            ResourceReviewBuckets = resourceReviewBuckets,
            ResourceReviewSamples = samples
        };
    }

    private static string FormatNextActionsSummary(ToolNextActionsReport report)
    {
        var lines = new List<string>
        {
            "ToolHub next actions",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Handoff: {(report.HandoffOk ? "OK" : "FAILED")} (failed sections {report.HandoffFailedSectionCount})",
            "Recommendations:"
        };

        foreach (var action in report.Recommendations)
        {
            lines.Add($"  [{action.Severity}] {action.Area}/{action.Id}: {action.Title}");
            lines.Add($"    reason: {action.Reason}");
            lines.Add($"    command: {action.Command}");
        }

        lines.Add("Map portal classifications:");
        lines.Add("  " + FormatIntCounts(report.PortalCoverageClassifications));

        lines.Add("Resource review queues:");
        if (report.ResourceReviewBuckets.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var bucket in report.ResourceReviewBuckets)
        {
            var samples = bucket.SampleIds.Count == 0 ? "" : " samples=" + string.Join(",", bucket.SampleIds);
            lines.Add($"  [{bucket.Severity}] {bucket.Type}: count={bucket.Count}{samples}");
            lines.Add($"    command: {bucket.ReviewCommand}");
        }

        lines.Add("Map-scene resource review:");
        if (report.MapSceneResourceReview.ActionCount == 0)
        {
            lines.Add("  none");
        }
        else
        {
            var mapReview = report.MapSceneResourceReview;
            var samples = mapReview.SampleIds.Count == 0 ? "" : " samples=" + string.Join(",", mapReview.SampleIds);
            lines.Add($"  actions={mapReview.ActionCount} scenes={mapReview.UniqueSceneCount} warnings={mapReview.WarningCount} withMapHints={mapReview.WithPortalHintCount} withoutMapHints={mapReview.WithoutPortalHintCount}{samples}");
            lines.Add("  classifications: " + FormatIntCounts(mapReview.PortalClassificationCounts));
            lines.Add($"  command: {mapReview.ReviewCommand}");
        }

        lines.Add("Resource review samples:");
        if (report.ResourceReviewSamples.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var sample in report.ResourceReviewSamples)
        {
            var target = string.IsNullOrWhiteSpace(sample.Target) ? "" : " -> " + sample.Target;
            lines.Add($"  [{sample.Severity}] {sample.Id} {sample.Type}: {sample.Source}{target}");
            if (!string.IsNullOrWhiteSpace(sample.MapPortalClassification))
            {
                lines.Add($"    map hint: {sample.MapPortalClassification} confidence={sample.MapPortalConfidence}");
                lines.Add($"    map reason: {sample.MapPortalReason}");
            }
            lines.Add($"    recommendation: {sample.Recommendation}");
            lines.Add($"    decide: {sample.DecideCommand}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static MapSceneResourceReviewReport BuildMapSceneResourceReviewReport(string root, ToolManifest manifest, int limit)
    {
        var portalSection = RunCapturedHandoffSection(
            "map-portal-review",
            "MapEditor portal review",
            "dotnet",
            BuildDotnetToolArgs(root, manifest, "map-editor", ["portal-review"]));

        var portalCoverageByScene = portalSection.Payload is JsonElement portalReview
            ? ReadPortalCoverageHints(portalReview)
            : new Dictionary<string, PortalCoverageHint>(StringComparer.OrdinalIgnoreCase);
        var allCandidates = ReadAllResourceReviewCandidates(root);
        var summary = BuildMapSceneResourceReviewSummary(allCandidates, portalCoverageByScene);
        var candidates = allCandidates
            .Where(IsCoreEngineMapSceneReview)
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, limit))
            .ToList();
        AddPortalCoverageHints(candidates, portalCoverageByScene);

        return new MapSceneResourceReviewReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            PlanPath = Path.Combine("BuildLogs", "resource_plan.json"),
            DecisionsPath = Path.Combine("BuildLogs", "resource_decisions.json"),
            PortalReviewExitCode = portalSection.ExitCode,
            PortalReviewCommand = portalSection.Command,
            PortalReviewIssue = portalSection.ExitCode == 0 ? "" : FirstNonEmptyLine(portalSection.Stderr, portalSection.Stdout),
            Limit = limit,
            Summary = summary,
            CandidateCount = candidates.Count,
            Candidates = candidates
        };
    }

    private static string FormatMapSceneResourceReviewSummary(MapSceneResourceReviewReport report)
    {
        var lines = new List<string>
        {
            "ToolHub map-scene resource review",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Plan: {report.PlanPath}",
            $"Decisions: {report.DecisionsPath}",
            string.IsNullOrWhiteSpace(report.OutputPath) ? "Output: stdout" : $"Output: {report.OutputPath} written={report.OutputWritten}",
            $"Portal review: {(report.PortalReviewExitCode == 0 ? "OK" : "FAILED")} exit={report.PortalReviewExitCode}",
            $"Counts: actions={report.Summary.ActionCount} scenes={report.Summary.UniqueSceneCount} warnings={report.Summary.WarningCount} info={report.Summary.InfoCount}",
            $"Map hints: with={report.Summary.WithPortalHintCount} without={report.Summary.WithoutPortalHintCount} classifications={FormatIntCounts(report.Summary.PortalClassificationCounts)}",
            $"Focused queue: {report.Summary.ReviewCommand}",
            $"Showing: {report.CandidateCount}/{report.Summary.ActionCount} (limit {report.Limit})",
            "Candidates:"
        };

        if (!string.IsNullOrWhiteSpace(report.PortalReviewIssue))
        {
            lines.Add($"Portal review issue: {report.PortalReviewIssue}");
        }

        if (report.Candidates.Count == 0)
        {
            lines.Add("  none");
        }
        foreach (var candidate in report.Candidates)
        {
            var target = string.IsNullOrWhiteSpace(candidate.Target) ? "" : " -> " + candidate.Target;
            lines.Add($"  [{candidate.Severity}] {candidate.Id} {candidate.Type}: {candidate.Source}{target}");
            if (!string.IsNullOrWhiteSpace(candidate.MapPortalClassification))
            {
                lines.Add($"    map hint: {candidate.MapPortalClassification} confidence={candidate.MapPortalConfidence}");
                lines.Add($"    map reason: {candidate.MapPortalReason}");
            }
            lines.Add($"    recommendation: {candidate.Recommendation}");
            lines.Add($"    decide: {candidate.DecideCommand}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ToolDumpIndexReport BuildDumpIndex(string root)
    {
        var artifacts = new List<ToolDumpArtifact>
        {
            BuildDumpArtifact(root, "map-project", "map", "canonical", "BuildLogs/map_project.json", true,
                "Current imported MapEditor project graph.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map import --summary -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map validate --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-index", "resource", "canonical", "BuildLogs/resource_index.json", true,
                "Current resource graph with incoming/outgoing res:// references.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource refresh -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource status --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-audit", "resource", "review", "BuildLogs/resource_audit.json", true,
                "Read-only resource cleanup/configuration audit.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource audit --out BuildLogs\\resource_audit.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource pending --summary --limit 10 -NoBuild"),
            BuildDumpArtifact(root, "resource-plan", "resource", "review", "BuildLogs/resource_plan.json", true,
                "Candidate resource review actions before any real mutation.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource plan --out BuildLogs\\resource_plan.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource pending --summary --limit 10 -NoBuild"),
            BuildDumpArtifact(root, "map-scene-resource-review", "resource-map", "review", "BuildLogs/map_scene_resource_review.json", true,
                "Fused review of CoreEngine map-scene resource plan actions with MapEditor portal hints.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource map-review --out BuildLogs\\map_scene_resource_review.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource map-review --summary --limit 20 -NoBuild"),
            BuildDumpArtifact(root, "mutation-plan", "toolhub", "review", "BuildLogs/mutation_plan.json", false,
                "Persisted pre-write design plan for the next mutating map/resource workflow.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary --out BuildLogs\\mutation_plan.json --domain map --intent \"describe intended edit\" --writes \"res://CoreEngine/...\" --before-dump \"BuildLogs/before.json\" --after-dump \"BuildLogs/after.json\" --summary-command \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --verifier \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --ux \"human review path\" --recovery \"rollback or reject path\" -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary --in BuildLogs\\mutation_plan.json -NoBuild"),
            BuildDumpArtifact(root, "map-ux-walkthrough", "map", "review", "BuildLogs/map_ux_walkthrough.json", false,
                "Human live UX walkthrough checklist for MapEditor import, inspect, edit preview, save/review, validation, and recovery flows.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-walkthrough --summary --out BuildLogs\\map_ux_walkthrough.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-walkthrough --summary -NoBuild"),
            BuildDumpArtifact(root, "map-ux-review-result", "map", "review", "BuildLogs/map_ux_review_result.json", false,
                "Human-recorded MapEditor UX walkthrough result with reviewer, overall result, per-step pass/partial/fail state, and remaining issues.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-review --summary --in BuildLogs\\map_ux_walkthrough.json --out BuildLogs\\map_ux_review_result.json --reviewer <name> --result pass --step-results \"launch=pass;import=pass;inspect=pass;edit-preview=pass;save-review=pass;error-recovery=pass;agent-mirror=pass\" -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map ux-review --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-decisions", "resource", "review", "BuildLogs/resource_decisions.json", false,
                "Human/Agent decision ledger for resource plan actions.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource decide --id <resource-plan-id> --decision defer --note \"owner review\" -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource status --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-apply-preview", "resource", "validation", "BuildLogs/resource_apply_preview.json", true,
                "Preview of accepted resource plan actions.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource apply --out BuildLogs\\resource_apply_preview.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource verify-outputs --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-approved-dependencies", "resource", "approval-output", "BuildLogs/resource_approved_dependencies.json", false,
                "Generated allow-list style output for accepted external dependencies.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource apply --execute --out BuildLogs\\resource_apply_preview.json --approved-out BuildLogs\\resource_approved_dependencies.json --cleanup-out BuildLogs\\resource_cleanup_candidates.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource verify-outputs --summary -NoBuild"),
            BuildDumpArtifact(root, "resource-cleanup-candidates", "resource", "approval-output", "BuildLogs/resource_cleanup_candidates.json", false,
                "Generated candidate list for accepted unreferenced-resource cleanup reviews.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource apply --execute --out BuildLogs\\resource_apply_preview.json --approved-out BuildLogs\\resource_approved_dependencies.json --cleanup-out BuildLogs\\resource_cleanup_candidates.json -NoBuild",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource verify-outputs --summary -NoBuild"),
            BuildDumpArtifact(root, "godot-import-log", "validation", "log", "BuildLogs/Godot_resource_import.log", false,
                "Latest Godot resource import log from build.ps1.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\build.ps1",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\build.ps1"),
            BuildDumpArtifact(root, "godot-smoke-log", "validation", "log", "BuildLogs/Godot_smoke_run.log", false,
                "Latest Godot smoke run log from build.ps1.",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\build.ps1",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\build.ps1")
        };

        return new ToolDumpIndexReport
        {
            ProjectRoot = root,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ArtifactCount = artifacts.Count,
            ExistingCount = artifacts.Count(x => x.Exists),
            MissingRequiredCount = artifacts.Count(x => x.Required && !x.Exists),
            CanonicalCount = artifacts.Count(x => x.Role.Equals("canonical", StringComparison.OrdinalIgnoreCase)),
            ReviewCount = artifacts.Count(x => x.Role.Equals("review", StringComparison.OrdinalIgnoreCase)),
            ValidationCount = artifacts.Count(x => x.Role.Equals("validation", StringComparison.OrdinalIgnoreCase) || x.Role.Equals("log", StringComparison.OrdinalIgnoreCase)),
            Artifacts = artifacts
        };
    }

    private static ToolDumpArtifact BuildDumpArtifact(
        string root,
        string id,
        string domain,
        string role,
        string path,
        bool required,
        string description,
        string refreshCommand,
        string verifyCommand)
    {
        var absolutePath = ResolveRootedPath(root, path);
        var exists = File.Exists(absolutePath);
        var info = exists ? new FileInfo(absolutePath) : null;
        return new ToolDumpArtifact
        {
            Id = id,
            Domain = domain,
            Role = role,
            Path = path.Replace('\\', '/'),
            Required = required,
            Exists = exists,
            Bytes = info?.Length ?? 0,
            LastWriteTimeUtc = info?.LastWriteTimeUtc.ToString("O") ?? "",
            Description = description,
            RefreshCommand = refreshCommand,
            VerifyCommand = verifyCommand
        };
    }

    private static string FormatDumpIndexSummary(ToolDumpIndexReport report)
    {
        var lines = new List<string>
        {
            "ToolHub dump index",
            $"Project: {report.ProjectRoot}",
            $"Generated UTC: {report.GeneratedAtUtc}",
            $"Overall: {(report.MissingRequiredCount == 0 ? "OK" : "MISSING")} requiredMissing={report.MissingRequiredCount}",
            $"Counts: artifacts={report.ArtifactCount} existing={report.ExistingCount} canonical={report.CanonicalCount} review={report.ReviewCount} validation={report.ValidationCount}",
            "Artifacts:"
        };

        foreach (var artifact in report.Artifacts.OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Role, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            var state = artifact.Exists ? "OK" : artifact.Required ? "MISSING" : "optional";
            lines.Add($"  {state} [{artifact.Domain}/{artifact.Role}] {artifact.Path} bytes={artifact.Bytes}");
            lines.Add($"    {artifact.Description}");
            lines.Add($"    refresh: {artifact.RefreshCommand}");
            lines.Add($"    verify: {artifact.VerifyCommand}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<ResourceReviewCandidate> ReadResourceReviewCandidates(string root, int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        return ReadAllResourceReviewCandidates(root).Take(limit).ToList();
    }

    private static List<ResourceReviewCandidate> ReadAllResourceReviewCandidates(string root)
    {
        var planPath = Path.Combine(root, "BuildLogs", "resource_plan.json");
        if (!File.Exists(planPath))
        {
            return [];
        }

        var decidedIds = ReadResourceDecisionIds(Path.Combine(root, "BuildLogs", "resource_decisions.json"));
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(planPath));
            if (!doc.RootElement.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return actions
                .EnumerateArray()
                .Select(ToResourceReviewCandidate)
                .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !decidedIds.Contains(x.Id))
                .OrderBy(x => SeverityRank(x.Severity))
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static MapSceneResourceReviewSummary BuildMapSceneResourceReviewSummary(
        List<ResourceReviewCandidate> candidates,
        Dictionary<string, PortalCoverageHint> portalCoverageByScene)
    {
        var mapCandidates = candidates
            .Where(IsCoreEngineMapSceneReview)
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AddPortalCoverageHints(mapCandidates, portalCoverageByScene);

        var classifications = mapCandidates
            .Where(x => !string.IsNullOrWhiteSpace(x.MapPortalClassification))
            .GroupBy(x => x.MapPortalClassification, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return new MapSceneResourceReviewSummary
        {
            ActionCount = mapCandidates.Count,
            UniqueSceneCount = mapCandidates
                .Select(x => x.Source)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            WarningCount = mapCandidates.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            InfoCount = mapCandidates.Count(x => string.Equals(x.Severity, "info", StringComparison.OrdinalIgnoreCase)),
            WithPortalHintCount = mapCandidates.Count(x => !string.IsNullOrWhiteSpace(x.MapPortalClassification)),
            WithoutPortalHintCount = mapCandidates.Count(x => string.IsNullOrWhiteSpace(x.MapPortalClassification)),
            PortalClassificationCounts = classifications,
            SampleIds = mapCandidates.Take(8).Select(x => x.Id).ToList(),
            ReviewCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource map-review --summary --limit 20 -NoBuild"
        };
    }

    private static Dictionary<string, PortalCoverageHint> ReadPortalCoverageHints(JsonElement portalReview)
    {
        var hints = new Dictionary<string, PortalCoverageHint>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetJsonProperty(portalReview, ["mapsWithoutPortals"], out var mapsWithoutPortals) ||
            mapsWithoutPortals.ValueKind != JsonValueKind.Array)
        {
            return hints;
        }

        foreach (var item in mapsWithoutPortals.EnumerateArray())
        {
            var scenePath = GetJsonString(item, "scenePath");
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                continue;
            }

            hints[scenePath] = new PortalCoverageHint
            {
                Classification = GetJsonString(item, "coverageClassification"),
                Confidence = GetJsonString(item, "classificationConfidence"),
                Reason = GetJsonString(item, "classificationReason"),
                Recommendation = GetJsonString(item, "recommendation")
            };
        }

        return hints;
    }

    private static void AddPortalCoverageHints(List<ResourceReviewCandidate> samples, Dictionary<string, PortalCoverageHint> portalCoverageByScene)
    {
        if (portalCoverageByScene.Count == 0)
        {
            return;
        }

        foreach (var sample in samples)
        {
            var path = sample.Source;
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("res://CoreEngine/Maps/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (portalCoverageByScene.TryGetValue(path, out var hint))
            {
                sample.MapPortalClassification = hint.Classification;
                sample.MapPortalConfidence = hint.Confidence;
                sample.MapPortalReason = hint.Reason;
                sample.MapPortalRecommendation = hint.Recommendation;
            }
        }
    }

    private static bool IsCoreEngineMapSceneReview(ResourceReviewCandidate candidate)
    {
        return candidate.Source.StartsWith("res://CoreEngine/Maps/", StringComparison.OrdinalIgnoreCase) &&
            candidate.Source.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ResourceReviewBucket> BuildResourceReviewBuckets(List<ResourceReviewCandidate> candidates)
    {
        return candidates
            .GroupBy(x => new
            {
                Severity = string.IsNullOrWhiteSpace(x.Severity) ? "unknown" : x.Severity,
                Type = string.IsNullOrWhiteSpace(x.Type) ? "unknown" : x.Type
            })
            .OrderBy(x => SeverityRank(x.Key.Severity))
            .ThenByDescending(x => x.Count())
            .ThenBy(x => x.Key.Type, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ResourceReviewBucket
            {
                Severity = x.Key.Severity,
                Type = x.Key.Type,
                Count = x.Count(),
                SampleIds = x.OrderBy(sample => sample.Id, StringComparer.OrdinalIgnoreCase).Take(5).Select(sample => sample.Id).ToList(),
                ReviewCommand = $"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource pending --summary --severity {x.Key.Severity} --type {x.Key.Type} --limit 20 -NoBuild"
            })
            .ToList();
    }

    private static string FormatResourceReviewBuckets(IReadOnlyCollection<ResourceReviewBucket> buckets)
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

    private static HashSet<string> ReadResourceDecisionIds(string decisionsPath)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(decisionsPath))
        {
            return ids;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(decisionsPath));
            if (!doc.RootElement.TryGetProperty("decisions", out var decisions) || decisions.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var decision in decisions.EnumerateArray())
            {
                var id = GetJsonString(decision, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }
        catch
        {
            return ids;
        }

        return ids;
    }

    private static ResourceReviewCandidate ToResourceReviewCandidate(JsonElement action)
    {
        var id = GetJsonString(action, "id");
        return new ResourceReviewCandidate
        {
            Id = id,
            Type = GetJsonString(action, "type"),
            Severity = GetJsonString(action, "severity"),
            Source = GetJsonString(action, "source"),
            Target = GetJsonString(action, "target"),
            Recommendation = GetJsonString(action, "recommendation"),
            DecideCommand = string.IsNullOrWhiteSpace(id)
                ? ""
                : $"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource decide --id {id} --decision defer --note \"owner review\" -NoBuild"
        };
    }

    private static int SeverityRank(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => 0,
            "warning" => 1,
            _ => 2
        };
    }

    private static string FirstNonEmptyLine(params string[] values)
    {
        foreach (var value in values)
        {
            var line = value
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return "";
    }

    private static bool GetJsonBool(JsonElement root, params string[] path)
    {
        return TryGetJsonProperty(root, path, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static int ParsePositiveInt(string value, int fallback)
    {
        return int.TryParse(value, out var result) && result > 0 ? result : fallback;
    }

    private static ToolHandoffSection BuildInlineHandoffSection(string id, string title, object payload)
    {
        return new ToolHandoffSection
        {
            Id = id,
            Title = title,
            Command = "inline",
            ExitCode = 0,
            Payload = payload
        };
    }

    private static ToolHandoffSection RunCapturedHandoffSection(
        string id,
        string title,
        string fileName,
        IReadOnlyList<string> args)
    {
        var result = RunProcessCapture(fileName, args);
        var stdout = StripTerminalControlSequences(result.Stdout).Trim();
        var stderr = StripTerminalControlSequences(result.Stderr).Trim();
        return new ToolHandoffSection
        {
            Id = id,
            Title = title,
            Command = fileName + " " + string.Join(" ", args.Select(QuoteIfNeeded)),
            ExitCode = result.ExitCode,
            Payload = TryParseJsonPayload(stdout),
            Stdout = stdout,
            Stderr = stderr
        };
    }

    private static List<string> BuildDotnetToolArgs(string root, ToolManifest manifest, string toolId, IReadOnlyList<string> toolArgs)
    {
        var tool = manifest.Tools.FirstOrDefault(x => x.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("tool not found: " + toolId);
        var projectPath = ResolveRootedPath(root, tool.Project);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"tool project not found: {tool.Project}", projectPath);
        }

        var dotnetArgs = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "-c",
            "Release",
            "--no-build",
            "--"
        };
        dotnetArgs.AddRange(toolArgs);
        dotnetArgs.Add("--godotRoot");
        dotnetArgs.Add(root);
        return dotnetArgs;
    }

    private static ToolHubStatus BuildStatus(string root, ToolManifest manifest)
    {
        var manifestIssues = ValidateManifest(root, manifest);
        var manifestPath = Path.Combine(root, "GodotTools", "tools.json");
        var buildScriptPath = Path.Combine(root, "build.ps1");
        var toolsScriptPath = Path.Combine(root, "tools.ps1");
        var toolsRoot = ResolveRootedPath(root, manifest.ToolsRoot);
        var outputRoot = ResolveRootedPath(root, manifest.OutputRoot);
        var outputRootSummary = BuildDirectorySummary(outputRoot);
        var tools = manifest.Tools
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(tool =>
            {
                var projectPath = string.IsNullOrWhiteSpace(tool.Project) ? "" : ResolveRootedPath(root, tool.Project);
                var outputPath = string.IsNullOrWhiteSpace(tool.Output) ? "" : ResolveRootedPath(root, tool.Output);
                var outputSummary = BuildDirectorySummary(outputPath);
                return new ToolStatus
                {
                    Id = tool.Id,
                    Name = tool.Name,
                    Category = tool.Category,
                    Project = tool.Project,
                    ProjectExists = projectPath.Length > 0 && File.Exists(projectPath),
                    ProjectLastWriteTimeUtc = GetFileLastWriteTimeUtc(projectPath),
                    Output = tool.Output,
                    OutputExists = outputSummary.Exists,
                    OutputFileCount = outputSummary.FileCount,
                    OutputTotalBytes = outputSummary.TotalBytes,
                    OutputLastWriteTimeUtc = outputSummary.LastWriteTimeUtc,
                    HasLaunch = tool.Launch != null,
                    HasSelfTest = tool.SelfTest != null,
                    RequiresProjectRoot = tool.RequiresProjectRoot,
                    Notes = tool.Notes
                };
            })
            .ToList();

        return new ToolHubStatus
        {
            ProjectRoot = root,
            ManifestPath = ToDisplayPath(root, manifestPath),
            ManifestExists = File.Exists(manifestPath),
            ManifestLastWriteTimeUtc = GetFileLastWriteTimeUtc(manifestPath),
            BuildScriptPath = ToDisplayPath(root, buildScriptPath),
            BuildScriptExists = File.Exists(buildScriptPath),
            ToolsScriptPath = ToDisplayPath(root, toolsScriptPath),
            ToolsScriptExists = File.Exists(toolsScriptPath),
            ToolsRoot = manifest.ToolsRoot,
            ToolsRootExists = Directory.Exists(toolsRoot),
            OutputRoot = manifest.OutputRoot,
            OutputRootExists = outputRootSummary.Exists,
            OutputRootFileCount = outputRootSummary.FileCount,
            OutputRootTotalBytes = outputRootSummary.TotalBytes,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ToolCount = tools.Count,
            ManifestIssueCount = manifestIssues.Count,
            ManifestIssues = manifestIssues,
            CategoryCounts = tools
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "(missing)" : x.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase),
            Tools = tools
        };
    }

    private static DirectorySummary BuildDirectorySummary(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return new DirectorySummary();
        }

        var fileCount = 0;
        long totalBytes = 0;
        DateTime latest = DateTime.MinValue;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            fileCount++;
            totalBytes += info.Length;
            if (info.LastWriteTimeUtc > latest)
            {
                latest = info.LastWriteTimeUtc;
            }
        }

        return new DirectorySummary
        {
            Exists = true,
            FileCount = fileCount,
            TotalBytes = totalBytes,
            LastWriteTimeUtc = latest == DateTime.MinValue ? "" : latest.ToString("O")
        };
    }

    private static string GetFileLastWriteTimeUtc(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).ToString("O")
            : "";
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

    private static int RunShow(ToolManifest manifest, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("show requires a tool id.");
            return 1;
        }

        var tool = manifest.Tools.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            Console.Error.WriteLine("tool not found: " + id);
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(tool, JsonOptions));
        return 0;
    }

    private static int RunValidateManifest(string root, ToolManifest manifest)
    {
        var issues = ValidateManifest(root, manifest);
        if (issues.Count == 0)
        {
            Console.WriteLine("ToolHub manifest validation OK.");
            return 0;
        }

        Console.Error.WriteLine($"ToolHub manifest validation found {issues.Count} issue(s).");
        foreach (var issue in issues)
        {
            Console.Error.WriteLine(issue);
        }
        return 1;
    }

    private static int RunExport(ToolManifest manifest, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(JsonSerializer.Serialize(manifest, JsonOptions));
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        File.WriteAllText(output, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine(output);
        return 0;
    }

    private static int RunToolCommand(string root, ToolManifest manifest, string[] args)
    {
        var positionals = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (positionals.Length < 2)
        {
            Console.Error.WriteLine("run requires: <tool-id> <launch|self-test>");
            return 1;
        }

        var id = positionals[0];
        var commandName = positionals[1].ToLowerInvariant();
        var tool = manifest.Tools.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            Console.Error.WriteLine("tool not found: " + id);
            return 1;
        }

        var command = commandName switch
        {
            "launch" => tool.Launch,
            "self-test" or "selftest" => tool.SelfTest,
            _ => null
        };
        if (command == null)
        {
            Console.Error.WriteLine($"tool command not found: {tool.Id} {commandName}");
            return 1;
        }
        if (!command.Kind.Equals("dotnet-run", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"unsupported command kind for {tool.Id}: {command.Kind}");
            return 1;
        }

        var projectPath = ResolveRootedPath(root, tool.Project);
        if (!File.Exists(projectPath))
        {
            Console.Error.WriteLine($"tool project not found: {tool.Project}");
            return 1;
        }

        var toolArgs = command.Args.Select(x => ExpandToken(x, root, manifest)).ToArray();
        var dotnetArgs = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "-c",
            "Release",
            "--no-build",
            "--"
        };
        dotnetArgs.AddRange(toolArgs);

        Console.WriteLine("dotnet " + string.Join(" ", dotnetArgs.Select(QuoteIfNeeded)));
        return RunProcess("dotnet", dotnetArgs);
    }

    private static int RunAllToolCommands(string root, ToolManifest manifest, string[] args)
    {
        var positionals = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var commandName = positionals.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (commandName is not ("self-test" or "selftest"))
        {
            Console.Error.WriteLine("run-all currently supports only: self-test");
            return 1;
        }

        var failures = new List<string>();
        foreach (var tool in manifest.Tools.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("");
            Console.WriteLine($"== ToolHub run-all self-test: {tool.Id} ==");
            var exitCode = RunToolCommand(root, manifest, [tool.Id, "self-test"]);
            if (exitCode != 0)
            {
                failures.Add($"{tool.Id} exit={exitCode}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("");
            Console.WriteLine("ToolHub run-all self-test OK.");
            return 0;
        }

        Console.Error.WriteLine("");
        Console.Error.WriteLine("ToolHub run-all self-test failures:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }
        return 1;
    }

    private static int RunResourceCommand(string root, ToolManifest manifest, string[] args)
    {
        var positionals = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var commandName = positionals.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(commandName) || IsHelp(commandName))
        {
            PrintResourceHelp();
            return 0;
        }

        if (commandName is "refresh" or "reindex")
        {
            var indexCode = RunToolCommand(root, manifest, ["resource-config", "launch"]);
            if (indexCode != 0)
            {
                return indexCode;
            }

            var auditCode = RunResourceConfigCommand(root, manifest, WithDefaultResourceAuditArgs(
                ["audit", "--out", Path.Combine(root, "BuildLogs", "resource_audit.json")]));
            if (auditCode != 0)
            {
                return auditCode;
            }

            var planCode = RunResourceConfigCommand(root, manifest, WithDefaultResourcePlanArgs(
                ["plan", "--out", Path.Combine(root, "BuildLogs", "resource_plan.json")]));
            return planCode;
        }

        if (commandName == "map-review")
        {
            return RunMapSceneResourceReview(root, manifest, args.Skip(1).ToArray());
        }

        if (commandName is not ("find" or "show" or "audit" or "plan" or "decide" or "apply" or "pending" or "status" or "verify-outputs"))
        {
            Console.Error.WriteLine("resource requires: refresh, audit, plan, decide, apply, pending, map-review, status, verify-outputs, find, or show");
            return 1;
        }

        var resourceArgs = StripRootOptions(args);
        if (commandName == "audit")
        {
            resourceArgs = WithDefaultResourceAuditArgs(resourceArgs);
        }
        if (commandName == "plan")
        {
            resourceArgs = WithDefaultResourcePlanArgs(resourceArgs);
        }
        if (commandName == "decide")
        {
            resourceArgs = WithDefaultResourceDecisionArgs(resourceArgs);
        }
        if (commandName == "apply")
        {
            resourceArgs = WithDefaultResourceApplyArgs(resourceArgs);
        }
        if (commandName == "pending")
        {
            resourceArgs = WithDefaultResourcePendingArgs(resourceArgs);
        }
        if (commandName == "verify-outputs")
        {
            resourceArgs = WithDefaultResourceVerifyOutputsArgs(resourceArgs);
        }

        return RunResourceConfigCommand(root, manifest, resourceArgs);
    }

    private static int RunMapCommand(string root, ToolManifest manifest, string[] args)
    {
        var positionals = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var commandName = positionals.FirstOrDefault()?.ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(commandName) || IsHelp(commandName))
        {
            PrintMapHelp();
            return 0;
        }

        if (commandName is not ("status" or "portal-review" or "runtime-verify" or "ux-audit" or "ux-walkthrough" or "ux-review" or "import" or "validate"))
        {
            Console.Error.WriteLine("map requires: status, portal-review, runtime-verify, ux-audit, ux-walkthrough, ux-review, import, or validate");
            return 1;
        }

        var mapArgs = StripRootOptions(args);
        if (commandName == "import")
        {
            mapArgs = WithDefaultMapImportArgs(root, mapArgs);
        }
        if (commandName == "validate")
        {
            mapArgs = WithDefaultMapValidateArgs(root, mapArgs);
        }
        if (commandName == "ux-review")
        {
            mapArgs = WithDefaultMapUxReviewArgs(root, mapArgs);
        }
        return RunMapEditorCommand(root, manifest, mapArgs);
    }

    private static string[] WithDefaultMapImportArgs(string root, string[] args)
    {
        if (HasOption(args, "out"))
        {
            return args;
        }

        return args.Concat(["--out", Path.Combine(root, "BuildLogs", "map_project.json")]).ToArray();
    }

    private static string[] WithDefaultMapValidateArgs(string root, string[] args)
    {
        if (HasOption(args, "in"))
        {
            return args;
        }

        return args.Concat(["--in", Path.Combine(root, "BuildLogs", "map_project.json")]).ToArray();
    }

    private static string[] WithDefaultMapUxReviewArgs(string root, string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "in"))
        {
            result.AddRange(["--in", Path.Combine(root, "BuildLogs", "map_ux_review_result.json")]);
        }
        return result.ToArray();
    }

    private static int RunMapEditorCommand(string root, ToolManifest manifest, string[] mapArgs)
    {
        var tool = manifest.Tools.FirstOrDefault(x => x.Id.Equals("map-editor", StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            Console.Error.WriteLine("tool not found: map-editor");
            return 1;
        }

        var projectPath = ResolveRootedPath(root, tool.Project);
        if (!File.Exists(projectPath))
        {
            Console.Error.WriteLine($"tool project not found: {tool.Project}");
            return 1;
        }

        var dotnetArgs = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "-c",
            "Release",
            "--no-build",
            "--"
        };
        dotnetArgs.AddRange(mapArgs);
        dotnetArgs.Add("--godotRoot");
        dotnetArgs.Add(root);

        Console.WriteLine("dotnet " + string.Join(" ", dotnetArgs.Select(QuoteIfNeeded)));
        return RunProcess("dotnet", dotnetArgs);
    }

    private static int RunResourceConfigCommand(string root, ToolManifest manifest, string[] resourceArgs)
    {
        var tool = manifest.Tools.FirstOrDefault(x => x.Id.Equals("resource-config", StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            Console.Error.WriteLine("tool not found: resource-config");
            return 1;
        }

        var projectPath = ResolveRootedPath(root, tool.Project);
        if (!File.Exists(projectPath))
        {
            Console.Error.WriteLine($"tool project not found: {tool.Project}");
            return 1;
        }

        var dotnetArgs = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "-c",
            "Release",
            "--no-build",
            "--"
        };
        dotnetArgs.AddRange(resourceArgs);
        dotnetArgs.Add("--godotRoot");
        dotnetArgs.Add(root);

        Console.WriteLine("dotnet " + string.Join(" ", dotnetArgs.Select(QuoteIfNeeded)));
        return RunProcess("dotnet", dotnetArgs);
    }

    private static int RunDoctor(string root, ToolManifest manifest)
    {
        Console.WriteLine("== ToolHub doctor: manifest ==");
        var manifestCode = RunValidateManifest(root, manifest);
        if (manifestCode != 0)
        {
            return manifestCode;
        }

        Console.WriteLine("");
        Console.WriteLine("== ToolHub doctor: self-tests ==");
        return RunAllToolCommands(root, manifest, ["self-test"]);
    }

    private static int RunAgentSelfTest()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ToolHub.AgentSelfTest." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, "GodotTools", "Demo", "Demo"));
        File.WriteAllText(Path.Combine(temp, "project.godot"), "[application]\nconfig/name=\"ToolHubSelfTest\"\n");
        File.WriteAllText(Path.Combine(temp, "GodotTools", "Demo", "Demo", "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(temp, "GodotTools", "tools.json"), """
        {
          "version": 1,
          "toolsRoot": "GodotTools",
          "outputRoot": "GodotTools-Build",
          "tools": [
            {
              "id": "demo",
              "name": "Demo",
              "category": "utility",
              "purpose": "Self-test tool.",
              "project": "GodotTools/Demo/Demo/Demo.csproj",
              "output": "GodotTools-Build/Demo",
              "launch": { "kind": "dotnet-run", "args": [] },
              "selfTest": { "kind": "dotnet-run", "args": ["--agent-self-test"] },
              "requiresProjectRoot": false,
              "notes": "ToolHub self-test fixture."
            }
          ]
        }
        """);

        var manifest = LoadManifest(temp);
        var issues = ValidateManifest(temp, manifest);
        if (manifest.Tools.Count != 1 || issues.Count != 0)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed manifest validation.");
            return 1;
        }
        var status = BuildStatus(temp, manifest);
        if (status.ToolCount != 1 ||
            status.ManifestIssueCount != 0 ||
            status.CategoryCounts.GetValueOrDefault("utility") != 1 ||
            !status.ToolsRootExists ||
            status.Tools[0].Id != "demo" ||
            !status.Tools[0].ProjectExists)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed status summary.");
            return 1;
        }

        var invalidManifest = JsonSerializer.Deserialize<ToolManifest>("""
        {
          "version": 1,
          "toolsRoot": "../outside",
          "outputRoot": "GodotTools-Build",
          "tools": [
            {
              "id": "Bad Id!",
              "name": "Bad",
              "category": "mystery",
              "purpose": "Invalid self-test fixture.",
              "project": "../outside/Bad.csproj",
              "output": "",
              "selfTest": { "kind": "shell", "args": ["--agent-self-test"] },
              "requiresProjectRoot": true,
              "notes": ""
            }
          ]
        }
        """, JsonOptions) ?? throw new InvalidDataException("invalid self-test manifest fixture failed to deserialize.");
        var invalidIssues = ValidateManifest(temp, invalidManifest);
        if (invalidIssues.Count < 7)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed invalid manifest detection.");
            return 1;
        }

        var expanded = manifest.Tools[0].SelfTest?.Args.Select(x => ExpandToken(x, temp, manifest)).ToArray() ?? [];
        if (expanded.Length != 1 || expanded[0] != "--agent-self-test")
        {
            Console.Error.WriteLine("ToolHub agent self-test failed command expansion.");
            return 1;
        }

        var opts = ParseOptions(["--godot-root", temp]);
        if (GetOption(opts, "godotRoot", "godot-root") != temp)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed godot-root alias parsing.");
            return 1;
        }

        var stripped = StripRootOptions(["find", "--query", "Settings", "--godotRoot", temp, "--kind", "resource"]);
        if (!stripped.SequenceEqual(["find", "--query", "Settings", "--kind", "resource"]))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed root option stripping.");
            return 1;
        }
        var cleaned = StripTerminalControlSequences("\u001b]9;4;3;\u001b\\{\"ok\":true}\u001b[?25l");
        var parsed = TryParseJsonPayload(cleaned);
        if (cleaned != "{\"ok\":true}" || parsed is not JsonElement jsonElement || !jsonElement.GetProperty("ok").GetBoolean())
        {
            Console.Error.WriteLine("ToolHub agent self-test failed handoff output cleanup.");
            return 1;
        }
        var dumpIndex = BuildDumpIndex(temp);
        if (dumpIndex.ArtifactCount == 0 || dumpIndex.MissingRequiredCount == 0)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed dump index missing-required fixture.");
            return 1;
        }
        var dumpSummary = FormatDumpIndexSummary(dumpIndex);
        if (!dumpSummary.Contains("ToolHub dump index", StringComparison.Ordinal) ||
            !dumpSummary.Contains("resource_index.json", StringComparison.Ordinal) ||
            !dumpSummary.Contains("requiredMissing=", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed dump index summary formatting.");
            return 1;
        }
        var summary = FormatHandoffSummary(new ToolHandoffReport
        {
            ProjectRoot = temp,
            GeneratedAtUtc = "2000-01-01T00:00:00.0000000Z",
            SectionCount = 2,
            FailedSectionCount = 0,
            Ok = true,
            Sections =
            [
                BuildInlineHandoffSection("toolhub-status", "ToolHub status", status),
                BuildInlineHandoffSection("dump-index", "ToolHub dump index", new
                {
                    artifactCount = 14,
                    existingCount = 13,
                    missingRequiredCount = 0,
                    canonicalCount = 2,
                    reviewCount = 7
                }),
                BuildInlineHandoffSection("map-portal-review", "MapEditor portal review", new
                {
                    mapsWithoutPortalsCount = 2,
                    portalsWithMissingTargetsCount = 0
                }),
                BuildInlineHandoffSection("map-runtime-verify", "MapEditor runtime verification", new
                {
                    ok = true,
                    issueCount = 0,
                    resolvedPortalTargetCount = 16,
                    portalTargetCount = 16,
                    resolvedEntryRoomCount = 6,
                    entryRoomCount = 6,
                    checkCount = 9
                }),
                BuildInlineHandoffSection("map-ux-audit", "MapEditor UX audit", new
                {
                    ok = true,
                    blockingIssueCount = 0,
                    warningCount = 0,
                    checkCount = 17
                })
            ]
        });
        if (!summary.Contains("ToolHub handoff summary", StringComparison.Ordinal) ||
            !summary.Contains("Overall: OK", StringComparison.Ordinal) ||
            !summary.Contains("ToolHub: tools=1 manifestIssues=0", StringComparison.Ordinal) ||
            !summary.Contains("DumpIndex: artifacts=14 existing=13 requiredMissing=0 canonical=2 review=7", StringComparison.Ordinal) ||
            !summary.Contains("PortalReview: mapsWithoutPortals=2 portalsWithMissingTargets=0", StringComparison.Ordinal) ||
            !summary.Contains("RuntimeVerify: ok=true issues=0 portalTargets=16/16 entryRooms=6/6 checks=9", StringComparison.Ordinal) ||
            !summary.Contains("UxAudit: ok=true blocking=0 warnings=0 checks=17", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed handoff summary formatting.");
            return 1;
        }
        var nextActions = new ToolNextActionsReport
        {
            ProjectRoot = temp,
            GeneratedAtUtc = "2000-01-01T00:00:00.0000000Z",
            HandoffOk = true,
            HandoffFailedSectionCount = 0,
            RecommendationCount = 1,
            ResourceReviewSampleCount = 0,
            MapSceneResourceReview = new MapSceneResourceReviewSummary
            {
                ActionCount = 2,
                UniqueSceneCount = 1,
                WarningCount = 1,
                InfoCount = 1,
                WithPortalHintCount = 1,
                WithoutPortalHintCount = 1,
                PortalClassificationCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["playable-isolated-candidate"] = 1
                },
                SampleIds = ["resource-plan-0001"],
                ReviewCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource map-review --summary --limit 20 -NoBuild"
            },
            Recommendations =
            [
                new ToolNextAction
                {
                    Id = "draft-mutation-plan-before-editing",
                    Area = "toolhub",
                    Severity = "info",
                    Title = "Draft a mutation plan before editing",
                    Reason = "Self-test fixture. Recheck it with mutation-plan --in before Coder starts writing.",
                    Command = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 mutation-plan --summary --out BuildLogs\\mutation_plan.json --domain map --intent \"describe intended edit\" --writes \"res://CoreEngine/...\" --before-dump \"BuildLogs/before.json\" --after-dump \"BuildLogs/after.json\" --summary-command \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --verifier \"powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 ... --summary -NoBuild\" --ux \"human review path\" --recovery \"rollback or reject path\" -NoBuild"
                }
            ]
        };
        var nextActionsSummary = FormatNextActionsSummary(nextActions);
        if (!nextActions.HandoffOk ||
            nextActions.RecommendationCount == 0 ||
            !nextActionsSummary.Contains("ToolHub next actions", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("Handoff: OK", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("Map portal classifications:", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("Resource review queues:", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("Map-scene resource review:", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("resource map-review", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("BuildLogs\\mutation_plan.json", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("mutation-plan --in", StringComparison.Ordinal) ||
            !nextActionsSummary.Contains("Resource review samples:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed next-actions formatting.");
            return 1;
        }
        var mapReviewSummary = FormatMapSceneResourceReviewSummary(new MapSceneResourceReviewReport
        {
            ProjectRoot = temp,
            GeneratedAtUtc = "2000-01-01T00:00:00.0000000Z",
            PlanPath = "BuildLogs/resource_plan.json",
            DecisionsPath = "BuildLogs/resource_decisions.json",
            PortalReviewExitCode = 0,
            Limit = 1,
            Summary = nextActions.MapSceneResourceReview,
            CandidateCount = 1,
            Candidates =
            [
                new ResourceReviewCandidate
                {
                    Id = "resource-plan-0001",
                    Type = "review-unreferenced-resource",
                    Severity = "warning",
                    Source = "res://CoreEngine/Maps/TestRoom.tscn",
                    Recommendation = "Self-test fixture.",
                    DecideCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 resource decide --id resource-plan-0001 --decision defer --note \"owner review\" -NoBuild",
                    MapPortalClassification = "playable-isolated-candidate",
                    MapPortalConfidence = "low",
                    MapPortalReason = "Self-test map hint."
                }
            ]
        });
        if (!mapReviewSummary.Contains("ToolHub map-scene resource review", StringComparison.Ordinal) ||
            !mapReviewSummary.Contains("Counts: actions=2 scenes=1", StringComparison.Ordinal) ||
            !mapReviewSummary.Contains("map hint: playable-isolated-candidate", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed map-scene resource review formatting.");
            return 1;
        }
        var closureSummary = FormatClosureGateSummary(new ToolClosureGateReport
        {
            ProjectRoot = temp,
            GeneratedAtUtc = "2000-01-01T00:00:00.0000000Z",
            HandoffOk = true,
            HandoffFailedSectionCount = 0,
            CurrentBaselineOk = true,
            MutatingWorkflowReady = false,
            GateCount = 3,
            OkGateCount = 1,
            PartialGateCount = 2,
            FailedGateCount = 0,
            MutationAcceptanceRule = "Self-test mutation rule.",
            Gates =
            [
                new ToolClosureGate
                {
                    Id = "human-friendly-dumps",
                    Title = "Human-friendly data dumps",
                    Status = "ok",
                    CurrentBaselineSatisfied = true,
                    Evidence = "dump-index artifacts=11 existing=11 requiredMissing=0",
                    Gap = "Self-test dump gap.",
                    VerifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 dump-index --summary -NoBuild",
                    RequiredForMutation = true,
                    FutureMutationRequirement = "Self-test dump mutation requirement."
                },
                new ToolClosureGate
                {
                    Id = "game-effect-verification",
                    Title = "Game-effect verification",
                    Status = "partial",
                    CurrentBaselineSatisfied = true,
                    Evidence = "map runtime-verify ok=true issues=0",
                    Gap = "Self-test runtime gap.",
                    VerifyCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild",
                    RequiredForMutation = true,
                    FutureMutationRequirement = "Self-test runtime mutation requirement."
                }
            ],
            RecommendedCommands =
            [
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 closure-gates --summary -NoBuild"
            ]
        });
        if (!closureSummary.Contains("ToolHub closure gates", StringComparison.Ordinal) ||
            !closureSummary.Contains("Current baseline: OK", StringComparison.Ordinal) ||
            !closureSummary.Contains("Future mutating workflows: NOT READY", StringComparison.Ordinal) ||
            !closureSummary.Contains("[partial] game-effect-verification", StringComparison.Ordinal) ||
            !closureSummary.Contains("closure-gates --summary", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed closure-gates summary formatting.");
            return 1;
        }
        var incompletePlan = BuildMutationPlanReport(temp, ["--domain", "map", "--intent", "Self-test incomplete plan"]);
        if (incompletePlan.ReadyForDesignReview ||
            incompletePlan.MissingCount == 0 ||
            !incompletePlan.MissingChecks.Contains("writes", StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed incomplete mutation-plan checks.");
            return 1;
        }
        var completePlan = BuildMutationPlanReport(temp,
        [
            "--domain", "map",
            "--intent", "Self-test complete plan",
            "--writes", "res://CoreEngine/Maps/TestRoom.tscn;BuildLogs/test_after.json",
            "--before-dump", "BuildLogs/test_before.json",
            "--after-dump", "BuildLogs/test_after.json",
            "--summary-command", "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map validate --summary -NoBuild",
            "--verifier", "powershell -NoProfile -ExecutionPolicy Bypass -File .\\tools.ps1 map runtime-verify --summary -NoBuild",
            "--ux", "MapEditor live review",
            "--recovery", "restore before dump"
        ]);
        var planSummary = FormatMutationPlanSummary(completePlan);
        if (!completePlan.ReadyForDesignReview ||
            completePlan.MissingCount != 0 ||
            !planSummary.Contains("ToolHub mutation plan", StringComparison.Ordinal) ||
            !planSummary.Contains("Ready for design review: YES", StringComparison.Ordinal) ||
            !planSummary.Contains("res://CoreEngine/Maps/TestRoom.tscn", StringComparison.Ordinal) ||
            !planSummary.Contains("[ok] verifier", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed complete mutation-plan summary formatting.");
            return 1;
        }
        var planOut = Path.Combine("BuildLogs", "mutation_plan.json");
        WriteMutationPlanReport(temp, completePlan, planOut);
        var reloadedPlan = BuildMutationPlanReport(temp, ["--in", planOut]);
        if (!File.Exists(Path.Combine(temp, planOut)) ||
            !reloadedPlan.ReadyForDesignReview ||
            !reloadedPlan.InputPath.Equals(planOut.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
            reloadedPlan.OutputWritten)
        {
            Console.Error.WriteLine("ToolHub agent self-test failed mutation-plan artifact round trip.");
            return 1;
        }
        var verifyPlans = BuildMutationPlanVerifyReport(temp, ["--dir", "BuildLogs"]);
        var verifyPlansSummary = FormatMutationPlanVerifySummary(verifyPlans);
        if (!verifyPlans.Ok ||
            verifyPlans.PlanCount != 1 ||
            verifyPlans.ReadyCount != 1 ||
            !verifyPlansSummary.Contains("ToolHub mutation plan verification", StringComparison.Ordinal) ||
            !verifyPlansSummary.Contains("Overall: OK plans=1 ready=1 failed=0", StringComparison.Ordinal) ||
            !verifyPlansSummary.Contains("BuildLogs/mutation_plan.json", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed mutation-plan verify success case.");
            return 1;
        }
        Directory.CreateDirectory(Path.Combine(temp, "EmptyBuildLogs"));
        var emptyVerifyPlans = BuildMutationPlanVerifyReport(temp, ["--dir", "EmptyBuildLogs"]);
        if (emptyVerifyPlans.Ok ||
            emptyVerifyPlans.PlanCount != 0 ||
            !FormatMutationPlanVerifySummary(emptyVerifyPlans).Contains("Suggested command:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ToolHub agent self-test failed mutation-plan verify empty case.");
            return 1;
        }

        Console.WriteLine("ToolHub agent self-test OK.");
        return 0;
    }

    private static ToolManifest LoadManifest(string root)
    {
        var path = Path.Combine(root, "GodotTools", "tools.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("tools.json not found.", path);
        }

        var manifest = JsonSerializer.Deserialize<ToolManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("tools.json is invalid.");
        manifest.Tools ??= [];
        return manifest;
    }

    private static List<string> ValidateManifest(string root, ToolManifest manifest)
    {
        var issues = new List<string>();
        root = Path.GetFullPath(root);

        if (manifest.Version <= 0)
        {
            issues.Add("manifest version must be positive.");
        }
        if (string.IsNullOrWhiteSpace(manifest.ToolsRoot))
        {
            issues.Add("toolsRoot is required.");
        }
        else if (!IsSafeRelativePath(manifest.ToolsRoot))
        {
            issues.Add($"toolsRoot must be a safe relative path: {manifest.ToolsRoot}");
        }
        else if (!Directory.Exists(ResolveRootedPath(root, manifest.ToolsRoot)))
        {
            issues.Add($"toolsRoot does not exist: {manifest.ToolsRoot}");
        }
        if (string.IsNullOrWhiteSpace(manifest.OutputRoot))
        {
            issues.Add("outputRoot is required.");
        }
        else if (!IsSafeRelativePath(manifest.OutputRoot))
        {
            issues.Add($"outputRoot must be a safe relative path: {manifest.OutputRoot}");
        }
        if (manifest.Tools.Count == 0)
        {
            issues.Add("manifest contains no tools.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in manifest.Tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Id))
            {
                issues.Add("tool id is required.");
                continue;
            }
            if (!IsKebabId(tool.Id))
            {
                issues.Add($"{tool.Id}: id must use lowercase letters, numbers, and hyphens.");
            }
            if (!ids.Add(tool.Id))
            {
                issues.Add($"duplicate tool id: {tool.Id}");
            }
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                issues.Add($"{tool.Id}: name is required.");
            }
            if (string.IsNullOrWhiteSpace(tool.Category))
            {
                issues.Add($"{tool.Id}: category is required.");
            }
            else if (!AllowedCategories.Contains(tool.Category))
            {
                issues.Add($"{tool.Id}: category must be one of: {string.Join(", ", AllowedCategories.OrderBy(x => x))}");
            }
            if (string.IsNullOrWhiteSpace(tool.Purpose))
            {
                issues.Add($"{tool.Id}: purpose is required.");
            }
            if (string.IsNullOrWhiteSpace(tool.Project))
            {
                issues.Add($"{tool.Id}: project is required.");
            }
            else if (!IsSafeRelativePath(tool.Project) || !tool.Project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{tool.Id}: project must be a safe relative .csproj path: {tool.Project}");
            }
            else if (!File.Exists(ResolveRootedPath(root, tool.Project)))
            {
                issues.Add($"{tool.Id}: project does not exist: {tool.Project}");
            }
            if (string.IsNullOrWhiteSpace(tool.Output))
            {
                issues.Add($"{tool.Id}: output is required.");
            }
            else if (!IsSafeRelativePath(tool.Output))
            {
                issues.Add($"{tool.Id}: output must be a safe relative path: {tool.Output}");
            }
            ValidateCommand(issues, tool, "launch", tool.Launch, required: false);
            if (tool.SelfTest == null)
            {
                issues.Add($"{tool.Id}: selfTest is required.");
            }
            else
            {
                ValidateCommand(issues, tool, "selfTest", tool.SelfTest, required: true);
            }
            if (tool.RequiresProjectRoot && !CommandUsesProjectRoot(tool.Launch) && !CommandUsesProjectRoot(tool.SelfTest))
            {
                issues.Add($"{tool.Id}: requiresProjectRoot is true but launch/selfTest args do not include {{projectRoot}}.");
            }
            if (string.IsNullOrWhiteSpace(tool.Notes))
            {
                issues.Add($"{tool.Id}: notes are required.");
            }
        }

        return issues;
    }

    private static void ValidateCommand(List<string> issues, ToolEntry tool, string label, ToolCommand? command, bool required)
    {
        if (command == null)
        {
            if (required)
            {
                issues.Add($"{tool.Id}: {label} is required.");
            }
            return;
        }

        if (!command.Kind.Equals("dotnet-run", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"{tool.Id}: unsupported {label} kind: {command.Kind}");
        }
        if (command.Args.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add($"{tool.Id}: {label} args must not contain empty values.");
        }
        foreach (var arg in command.Args)
        {
            var tokenIssues = FindUnknownTokens(arg).ToArray();
            foreach (var tokenIssue in tokenIssues)
            {
                issues.Add($"{tool.Id}: {label} arg uses unknown token: {tokenIssue}");
            }
        }
    }

    private static IEnumerable<string> FindUnknownTokens(string value)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "{projectRoot}",
            "{toolsRoot}",
            "{outputRoot}"
        };

        var start = 0;
        while (start < value.Length)
        {
            var open = value.IndexOf('{', start);
            if (open < 0)
            {
                yield break;
            }
            var close = value.IndexOf('}', open + 1);
            if (close < 0)
            {
                yield return value[open..];
                yield break;
            }

            var token = value[open..(close + 1)];
            if (!allowed.Contains(token))
            {
                yield return token;
            }
            start = close + 1;
        }
    }

    private static bool CommandUsesProjectRoot(ToolCommand? command) =>
        command?.Args.Any(x => x.Contains("{projectRoot}", StringComparison.Ordinal)) == true;

    private static bool IsKebabId(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var ch in value)
        {
            var ok = ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '-';
            if (!ok || ch == '-' && previousHyphen)
            {
                return false;
            }
            previousHyphen = ch == '-';
        }
        return true;
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return false;
        }

        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.All(part => part != "." && part != "..");
    }

    private static string ResolveRootedPath(string root, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ExpandToken(string value, string root, ToolManifest manifest)
    {
        var toolsRoot = ResolveRootedPath(root, manifest.ToolsRoot);
        var outputRoot = ResolveRootedPath(root, manifest.OutputRoot);
        return value
            .Replace("{projectRoot}", root)
            .Replace("{toolsRoot}", toolsRoot)
            .Replace("{outputRoot}", outputRoot);
    }

    private static int RunProcess(string fileName, IReadOnlyList<string> args)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static CapturedProcessResult RunProcessCapture(string fileName, IReadOnlyList<string> args)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CapturedProcessResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdout,
            Stderr = stderr
        };
    }

    private static object? TryParseJsonPayload(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static string StripTerminalControlSequences(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch != '\u001b')
            {
                result.Append(ch);
                continue;
            }

            if (i + 1 >= text.Length)
            {
                continue;
            }

            var next = text[i + 1];
            if (next == ']')
            {
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '\a')
                    {
                        break;
                    }
                    if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (next == '[')
            {
                i += 2;
                while (i < text.Length && !IsAnsiFinalByte(text[i]))
                {
                    i++;
                }
                continue;
            }
        }

        return result.ToString();
    }

    private static bool IsAnsiFinalByte(char ch)
    {
        return ch is >= '@' and <= '~';
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }
        return value.Any(char.IsWhiteSpace) ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
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
            d[key] = value;
        }
        return d;
    }

    private static string GetOption(Dictionary<string, string> opts, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (opts.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return "";
    }

    private static string[] StripRootOptions(string[] args)
    {
        var stripped = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--godotRoot", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--godot-root", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    i++;
                }
                continue;
            }
            stripped.Add(arg);
        }
        return stripped.ToArray();
    }

    private static string[] WithDefaultResourceAuditArgs(string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "scope"))
        {
            result.AddRange(["--scope", "core"]);
        }
        if (!HasOption(result, "allow-missing-prefix") && !HasOption(result, "allowMissingPrefix"))
        {
            result.AddRange([
                "--allow-missing-prefix",
                "res://addons/CustomRunner/",
                "--allow-missing-prefix",
                "res://000_UserInput/"
            ]);
        }
        return result.ToArray();
    }

    private static string[] WithDefaultResourcePlanArgs(string[] args)
    {
        return WithDefaultResourceAuditArgs(args);
    }

    private static string[] WithDefaultResourceDecisionArgs(string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "plan"))
        {
            result.AddRange(["--plan", Path.Combine("BuildLogs", "resource_plan.json")]);
        }
        if (!HasOption(result, "out"))
        {
            result.AddRange(["--out", Path.Combine("BuildLogs", "resource_decisions.json")]);
        }
        return result.ToArray();
    }

    private static string[] WithDefaultResourceApplyArgs(string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "plan"))
        {
            result.AddRange(["--plan", Path.Combine("BuildLogs", "resource_plan.json")]);
        }
        if (!HasOption(result, "decisions"))
        {
            result.AddRange(["--decisions", Path.Combine("BuildLogs", "resource_decisions.json")]);
        }
        if (!HasOption(result, "out"))
        {
            result.AddRange(["--out", Path.Combine("BuildLogs", "resource_apply_preview.json")]);
        }
        if (!HasOption(result, "cleanup-out"))
        {
            result.AddRange(["--cleanup-out", Path.Combine("BuildLogs", "resource_cleanup_candidates.json")]);
        }
        return result.ToArray();
    }

    private static string[] WithDefaultResourcePendingArgs(string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "plan"))
        {
            result.AddRange(["--plan", Path.Combine("BuildLogs", "resource_plan.json")]);
        }
        if (!HasOption(result, "decisions"))
        {
            result.AddRange(["--decisions", Path.Combine("BuildLogs", "resource_decisions.json")]);
        }
        return result.ToArray();
    }

    private static string[] WithDefaultResourceVerifyOutputsArgs(string[] args)
    {
        var result = new List<string>(args);
        if (!HasOption(result, "plan"))
        {
            result.AddRange(["--plan", Path.Combine("BuildLogs", "resource_plan.json")]);
        }
        if (!HasOption(result, "decisions"))
        {
            result.AddRange(["--decisions", Path.Combine("BuildLogs", "resource_decisions.json")]);
        }
        if (!HasOption(result, "apply"))
        {
            result.AddRange(["--apply", Path.Combine("BuildLogs", "resource_apply_preview.json")]);
        }
        if (!HasOption(result, "approved"))
        {
            result.AddRange(["--approved", Path.Combine("BuildLogs", "resource_approved_dependencies.json")]);
        }
        if (!HasOption(result, "cleanup"))
        {
            result.AddRange(["--cleanup", Path.Combine("BuildLogs", "resource_cleanup_candidates.json")]);
        }
        return result.ToArray();
    }

    private static bool HasOption(IEnumerable<string> args, string name)
    {
        var option = "--" + name;
        return args.Any(x => x.Equals(option, StringComparison.OrdinalIgnoreCase));
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
        Console.WriteLine("ToolHub usage:");
        Console.WriteLine("  list --godotRoot <dir>");
        Console.WriteLine("  status --godotRoot <dir>");
        Console.WriteLine("  dump-index --godotRoot <dir> [--summary]");
        Console.WriteLine("  closure-gates --godotRoot <dir> [--summary]");
        Console.WriteLine("  mutation-plan --godotRoot <dir> [--summary] [--out <file>] --domain <domain> --intent <text> --writes <a;b> --before-dump <file> --after-dump <file> --summary-command <cmd> --verifier <cmd> --ux <text> --recovery <text>");
        Console.WriteLine("  mutation-plan --godotRoot <dir> [--summary] --in <file>");
        Console.WriteLine("  mutation-plan verify --godotRoot <dir> [--dir <BuildLogs>] [--pattern <mutation_plan*.json>] [--summary]");
        Console.WriteLine("  handoff --godotRoot <dir> [--summary]");
        Console.WriteLine("  next-actions --godotRoot <dir> [--summary] [--limit <n>]");
        Console.WriteLine("  show <tool-id> --godotRoot <dir>");
        Console.WriteLine("  run <tool-id> <launch|self-test> --godotRoot <dir>");
        Console.WriteLine("  run-all self-test --godotRoot <dir>");
        Console.WriteLine("  map status --godotRoot <dir> [--summary]");
        Console.WriteLine("  map portal-review --godotRoot <dir> [--summary]");
        Console.WriteLine("  map runtime-verify --godotRoot <dir> [--summary]");
        Console.WriteLine("  map ux-audit --godotRoot <dir> [--summary]");
        Console.WriteLine("  map ux-walkthrough --godotRoot <dir> [--out <file>] [--summary]");
        Console.WriteLine("  map ux-review --godotRoot <dir> [--in <file>] [--out <file>] [--reviewer <name>] [--result pass|partial|fail|pending] [--step-results <id=pass;id=fail>] [--summary]");
        Console.WriteLine("  map import --godotRoot <dir> [--out <file>] [--summary]");
        Console.WriteLine("  map validate --godotRoot <dir> [--in <file>] [--summary]");
        Console.WriteLine("  resource refresh --godotRoot <dir>");
        Console.WriteLine("  resource audit --godotRoot <dir> [--out <file>] [--limit <n>]");
        Console.WriteLine("  resource plan --godotRoot <dir> [--out <file>] [--limit <n>]");
        Console.WriteLine("  resource decide --godotRoot <dir> --id <resource-plan-id> --decision accept|defer|reject [--note <text>]");
        Console.WriteLine("  resource apply --godotRoot <dir> [--plan <file>] [--decisions <file>] [--out <file>] [--execute] [--approved-out <file>] [--cleanup-out <file>]");
        Console.WriteLine("  resource pending --godotRoot <dir> [--plan <file>] [--decisions <file>] [--limit <n>] [--severity error|warning|info] [--type <action-type>] [--query <text>] [--summary] [--commands]");
        Console.WriteLine("  resource map-review --godotRoot <dir> [--summary] [--limit <n>]");
        Console.WriteLine("  resource status --godotRoot <dir> [--dir <BuildLogs>] [--summary]");
        Console.WriteLine("  resource verify-outputs --godotRoot <dir> [--plan <file>] [--decisions <file>] [--apply <file>] [--approved <file>] [--cleanup <file>] [--summary]");
        Console.WriteLine("  resource find --godotRoot <dir> [--query <text>] [--kind <kind>] [--extension <ext>] [--limit <n>]");
        Console.WriteLine("  resource show --godotRoot <dir> --path <res://...> [--refresh]");
        Console.WriteLine("  doctor --godotRoot <dir>");
        Console.WriteLine("  validate-manifest --godotRoot <dir>");
        Console.WriteLine("  export --godotRoot <dir> [--out <file>]");
        Console.WriteLine("  --agent-self-test");
    }

    private static void PrintResourceHelp()
    {
        Console.WriteLine("ToolHub resource shortcuts:");
        Console.WriteLine("  resource refresh --godotRoot <dir>");
        Console.WriteLine("  resource audit --godotRoot <dir> [--out <file>] [--limit <n>]");
        Console.WriteLine("  resource plan --godotRoot <dir> [--out <file>] [--limit <n>]");
        Console.WriteLine("  resource decide --godotRoot <dir> --id <resource-plan-id> --decision accept|defer|reject [--note <text>]");
        Console.WriteLine("  resource apply --godotRoot <dir> [--plan <file>] [--decisions <file>] [--out <file>] [--execute] [--approved-out <file>] [--cleanup-out <file>]");
        Console.WriteLine("  resource pending --godotRoot <dir> [--plan <file>] [--decisions <file>] [--limit <n>] [--severity error|warning|info] [--type <action-type>] [--query <text>] [--summary] [--commands]");
        Console.WriteLine("  resource map-review --godotRoot <dir> [--summary] [--limit <n>]");
        Console.WriteLine("  resource status --godotRoot <dir> [--dir <BuildLogs>] [--summary]");
        Console.WriteLine("  resource verify-outputs --godotRoot <dir> [--plan <file>] [--decisions <file>] [--apply <file>] [--approved <file>] [--cleanup <file>] [--summary]");
        Console.WriteLine("  resource find --godotRoot <dir> [--query <text>] [--kind <kind>] [--extension <ext>] [--limit <n>]");
        Console.WriteLine("  resource show --godotRoot <dir> --path <res://...> [--refresh]");
    }

    private static void PrintMapHelp()
    {
        Console.WriteLine("ToolHub map shortcuts:");
        Console.WriteLine("  map status --godotRoot <dir> [--summary]");
        Console.WriteLine("  map portal-review --godotRoot <dir> [--summary]");
        Console.WriteLine("  map runtime-verify --godotRoot <dir> [--summary]");
        Console.WriteLine("  map ux-audit --godotRoot <dir> [--summary]");
        Console.WriteLine("  map ux-walkthrough --godotRoot <dir> [--out <file>] [--summary]");
        Console.WriteLine("  map ux-review --godotRoot <dir> [--in <file>] [--out <file>] [--reviewer <name>] [--result pass|partial|fail|pending] [--step-results <id=pass;id=fail>] [--summary]");
        Console.WriteLine("  map import --godotRoot <dir> [--out <file>] [--summary]");
        Console.WriteLine("  map validate --godotRoot <dir> [--in <file>] [--summary]");
    }
}

internal sealed class DirectorySummary
{
    public bool Exists { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public string LastWriteTimeUtc { get; set; } = "";
}

internal sealed class CapturedProcessResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
}

public sealed class ToolHandoffReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public int SectionCount { get; set; }
    public int FailedSectionCount { get; set; }
    public bool Ok { get; set; }
    public List<ToolHandoffSection> Sections { get; set; } = [];
}

public sealed class ToolDumpIndexReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public int ArtifactCount { get; set; }
    public int ExistingCount { get; set; }
    public int MissingRequiredCount { get; set; }
    public int CanonicalCount { get; set; }
    public int ReviewCount { get; set; }
    public int ValidationCount { get; set; }
    public List<ToolDumpArtifact> Artifacts { get; set; } = [];
}

public sealed class ToolDumpArtifact
{
    public string Id { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Role { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Required { get; set; }
    public bool Exists { get; set; }
    public long Bytes { get; set; }
    public string LastWriteTimeUtc { get; set; } = "";
    public string Description { get; set; } = "";
    public string RefreshCommand { get; set; } = "";
    public string VerifyCommand { get; set; } = "";
}

public sealed class ToolClosureGateReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public bool HandoffOk { get; set; }
    public int HandoffFailedSectionCount { get; set; }
    public bool CurrentBaselineOk { get; set; }
    public bool MutatingWorkflowReady { get; set; }
    public int GateCount { get; set; }
    public int OkGateCount { get; set; }
    public int PartialGateCount { get; set; }
    public int FailedGateCount { get; set; }
    public string MutationAcceptanceRule { get; set; } = "";
    public List<ToolClosureGate> Gates { get; set; } = [];
    public List<string> RecommendedCommands { get; set; } = [];
}

public sealed class ToolClosureGate
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public bool CurrentBaselineSatisfied { get; set; }
    public string Evidence { get; set; } = "";
    public string Gap { get; set; } = "";
    public string VerifyCommand { get; set; } = "";
    public bool RequiredForMutation { get; set; }
    public string FutureMutationRequirement { get; set; } = "";
}

public sealed class ToolMutationPlanReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<string> WriteTargets { get; set; } = [];
    public string BeforeDump { get; set; } = "";
    public string AfterDump { get; set; } = "";
    public string SummaryCommand { get; set; } = "";
    public string VerifierCommand { get; set; } = "";
    public string UxPath { get; set; } = "";
    public string RecoveryPath { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public bool OutputWritten { get; set; }
    public bool ReadyForDesignReview { get; set; }
    public int MissingCount { get; set; }
    public List<string> MissingChecks { get; set; } = [];
    public int CheckCount { get; set; }
    public List<ToolMutationPlanCheck> Checks { get; set; } = [];
    public string AcceptanceRule { get; set; } = "";
    public string SuggestedNextCommand { get; set; } = "";
}

public sealed class ToolMutationPlanCheck
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Satisfied { get; set; }
    public string Requirement { get; set; } = "";
}

public sealed class ToolMutationPlanVerifyReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string Directory { get; set; } = "";
    public string Pattern { get; set; } = "";
    public bool Ok { get; set; }
    public int PlanCount { get; set; }
    public int ReadyCount { get; set; }
    public int FailedCount { get; set; }
    public string SuggestedCommand { get; set; } = "";
    public List<ToolMutationPlanVerifyItem> Plans { get; set; } = [];
}

public sealed class ToolMutationPlanVerifyItem
{
    public string Path { get; set; } = "";
    public bool ReadyForDesignReview { get; set; }
    public int MissingCount { get; set; }
    public List<string> MissingChecks { get; set; } = [];
    public string Domain { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Issue { get; set; } = "";
}

public sealed class ToolHandoffSection
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
    public object? Payload { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
}

public sealed class ToolNextActionsReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public bool HandoffOk { get; set; }
    public int HandoffFailedSectionCount { get; set; }
    public int RecommendationCount { get; set; }
    public int PortalCoverageClassificationCount { get; set; }
    public Dictionary<string, int> PortalCoverageClassifications { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ResourceReviewBucketCount { get; set; }
    public int MapSceneResourceReviewCount { get; set; }
    public MapSceneResourceReviewSummary MapSceneResourceReview { get; set; } = new();
    public int ResourceReviewSampleCount { get; set; }
    public List<ToolNextAction> Recommendations { get; set; } = [];
    public List<ResourceReviewBucket> ResourceReviewBuckets { get; set; } = [];
    public List<ResourceReviewCandidate> ResourceReviewSamples { get; set; } = [];
}

public sealed class ToolNextAction
{
    public string Id { get; set; } = "";
    public string Area { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Command { get; set; } = "";
}

public sealed class ResourceReviewBucket
{
    public string Severity { get; set; } = "";
    public string Type { get; set; } = "";
    public int Count { get; set; }
    public List<string> SampleIds { get; set; } = [];
    public string ReviewCommand { get; set; } = "";
}

public sealed class MapSceneResourceReviewSummary
{
    public int ActionCount { get; set; }
    public int UniqueSceneCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int WithPortalHintCount { get; set; }
    public int WithoutPortalHintCount { get; set; }
    public Dictionary<string, int> PortalClassificationCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SampleIds { get; set; } = [];
    public string ReviewCommand { get; set; } = "";
}

public sealed class MapSceneResourceReviewReport
{
    public string ProjectRoot { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string PlanPath { get; set; } = "";
    public string DecisionsPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public bool OutputWritten { get; set; }
    public int PortalReviewExitCode { get; set; }
    public string PortalReviewCommand { get; set; } = "";
    public string PortalReviewIssue { get; set; } = "";
    public int Limit { get; set; }
    public MapSceneResourceReviewSummary Summary { get; set; } = new();
    public int CandidateCount { get; set; }
    public List<ResourceReviewCandidate> Candidates { get; set; } = [];
}

public sealed class PortalCoverageHint
{
    public string Classification { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class ResourceReviewCandidate
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string DecideCommand { get; set; } = "";
    public string MapPortalClassification { get; set; } = "";
    public string MapPortalConfidence { get; set; } = "";
    public string MapPortalReason { get; set; } = "";
    public string MapPortalRecommendation { get; set; } = "";
}

public sealed class ToolHubStatus
{
    public string ProjectRoot { get; set; } = "";
    public string ManifestPath { get; set; } = "";
    public bool ManifestExists { get; set; }
    public string ManifestLastWriteTimeUtc { get; set; } = "";
    public string BuildScriptPath { get; set; } = "";
    public bool BuildScriptExists { get; set; }
    public string ToolsScriptPath { get; set; } = "";
    public bool ToolsScriptExists { get; set; }
    public string ToolsRoot { get; set; } = "";
    public bool ToolsRootExists { get; set; }
    public string OutputRoot { get; set; } = "";
    public bool OutputRootExists { get; set; }
    public int OutputRootFileCount { get; set; }
    public long OutputRootTotalBytes { get; set; }
    public string GeneratedAtUtc { get; set; } = "";
    public int ToolCount { get; set; }
    public int ManifestIssueCount { get; set; }
    public List<string> ManifestIssues { get; set; } = [];
    public Dictionary<string, int> CategoryCounts { get; set; } = [];
    public List<ToolStatus> Tools { get; set; } = [];
}

public sealed class ToolStatus
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Project { get; set; } = "";
    public bool ProjectExists { get; set; }
    public string ProjectLastWriteTimeUtc { get; set; } = "";
    public string Output { get; set; } = "";
    public bool OutputExists { get; set; }
    public int OutputFileCount { get; set; }
    public long OutputTotalBytes { get; set; }
    public string OutputLastWriteTimeUtc { get; set; } = "";
    public bool HasLaunch { get; set; }
    public bool HasSelfTest { get; set; }
    public bool RequiresProjectRoot { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ToolManifest
{
    public int Version { get; set; }
    public string ToolsRoot { get; set; } = "GodotTools";
    public string OutputRoot { get; set; } = "GodotTools-Build";
    public List<ToolEntry> Tools { get; set; } = [];
}

public sealed class ToolEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Project { get; set; } = "";
    public string Output { get; set; } = "";
    public ToolCommand? Launch { get; set; }
    public ToolCommand? SelfTest { get; set; }
    public bool RequiresProjectRoot { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ToolCommand
{
    public string Kind { get; set; } = "";
    public List<string> Args { get; set; } = [];
}
